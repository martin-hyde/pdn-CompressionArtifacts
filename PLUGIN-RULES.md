# paint.net plugin rules audit

This source review covers Compression Artifacts v1.0.0 against the rules supplied for publication. It records the implementation and checks performed; it is not approval from the paint.net maintainers.

| Rule | Finding |
| --- | --- |
| 1.1 — Do not write to the source surface | The effect reads its source bitmap into a separate byte array. It never assigns through the source bitmap lock. Codec operations use their own memory; all six codec paths pass a source-buffer preservation check. |
| 1.2 — Write only within the requested region | Destination writes occur inside loops bounded by `IBitmapEffectOutput.Bounds`. The output buffer is indexed relative to those bounds. This is the modern bitmap-effect API's region contract, corresponding to the older `rois` API. |
| 1.3 — Render only from Render/OnRender | `OnRender` is the only entry point that starts the effect's codec work or locks its output buffer. Encoding helpers are called from that render path. Configuration creation builds properties and controls; token changes invalidate the cache. Neither renders pixels. |
| 2 — No beta requirements | The project targets released .NET 11 and stable paint.net 5.2. Its package references use stable versions: Paint.NET.Plugin.SDK 5.2.0, Magick.NET-Q8-x64 14.15.0, LibHeifSharp 3.2.0, and LibHeif.Native.Runtime 1.20.2. The plugin does not require a separate SDK installation for end users. |
| 3 — No export masquerading or side effects | This is an effect, not a FileType plugin. It has no upload or export command and writes no user image files. Codec round trips take place in memory. |
| 4 — Use the supplied FileType stream | Not applicable: this effect implements no FileType API and receives no FileType stream. Its codec helpers create and use their own memory streams. |
| 5 — No reflection-based paint.net modifications | The plugin does not use reflection to change paint.net or add commands, tools, or toolbar items. Its normal Effects entry and configuration dialog use the supported SDK. |
| 6 — No access to paint.net internals or private resources | The plugin compiles against the official SDK reference assemblies. It does not inspect private/internal paint.net members or use paint.net UI resources. The effect icon is a project resource. The submenu uses the public `SubmenuNames.Photo` API. |
| 7 — Do not modify paint.net's UI | Only the normal effect registration and SDK-managed configuration are used. The plugin does not alter the application's menus, toolbar, tools, or other UI. |
| 8 — Do not reuse internal paint.net controls | The configuration uses `PropertyBasedBitmapEffect`, `ControlInfo`, and the supported IndirectUI property APIs. It does not instantiate controls such as `DocumentView`, `ImageListMenu`, `SurfaceBox`, or `TaskDialog`. |
| 9 — Do not use internal built-in FileTypes | No built-in paint.net FileType implementation is used. JPEG XR uses Windows Imaging Component through WPF; the other real codecs use the bundled third-party libraries. |
| 10 — Do not replace paint.net DLL/EXE files | Installation adds one plugin folder under `Effects`. The release includes only this plugin and its codec dependencies, with no paint.net assemblies or executables. |
| 11 — Do not use SystemLayer | The project has no SystemLayer reference or API usage. |

## Reflection and loading boundaries

`System.Reflection` supplies the plugin's own assembly metadata attributes and version lookup. The AVIF loader locates this plugin's codec DLL and registers a native-library resolver for LibHeifSharp. Neither operation inspects or changes paint.net.

The developer-only `UiProbe` uses a project reference and typed property APIs. `InternalsVisibleTo("UiProbe")` grants it access to this project's configuration factory; it grants no access to paint.net internals. The probe's .NET assembly loader finds the public runtime implementations for SDK types in an installed paint.net directory. The probe is excluded from the plugin and its release packages.

## Verification and limits

- The release build compiles against the stable SDK with no warnings or errors.
- Codec checks cover source-buffer preservation, round trips, odd dimensions, alpha handling, chroma, lossless output, speed/effort endpoints, and cancellation checks.
- Typed control checks cover codec/lossless transitions and cloned settings.
- Release contents are checked for accidental inclusion of paint.net assemblies, test executables, and local build tools.
- The source review verifies the destination bounds and render call path. The standalone probes do not run the effect through paint.net's canvas or test selection compositing. Complete the in-application checks in [CONTRIBUTING.md](CONTRIBUTING.md#verification) before publication.

The output buffer contract is documented by [IBitmapEffectOutput](https://paintdotnet.github.io/apidocs/api/PaintDotNet.Effects.IBitmapEffectOutput.html). Stable [paint.net 5.2](https://paint.net/roadmap.html) and [.NET 11.0](https://dotnet.microsoft.com/en-us/download/dotnet/11.0) were released on November 10, 2026.
