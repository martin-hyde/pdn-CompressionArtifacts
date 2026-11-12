#nullable enable
using System;
using System.IO;

namespace CompressionArtifactsEffect;

internal enum CodecOptions { JPEG, JPEG_XL, JPEG_XR, WebP, HEIC, AV1_AVIF }
internal enum ChromaOptions { Auto, YUV444, YUV422, YUV420 }

internal sealed record CompressionSettings(
    CodecOptions Codec = CodecOptions.JPEG,
    int Quality = 35,
    ChromaOptions Chroma = ChromaOptions.YUV420,
    int Effort = 0,
    bool Lossless = false,
    int BackgroundColor = 0xFFFFFF,
    bool PreserveOriginalAlpha = true,
    bool LosslessAlpha = true)
{
    public bool UsesLossless => Lossless && Codec is CodecOptions.JPEG_XL or CodecOptions.WebP or CodecOptions.AV1_AVIF;
    public bool UsesAlphaFill => Codec is CodecOptions.JPEG or CodecOptions.HEIC;
}

internal interface ICompressionBackend
{
    byte[] RoundTrip(byte[] pixels, int width, int height, CompressionSettings settings, Func<bool>? isCanceled);
}

internal static class CompressionCodec
{
    public static byte[] EncodeDecode(byte[] input, int width, int height, CompressionSettings settings,
        Func<bool>? isCanceled = null, ICompressionBackend? backend = null)
    {
        if (width <= 0 || height <= 0 || input.Length != checked(width * height * 4))
            throw new ArgumentException("The pixel buffer must match the image dimensions.");
        CheckCanceled(isCanceled);
        if (settings.UsesLossless) return (byte[])input.Clone();
        if (backend is null)
            throw new InvalidOperationException("Real codec round trips require paint.net's FileTypes service.");

        byte[] pixels = settings.UsesAlphaFill ? PrepareOpaquePixels(input, settings) : input;
        byte[] result = backend.RoundTrip(pixels, width, height, settings, isCanceled);
        if (result.Length != input.Length) throw new InvalidDataException("The codec changed the image dimensions.");
        if (settings.UsesAlphaFill && settings.PreserveOriginalAlpha)
        {
            for (int i = 3; i < result.Length; i += 4) result[i] = input[i];
        }
        CheckCanceled(isCanceled);
        return result;
    }
    internal static byte[] PrepareOpaquePixels(byte[] input, CompressionSettings settings)
    {
        byte[] pixels = (byte[])input.Clone();
        int[] background = [settings.BackgroundColor & 255, (settings.BackgroundColor >> 8) & 255, (settings.BackgroundColor >> 16) & 255];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int alpha = pixels[i + 3];
            for (int c = 0; c < 3; c++)
            {
                // Keep straight RGB at partially transparent edges when restoring alpha.
                // Matting those pixels first would apply their alpha twice and create halos.
                if (settings.PreserveOriginalAlpha)
                {
                    if (alpha == 0) pixels[i + c] = (byte)background[c];
                }
                else pixels[i + c] = (byte)((pixels[i + c] * alpha + background[c] * (255 - alpha) + 127) / 255);
            }
            pixels[i + 3] = 255;
        }
        return pixels;
    }

    private static void CheckCanceled(Func<bool>? isCanceled)
    {
        if (isCanceled?.Invoke() == true) throw new OperationCanceledException();
    }
}
