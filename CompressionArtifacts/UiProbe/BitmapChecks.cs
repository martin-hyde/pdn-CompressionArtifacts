using CompressionArtifactsEffect;
using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System.Runtime.CompilerServices;

internal static class BitmapChecks
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static unsafe int Run()
    {
        const int width = 9, height = 7, guard = 20;
        byte[] pixels = new byte[width * height * 4];
        new Random(546).NextBytes(pixels);
        byte[] original = (byte[])pixels.Clone();
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }

        foreach (int sourceHeight in new[] { 1, 129 })
        {
            byte[] sourcePixels = new byte[17 * sourceHeight * 4];
            new Random(27).NextBytes(sourcePixels);
            byte[] expectedPixels = (byte[])sourcePixels.Clone();
            using var bitmap = new ArrayBitmapSource(sourcePixels, 17, sourceHeight);
            byte[] read = BitmapTransfer.ReadSource(bitmap, CancellationToken.None);
            Check(read.SequenceEqual(expectedPixels), "Bulk source copy changed pixel order or missed a final strip.");
            Check(sourcePixels.SequenceEqual(expectedPixels) && !bitmap.IsDisposed, "Source copy modified or disposed the borrowed bitmap.");
        }

        using (var canceled = new CancellationTokenSource())
        using (var bitmap = new ArrayBitmapSource(new byte[17 * 129 * 4], 17, 129) { AfterCopy = canceled.Cancel })
        {
            bool threw = false;
            try { BitmapTransfer.ReadSource(bitmap, canceled.Token); }
            catch (OperationCanceledException) { threw = true; }
            Check(threw && bitmap.CopyCalls == 1, "Source copy did not stop between strips after cancellation.");
        }

        foreach (RectInt32 bounds in new[] { new RectInt32(0, 0, width, height), new RectInt32(2, 1, 3, 5), new RectInt32(8, 6, 1, 1) })
        foreach (int padding in new[] { 0, 12 })
        {
            int stride = bounds.Width * 4 + padding;
            byte[] destination = Enumerable.Repeat((byte)0xCD, guard + stride * bounds.Height + guard).ToArray();
            byte[] expected = (byte[])destination.Clone();
            int rowZero = guard;
            int rowStride = stride;
            for (int y = 0; y < bounds.Height; y++)
            for (int x = 0; x < bounds.Width * 4; x++)
                expected[rowZero + y * rowStride + x] = pixels[((bounds.Top + y) * width + bounds.Left) * 4 + x];

            fixed (byte* pointer = destination)
            {
                var region = new RegionPtr<ColorBgra32>((ColorBgra32*)(pointer + rowZero), bounds.Width, bounds.Height, rowStride);
                BitmapTransfer.CopyRegion(pixels, width, height, bounds, region, CancellationToken.None);
                Check(destination.SequenceEqual(expected), "Region copy changed pixels outside its requested bounds or row padding.");
                Array.Fill(destination, (byte)0xCD);
                using var canceled = new CancellationTokenSource();
                canceled.Cancel();
                bool threw = false;
                try { BitmapTransfer.CopyRegion(pixels, width, height, bounds, region, canceled.Token); }
                catch (OperationCanceledException) { threw = true; }
                Check(threw && destination.All(value => value == 0xCD), "A canceled region copy wrote destination pixels.");
            }
        }

        Check(pixels.SequenceEqual(original), "Region copies changed the source buffer.");
        using (IImagingFactory factory = ImagingFactory.CreateRef())
        using (IColorContext srgb = factory.CreateColorContext(KnownColorSpace.Srgb))
        using (IColorContext displayP3 = factory.CreateColorContext(KnownColorSpace.DisplayP3))
        using (var bitmap = new ArrayBitmapSource([128, 128, 128, 128, 0, 0, 255, 255, 0, 255, 0, 64], 3, 1))
        {
            byte[] unchanged = BitmapTransfer.ReadSource(bitmap, srgb, srgb, CancellationToken.None);
            Check(unchanged.SequenceEqual(new byte[] { 128, 128, 128, 128, 0, 0, 255, 255, 0, 255, 0, 64 }), "Matching color profiles must preserve exact pixels.");
            byte[] converted = BitmapTransfer.ReadSource(bitmap, srgb, displayP3, CancellationToken.None);
            Check(converted[3] == 128 && converted[7] == 255 && converted[11] == 64, "Color conversion changed transparency.");
            Check(Math.Abs(converted[0] - 128) <= 2 && Math.Abs(converted[1] - 128) <= 2 && Math.Abs(converted[2] - 128) <= 2, "Color conversion changed a neutral gray.");
            // sRGB red expressed in Display P3 is approximately RGB (234, 51, 35).
            Check(Math.Abs(converted[4] - 35) <= 4 && Math.Abs(converted[5] - 51) <= 4 && Math.Abs(converted[6] - 234) <= 4, "Decoded pixels were not converted to the target profile.");
            Check(!bitmap.IsDisposed && !srgb.IsDisposed && !displayP3.IsDisposed, "Color conversion disposed borrowed inputs.");
        }
        byte[] untouched = Enumerable.Repeat((byte)0xCD, 4).ToArray();
        fixed (byte* pointer = untouched)
        {
            var region = new RegionPtr<ColorBgra32>((ColorBgra32*)pointer, 1, 1, 4);
            bool rejected = false;
            try { BitmapTransfer.CopyRegion(pixels, width, height, new RectInt32(-1, 0, 1, 1), region, CancellationToken.None); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && untouched.All(value => value == 0xCD), "Invalid source bounds were not rejected before writing.");
        }

        Console.WriteLine($"PASS: {checks} bitmap-transfer and color-profile checks, including alpha preservation, padded strides, output regions, and cancellation.");
        return 0;
    }

    private sealed class ArrayBitmapSource(byte[] pixels, int width, int height)
        : BitmapSourceBase<ColorBgra32>(new SizeInt32(width, height))
    {
        internal int CopyCalls { get; private set; }
        internal Action? AfterCopy { get; init; }

        protected override unsafe void OnCopyPixels(RegionPtr<ColorBgra32> destination, Point2Int32 sourceOffset)
        {
            fixed (byte* pointer = pixels)
            {
                var source = new RegionPtr<ColorBgra32>((ColorBgra32*)pointer, width, height, width * 4);
                source.Slice(new RectInt32(sourceOffset.X, sourceOffset.Y, destination.Width, destination.Height)).CopyTo(destination);
            }
            CopyCalls++;
            AfterCopy?.Invoke();
        }
    }
}
