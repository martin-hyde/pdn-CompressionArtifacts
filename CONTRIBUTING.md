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

## Release packaging

Keep the assembly version and GitHub release tag consistent when preparing a release. Build and run the relevant checks first.

The install ZIP contains only seven runtime files under `CompressionArtifacts`, `INSTALL.txt`, the project and dependency license notices, and the supplied dependency sources. Debug symbols, tests, developer documentation, and build tools are excluded.

The dependency source archives must be available under `CompressionArtifacts/dist/sources`:

- [libheif 1.20.2](https://codeload.github.com/strukturag/libheif/tar.gz/refs/tags/v1.20.2), saved as `libheif-1.20.2.tar.gz`
- [LibHeifSharp 3.2.0 source revision](https://codeload.github.com/0xC0000054/libheif-sharp/tar.gz/d208cd6f3ba9023c6c3692678a7c421c3e33c7b9), saved as `libheif-sharp-3.2.0.tar.gz`

Create the ZIP with the packaging script, which uses an explicit list of runtime and legal files:

```powershell
./scripts/Package.ps1 -OutputPath CompressionArtifacts/CompressionArtifacts-v1.0.0-win-x64.zip
```

Use the appropriate version in the output filename. The script also accepts `-RuntimeDirectory` and `-SourcesDirectory` when using another build location. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency versions and source references; update the script and notices when changing dependencies.

Attach the ZIP to the GitHub release. Commit source, project files, the icon, user/developer documentation, packaging scripts, and license texts to the repository. The `.gitignore` excludes build output, local build tools, distribution files, and generated archives.
