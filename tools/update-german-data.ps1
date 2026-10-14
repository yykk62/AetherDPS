# Holt die deutschen Skill- und Mob-Namen aus A2Tools (GPL-3.0, taengu/A2Tools-DPS-Meter).
# Aufruf aus dem Repo-Root:  powershell -File tools\update-german-data.ps1
$ErrorActionPreference = 'Stop'
$base = 'https://raw.githubusercontent.com/taengu/A2Tools-DPS-Meter/main/src/data/i18n'
$ziel = Join-Path $PSScriptRoot '..\AetherDPS.Engine\GameData\de'
New-Item -ItemType Directory -Force $ziel | Out-Null

$dateien = @{ 'skills/de.json' = 'skills.json'; 'npcs/de.json' = 'mobs.json' }
foreach ($quelle in $dateien.Keys) {
    $pfad = Join-Path $ziel $dateien[$quelle]
    Invoke-WebRequest "$base/$quelle" -OutFile "$pfad.tmp" -UseBasicParsing
    # Nur übernehmen, wenn es gültiges JSON ist – sonst bleibt die alte Datei.
    Get-Content "$pfad.tmp" -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null
    Move-Item -Force "$pfad.tmp" $pfad
    Write-Host "aktualisiert: $($dateien[$quelle])"
}
