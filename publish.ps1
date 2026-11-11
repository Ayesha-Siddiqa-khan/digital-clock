# Builds, tests, and publishes TimeBridge as a single self-contained Windows executable.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet test tests/TimeBridge.Tests
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
if (Test-Path dist/publish) { Remove-Item dist/publish -Recurse -Force }
dotnet publish src/TimeBridge.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -o dist/publish
Write-Host "Done: dist\publish\TimeBridge.exe"
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) { & $iscc installer\TimeBridge.iss } else { Write-Host 'Inno Setup 6 not found; skipping installer (see installer\TimeBridge.iss).' }
