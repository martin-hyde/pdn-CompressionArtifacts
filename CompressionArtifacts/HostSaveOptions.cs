#nullable enable
using PaintDotNet.PropertySystem;
using System;
using System.Linq;

namespace CompressionArtifactsEffect;

internal static class HostSaveOptions
{
    internal static void Configure(PropertyCollection properties, CompressionSettings settings)
    {
        SetOptionalBoolean(properties, false, "Lossless");
        SetNumber(Required(properties, "Quality", "ImageQuality", "ImageQualityLevel"), settings.Quality / 100.0);
        switch (settings.Codec)
        {
            case CodecOptions.JPEG_XL:
            case CodecOptions.WebP:
                SetNumber(Required(properties, "Effort"), settings.Effort / 9.0);
                break;
            case CodecOptions.AV1_AVIF:
                SetEffortPreset(Required(properties, "EncoderPreset", "Speed"), settings.Effort);
                if (settings.Chroma != ChromaOptions.Auto)
                {
                    string suffix = settings.Chroma switch { ChromaOptions.YUV444 => "444", ChromaOptions.YUV422 => "422", _ => "420" };
                    SetChoice(Required(properties, "YUVChromaSubsampling", "ChromaSubsampling"), suffix);
                }
                Required(properties, "LosslessAlphaCompression").Value = settings.LosslessAlpha;
                SetOptionalBoolean(properties, false, "PremultipliedAlpha", "PreserveExistingTileSize");
                break;
        }
    }

    internal static Property Required(PropertyCollection properties, params string[] names)
    {
        foreach (string name in names) if (properties[name] is Property property) return property;
        throw new NotSupportedException($"The installed FileType does not expose the expected {names[0]} save option.");
    }

    private static void SetOptionalBoolean(PropertyCollection properties, bool value, params string[] names)
    {
        foreach (string name in names) if (properties[name] is BooleanProperty property) property.Value = value;
    }

    internal static void SetNumber(Property property, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        switch (property)
        {
            case Int32Property integer:
                integer.Value = integer.MinValue + (int)Math.Round(amount * (integer.MaxValue - integer.MinValue));
                break;
            case DoubleProperty number:
                number.Value = number.MinValue + amount * (number.MaxValue - number.MinValue);
                break;
            default:
                throw new NotSupportedException($"The FileType's {property.Name} option is not numeric.");
        }
    }

    private static void SetEffortPreset(Property property, int effort)
    {
        if (property is StaticListChoiceProperty choices)
        {
            string[] presets = ["Fast", "Medium", "Slow", "VerySlow"];
            string desired = presets[Math.Clamp((int)Math.Round(effort / 3.0), 0, 3)];
            object? value = choices.ValueChoices.FirstOrDefault(choice => string.Equals(choice.ToString(), desired, StringComparison.OrdinalIgnoreCase));
            if (value is null) throw new NotSupportedException($"The AVIF FileType does not expose the expected {desired} encoder preset.");
            choices.Value = value;
        }
        else SetNumber(property, 1 - Math.Clamp(effort, 0, 9) / 9.0);
    }

    private static void SetChoice(Property property, string suffix)
    {
        if (property is not StaticListChoiceProperty choices) throw new NotSupportedException($"The FileType's {property.Name} option is not a choice.");
        object? value = choices.ValueChoices.FirstOrDefault(choice => choice.ToString()?.EndsWith(suffix, StringComparison.Ordinal) == true);
        if (value is null) throw new NotSupportedException($"The FileType does not support {suffix} chroma subsampling.");
        choices.Value = value;
    }
}
