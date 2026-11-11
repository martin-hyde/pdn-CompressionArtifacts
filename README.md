# Compression Artifacts

Compression Artifacts is a paint.net effect for trying image compression directly on the canvas. Preview the result, adjust the settings, and apply it when you like what you see—all without exporting a file and opening it again.

It is useful for two things:

- **Exploring compression for smaller files.** See how lower quality and different codecs affect text, fine detail, gradients, and transparent edges before choosing your export settings.
- **Using compression as an aesthetic.** Add blockiness, color bleeding, softened detail, and other compression artifacts to artwork, textures, or deliberately degraded images.

You can cancel the preview or undo the effect like any other paint.net edit. Apply it to a duplicate layer to keep the original available while you compare or combine the results.

The effect changes pixels; it does not export a compressed file, estimate its size, or make your project file smaller. Actual file-size savings depend on the format and settings you use when saving. For a smaller final file, use the preview to explore the look, then export from the original image with your chosen settings. Exporting an already compressed result to another lossy format can introduce additional artifacts.

## Requirements

- paint.net **5.2 or newer**
- Windows **x64** (Intel/AMD)

You do not need to install the .NET SDK to use the plugin.

## Installation

1. Download [CompressionArtifacts-v1.0.0-win-x64.zip](https://github.com/martin-hyde/pdn-CompressionArtifacts/releases/download/v1.0.0/CompressionArtifacts-v1.0.0-win-x64.zip) from the [Releases page](https://github.com/martin-hyde/pdn-CompressionArtifacts/releases).
2. Close paint.net and extract the ZIP.
3. Copy the extracted `CompressionArtifacts` folder into paint.net's `Effects` folder. For a typical desktop installation, this is `C:\Program Files\paint.net\Effects`.
4. Restart paint.net and open **Effects → Photo → Compression Artifacts**.

Copy the whole plugin folder: the effect needs the DLLs beside it. Keep the release's license notices and dependency sources with your downloaded package. The release ZIP is the ready-to-install download; GitHub's automatically generated source archives contain the project source.

## Using the effect

1. Open an image. To keep an editable original, duplicate its layer and apply the effect to the copy.
2. Open **Effects → Photo → Compression Artifacts**.
3. Choose a codec and adjust **Quality / fidelity**. Lower values generally produce stronger artifacts.
4. Adjust the available codec settings while watching the preview. Controls that do not apply are greyed out.
5. Choose **OK** to apply the result, or **Cancel** to leave the image as it was. Use **Undo** to reverse an applied effect.

## Supported codecs

| Codec | Available settings |
| --- | --- |
| JPEG | Quality, chroma subsampling, background color, preservation of original transparency |
| JPEG XL | Quality, encoding effort, lossless mode |
| JPEG XR | Quality; retains an alpha channel |
| WebP | Quality, encoding effort, alpha quality, lossless mode |
| HEIC approximation | Quality, chroma subsampling |
| AV1 / AVIF | Quality, chroma subsampling, encoding speed, CPU threads, alpha quality, lossless mode |

JPEG, JPEG XL, JPEG XR, WebP, and AVIF use real encoding and decoding in memory. **HEIC is an approximation**, not an HEVC encode, and should not be used to judge how an actual HEIC export will look.

**Lossless mode** leaves the original pixels unchanged. It adds no artifacts and skips encoding, so settings that affect lossy artifacts or encoding effort are disabled.

### JPEG and transparency

**Preserve original transparency** keeps the original alpha channel and applies JPEG compression to the color channels. This lets you add JPEG artifacts to a transparent layer even though JPEG files themselves do not support transparency.

With preservation enabled, the background color fills fully transparent pixels during compression. It can influence color bleeding around their edges. With preservation disabled, transparent and partially transparent pixels are flattened against the chosen color. The color picker accepts arbitrary colors, including hexadecimal RGB values.

### AVIF speed

AVIF uses a multithreaded CPU encoder. **Speed 9** is the fastest setting and the default; lower values spend more time encoding and can produce different artifacts at the same quality.

**CPU threads 0** selects automatically, using up to 16 logical processors. You can select 1–64 threads manually. Extra threads are most useful for larger images; they may make little difference on small images.

Very slow settings can delay preview updates and cancellation because an AVIF encode must finish before it can stop.

## Building and contributing

Building requires the **.NET 11 SDK**. From the repository root:

```powershell
dotnet publish CompressionArtifacts/CompressionArtifacts.csproj -c Release -r win-x64 --self-contained false -o CompressionArtifacts/dist/CompressionArtifacts
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the source layout, verification commands, and release packaging instructions.

## License

The plugin source is available under the [Zero-Clause BSD license](LICENSE.md). Codec dependencies have their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
