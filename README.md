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

1. Download [CompressionArtifacts-v2.0.0-win-x64.7z](https://github.com/martin-hyde/pdn-CompressionArtifacts/releases/download/v2.0.0/CompressionArtifacts-v2.0.0-win-x64.7z) from the [Releases page](https://github.com/martin-hyde/pdn-CompressionArtifacts/releases).
2. Close paint.net and extract the ZIP.
3. Copy the extracted `CompressionArtifacts.dll` file into paint.net's `Effects` folder. For a typical desktop installation, this is `C:\Program Files\paint.net\Effects`.
4. Restart paint.net and open **Effects → Photo → Compression Artifacts**.

Copy the whole plugin folder. The release ZIP is the ready-to-install download; GitHub's automatically generated source archives contain the project source.

When updating from the version with bundled encoders, replace the old `CompressionArtifacts.dll` plugin file with the extracted one.

Version 2 uses paint.net's codecs and needs no bundled encoder libraries.

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
| WebP | Quality, encoding effort, lossless mode |
| HEIC | Quality, background color, preservation of original transparency; requires working Windows HEIC/HEVC codec support |
| AV1 / AVIF | Quality, chroma subsampling, encoding effort, lossless alpha, lossless mode |

All six formats use paint.net's public `IFileTypesService` to encode and decode in memory. **HEIC uses the real encoder only.** If Windows HEIC/HEVC codec support is unavailable, selecting HEIC reports an error. There is no approximation or fallback. Its encoder exposes only quality; chroma subsampling and encoding effort are disabled for it. The effect supplies the shared transparency and fill controls described below.

**Lossless mode** leaves the original pixels unchanged. It adds no artifacts and skips encoding, so settings that affect lossy artifacts or encoding effort are disabled.

### JPEG and HEIC transparency

**Preserve original transparency (JPEG/HEIC)** compresses the color channels and restores the original alpha channel after decoding. This lets you add real JPEG or HEIC compression artifacts to a transparent layer. Transparency is restored by the effect; it is not stored in the temporary compressed stream.

With preservation enabled, **JPEG/HEIC Alpha Fill** fills fully transparent pixels during compression. It can influence color bleeding around their edges. Partially transparent pixels retain their straight RGB values before alpha is restored, avoiding double matting. With preservation disabled, transparent and partially transparent pixels are flattened against the chosen color. Choose any RGB color using the color wheel or the R, G, and B fields.

### Encoding effort

JPEG XL, WebP, and AVIF share one **Encoding effort** control: **0 is fast, 9 is slow**. The default is 0. Higher effort spends more time encoding and can produce different artifacts at the same quality. JPEG XL maps this scale to effort 1–9, WebP to effort 0–9, and AVIF to paint.net's Fast, Medium, Slow, and VerySlow presets. Several adjacent slider positions therefore use the same AVIF preset.

paint.net's AVIF encoder manages its CPU threads internally. Its public save options do not expose a thread count. **Lossless alpha (AVIF)** preserves transparency exactly; disabling it lets AVIF compress the alpha channel lossily. WebP manages alpha compression internally.

Very slow settings can delay preview updates and cancellation because an AVIF encode must finish before it can stop.

## Building and contributing

Building requires the **.NET 11 SDK**. From the repository root:

```powershell
dotnet publish CompressionArtifacts/CompressionArtifacts.csproj -c Release -r win-x64 --self-contained false -o CompressionArtifacts/dist/CompressionArtifacts
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the source layout, verification commands, and release packaging instructions.

## License

The plugin source is available under the [Zero-Clause BSD license](LICENSE.md).
