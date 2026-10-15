# Baut AetherDPS.Engine/GameData/field_bosses.json.
#  - Bosse je Karte mit Koordinaten, Kartennamen, feste Zeitpläne (Abyss): SkeeveAN/Aion-DPS-Meter (MIT, (c) 2026 SkeeveTV)
#  - Slot im Paket 0x9101: eigene Regel (Karte × 100 + Rang nach NPC-Code; Eltnen/Morheim: der NPC-Code selbst)
#  - Respawn-Minuten: selbst gemessen (der Meter lernt sie ohnehin bei jedem beobachteten Kill, siehe FieldBosses.cs)
# Namen der Bosse kommen nicht von hier, sondern in 10 Sprachen aus GameData/<sprache>/npcs.json.
# Aufruf aus dem Repo-Root:  python tools/build-field-bosses.py
import json, urllib.request, pathlib

SKEEVE = "https://raw.githubusercontent.com/SkeeveAN/Aion-DPS-Meter/main/Client/assets/aion2/bosses/field_bosses.json"

# Karten-Ids im Spiel (Verteron, Eltnen, Altgard, Morheim, untere und mittlere Abyss)
MAP_IDS = {"World_L_A": 1010, "World_L_B": 1011, "World_D_A": 1110, "World_D_B": 1111,
           "Abyss_Reshanta_A": 20, "Abyss_Reshanta_C": 22}
# Eltnen und Morheim: das Paket trägt den NPC-Code statt einer laufenden Nummer
NPC_AS_SLOT = {1011, 1111}
# Ausnahmen von der Rang-Regel (untere Abyss: zwei Plätze vertauscht)
SLOT_EXCEPTIONS = {2600089: 2002, 2600084: 2003}
# Steht in keiner Feldboss-Liste des Servers
NOT_LISTED = {2101349}
# Selbst gemessen: Kill gesehen, danach nannte der Server die Respawn-Zeit (Minuten)
RESPAWN = {101010: 180, 101017: 120}


def get(url):
    with urllib.request.urlopen(url, timeout=30) as r:
        return r.read().decode("utf-8-sig")


skeeve = json.loads(get(SKEEVE))
maps = []
for m in skeeve["maps"]:
    map_id = MAP_IDS[m["key"]]
    listed = sorted((b for b in m["bosses"] if b["npc"] not in NOT_LISTED), key=lambda b: b["npc"])
    bosses = []
    for rank, b in enumerate(listed, start=1):
        slot = b["npc"] if map_id in NPC_AS_SLOT else SLOT_EXCEPTIONS.get(b["npc"], map_id * 100 + rank)
        entry = {"slot": slot, "npc": b["npc"], "x": b["x"], "y": b["y"]}
        if slot in RESPAWN: entry["respawn"] = RESPAWN[slot]
        if b.get("fixed"): entry["fixed"] = b["fixed"]
        bosses.append(entry)
    maps.append({"map": map_id, "key": m["key"], "names": m["names"], "bosses": sorted(bosses, key=lambda b: b["slot"])})

out = {
    "_source": "SkeeveAN/Aion-DPS-Meter (MIT, (c) 2026 SkeeveTV): Bosse, Koordinaten, Kartennamen. "
               "Slots und Respawn-Zeiten: eigene Auswertung. Spieldaten (c) NCSOFT. Erzeugt von tools/build-field-bosses.py.",
    "maps": maps,
}
target = pathlib.Path(__file__).resolve().parent.parent / "AetherDPS.Engine" / "GameData" / "field_bosses.json"
target.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
print(f"{target.name}: {len(maps)} Karten, {sum(len(m['bosses']) for m in maps)} Bosse")
