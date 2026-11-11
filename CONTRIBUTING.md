# Building and contributing

## Development requirements

- Windows x64
- .NET 11 SDK
- paint.net 5.2 or newer for installing the effect and checking it in the application

The project references `Paint.NET.Plugin.SDK` through NuGet. A local paint.net installation is not needed to compile it. NuGet restores the codec dependencies during the build.

Run the commands below from the repository root.

## Source layout

| Path | Purpose |
| --- | --- |
| `CompressionArtifacts/CompressionArtifacts.cs` | Effect entry point, configuration controls, and paint.net rendering |
| `CompressionArtifacts/CompressionCodec.cs` | Codec round trips, JPEG transparency handling, and the HEIC approximation |
| `CompressionArtifacts/AvifCodec.cs` | AVIF encoding, decoding, and CPU thread selection |
| `CompressionArtifacts/CompressionArtifacts.csproj` | Build configuration and dependency versions |
| `CompressionArtifacts/CompressionArtifacts.png` | Effect icon |
| `CompressionArtifacts/CodecProbe/` | Codec regression checks and optional AVIF benchmarks |
| `CompressionArtifacts/UiProbe/` | Control-state checks against an installed paint.net runtime |
| `licenses/` | Third-party license texts included in release packages |

## Build

```powershell
dotnet publish CompressionArtifacts/CompressionArtifacts.csproj -c Release -r win-x64 --self-contained false -o CompressionArtifacts/dist/CompressionArtifacts
```

The output folder contains the plugin and the codec DLLs it needs. Copy that folder into paint.net's `Effects` folder while paint.net is closed, then restart the application.

## Verification

Run the codec checks:

```powershell
dotnet run --project CompressionArtifacts/CodecProbe -c Release
```

These cover codec round trips, odd image dimensions, JPEG matting and alpha restoration, lossless pixel identity, chroma selection, alpha quality, effort/speed endpoints, and cancellation checks.

To measure AVIF performance on your machine, add `--benchmark`:

```powershell
dotnet run --project CompressionArtifacts/CodecProbe -c Release -- --benchmark
```

The benchmark uses a generated image. Results depend on the image, CPU, thread count, and speed setting. Compare thread counts at the same speed setting when measuring the benefit of multithreading.

Run the control-state checks against an installed stable paint.net runtime. Change the path if paint.net is installed elsewhere:

```powershell
dotnet run --project CompressionArtifacts/UiProbe -c Release -- "C:\Program Files\paint.net"
```

These check which controls are enabled when the codec or lossless setting changes, including cloned settings. The check calls this project's configuration factory directly and uses the SDK's public property APIs. It does not use reflection to discover or invoke members. Its assembly loader resolves the SDK's public runtime assemblies from the specified installation; it does not inspect paint.net internals.

The automated checks do not inspect the dialog visually or render through the paint.net canvas. Before releasing a change, also check the effect in paint.net with both opaque and transparent images, a small rectangular selection, and a nonrectangular selection. Confirm that pixels outside the selection and the original layer remain unchanged.

See [PLUGIN-RULES.md](PLUGIN-RULES.md) for the rule-by-rule source audit and the implementation boundaries to preserve.

## Release packaging

The first public release is **v1.0.0**. Its assembly version is `1.0.0.0` in `CompressionArtifacts.cs`. Keep the assembly version and release tag consistent when preparing future releases.

1. Build and run the relevant checks.
2. Copy the repository documentation and license texts into the distribution folder:

   ```powershell
   Copy-Item README.md, LICENSE.md, THIRD-PARTY-NOTICES.md, CONTRIBUTING.md, CHANGELOG.md, PLUGIN-RULES.md -Destination CompressionArtifacts/dist
   Copy-Item licenses -Destination CompressionArtifacts/dist -Recurse -Force
   ```

3. Include the unmodified dependency source archives under `CompressionArtifacts/dist/sources`. The initial release includes these archives:

   - [libheif 1.20.2](https://codeload.github.com/strukturag/libheif/tar.gz/refs/tags/v1.20.2), saved as `libheif-1.20.2.tar.gz`
   - [LibHeifSharp 3.2.0 source revision](https://codeload.github.com/0xC0000054/libheif-sharp/tar.gz/d208cd6f3ba9023c6c3692678a7c421c3e33c7b9), saved as `libheif-sharp-3.2.0.tar.gz`

   See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency versions and source references. Update the notices and supplied sources when changing dependencies.

4. Create the installable archive:

   ```powershell
   Compress-Archive -Path CompressionArtifacts/dist/* -DestinationPath CompressionArtifacts/CompressionArtifacts-v1.0.0-win-x64.zip -Force
   ```

5. Attach the ZIP to the GitHub release. Users should download this package to install the plugin.

Commit the source, project files, icon, documentation, and license texts. The `.gitignore` excludes build output, local build tools, the distribution folder, and generated ZIP/7z archives. Publish installable archives as release assets.
