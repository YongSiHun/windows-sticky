$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    # Both platforms use the checked-in version without modifying it.
    $version = (Get-Content (Join-Path $PSScriptRoot '..\VERSION') -Raw).Trim()
    Write-Host "Building version $version"
    foreach ($platform in @('x86', 'x64')) {
        # CI: restore build-only refs/analyzers, then run STA regression suite.
        & msbuild StickyNote.sln /restore /m /p:Configuration=Release /p:Platform=$platform "/p:RestoreConfigFile=$pwd\NuGet.Config"
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $platform" }
        & ".\tests\StickyNote.Tests\bin\$platform\Release\StickyNote.Tests.exe"
        if ($LASTEXITCODE -ne 0) { throw "Tests failed: $platform" }
    }
}
finally { Pop-Location }
