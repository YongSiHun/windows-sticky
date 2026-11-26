param(
    [string]$OutputDirectory = 'artifacts',
    [string]$RuntimePath,
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath((Join-Path $repository $OutputDirectory))
if (!$output.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be inside repository.' }
$cache = Join-Path $repository '.buildprofile\installer'
New-Item -ItemType Directory -Force -Path $output, $cache | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# SIG: check pinned hash when supplied, then validate trusted publisher.
function Assert-Publisher([string]$Path, [string]$Publisher, [string]$ExpectedHash) {
    if ($ExpectedHash -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $ExpectedHash) { throw "Unexpected SHA256: $Path" }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') { throw "Invalid signature: $Path" }
    if ($signature.SignerCertificate.Subject -notmatch $Publisher) { throw "Unexpected publisher: $Path" }
}

# GET: build-time download; target machines need no network.
function Get-Payload([string]$Url, [string]$Path, [string]$Publisher, [string]$ExpectedHash) {
    if (Test-Path -LiteralPath $Path) { Assert-Publisher $Path $Publisher $ExpectedHash; return }
    $temporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp.exe'
    try {
        Write-Host "Downloading: $Url"
        Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $temporary
        Assert-Publisher $temporary $Publisher $ExpectedHash
        Move-Item -LiteralPath $temporary -Destination $Path
    }
    catch { throw "Payload failed [$Url]: $($_.Exception.Message)" }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
}

if (!$RuntimePath) {
    $RuntimePath = Join-Path $cache 'net48.exe'
    Get-Payload 'https://go.microsoft.com/fwlink/?linkid=2088631' $RuntimePath 'O=Microsoft Corporation'
}
$RuntimePath = (Resolve-Path -LiteralPath $RuntimePath).Path
Assert-Publisher $RuntimePath 'O=Microsoft Corporation'

if (!$CompilerPath) {
    # IS: pinned compiler produces Windows 7 SP1 compatible setup executables.
    $CompilerPath = Join-Path $cache 'inno\ISCC.exe'
    if (!(Test-Path -LiteralPath $CompilerPath)) {
        $installer = Join-Path $cache 'innosetup-6.2.2.exe'
        # SRC: official immutable release; hash and legacy signer from publisher docs.
        Get-Payload 'https://github.com/jrsoftware/issrc/releases/download/is-6_2_2/innosetup-6.2.2.exe' $installer 'Open Source Developer, Martijn Laan' '8117d10d00a2ad33a1390978ea3872861c330e087914410a6377b22c4c5b8563'
        $directory = Join-Path $cache 'inno'
        $process = Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', ('/DIR="' + $directory + '"')) -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0) { throw "Compiler setup failed: $($process.ExitCode)" }
    }
}
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
$checksums = @()
foreach ($platform in @('x86', 'x64')) {
    $temporary = Join-Path $output ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporary | Out-Null
    try {
        & $CompilerPath "/DPlatform=$platform" "/DRepository=$repository" "/DRuntime=$RuntimePath" "/DOutput=$temporary" (Join-Path $PSScriptRoot 'setup.iss')
        if ($LASTEXITCODE -ne 0) { throw "Installer compile failed: $platform" }
        $name = "sticky_note_setup_$platform.exe"
        $source = Join-Path $temporary $name
        $target = Join-Path $output $name
        $stream = [IO.File]::Open($source, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite)
        try { $stream.Flush($true) }
        finally { $stream.Dispose() }
        if (Test-Path -LiteralPath $target) { [IO.File]::Replace($source, $target, "$target.bak") }
        else { [IO.File]::Move($source, $target) }
        $checksums += (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $name
    }
    finally {
        if (!(Get-ChildItem -LiteralPath $temporary -Force)) { Remove-Item -LiteralPath $temporary }
    }
}
$checksumPath = Join-Path $output 'SHA256SUMS.txt'
$checksumTemp = $checksumPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
$bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(($checksums -join "`n") + "`n")
$stream = [IO.File]::Open($checksumTemp, [IO.FileMode]::CreateNew)
try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
finally { $stream.Dispose() }
if (Test-Path -LiteralPath $checksumPath) { [IO.File]::Replace($checksumTemp, $checksumPath, "$checksumPath.bak") }
else { [IO.File]::Move($checksumTemp, $checksumPath) }
