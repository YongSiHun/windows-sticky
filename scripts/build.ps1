$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    # Reserve one version for the entire x86/x64 build.
    $version = & (Join-Path $PSScriptRoot 'increment-version.ps1')
    Write-Host "Building version $version"
    foreach ($platform in @('x86', 'x64')) {
        # CI: restore build-only refs/analyzers, then run STA regression suite.
        & msbuild StickyNote.sln /restore /m /p:Configuration=Release /p:Platform=$platform /p:AutoIncrementVersion=false "/p:RestoreConfigFile=$pwd\NuGet.Config"
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $platform" }
        & ".\tests\StickyNote.Tests\bin\$platform\Release\StickyNote.Tests.exe"
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $platform" }
    }
}
finally { Pop-Location }
