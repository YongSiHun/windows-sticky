$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\VERSION'))
# Serialize version reservations across simultaneous build processes.
$hash = [Security.Cryptography.SHA256]::Create()
try { $key = [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($path.ToUpperInvariant()))).Replace('-', '') }
finally { $hash.Dispose() }
$mutex = New-Object Threading.Mutex($false, ('Local\WindowsStickyVersion-' + $key))
$acquired = $false
$temporary = $path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    try { $acquired = $mutex.WaitOne(30000) }
    catch [Threading.AbandonedMutexException] { $acquired = $true }
    if (!$acquired) { throw 'Timed out waiting for the VERSION lock.' }
    $current = [IO.File]::ReadAllText($path).Trim()
    if ($current -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw 'VERSION must be major.minor.patch. The original was preserved.'
    }
    $version = [Version]$current
    if ($version.Major -gt 65534 -or $version.Minor -gt 65534 -or $version.Build -ge 65534) {
        throw 'VERSION exceeds assembly version limits. Adjust major/minor/patch manually.'
    }
    $next = '{0}.{1}.{2}' -f $version.Major, $version.Minor, ($version.Build + 1)
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes($next + "`n")
    $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
    [IO.File]::Replace($temporary, $path, ($path + '.bak'))
    Write-Output $next
}
finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
