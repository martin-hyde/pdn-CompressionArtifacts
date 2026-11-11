using CompressionArtifactsEffect;
using ImageMagick;
using System.Diagnostics;
using System.IO;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}
byte[] Fixture(int width, int height, bool alpha)
{
    var pixels = new byte[width * height * 4];
    var random = new Random(546);
    for (int y = 0; y < height; y++)
    for (int x = 0; x < width; x++)
    {
        int i = (y * width + x) * 4;
        pixels[i] = (byte)((x * 11 + y * 3 + random.Next(16)) % 256);
        pixels[i + 1] = (byte)((y * 7 + (x / 8) * 40) % 256);
        pixels[i + 2] = (byte)((x * 9 + y * 13) % 256);
        pixels[i + 3] = alpha ? (byte)((x * 5 + y * 17) % 256) : (byte)255;
    }
    return pixels;
}
void SameAlpha(byte[] source, byte[] result, string context)
{
    Check(Enumerable.Range(0, source.Length / 4).All(p => source[p * 4 + 3] == result[p * 4 + 3]), context);
}

var input = Fixture(65, 49, true);
foreach (CodecOptions codec in Enum.GetValues<CodecOptions>())
{
    byte[] original = (byte[])input.Clone();
    var result = CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(Codec: codec));
    Check(input.SequenceEqual(original), $"{codec}: source pixels were changed");
    Check(result.Length == input.Length, $"{codec}: incorrect size");
    Check(!input.SequenceEqual(result), $"{codec}: lossy output was unchanged");
    if (codec is CodecOptions.JPEG or CodecOptions.HEIC) SameAlpha(input, result, $"{codec}: alpha was changed");
    Console.WriteLine($"{codec}: odd-size RGBA round trip OK");
}
foreach (CodecOptions codec in new[] { CodecOptions.JPEG_XL, CodecOptions.WebP, CodecOptions.AV1_AVIF })
{
    var result = CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(Codec: codec, Lossless: true));
    Check(input.SequenceEqual(result), $"{codec}: lossless RGB/alpha mismatch");
}

// Verify matting independently with exact expected bytes, including a half-alpha edge.
byte[] edge = [20, 60, 180, 128, 99, 77, 55, 0, 10, 20, 30, 255];
var jpegSettings = new CompressionSettings(BackgroundColor: 0x123456, PreserveJpegAlpha: false);
byte[] matte = CompressionCodec.PrepareJpeg(edge, jpegSettings);
Check(matte.SequenceEqual(new byte[] { 53, 56, 99, 255, 86, 52, 18, 255, 10, 20, 30, 255 }), "JPEG: custom matte blend mismatch");
byte[] straight = CompressionCodec.PrepareJpeg(edge, jpegSettings with { PreserveJpegAlpha = true });
Check(straight.SequenceEqual(new byte[] { 20, 60, 180, 255, 86, 52, 18, 255, 10, 20, 30, 255 }), "JPEG: straight RGB edge was matted twice");
var opaque = CompressionCodec.EncodeDecode(input, 65, 49, jpegSettings);
Check(Enumerable.Range(0, opaque.Length / 4).All(p => opaque[p * 4 + 3] == 255), "JPEG: flatten mode retained alpha");
var black = CompressionCodec.EncodeDecode(input, 65, 49, jpegSettings with { BackgroundColor = 0 });
Check(!black.SequenceEqual(opaque), "JPEG: background color had no effect");

foreach (CodecOptions codec in new[] { CodecOptions.JPEG, CodecOptions.HEIC, CodecOptions.AV1_AVIF })
{
    byte[][] chromaResults = new[] { ChromaOptions.YUV444, ChromaOptions.YUV422, ChromaOptions.YUV420 }
        .Select(chroma => CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(Codec: codec, Chroma: chroma))).ToArray();
    Check(!chromaResults[0].SequenceEqual(chromaResults[1]) && !chromaResults[1].SequenceEqual(chromaResults[2]), $"{codec}: chroma choices produced identical pixels");
}
foreach (CodecOptions codec in new[] { CodecOptions.WebP, CodecOptions.AV1_AVIF })
{
    var low = CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(Codec: codec, AlphaQuality: 5));
    var high = CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(Codec: codec, AlphaQuality: 100));
    Check(Enumerable.Range(0, input.Length / 4).Any(p => low[p * 4 + 3] != high[p * 4 + 3]), $"{codec}: alpha quality had no effect");
    SameAlpha(input, high, $"{codec}: alpha quality 100 was not lossless");
}
foreach (int effort in new[] { 0, 9 })
foreach (CodecOptions codec in new[] { CodecOptions.JPEG_XL, CodecOptions.WebP })
    Check(CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(Codec: codec, Effort: effort)).Length == input.Length, $"{codec}: effort endpoint failed");
foreach (int speed in new[] { 0, 9 })
    Check(CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(Codec: CodecOptions.AV1_AVIF, AvifSpeed: speed)).Length == input.Length, "AVIF: speed endpoint failed");
try
{
    CompressionCodec.EncodeDecode(input, 65, 49, new CompressionSettings(), () => true);
    throw new InvalidOperationException("Cancellation was ignored");
}
catch (OperationCanceledException) { checks++; }
Check(edge.SequenceEqual(new byte[] { 20, 60, 180, 128, 99, 77, 55, 0, 10, 20, 30, 255 }), "JPEG mutated the original buffer");
Console.WriteLine($"PASS: {checks} codec, alpha, chroma, lossless, and cancellation checks.");

if (args.Contains("--benchmark"))
{
    const int width = 1024, height = 768;
    byte[] benchmarkInput = Fixture(width, height, false);
    Console.WriteLine($"AVIF benchmark {width}x{height}; CPUs={Environment.ProcessorCount}; auto threads={AvifCodec.GetThreadCount(0)}");
    foreach (var run in new[] { (Speed: 6, Threads: 1), (Speed: 6, Threads: 0), (Speed: 9, Threads: 0) })
    {
        var watch = Stopwatch.StartNew();
        var result = CompressionCodec.EncodeDecode(benchmarkInput, width, height, new CompressionSettings(Codec: CodecOptions.AV1_AVIF, AvifSpeed: run.Speed, AvifThreads: run.Threads));
        Console.WriteLine($"Direct AVIF speed={run.Speed} threads={AvifCodec.GetThreadCount(run.Threads)}: {watch.Elapsed.TotalSeconds:F3}s");
    }
    using var oldImage = new MagickImage(benchmarkInput, new MagickReadSettings { Width = width, Height = height, Depth = 8, Format = MagickFormat.Bgra }) { Format = MagickFormat.Avif, Quality = 35 };
    oldImage.Settings.SetDefine(MagickFormat.Heic, "speed", "4");
    var oldWatch = Stopwatch.StartNew();
    using var stream = new MemoryStream();
    oldImage.Write(stream);
    stream.Position = 0;
    using var oldDecoded = new MagickImage(stream);
    using var oldPixels = oldDecoded.GetPixels();
    _ = oldPixels.ToByteArray(PixelMapping.BGRA);
    Console.WriteLine($"ImageMagick AVIF comparison speed=4: {oldWatch.Elapsed.TotalSeconds:F3}s");
}
