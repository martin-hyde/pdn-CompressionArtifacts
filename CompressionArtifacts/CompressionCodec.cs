#nullable enable
using ImageMagick;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;

namespace CompressionArtifactsEffect;

internal enum CodecOptions { JPEG, JPEG_XL, JPEG_XR, WebP, HEIC, AV1_AVIF }
internal enum ChromaOptions { Auto, YUV444, YUV422, YUV420 }

internal sealed record CompressionSettings(
    CodecOptions Codec = CodecOptions.JPEG,
    int Quality = 35,
    ChromaOptions Chroma = ChromaOptions.YUV420,
    int Effort = 5,
    bool Lossless = false,
    int AlphaQuality = 75,
    int BackgroundColor = 0xFFFFFF,
    bool PreserveJpegAlpha = true,
    int AvifSpeed = 9,
    int AvifThreads = 0)
{
    public bool UsesLossless => Lossless && Codec is CodecOptions.JPEG_XL or CodecOptions.WebP or CodecOptions.AV1_AVIF;
}

internal static class CompressionCodec
{
    public static byte[] EncodeDecode(byte[] input, int width, int height, CompressionSettings settings, Func<bool>? isCanceled = null)
    {
        if (width <= 0 || height <= 0 || input.Length != checked(width * height * 4))
            throw new ArgumentException("The pixel buffer must match the image dimensions.");
        CheckCanceled(isCanceled);

        // A lossless round trip cannot add artifacts. Skip encoding entirely, avoiding
        // expensive AV1/JXL work and guaranteeing identical RGB and alpha channels.
        if (settings.UsesLossless) return (byte[])input.Clone();
        if (settings.Codec == CodecOptions.AV1_AVIF) return AvifCodec.EncodeDecode(input, width, height, settings, isCanceled);
        if (settings.Codec == CodecOptions.JPEG_XR) return EncodeDecodeJpegXr(input, width, height, settings.Quality);
        if (settings.Codec == CodecOptions.HEIC) return SimulateHeic(input, width, height, settings.Quality, settings.Chroma, isCanceled);

        byte[] pixels = settings.Codec == CodecOptions.JPEG ? PrepareJpeg(input, settings) : input;
        var readSettings = new MagickReadSettings { Width = (uint)width, Height = (uint)height, Format = MagickFormat.Bgra, Depth = 8 };
        using var image = new MagickImage(pixels, readSettings);
        image.Progress += (_, args) => args.Cancel = isCanceled?.Invoke() == true;
        image.Quality = (uint)settings.Quality;
        image.Format = settings.Codec switch
        {
            CodecOptions.JPEG => MagickFormat.Jpeg,
            CodecOptions.JPEG_XL => MagickFormat.Jxl,
            CodecOptions.WebP => MagickFormat.WebP,
            _ => throw new ArgumentOutOfRangeException(nameof(settings)),
        };

        switch (settings.Codec)
        {
            case CodecOptions.JPEG:
                image.Alpha(AlphaOption.Off);
                string? sampling = settings.Chroma switch
                {
                    ChromaOptions.YUV444 => "4:4:4",
                    ChromaOptions.YUV422 => "4:2:2",
                    ChromaOptions.YUV420 => "4:2:0",
                    _ => null,
                };
                if (sampling is not null) image.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", sampling);
                break;
            case CodecOptions.WebP:
                int webpMethod = Math.Clamp((int)Math.Round(settings.Effort * (6.0 / 9.0)), 0, 6);
                image.Settings.SetDefine(MagickFormat.WebP, "method", Number(webpMethod));
                image.Settings.SetDefine(MagickFormat.WebP, "alpha-quality", Number(settings.AlphaQuality));
                image.Settings.SetDefine(MagickFormat.WebP, "lossless", "false");
                break;
            case CodecOptions.JPEG_XL:
                int jxlEffort = 3 + (int)Math.Round(settings.Effort * (6.0 / 9.0));
                image.Settings.SetDefine(MagickFormat.Jxl, "effort", Number(jxlEffort));
                break;
        }

        CheckCanceled(isCanceled);
        using var encoded = new MemoryStream();
        image.Write(encoded);
        CheckCanceled(isCanceled);
        encoded.Position = 0;
        using var decoded = new MagickImage(encoded);
        if (decoded.Width != width || decoded.Height != height) throw new InvalidDataException("The codec changed the image dimensions.");
        decoded.ColorSpace = ColorSpace.sRGB;
        decoded.Alpha(AlphaOption.On);
        using var decodedPixels = decoded.GetPixels();
        byte[] result = decodedPixels.ToByteArray(PixelMapping.BGRA) ?? throw new InvalidDataException("The codec returned no pixels.");
        if (settings.Codec == CodecOptions.JPEG && settings.PreserveJpegAlpha)
        {
            for (int i = 3; i < result.Length; i += 4) result[i] = input[i];
        }
        CheckCanceled(isCanceled);
        return result;
    }

    internal static byte[] PrepareJpeg(byte[] input, CompressionSettings settings)
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
                if (settings.PreserveJpegAlpha)
                {
                    if (alpha == 0) pixels[i + c] = (byte)background[c];
                }
                else pixels[i + c] = (byte)((pixels[i + c] * alpha + background[c] * (255 - alpha) + 127) / 255);
            }
            pixels[i + 3] = 255;
        }
        return pixels;
    }

    private static byte[] EncodeDecodeJpegXr(byte[] input, int width, int height, int quality)
    {
        BitmapSource source = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, input, width * 4);
        var encoder = new WmpBitmapEncoder { ImageQualityLevel = quality / 100f };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var encoded = new MemoryStream();
        encoder.Save(encoded);
        encoded.Position = 0;
        BitmapDecoder decoder = BitmapDecoder.Create(encoded, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource frame = new FormatConvertedBitmap(decoder.Frames[0], System.Windows.Media.PixelFormats.Bgra32, null, 0);
        byte[] result = new byte[checked(width * height * 4)];
        frame.CopyPixels(result, width * 4, 0);
        return result;
    }

    // An approximation, not an HEVC encoder. The redistributable codec has no HEVC encoder.
    private static byte[] SimulateHeic(byte[] input, int width, int height, int quality, ChromaOptions chroma, Func<bool>? isCanceled)
    {
        byte[] result = (byte[])input.Clone();
        int qY = Math.Max(1, (101 - quality) / 3);
        int qC = Math.Max(1, (101 - quality) / 2);
        int blockWidth = chroma == ChromaOptions.YUV444 ? 1 : 2;
        int blockHeight = chroma is ChromaOptions.YUV444 or ChromaOptions.YUV422 ? 1 : 2;
        for (int by = 0; by < height; by += blockHeight)
        {
            CheckCanceled(isCanceled);
            for (int bx = 0; bx < width; bx += blockWidth)
            {
                double cbTotal = 0, crTotal = 0;
                int count = 0;
                for (int y = by; y < Math.Min(by + blockHeight, height); y++)
                for (int x = bx; x < Math.Min(bx + blockWidth, width); x++)
                {
                    int i = ((y * width) + x) * 4;
                    double r = input[i + 2], g = input[i + 1], b = input[i];
                    cbTotal += 128 - 0.168736 * r - 0.331264 * g + 0.5 * b;
                    crTotal += 128 + 0.5 * r - 0.418688 * g - 0.081312 * b;
                    count++;
                }
                double cb = Quantize(cbTotal / count, qC), cr = Quantize(crTotal / count, qC);
                for (int y = by; y < Math.Min(by + blockHeight, height); y++)
                for (int x = bx; x < Math.Min(bx + blockWidth, width); x++)
                {
                    int i = ((y * width) + x) * 4;
                    double luma = Quantize(0.299 * input[i + 2] + 0.587 * input[i + 1] + 0.114 * input[i], qY);
                    result[i + 2] = Clamp(luma + 1.402 * (cr - 128));
                    result[i + 1] = Clamp(luma - 0.344136 * (cb - 128) - 0.714136 * (cr - 128));
                    result[i] = Clamp(luma + 1.772 * (cb - 128));
                }
            }
        }
        return result;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static double Quantize(double value, int step) => Math.Round(value / step) * step;
    private static byte Clamp(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    private static void CheckCanceled(Func<bool>? isCanceled)
    {
        if (isCanceled?.Invoke() == true) throw new OperationCanceledException();
    }
}
