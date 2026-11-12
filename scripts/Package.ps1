param(
    [string] $RuntimeDirectory = 'CompressionArtifacts/dist/CompressionArtifacts',
    [string] $SourcesDirectory = 'CompressionArtifacts/dist/sources',
    [string] $OutputPath = 'CompressionArtifacts/CompressionArtifacts-install-win-x64.zip'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-RepoPath([string] $Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

$runtimeRoot = Resolve-RepoPath $RuntimeDirectory
$sourcesRoot = Resolve-RepoPath $SourcesDirectory
$outputFile = Resolve-RepoPath $OutputPath
$runtimeFiles = @(
    'CompressionArtifacts.dll'
    'CompressionArtifacts.deps.json'
    'Magick.NET.Core.dll'
    'Magick.NET-Q8-x64.dll'
    'Magick.Native-Q8-x64.dll'
    'LibHeifSharp.dll'
    'libheif.dll'
)
$sourceFiles = @('libheif-1.20.2.tar.gz', 'libheif-sharp-3.2.0.tar.gz')
$entries = @()
foreach ($name in $runtimeFiles) {
    $entries += @{ Source = Join-Path $runtimeRoot $name; Name = "CompressionArtifacts/$name" }
}
foreach ($name in @('INSTALL.txt', 'LICENSE.md', 'THIRD-PARTY-NOTICES.md')) {
    $entries += @{ Source = Join-Path $repoRoot $name; Name = $name }
}
foreach ($name in $sourceFiles) {
    $entries += @{ Source = Join-Path $sourcesRoot $name; Name = "sources/$name" }
}
$licenseRoot = Join-Path $repoRoot 'licenses'
foreach ($file in Get-ChildItem -LiteralPath $licenseRoot -File -Recurse) {
    $relativeName = $file.FullName.Substring($licenseRoot.Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
    $entries += @{ Source = $file.FullName; Name = "licenses/$relativeName" }
}

# Validate every input before creating or replacing an archive.
foreach ($entry in $entries) {
    if (-not (Test-Path -LiteralPath $entry.Source -PathType Leaf)) { throw "Missing package input: $($entry.Source)" }
    if ([IO.Path]::GetFullPath($entry.Source) -eq $outputFile) { throw 'Package output cannot replace one of its inputs.' }
}
$outputParent = Split-Path -Parent $outputFile
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open($outputFile, [IO.FileMode]::Create, [IO.FileAccess]::Write)
try {
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($entry in $entries) {
            $zipEntry = $archive.CreateEntry($entry.Name, [IO.Compression.CompressionLevel]::Optimal)
            $inputStream = [IO.File]::OpenRead($entry.Source)
            $entryStream = $zipEntry.Open()
            try { $inputStream.CopyTo($entryStream) }
            finally { $entryStream.Dispose(); $inputStream.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}
finally { $stream.Dispose() }
Write-Output "Created $outputFile ($($entries.Count) install/legal files)."
