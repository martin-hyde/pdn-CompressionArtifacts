# Third-party components

The Compression Artifacts source uses the [Zero-Clause BSD license](LICENSE.md). Its codec dependencies have separate licenses. The release package contains the unmodified libraries listed below as separate DLLs. Compatible library DLLs may be substituted; reverse engineering to debug modifications to the LGPL libraries is permitted.

| Component | Version | License | Source |
| --- | --- | --- | --- |
| Magick.NET / ImageMagick and bundled codecs | Magick.NET 14.15.0 | Apache 2.0 / ImageMagick and component licenses | [Magick.NET source](https://github.com/dlemstra/Magick.NET/tree/14.15.0) |
| LibHeifSharp, Nicholas Hayes and contributors | 3.2.0 | LGPL 3.0 or later | [LibHeifSharp source revision](https://github.com/0xC0000054/libheif-sharp/tree/d208cd6f3ba9023c6c3692678a7c421c3e33c7b9) |
| libheif, Dirk Farin and contributors | 1.20.2 | LGPL 3.0 or later | [libheif source](https://github.com/strukturag/libheif/tree/v1.20.2) |
| LibHeif.Native.Runtime packaging, Boris Siljanovski | 1.20.2 | LGPL 3.0 or later | [Packaging repository and revision identified by the NuGet package](https://github.com/borka-s/LibHeif.Native.Runtime/tree/797a1d64036ca7a9aed612c194e958a2306bdab1) |

The native libheif package includes libaom (BSD 2-Clause), dav1d (BSD 2-Clause), and libde265 (LGPL 3.0 or later). Their upstream sources are available at:

- [libaom](https://aomedia.googlesource.com/aom/)
- [dav1d](https://code.videolan.org/videolan/dav1d)
- [libde265](https://github.com/strukturag/libde265)

The repository's `licenses` folder contains the dependency license texts. Release packages also include this folder and the upstream libheif and LibHeifSharp source archives under `sources`. See [CONTRIBUTING.md](CONTRIBUTING.md#release-packaging) for the source archive references and packaging instructions.

`licenses/Magick.NET-Notice.txt` contains the ImageMagick and bundled codec notices. `licenses/LGPL-3.0.txt` and `licenses/GPL-3.0.txt` contain the GNU license texts. The paint.net SDK supplies reference assemblies for compilation and is not included in the release package.
