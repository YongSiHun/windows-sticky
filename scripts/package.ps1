param([string]$OutputDirectory = 'artifacts')
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath((Join-Path $repository $OutputDirectory))
if (!$output.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be inside repository.' }
New-Item -ItemType Directory -Force -Path $output | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$checksums = @()
foreach ($platform in @('x86', 'x64')) {
    $build = Join-Path $repository "src\StickyNote\bin\$platform\Release"
    $zip = Join-Path $output "sticky_note_windows_$platform.zip"
    $temporary = $zip + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $archive = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        # Explicit entries preserve README links and exclude stale build output.
        foreach ($name in @('StickyNote.exe', 'StickyNote.exe.config', 'README.md', 'AGENTS.md', 'docs/architecture.md', 'docs/desktop_widget.md', 'docs/compatibility.md', 'docs/validation.md')) {
            $source = if ($name.StartsWith('StickyNote.exe')) { Join-Path $build $name } else { Join-Path $repository $name }
            if (!(Test-Path -LiteralPath $source)) { throw "Missing package input: $source" }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporary -Destination $zip -Force
    $checksums += (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($zip)
}
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), $checksums, (New-Object Text.UTF8Encoding($false)))
