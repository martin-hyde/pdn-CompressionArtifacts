#nullable enable
using PaintDotNet;
using PaintDotNet.Effects;
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

[assembly: AssemblyProduct("Compression Artifacts")]
[assembly: AssemblyTitle("Compression Artifacts plugin for Paint.NET")]
[assembly: AssemblyDescription("Preview compression artifacts without saving the image")]
[assembly: AssemblyConfiguration("effect|photo|compression|jpeg|webp|avif|heic")]
[assembly: AssemblyCopyright("Copyright ©2026")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: SupportedOSPlatform("Windows")]
[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("UiProbe")]

namespace CompressionArtifactsEffect;

public sealed class PluginSupportInfo : IPluginSupportInfo
{
    public string DisplayName => "Compression Artifacts";
    public string Author => "Compression Artifacts contributors";
    public string Copyright => "Copyright ©2026";
    public Version Version => GetType().Assembly.GetName().Version!;
    public Uri WebsiteUri { get; } = new("https://github.com/martin-hyde/pdn-CompressionArtifacts");
}

[PluginSupportInfo(typeof(PluginSupportInfo))]
public sealed class CompressionArtifactsEffect : PropertyBasedBitmapEffect
{
    internal enum PropertyNames { Codec, Quality, Chroma, Effort, Lossless, AlphaQuality, BackgroundColor, PreserveJpegAlpha, AvifSpeed, AvifThreads }
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
        new Int32Property(PropertyNames.Effort, 5, 0, 9),
        new Int32Property(PropertyNames.AvifSpeed, 9, 0, 9),
        new Int32Property(PropertyNames.AvifThreads, 0, 0, 64),
        new BooleanProperty(PropertyNames.Lossless, false),
        new Int32Property(PropertyNames.AlphaQuality, 75, 0, 100),
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
        yield return DisableExcept(PropertyNames.Chroma, true, CodecOptions.JPEG, CodecOptions.HEIC, CodecOptions.AV1_AVIF);
        yield return DisableExcept(PropertyNames.Effort, true, CodecOptions.JPEG_XL, CodecOptions.WebP);
        yield return DisableExcept(PropertyNames.AvifSpeed, true, CodecOptions.AV1_AVIF);
        yield return DisableExcept(PropertyNames.AvifThreads, true, CodecOptions.AV1_AVIF);
        yield return DisableExcept(PropertyNames.AlphaQuality, true, CodecOptions.WebP, CodecOptions.AV1_AVIF);
        yield return DisableExcept(PropertyNames.PreserveJpegAlpha, false, CodecOptions.JPEG);
        yield return DisableExcept(PropertyNames.BackgroundColor, false, CodecOptions.JPEG);
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
        Set(config, PropertyNames.Codec, "Codec (HEIC is an approximation)");
        Set(config, PropertyNames.Quality, "Quality / fidelity");
        Set(config, PropertyNames.Chroma, "Chroma subsampling");
        Set(config, PropertyNames.Effort, "Encoding effort (JXL/WebP: 0 fast – 9 slow)");
        Set(config, PropertyNames.AvifSpeed, "AVIF speed (0 slow – 9 fast)");
        Set(config, PropertyNames.AvifThreads, "AVIF CPU threads (0 = automatic)");
        Set(config, PropertyNames.Lossless, "Lossless (JXL/WebP/AVIF: no artifacts)");
        Set(config, PropertyNames.AlphaQuality, "Alpha quality (WebP/AVIF)");
        Set(config, PropertyNames.PreserveJpegAlpha, "Preserve original transparency (JPEG effect)");
        Set(config, PropertyNames.BackgroundColor, "JPEG background / transparent-pixel fill");
        config.SetPropertyControlType(PropertyNames.BackgroundColor, PropertyControlType.ColorWheel);
        config.SetPropertyControlValue(PropertyNames.Quality, ControlInfoPropertyNames.SliderLargeChange, 10);
        config.SetPropertyControlValue(PropertyNames.Effort, ControlInfoPropertyNames.SliderLargeChange, 1);
        config.SetPropertyControlValue(PropertyNames.AvifSpeed, ControlInfoPropertyNames.SliderLargeChange, 1);
        config.SetPropertyControlValue(PropertyNames.AvifThreads, ControlInfoPropertyNames.SliderLargeChange, 4);
        config.SetPropertyControlValue(PropertyNames.AlphaQuality, ControlInfoPropertyNames.SliderLargeChange, 10);
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
        // Snapshot the cached result while holding the lock: changing a token must
        // never leave tile workers reading a reset width or a cleared pixel array.
        byte[]? pixels;
        int imageWidth;
        lock (renderSync)
        {
            if (IsCancelRequested) return;
            if (renderedBgra is null && renderError is null)
            {
                try { renderedBgra = EncodeDecode(); }
                catch (OperationCanceledException) when (IsCancelRequested) { return; }
                catch (Exception) when (IsCancelRequested) { return; }
                catch (Exception ex) { renderError = ex; }
            }
            if (renderError is not null)
                throw new InvalidOperationException("The selected codec could not encode this image. " + renderError.Message, renderError);
            pixels = renderedBgra;
            imageWidth = renderedWidth;
        }
        if (IsCancelRequested || pixels is null) return;
        using IBitmapLock<ColorBgra32> destinationLock = output.LockBgra32();
        RegionPtr<ColorBgra32> destination = destinationLock.AsRegionPtr();
        RectInt32 bounds = output.Bounds;
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            if (IsCancelRequested) return;
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                int i = ((y * imageWidth) + x) * 4;
                destination[x - bounds.Left, y - bounds.Top] = new ColorBgra32(pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
            }
        }
    }

    private byte[] EncodeDecode()
    {
        using IEffectInputBitmap<ColorBgra32> sourceBitmap = Environment.GetSourceBitmapBgra32();
        using IBitmapLock<ColorBgra32> sourceLock = sourceBitmap.Lock(new RectInt32(0, 0, sourceBitmap.Size));
        RegionPtr<ColorBgra32> source = sourceLock.AsRegionPtr();
        int width = sourceBitmap.Size.Width, height = sourceBitmap.Size.Height;
        renderedWidth = width;
        byte[] input = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++)
        {
            if (IsCancelRequested) throw new OperationCanceledException();
            for (int x = 0; x < width; x++)
            {
                ColorBgra32 pixel = source[x, y];
                int i = ((y * width) + x) * 4;
                input[i] = pixel.B;
                input[i + 1] = pixel.G;
                input[i + 2] = pixel.R;
                input[i + 3] = pixel.A;
            }
        }
        var settings = new CompressionSettings(
            (CodecOptions)GetInt(PropertyNames.Codec), GetInt(PropertyNames.Quality),
            (ChromaOptions)GetInt(PropertyNames.Chroma), GetInt(PropertyNames.Effort),
            (bool)Token!.GetProperty(PropertyNames.Lossless)!.Value!, GetInt(PropertyNames.AlphaQuality),
            GetInt(PropertyNames.BackgroundColor), (bool)Token.GetProperty(PropertyNames.PreserveJpegAlpha)!.Value!,
            GetInt(PropertyNames.AvifSpeed), GetInt(PropertyNames.AvifThreads));
        return CompressionCodec.EncodeDecode(input, width, height, settings, () => IsCancelRequested);
    }

    private int GetInt(PropertyNames name) => (int)Token!.GetProperty(name)!.Value!;
}
