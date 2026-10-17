<#
  Baut die veröffentlichbare AetherDPS.exe (eine Datei, ohne .NET-Installation lauffähig)
  und die Prüfsumme, die das Auto-Update verlangt.

  powershell -File tools\release.ps1                 # Version aus AetherDPS.App.csproj
  powershell -File tools\release.ps1 -Version 0.2.0  # andere Version

  Ergebnis: artifacts\release\AetherDPS.exe + AetherDPS.exe.sha256
  Veröffentlicht wird über ein Git-Tag (v0.2.0) → .github/workflows/aetherdps-release.yml.

  powershell -File tools\release.ps1 -Version 0.8.94 -Preview -OutDir artifacts\vorschau
  Vorschau nur für den eigenen PC: die exe kennt zusätzlich den lokalen Update-Ordner
  %LOCALAPPDATA%\AetherDPS\Vorschau (latest.json + exe). „Nach Updates suchen“ findet dort neue Vorschauen,
  öffentliche Releases auf GitHub weiterhin auch – die höhere Version gewinnt.
#>
param(
    [string]$Version,
    [string]$OutDir = (Join-Path $PSScriptRoot '..\artifacts\release'),
    [switch]$SkipTests,
    [switch]$Preview
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
$feedDir = Join-Path $env:LOCALAPPDATA 'AetherDPS\Vorschau'
$extra = @()
if ($Preview) { $extra += "-p:PreviewFeed=$(Join-Path $feedDir 'latest.json')" }
& $dotnet publish (Join-Path $root 'AetherDPS.App') -c Release -o $publish -p:Version=$Version @extra
if ($LASTEXITCODE) { throw 'Publish fehlgeschlagen.' }

New-Item -ItemType Directory -Force $OutDir | Out-Null
$exe = Join-Path $OutDir 'AetherDPS.exe'
Copy-Item (Join-Path $publish 'AetherDPS.exe') $exe -Force
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash
# Format wie sha256sum: "<hash>  <datei>"
[IO.File]::WriteAllText("$exe.sha256", "$hash  AetherDPS.exe`n")

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "AetherDPS $Version → $exe ($size MB)"
Write-Host "SHA-256: $hash"

if ($Preview) {
    # Vorschau-Feed: versionierte exe + latest.json im Format eines GitHub-Releases (lokale Pfade)
    New-Item -ItemType Directory -Force $feedDir | Out-Null
    $feedExe = Join-Path $feedDir "AetherDPS-$Version.exe"
    Copy-Item $exe $feedExe -Force
    [IO.File]::WriteAllText("$feedExe.sha256", "$hash  AetherDPS.exe`n")
    $release = [ordered]@{
        tag_name = "v$Version"; html_url = ""; draft = $false; prerelease = $false
        assets = @(
            [ordered]@{ name = 'AetherDPS.exe'; browser_download_url = ([Uri]$feedExe).AbsoluteUri; size = (Get-Item $feedExe).Length; digest = "sha256:$($hash.ToLowerInvariant())" },
            [ordered]@{ name = 'AetherDPS.exe.sha256'; browser_download_url = ([Uri]"$feedExe.sha256").AbsoluteUri }
        )
    }
    [IO.File]::WriteAllText((Join-Path $feedDir 'latest.json'), ($release | ConvertTo-Json -Depth 4))
    # nur die letzten drei Vorschauen behalten
    Get-ChildItem $feedDir -Filter 'AetherDPS-*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -Skip 3 |
        ForEach-Object { Remove-Item $_.FullName, "$($_.FullName).sha256" -ErrorAction SilentlyContinue }
    Write-Host "Vorschau-Feed: $feedDir"
}
