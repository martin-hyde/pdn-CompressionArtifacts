#nullable enable
using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Threading;

namespace CompressionArtifactsEffect;

internal static class BitmapTransfer
{
    internal static byte[] ReadSource(IBitmapSource<ColorBgra32> source, IColorContext sourceColorContext,
        IColorContext? targetColorContext, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (targetColorContext is null || sourceColorContext.Equals(targetColorContext))
            return ReadSource(source, cancellation);

        // A codec may return a different profile. Match the working space before
        // writing its pixel values back into the effect's output.
        using IBitmapSource<ColorBgra32> converted = source.CreateColorTransformer(sourceColorContext, targetColorContext);
        return ReadSource(converted, cancellation);
    }

    internal static unsafe byte[] ReadSource(IBitmapSource<ColorBgra32> source, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        int width = source.Size.Width, height = source.Size.Height;
        int stride = checked(width * 4);
        byte[] pixels = new byte[checked(stride * height)];
        fixed (byte* pixelPointer = pixels)
        {
            var destination = new RegionPtr<ColorBgra32>((ColorBgra32*)pixelPointer, width, height, stride);
            // Bounded strips keep cancellation responsive without channel-by-channel
            // copying or retaining a source lock while a native codec is running.
            for (int top = 0; top < height; top += 64)
            {
                cancellation.ThrowIfCancellationRequested();
                int rows = Math.Min(64, height - top);
                source.CopyPixels(destination.SliceRows(top, rows), new Point2Int32(0, top));
            }
        }
        cancellation.ThrowIfCancellationRequested();
        return pixels;
    }

    internal static unsafe void CopyRegion(ReadOnlySpan<byte> pixels, int width, int height,
        RectInt32 bounds, RegionPtr<ColorBgra32> destination, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4))
            throw new ArgumentException("Pixel buffer does not match the image dimensions.", nameof(pixels));
        if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > width || bounds.Bottom > height ||
            destination.Width != bounds.Width || destination.Height != bounds.Height)
            throw new ArgumentOutOfRangeException(nameof(bounds));

        fixed (byte* pixelPointer = pixels)
        {
            var source = new RegionPtr<ColorBgra32>((ColorBgra32*)pixelPointer, width, height, checked(width * 4)).Slice(bounds);
            for (int row = 0; row < bounds.Height; row++)
            {
                cancellation.ThrowIfCancellationRequested();
                source.SliceRows(row, 1).CopyTo(destination.SliceRows(row, 1));
            }
        }
    }
}
