#nullable enable
using PaintDotNet;
using PaintDotNet.ComponentModel;
using PaintDotNet.FileTypes;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.IO;
using System.Threading;

namespace CompressionArtifactsEffect;

internal sealed class PaintNetCodecBackend(
    IFileTypesService fileTypes,
    IFileTypeDocumentFactory documents,
    IColorContext? colorContext = null,
    CancellationToken cancellation = default) : ICompressionBackend
{
    public byte[] RoundTrip(byte[] pixels, int width, int height, CompressionSettings settings, Func<bool>? isCanceled)
    {
        CheckCanceled(isCanceled);
        using var stream = new MemoryStream();
        Save(stream, pixels, width, height, settings, isCanceled);
        CheckCanceled(isCanceled);
        stream.Position = 0;
        using IFileType loadType = CreateFileType(settings.Codec, forSaving: false);
        using IFileTypeLoader loader = loadType.CreateLoader();
        using IFileTypeDocument decoded = loader.Load(stream);
        if (decoded.Size.Width != width || decoded.Size.Height != height)
            throw new InvalidDataException("The codec changed the image dimensions.");
        using IFileTypeCompositeBitmap composite = decoded.GetCompositeBitmap(PixelFormats.Bgra32);
        using IColorContext decodedColorContext = decoded.GetColorContext();
        using IBitmapSource<ColorBgra32> source = composite.Cast<ColorBgra32>();
        byte[] result = BitmapTransfer.ReadSource(source, decodedColorContext, colorContext, cancellation);
        CheckCanceled(isCanceled);
        return result;
    }

    private void Save(Stream stream, byte[] pixels, int width, int height, CompressionSettings settings, Func<bool>? isCanceled)
    {
        // Dispose the temporary document and saver before allocating the decoded image.
        using IFileType type = CreateFileType(settings.Codec, forSaving: true);
        using IFileTypeSaver saver = type.CreateSaver();
        IFileTypeSaveOptions options = saver.CreateDefaultSaveOptions();
        Configure(options, settings);
        using IFileTypeDocument document = documents.CreateDocument(width, height, PixelFormats.Bgra32);
        if (colorContext is not null) document.SetColorContext(colorContext);
        using IFileTypeDocument<ColorBgra32> typedDocument = document.CreateRef<IFileTypeDocument<ColorBgra32>>();
        using (IFileTypeBitmapLayer<ColorBgra32> layer = typedDocument.CreateBitmapLayer())
        {
            using (IFileTypeBitmapSink<ColorBgra32> bitmap = layer.GetBitmap())
            using (IFileTypeBitmapLock<ColorBgra32> bitmapLock = bitmap.Lock())
            {
                BitmapTransfer.CopyRegion(pixels, width, height, new RectInt32(0, 0, width, height),
                    bitmapLock.AsRegionPtr(), cancellation);
            }
            typedDocument.Layers.Add(layer);
        }

        saver.Save(stream, document, options, (_, _) =>
        {
            // Some native FileTypes cannot safely unwind a cancellation exception.
            if (type.Info.SupportsCancellationExceptions) CheckCanceled(isCanceled);
        });
    }

    internal IFileType CreateFileType(CodecOptions codec, bool forSaving)
    {
        if (codec == CodecOptions.JPEG) return fileTypes.CreateJpegFileType();
        string[] extensions = codec switch
        {
            CodecOptions.JPEG_XL => [".jxl"],
            CodecOptions.JPEG_XR => [".jxr", ".wdp", ".wmp"],
            CodecOptions.WebP => [".webp"],
            CodecOptions.AV1_AVIF => [".avif"],
            CodecOptions.HEIC => [".heic", ".heif", ".hif"],
            _ => throw new ArgumentOutOfRangeException(nameof(codec)),
        };
        foreach (string extension in extensions)
        {
            IFileTypeInfo? info = forSaving
                ? fileTypes.FindFileTypeForSavingExtension(extension)
                : fileTypes.FindFileTypeForLoadingExtension(extension);
            if (info is not null) return info.CreateInstance();
        }
        throw new NotSupportedException($"paint.net does not have an available {codec} {(forSaving ? "encoder" : "decoder")}.");
    }

    internal static void Configure(IFileTypeSaveOptions options, CompressionSettings settings)
    {
        if (options is IJpegFileTypeSaveOptions jpeg)
        {
            jpeg.Quality = settings.Quality;
            if (settings.Chroma != ChromaOptions.Auto)
            {
                jpeg.YCrCbSubsampling = settings.Chroma switch
                {
                    ChromaOptions.YUV444 => JpegYCrCbSubsamplingOption.Subsample444,
                    ChromaOptions.YUV422 => JpegYCrCbSubsamplingOption.Subsample422,
                    _ => JpegYCrCbSubsamplingOption.Subsample420,
                };
            }
            return;
        }
        if (options is not IPropertyBasedFileTypeSaveOptions propertyOptions)
            throw new NotSupportedException($"The installed {settings.Codec} FileType does not expose supported save options.");
        HostSaveOptions.Configure(propertyOptions.Properties, settings);
    }

    private static void CheckCanceled(Func<bool>? isCanceled)
    {
        if (isCanceled?.Invoke() == true) throw new OperationCanceledException();
    }
}
