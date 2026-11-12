using CompressionArtifactsEffect;
using PaintDotNet.PropertySystem;
using System.Runtime.CompilerServices;
using Effect = CompressionArtifactsEffect.CompressionArtifactsEffect;
using PropertyNames = CompressionArtifactsEffect.CompressionArtifactsEffect.PropertyNames;

internal static class ControlChecks
{
    // Resolve paint.net's runtime assemblies after Program registers the loader.
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int Run()
    {
        PropertyCollection properties = Effect.CreateConfigurationProperties();
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        Check((int)properties[PropertyNames.Effort]!.Value! == 0, "Fast effort is the default");
        Check(properties["AvifSpeed"] is null, "AVIF uses the shared effort control");
        Check(new PluginSupportInfo().Copyright.Length == 0, "Plugin has no copyright claim");

        foreach (PropertyCollection owner in new[] { properties, properties.Clone() })
        {
            Set(PropertyNames.Codec, CodecOptions.JPEG, owner);
            Check(Disabled(PropertyNames.LosslessAlpha, owner) && Disabled(PropertyNames.Effort, owner) && !Disabled(PropertyNames.BackgroundColor, owner) && !Disabled(PropertyNames.PreserveJpegAlpha, owner), "JPEG controls");
            Set(PropertyNames.Codec, CodecOptions.AV1_AVIF, owner);
            Check(!Disabled(PropertyNames.LosslessAlpha, owner) && Disabled(PropertyNames.BackgroundColor, owner), "AVIF controls");
            Check(!Disabled(PropertyNames.Effort, owner), "AVIF shares the encoding effort control");
            Set(PropertyNames.Lossless, true, owner);
            Check(Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.Effort, owner) && Disabled(PropertyNames.LosslessAlpha, owner), "AVIF lossless controls");
            Set(PropertyNames.Codec, CodecOptions.JPEG, owner);
            Check(!Disabled(PropertyNames.Quality, owner) && !Disabled(PropertyNames.BackgroundColor, owner), "Lossless-to-JPEG reset");
            Set(PropertyNames.Codec, CodecOptions.HEIC, owner);
            Check(Disabled(PropertyNames.Effort, owner) && Disabled(PropertyNames.Lossless, owner) && Disabled(PropertyNames.LosslessAlpha, owner) && Disabled(PropertyNames.Chroma, owner) && !Disabled(PropertyNames.Quality, owner), "Native HEIC encoder exposes quality only");
            Check(!Disabled(PropertyNames.BackgroundColor, owner) && !Disabled(PropertyNames.PreserveJpegAlpha, owner), "HEIC shares alpha preservation and fill controls");
            Set(PropertyNames.Codec, CodecOptions.JPEG_XR, owner);
            Check(!Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.Chroma, owner) && Disabled(PropertyNames.BackgroundColor, owner) && Disabled(PropertyNames.PreserveJpegAlpha, owner), "JXR controls");
            Set(PropertyNames.Codec, CodecOptions.JPEG_XL, owner);
            Check(!Disabled(PropertyNames.Effort, owner) && !Disabled(PropertyNames.Lossless, owner) && Disabled(PropertyNames.LosslessAlpha, owner), "JXL controls");
            Set(PropertyNames.Lossless, true, owner);
            Check(Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.Effort, owner), "JXL lossless controls");
            Set(PropertyNames.Codec, CodecOptions.WebP, owner);
            Check(Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.LosslessAlpha, owner), "Lossless mode carries across supported codecs");
            Set(PropertyNames.Lossless, false, owner);
            Check(!Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.LosslessAlpha, owner) && !Disabled(PropertyNames.Effort, owner), "WebP lossy controls");
        }

        Console.WriteLine($"PASS: {checks} dynamic control checks using typed APIs, including cloned settings and codec/lossless transitions.");
        return 0;
    }

    private static Property Property(PropertyNames name, PropertyCollection owner) =>
        owner[name] ?? throw new InvalidOperationException($"Missing property: {name}");

    private static void Set(PropertyNames name, object value, PropertyCollection owner) => Property(name, owner).Value = value;
    private static bool Disabled(PropertyNames name, PropertyCollection owner) => Property(name, owner).ReadOnly;
}
