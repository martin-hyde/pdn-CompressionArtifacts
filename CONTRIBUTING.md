# Building and contributing

## Development requirements

- Windows x64
- .NET 11 SDK
- paint.net 5.2 or newer for installing the effect and checking it in the application

The project references `Paint.NET.Plugin.SDK` through NuGet. A local paint.net installation is not needed to compile it. Encoding and decoding use paint.net's public FileTypes service, so no codec libraries are bundled.

Run the commands below from the repository root.

## Source layout

| Path | Purpose |
| --- | --- |
| `CompressionArtifacts/CompressionArtifacts.cs` | Effect entry point, configuration controls, and paint.net rendering |
| `CompressionArtifacts/BitmapTransfer.cs` | Bulk source reads and output-region copies using the public bitmap APIs |
| `CompressionArtifacts/CompressionCodec.cs` | Round-trip coordination and shared JPEG/HEIC transparency handling |
| `CompressionArtifacts/PaintNetCodecBackend.cs` | In-memory documents, streams, and paint.net FileType loaders/savers |
| `CompressionArtifacts/HostSaveOptions.cs` | Mappings to publicly exposed FileType save properties |
| `CompressionArtifacts/CompressionArtifacts.csproj` | Build configuration and dependency versions |
| `CompressionArtifacts/CompressionArtifacts.png` | Effect icon |
| `CompressionArtifacts/CodecProbe/` | JPEG/HEIC alpha and fill, native-only HEIC, lossless, cancellation, and backend-contract checks |
| `CompressionArtifacts/UiProbe/` | Control-state and bitmap-transfer checks against an installed paint.net runtime |

## Build

```powershell
dotnet publish CompressionArtifacts/CompressionArtifacts.csproj -c Release -r win-x64 --self-contained false -o CompressionArtifacts/dist/CompressionArtifacts
```

Copy `CompressionArtifacts.dll` and `CompressionArtifacts.deps.json` from the output into a `CompressionArtifacts` folder inside paint.net's `Effects` folder while paint.net is closed, then restart the application.

## Verification

Run the codec checks:

```powershell
dotnet run --project CompressionArtifacts/CodecProbe -c Release
```

These cover shared JPEG/HEIC matting and alpha restoration, opaque encoder inputs, lossless pixel identity, backend settings/dimensions, source preservation, and cancellation. They also verify that HEIC requires a native backend and propagates encoder availability failures without approximating. A test backend exercises the coordination code without pretending to test paint.net's codecs outside their host.

Run the control-state checks against an installed stable paint.net runtime. Change the path if paint.net is installed elsewhere:

```powershell
dotnet run --project CompressionArtifacts/UiProbe -c Release -- "C:\Program Files\paint.net"
```

These check which controls are enabled when the codec or lossless setting changes, including cloned settings, as well as bulk source reads, output-region offsets, row padding, source preservation, and cancellation. Color-profile checks verify exact pixels for matching profiles and sRGB-to-Display-P3 conversion with alpha preservation. The checks call this project's helpers directly and use the SDK's public APIs. A test source derives from the public `BitmapSourceBase<ColorBgra32>` extensibility class. No members are discovered or invoked through reflection. The assembly loader resolves the SDK's public runtime assemblies from the specified installation; it does not inspect paint.net internals.

The automated checks do not invoke paint.net's native encoders or render through its canvas. Before releasing a change, check every real codec in paint.net with both opaque and transparent images, different quality settings, and both effort endpoints where available. Check a small rectangular selection and a nonrectangular selection; confirm that pixels outside the selection and the original layer remain unchanged. The shared JPEG/HEIC alpha-preservation option must preserve every original alpha value.

Use only public SDK interfaces. FileTypes are obtained through `IFileTypesService` and recreated for each round trip; loaders and savers are single-use. Save-option property names are supplied by the installed FileType, so verify their mappings when paint.net changes. Respect `SupportsCancellationExceptions` before throwing from a native saver callback. HEIC always uses the real FileType and must fail if its Windows codec is unavailable; do not add an approximation fallback.

## Release packaging

Keep the assembly version and GitHub release tag consistent when preparing a release. Build and run the relevant checks first.

The install ZIP contains only `CompressionArtifacts/CompressionArtifacts.dll`, `CompressionArtifacts/CompressionArtifacts.deps.json`, `INSTALL.txt`, and `LICENSE.md`. Debug symbols, tests, developer documentation, SDK/runtime files, and codec libraries are excluded.

Create the ZIP with the packaging script, which uses an explicit list of runtime and legal files:

```powershell
./scripts/Package.ps1 -OutputPath CompressionArtifacts/CompressionArtifacts-v2.0.0-win-x64.zip
```

Use the appropriate version in the output filename. The script also accepts `-RuntimeDirectory` when using another build location. The published v1.0.0 package predates the service migration and retains its original encoder dependencies and legal materials; do not replace its binaries with a different source revision.

Attach the ZIP to the GitHub release. Commit source, project files, the icon, user/developer documentation, packaging scripts, and license texts to the repository. The `.gitignore` excludes build output, local build tools, distribution files, and generated archives.
