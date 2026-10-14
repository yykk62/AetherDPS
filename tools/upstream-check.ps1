# Tägliche Wartung: Upstream-Fixes (RATmeter) holen, deutsche Daten aktualisieren,
# bauen und testen – alles auf einem eigenen Wartungs-Branch, main bleibt unberührt.
# Aufruf aus dem Repo-Root:  powershell -File tools\upstream-check.ps1
# Ausgabe endet mit einer Zeile "ERGEBNIS: <status>":
#   NICHTS_NEU | GRUEN (Branch bereit zum Übernehmen) | KONFLIKT | ROT | ABBRUCH
$ErrorActionPreference = 'Continue'   # git schreibt Statusmeldungen nach stderr
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnet = 'C:\Program Files\dotnet\dotnet.exe'
Set-Location (Join-Path $PSScriptRoot '..')

function Ende($status) { Write-Host "ERGEBNIS: $status"; exit 0 }

if (git status --porcelain) {
    Write-Host 'Arbeitsverzeichnis hat ungespeicherte Änderungen – keine automatische Wartung.'
    git status --short
    Ende 'ABBRUCH'
}
git checkout -q main

git fetch -q upstream
$neu = git log --oneline main..upstream/master
Write-Host "Neue Upstream-Commits: $(@($neu).Count)"
$neu | ForEach-Object { Write-Host "  $_" }

$branch = "wartung/$(Get-Date -Format 'yyyy-MM-dd')"
cmd /c "git branch -D $branch 2>nul" | Out-Null
git checkout -q -b $branch

if ($neu) {
    git merge --no-edit upstream/master 2>&1 | Write-Host
    $konflikte = git diff --name-only --diff-filter=U
    if ($konflikte) {
        Write-Host 'Konflikte in:'
        $konflikte | ForEach-Object { Write-Host "  $_" }
        Ende 'KONFLIKT'
    }
}

# Deutsche Skill-/Mob-Namen (A2Tools)
& powershell -NoProfile -ExecutionPolicy Bypass -File tools\update-german-data.ps1
if (git status --porcelain AetherDPS.Engine/GameData) {
    git add AetherDPS.Engine/GameData
    git commit -q -m 'Wartung: deutsche Spieldaten aktualisiert (A2Tools)'
    Write-Host 'Deutsche Spieldaten aktualisiert.'
    $neu = $true
}

if (-not $neu) {
    git checkout -q main
    git branch -D $branch | Out-Null
    Ende 'NICHTS_NEU'
}

# Release-Konfiguration, damit eine laufende Debug-App die Dateien nicht sperrt
& $dotnet build AetherDPS.slnx -c Release 2>&1 | Select-String -Pattern ' error |Fehler' | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { Ende 'ROT' }
& $dotnet test AetherDPS.Tests -c Release --no-build 2>&1 | Select-String -Pattern 'Fehler |Bestanden|Assert|Expected|Actual' | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { Ende 'ROT' }

Ende "GRUEN (Branch $branch bereit zum Übernehmen)"
