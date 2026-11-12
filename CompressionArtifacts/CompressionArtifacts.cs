#nullable enable
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.ComponentModel;
using PaintDotNet.FileTypes;
using PaintDotNet.Imaging;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;

[assembly: AssemblyProduct("Compression Artifacts")]
[assembly: AssemblyTitle("Compression Artifacts plugin for Paint.NET")]
[assembly: AssemblyDescription("Preview compression artifacts without saving the image")]
[assembly: AssemblyConfiguration("effect|photo|compression|jpeg|webp|avif|heic")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: AssemblyInformationalVersion("2.0.0")]
[assembly: SupportedOSPlatform("Windows")]
[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("UiProbe")]
[assembly: InternalsVisibleTo("HostProbe")]

namespace CompressionArtifactsEffect;

public sealed class PluginSupportInfo : IPluginSupportInfo
{
    public string DisplayName => "Compression Artifacts";
    public string Author => "Compression Artifacts contributors";
    public string Copyright => string.Empty;
    public Version Version => GetType().Assembly.GetName().Version!;
    public Uri WebsiteUri { get; } = new("https://github.com/martin-hyde/pdn-CompressionArtifacts");
}

[PluginSupportInfo(typeof(PluginSupportInfo))]
public sealed class CompressionArtifactsEffect : PropertyBasedBitmapEffect
{
    // PreserveJpegAlpha remains the token key for existing saved settings; it now also applies to HEIC.
    internal enum PropertyNames { Codec, Quality, Chroma, Effort, Lossless, LosslessAlpha, BackgroundColor, PreserveJpegAlpha }
    private readonly object renderSync = new();
    private byte[]? renderedBgra;
    private Exception? renderError;
    private int renderedWidth;

    public CompressionArtifactsEffect()
        : base("Compression Artifacts", new System.Drawing.Bitmap(typeof(CompressionArtifactsEffect), "CompressionArtifacts.png"), SubmenuNames.Photo,
            BitmapEffectOptions.Create() with { IsConfigurable = true }) { }

    protected override PropertyCollection OnCreatePropertyCollection() => CreateConfigurationProperties();

    internal static PropertyCollection CreateConfigurationProperties() => new([
        StaticListChoiceProperty.CreateForEnum(PropertyNames.Codec, CodecOptions.JPEG),
        new Int32Property(PropertyNames.Quality, 35, 1, 100),
        StaticListChoiceProperty.CreateForEnum(PropertyNames.Chroma, ChromaOptions.YUV420),
        new Int32Property(PropertyNames.Effort, 0, 0, 9),
        new BooleanProperty(PropertyNames.Lossless, false),
        new BooleanProperty(PropertyNames.LosslessAlpha, true),
        new BooleanProperty(PropertyNames.PreserveJpegAlpha, true),
        new Int32Property(PropertyNames.BackgroundColor, 0xFFFFFF, 0, 0xFFFFFF),
    ], CreateControlRules());

    private static IEnumerable<PropertyCollectionRule> CreateControlRules()
    {
        // Reset an inapplicable lossless value before updating dependent controls.
        yield return new SetTargetWhenSourceEqualsAnyValueRule(PropertyNames.Lossless, false, PropertyNames.Codec,
            new object[] { CodecOptions.JPEG, CodecOptions.JPEG_XR, CodecOptions.HEIC }, false);
        yield return DisableExcept(PropertyNames.Lossless, false, CodecOptions.JPEG_XL, CodecOptions.WebP, CodecOptions.AV1_AVIF);
        yield return new ReadOnlyBoundToBooleanRule(PropertyNames.Quality, PropertyNames.Lossless, false);
        yield return DisableExcept(PropertyNames.Chroma, true, CodecOptions.JPEG, CodecOptions.AV1_AVIF);
        yield return DisableExcept(PropertyNames.Effort, true, CodecOptions.JPEG_XL, CodecOptions.WebP, CodecOptions.AV1_AVIF);
        yield return DisableExcept(PropertyNames.LosslessAlpha, true, CodecOptions.AV1_AVIF);
        yield return DisableExcept(PropertyNames.PreserveJpegAlpha, false, CodecOptions.JPEG, CodecOptions.HEIC);
        yield return DisableExcept(PropertyNames.BackgroundColor, false, CodecOptions.JPEG, CodecOptions.HEIC);
    }

    private static PropertyCollectionRule DisableExcept(PropertyNames target, bool disableForLossless, params CodecOptions[] supported)
    {
        var conditions = Enum.GetValues<CodecOptions>().Except(supported)
            .Select(codec => ((object)PropertyNames.Codec, (object?)codec)).ToList();
        if (disableForLossless) conditions.Add((PropertyNames.Lossless, true));
        return new ReadOnlyBoundToNameValuesRule(target, false, conditions);
    }

    protected override ControlInfo OnCreateConfigUI(PropertyCollection props)
    {
        ControlInfo config = CreateDefaultConfigUI(props);
        Set(config, PropertyNames.Codec, "Codec");
        Set(config, PropertyNames.Quality, "Quality / fidelity");
        Set(config, PropertyNames.Chroma, "Chroma subsampling");
        Set(config, PropertyNames.Effort, "Encoding effort (0 fast – 9 slow)");
        Set(config, PropertyNames.Lossless, "Lossless");
        config.SetPropertyControlValue(PropertyNames.Lossless, ControlInfoPropertyNames.Description, "Lossless (JXL/WebP/AVIF; no artifacts)");
        Set(config, PropertyNames.LosslessAlpha, "Lossless alpha");
        config.SetPropertyControlValue(PropertyNames.LosslessAlpha, ControlInfoPropertyNames.Description, "Lossless alpha (AVIF)");
        Set(config, PropertyNames.PreserveJpegAlpha, "Preserve original transparency");
        config.SetPropertyControlValue(PropertyNames.PreserveJpegAlpha, ControlInfoPropertyNames.Description, "Preserve original transparency (JPEG/HEIC)");
        Set(config, PropertyNames.BackgroundColor, "JPEG/HEIC Alpha Fill");
        config.SetPropertyControlType(PropertyNames.BackgroundColor, PropertyControlType.ColorWheel);
        config.SetPropertyControlValue(PropertyNames.Quality, ControlInfoPropertyNames.SliderLargeChange, 10);
        config.SetPropertyControlValue(PropertyNames.Effort, ControlInfoPropertyNames.SliderLargeChange, 1);
        PropertyControlInfo codecs = config.FindControlForPropertyName(PropertyNames.Codec)!;
        codecs.SetValueDisplayName(CodecOptions.JPEG_XL, "JPEG XL");
        codecs.SetValueDisplayName(CodecOptions.JPEG_XR, "JPEG XR");
        codecs.SetValueDisplayName(CodecOptions.AV1_AVIF, "AV1 / AVIF");
        PropertyControlInfo chroma = config.FindControlForPropertyName(PropertyNames.Chroma)!;
        chroma.SetValueDisplayName(ChromaOptions.Auto, "Automatic");
        chroma.SetValueDisplayName(ChromaOptions.YUV444, "4:4:4");
        chroma.SetValueDisplayName(ChromaOptions.YUV422, "4:2:2");
        chroma.SetValueDisplayName(ChromaOptions.YUV420, "4:2:0");
        return config;
    }

    private static void Set(ControlInfo config, PropertyNames property, string displayName)
    {
        config.SetPropertyControlValue(property, ControlInfoPropertyNames.DisplayName, displayName);
        config.SetPropertyControlValue(property, ControlInfoPropertyNames.ShowHeaderLine, false);
    }

    protected override void OnSetToken(PropertyBasedEffectConfigToken? newToken)
    {
        lock (renderSync)
        {
            renderedBgra = null;
            renderError = null;
            renderedWidth = 0;
            base.OnSetToken(newToken);
        }
    }

    protected override void OnRender(IBitmapEffectOutput output)
    {
        // Capture only for this call. The host replaces its cancellation token
        // whenever a new rendering task starts.
        CancellationToken cancellation = CancellationToken;
        // Snapshot the cached result while holding the lock: changing a token must
        // never leave tile workers reading a reset width or a cleared pixel array.
        byte[]? pixels;
        int imageWidth;
        lock (renderSync)
        {
            if (cancellation.IsCancellationRequested) return;
            if (renderedBgra is null && renderError is null)
            {
                try { renderedBgra = EncodeDecode(cancellation); }
                catch (Exception) when (cancellation.IsCancellationRequested) { return; }
                catch (Exception ex) { renderError = ex; }
            }
            if (renderError is not null)
                throw new InvalidOperationException("The selected codec could not encode this image. " + renderError.Message, renderError);
            pixels = renderedBgra;
            imageWidth = renderedWidth;
        }
        if (cancellation.IsCancellationRequested || pixels is null) return;
        using IBitmapLock<ColorBgra32> destinationLock = output.LockBgra32();
        RegionPtr<ColorBgra32> destination = destinationLock.AsRegionPtr();
        try
        {
            BitmapTransfer.CopyRegion(pixels, imageWidth, pixels.Length / (imageWidth * 4), output.Bounds, destination, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
    }

    private byte[] EncodeDecode(CancellationToken cancellation)
    {
        byte[] input;
        int width, height;
        using (IEffectInputBitmap<ColorBgra32> sourceBitmap = Environment.GetSourceBitmapBgra32())
        {
            width = sourceBitmap.Size.Width;
            height = sourceBitmap.Size.Height;
            input = BitmapTransfer.ReadSource(sourceBitmap, cancellation);
        }
        // Release the host bitmap before entering a potentially long native encode.
        renderedWidth = width;
        var settings = new CompressionSettings(
            (CodecOptions)GetInt(PropertyNames.Codec), GetInt(PropertyNames.Quality),
            (ChromaOptions)GetInt(PropertyNames.Chroma), GetInt(PropertyNames.Effort),
            (bool)Token!.GetProperty(PropertyNames.Lossless)!.Value!,
            GetInt(PropertyNames.BackgroundColor), (bool)Token.GetProperty(PropertyNames.PreserveJpegAlpha)!.Value!,
            (bool)Token.GetProperty(PropertyNames.LosslessAlpha)!.Value!);
        ICompressionBackend? backend = settings.UsesLossless ? null :
            new PaintNetCodecBackend(Services.GetService<IFileTypesService>()!,
                Services.GetService<IFileTypeDocumentFactory>()!, Environment.Document.ColorContext, cancellation);
        return CompressionCodec.EncodeDecode(input, width, height, settings, () => cancellation.IsCancellationRequested, backend);
    }

    protected override void OnDispose(bool disposing)
    {
        if (disposing)
        {
            lock (renderSync)
            {
                renderedBgra = null;
                renderError = null;
                renderedWidth = 0;
            }
        }
        base.OnDispose(disposing);
    }

    private int GetInt(PropertyNames name) => (int)Token!.GetProperty(name)!.Value!;
}
