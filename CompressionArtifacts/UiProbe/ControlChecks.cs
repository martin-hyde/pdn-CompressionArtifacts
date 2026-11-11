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

        foreach (PropertyCollection owner in new[] { properties, properties.Clone() })
        {
            Set(PropertyNames.Codec, CodecOptions.JPEG, owner);
            Check(Disabled(PropertyNames.AvifThreads, owner) && Disabled(PropertyNames.Effort, owner) && !Disabled(PropertyNames.BackgroundColor, owner), "JPEG controls");
            Set(PropertyNames.Codec, CodecOptions.AV1_AVIF, owner);
            Check(!Disabled(PropertyNames.AvifThreads, owner) && !Disabled(PropertyNames.AlphaQuality, owner) && Disabled(PropertyNames.BackgroundColor, owner), "AVIF controls");
            Set(PropertyNames.Lossless, true, owner);
            Check(Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.AvifSpeed, owner) && Disabled(PropertyNames.AlphaQuality, owner), "AVIF lossless controls");
            Set(PropertyNames.Codec, CodecOptions.JPEG, owner);
            Check(!Disabled(PropertyNames.Quality, owner) && !Disabled(PropertyNames.BackgroundColor, owner), "Lossless-to-JPEG reset");
            Set(PropertyNames.Codec, CodecOptions.HEIC, owner);
            Check(Disabled(PropertyNames.Effort, owner) && Disabled(PropertyNames.Lossless, owner) && Disabled(PropertyNames.AlphaQuality, owner) && !Disabled(PropertyNames.Chroma, owner), "HEIC controls");
            Set(PropertyNames.Codec, CodecOptions.JPEG_XR, owner);
            Check(!Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.Chroma, owner) && Disabled(PropertyNames.BackgroundColor, owner), "JXR controls");
            Set(PropertyNames.Codec, CodecOptions.JPEG_XL, owner);
            Check(!Disabled(PropertyNames.Effort, owner) && !Disabled(PropertyNames.Lossless, owner) && Disabled(PropertyNames.AlphaQuality, owner), "JXL controls");
            Set(PropertyNames.Lossless, true, owner);
            Check(Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.Effort, owner), "JXL lossless controls");
            Set(PropertyNames.Codec, CodecOptions.WebP, owner);
            Check(Disabled(PropertyNames.Quality, owner) && Disabled(PropertyNames.AlphaQuality, owner), "Lossless mode carries across supported codecs");
            Set(PropertyNames.Lossless, false, owner);
            Check(!Disabled(PropertyNames.Quality, owner) && !Disabled(PropertyNames.AlphaQuality, owner) && !Disabled(PropertyNames.Effort, owner), "WebP lossy controls");
        }

        Console.WriteLine($"PASS: {checks} dynamic control checks using typed APIs, including cloned settings and codec/lossless transitions.");
        return 0;
    }

    private static Property Property(PropertyNames name, PropertyCollection owner) =>
        owner[name] ?? throw new InvalidOperationException($"Missing property: {name}");

    private static void Set(PropertyNames name, object value, PropertyCollection owner) => Property(name, owner).Value = value;
    private static bool Disabled(PropertyNames name, PropertyCollection owner) => Property(name, owner).ReadOnly;
}
