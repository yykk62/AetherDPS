# Holt die Spieldaten-Texte in allen Sprachen aus A2Tools (GPL-3.0, taengu/A2Tools-DPS-Meter):
# Skill-, NPC-, Buff- und Dungeon-Namen, Klassennamen (ui), Servernamen und die Liste der offenen Welt.
# Aufruf aus dem Repo-Root:  powershell -File tools\update-game-data.ps1
$ErrorActionPreference = 'Stop'
$base = 'https://raw.githubusercontent.com/taengu/A2Tools-DPS-Meter/main/src/data'
$ziel = Join-Path $PSScriptRoot '..\AetherDPS.Engine\GameData'
$sprachen = 'de', 'en', 'es', 'fr', 'ja', 'ko', 'pt', 'ru', 'zh-Hans', 'zh-Hant'
$ordner = 'skills', 'npcs', 'abnormals', 'dungeons', 'ui'

function Hole($quelle, $pfad) {
    try {
        Invoke-WebRequest "$base/$quelle" -OutFile "$pfad.tmp" -UseBasicParsing
    } catch {
        if (Test-Path "$pfad.tmp") { Remove-Item "$pfad.tmp" }
        return   # gibt es in dieser Sprache nicht (z. B. Dungeons nur en/ko/zh) → Englisch als Rückfall
    }
    # Nur übernehmen, wenn es gültiges JSON ist – sonst bleibt die alte Datei.
    Get-Content "$pfad.tmp" -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null
    Move-Item -Force "$pfad.tmp" $pfad
    Write-Host "aktualisiert: $quelle"
}

foreach ($sprache in $sprachen) {
    New-Item -ItemType Directory -Force (Join-Path $ziel $sprache) | Out-Null
    foreach ($o in $ordner) { Hole "i18n/$o/$sprache.json" (Join-Path $ziel "$sprache\$o.json") }
}
Hole 'servers.json' (Join-Path $ziel 'servers.json')
Hole 'open_world_maps.json' (Join-Path $ziel 'open_world_maps.json')
