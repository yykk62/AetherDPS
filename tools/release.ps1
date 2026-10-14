<#
  Baut die veröffentlichbare AetherDPS.exe (eine Datei, ohne .NET-Installation lauffähig)
  und die Prüfsumme, die das Auto-Update verlangt.

  powershell -File tools\release.ps1                 # Version aus AetherDPS.App.csproj
  powershell -File tools\release.ps1 -Version 0.2.0  # andere Version

  Ergebnis: artifacts\release\AetherDPS.exe + AetherDPS.exe.sha256
  Veröffentlicht wird über ein Git-Tag (v0.2.0) → .github/workflows/aetherdps-release.yml.
#>
param(
    [string]$Version,
    [string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts\release'),
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dotnet = if (Get-Command dotnet -ErrorAction SilentlyContinue) { 'dotnet' } else { 'C:\Program Files\dotnet\dotnet.exe' }

if (-not $Version) {
    [xml]$csproj = Get-Content (Join-Path $root 'AetherDPS.App\AetherDPS.App.csproj')
    $Version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$Version' muss die Form 1.2.3 haben." }

if (-not $SkipTests) {
    & $dotnet test (Join-Path $root 'AetherDPS.Tests') -c Release
    if ($LASTEXITCODE) { throw 'Tests fehlgeschlagen – kein Release.' }
}

$publish = Join-Path $root 'artifacts\publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
& $dotnet publish (Join-Path $root 'AetherDPS.App') -c Release -o $publish -p:Version=$Version
if ($LASTEXITCODE) { throw 'Publish fehlgeschlagen.' }

New-Item -ItemType Directory -Force $OutDir | Out-Null
$exe = Join-Path $OutDir 'AetherDPS.exe'
Copy-Item (Join-Path $publish 'AetherDPS.exe') $exe -Force
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
# Format wie sha256sum: "<hash>  <datei>"
[IO.File]::WriteAllText("$exe.sha256", "$hash  AetherDPS.exe`n")
# Übergang: Versionen bis 0.5.0 hießen Aion2Meter und suchen im Release nach Aion2Meter.exe.
# Dieselbe Datei unter altem Namen, damit sie sich auf AetherDPS aktualisieren.
$legacy = Join-Path $OutDir 'Aion2Meter.exe'
Copy-Item $exe $legacy -Force
[IO.File]::WriteAllText("$legacy.sha256", "$hash  Aion2Meter.exe`n")

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "AetherDPS $Version → $exe ($size MB)"
Write-Host "SHA-256: $hash"
