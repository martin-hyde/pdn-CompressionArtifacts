using CompressionArtifactsEffect;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}
void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException(message);
}
byte[] input = new byte[65 * 49 * 4];
new Random(546).NextBytes(input);
byte[] original = (byte[])input.Clone();
var backend = new EchoBackend();
foreach (CodecOptions codec in new[] { CodecOptions.JPEG_XL, CodecOptions.WebP, CodecOptions.AV1_AVIF })
{
    byte[] lossless = CompressionCodec.EncodeDecode(input, 65, 49, new(Codec: codec, Lossless: true));
    Check(lossless.SequenceEqual(input) && !ReferenceEquals(lossless, input), $"{codec}: lossless must preserve pixels without sharing the input array");
}
// Exact matting values catch double application of partial transparency.
byte[] edge = [20, 60, 180, 128, 99, 77, 55, 0, 10, 20, 30, 255];
foreach (CodecOptions codec in new[] { CodecOptions.JPEG, CodecOptions.HEIC })
{
    var settings = new CompressionSettings(Codec: codec, BackgroundColor: 0x123456, PreserveOriginalAlpha: false);
    Check(CompressionCodec.PrepareOpaquePixels(edge, settings).SequenceEqual(new byte[] { 53, 56, 99, 255, 86, 52, 18, 255, 10, 20, 30, 255 }), $"{codec}: custom matte");
    Check(CompressionCodec.PrepareOpaquePixels(edge, settings with { PreserveOriginalAlpha = true }).SequenceEqual(new byte[] { 20, 60, 180, 255, 86, 52, 18, 255, 10, 20, 30, 255 }), $"{codec}: straight RGB");
    byte[] restored = CompressionCodec.EncodeDecode(edge, 3, 1, settings with { PreserveOriginalAlpha = true }, backend: backend);
    Check(restored[3] == 128 && restored[7] == 0 && restored[11] == 255, $"{codec}: restores original alpha after the backend");
    Check(backend.LastPixels!.SequenceEqual(new byte[] { 20, 60, 180, 255, 86, 52, 18, 255, 10, 20, 30, 255 }), $"{codec}: encoder receives opaque RGB with transparent-pixel fill");
    byte[] opaque = CompressionCodec.EncodeDecode(edge, 3, 1, settings, backend: backend);
    Check(opaque.SequenceEqual(new byte[] { 53, 56, 99, 255, 86, 52, 18, 255, 10, 20, 30, 255 }), $"{codec}: flatten mode uses the chosen color");
    Check(edge.SequenceEqual(new byte[] { 20, 60, 180, 128, 99, 77, 55, 0, 10, 20, 30, 255 }), $"{codec}: source preservation");
}
foreach (CodecOptions codec in new[] { CodecOptions.JPEG_XL, CodecOptions.JPEG_XR, CodecOptions.WebP, CodecOptions.HEIC, CodecOptions.AV1_AVIF })
{
    var settings = new CompressionSettings(Codec: codec, Quality: 17, Effort: 9, LosslessAlpha: false);
    _ = CompressionCodec.EncodeDecode(input, 65, 49, settings, backend: backend);
    Check(backend.LastSettings == settings && backend.Width == 65 && backend.Height == 49, $"{codec}: settings and dimensions reach the backend");
}
Throws<InvalidOperationException>(() => CompressionCodec.EncodeDecode(input, 65, 49, new(Codec: CodecOptions.HEIC)), "HEIC must require the real backend");
var missingHeic = new NotSupportedException("No HEIC encoder is installed.");
try
{
    CompressionCodec.EncodeDecode(input, 65, 49, new(Codec: CodecOptions.HEIC), backend: new EchoBackend { Error = missingHeic });
    throw new InvalidOperationException("Missing HEIC support must fail without approximating");
}
catch (NotSupportedException ex)
{
    Check(ReferenceEquals(ex, missingHeic), "Propagate the HEIC availability error unchanged");
}
Check(input.SequenceEqual(original), "Source preservation");
Throws<ArgumentException>(() => CompressionCodec.EncodeDecode(input, 0, 49, new()), "Invalid dimensions");
Throws<ArgumentException>(() => CompressionCodec.EncodeDecode(input, 64, 49, new()), "Mismatched buffer");
Throws<InvalidOperationException>(() => CompressionCodec.EncodeDecode(input, 65, 49, new()), "Real codecs need a host backend");
Throws<OperationCanceledException>(() => CompressionCodec.EncodeDecode(input, 65, 49, new(), () => true, backend), "Pre-cancel");
int cancellationChecks = 0;
Throws<OperationCanceledException>(() => CompressionCodec.EncodeDecode(input, 65, 49, new(Codec: CodecOptions.HEIC), () => ++cancellationChecks > 1, backend), "Check cancellation after a native HEIC round trip");
Throws<System.IO.InvalidDataException>(() => CompressionCodec.EncodeDecode(input, 65, 49, new(), backend: new EchoBackend { WrongSize = true }), "Reject changed dimensions");
Console.WriteLine($"PASS: {checks} JPEG/HEIC alpha and fill, native-only HEIC, lossless, backend contract, and cancellation checks.");
sealed class EchoBackend : ICompressionBackend
{
    public CompressionSettings? LastSettings { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool WrongSize { get; init; }
    public Exception? Error { get; init; }
    public byte[]? LastPixels { get; private set; }
    public byte[] RoundTrip(byte[] pixels, int width, int height, CompressionSettings settings, Func<bool>? isCanceled)
    {
        LastSettings = settings; Width = width; Height = height;
        LastPixels = (byte[])pixels.Clone();
        if (Error is not null) throw Error;
        return WrongSize ? [] : (byte[])pixels.Clone();
    }
}
