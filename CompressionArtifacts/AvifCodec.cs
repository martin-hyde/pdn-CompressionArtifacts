#nullable enable
using LibHeifSharp;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CompressionArtifactsEffect;

internal static class AvifCodec
{
    // Keep native loading local to this plugin, including when Paint.NET uses an
    // isolated plugin load context rather than the application's dependency graph.
    static AvifCodec()
    {
        NativeLibrary.SetDllImportResolver(typeof(HeifContext).Assembly, (name, assembly, path) =>
        {
            if (name != "libheif") return IntPtr.Zero;
            string localPath = Path.Combine(Path.GetDirectoryName(typeof(AvifCodec).Assembly.Location)!, "libheif.dll");
            return NativeLibrary.Load(localPath);
        });
    }

    internal static int GetThreadCount(int requested) => requested == 0 ? Math.Clamp(Environment.ProcessorCount, 1, 16) : Math.Clamp(requested, 1, 64);

    public static byte[] EncodeDecode(byte[] input, int width, int height, CompressionSettings settings, Func<bool>? isCanceled)
    {
        CheckCanceled(isCanceled);
        using var context = new HeifContext();
        // Select AOM explicitly: its thread, speed and alpha parameters are known.
        var descriptor = context.GetEncoderDescriptors(HeifCompressionFormat.Av1, "aom").FirstOrDefault()
            ?? throw new InvalidOperationException("The bundled libheif library has no AOM AV1 encoder.");
        using var encoder = context.GetEncoder(descriptor);
        encoder.SetLossyQuality(settings.Quality);
        encoder.SetParameter("speed", settings.AvifSpeed);
        encoder.SetParameter("threads", GetThreadCount(settings.AvifThreads));
        encoder.SetParameter("alpha-quality", settings.AlphaQuality);
        encoder.SetParameter("chroma", settings.Chroma switch
        {
            ChromaOptions.YUV444 => "444",
            ChromaOptions.YUV422 => "422",
            _ => "420",
        });

        bool hasAlpha = false;
        for (int i = 3; i < input.Length; i += 4)
        {
            if (input[i] != 255) { hasAlpha = true; break; }
        }
        int channels = hasAlpha ? 4 : 3;
        using var image = new HeifImage(width, height, HeifColorspace.Rgb, hasAlpha ? HeifChroma.InterleavedRgba32 : HeifChroma.InterleavedRgb24);
        image.AddPlane(HeifChannel.Interleaved, width, height, 8);
        HeifPlaneData plane = image.GetPlane(HeifChannel.Interleaved);
        byte[] row = new byte[checked(width * channels)];
        for (int y = 0; y < height; y++)
        {
            CheckCanceled(isCanceled);
            for (int x = 0; x < width; x++)
            {
                int i = ((y * width) + x) * 4, j = x * channels;
                row[j] = input[i + 2]; row[j + 1] = input[i + 1]; row[j + 2] = input[i];
                if (hasAlpha) row[j + 3] = input[i + 3];
            }
            Marshal.Copy(row, 0, IntPtr.Add(plane.Scan0, y * plane.Stride), row.Length);
        }
        context.EncodeImage(image, encoder, new HeifEncodingOptions { SaveAlphaChannel = hasAlpha });
        CheckCanceled(isCanceled);
        using var encoded = new MemoryStream();
        context.WriteToStream(encoded);
        encoded.Position = 0;
        using var decodedContext = new HeifContext(encoded, true);
        decodedContext.SetMaximumDecodingThreads(GetThreadCount(settings.AvifThreads));
        using var handle = decodedContext.GetPrimaryImageHandle();
        using var decoded = handle.Decode(HeifColorspace.Rgb, HeifChroma.InterleavedRgba32, new HeifDecodingOptions());
        if (decoded.Width != width || decoded.Height != height) throw new InvalidDataException("The AVIF codec changed the image dimensions.");
        HeifPlaneData decodedPlane = decoded.GetPlane(HeifChannel.Interleaved);
        byte[] result = new byte[input.Length];
        byte[] rgbaRow = new byte[width * 4];
        for (int y = 0; y < height; y++)
        {
            CheckCanceled(isCanceled);
            Marshal.Copy(IntPtr.Add(decodedPlane.Scan0, y * decodedPlane.Stride), rgbaRow, 0, rgbaRow.Length);
            for (int x = 0; x < width; x++)
            {
                int i = ((y * width) + x) * 4, j = x * 4;
                result[i] = rgbaRow[j + 2]; result[i + 1] = rgbaRow[j + 1]; result[i + 2] = rgbaRow[j]; result[i + 3] = hasAlpha ? rgbaRow[j + 3] : (byte)255;
            }
        }
        return result;
    }

    private static void CheckCanceled(Func<bool>? isCanceled)
    {
        if (isCanceled?.Invoke() == true) throw new OperationCanceledException();
    }
}
