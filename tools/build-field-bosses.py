# Baut AetherDPS.Engine/GameData/field_bosses.json aus drei offenen Quellen:
#  - SkeeveAN/Aion-DPS-Meter (MIT, (c) 2026 SkeeveTV): Bosse je Karte mit Koordinaten, Kartennamen, feste Zeitpläne (Abyss)
#  - Waffle-ens/waffle_meter (MIT, (c) TK-open-public): welcher Slot im Paket 0x9101 zu welchem Boss gehört
#  - TheLoop705/aion2dps (GPL-3.0-or-later): in der EU gemessene Respawn-Zeiten
# Namen der Bosse kommen nicht von hier, sondern in 10 Sprachen aus GameData/<sprache>/npcs.json.
# Aufruf aus dem Repo-Root:  python tools/build-field-bosses.py
import json, re, urllib.request, pathlib

RAW = "https://raw.githubusercontent.com"
SKEEVE = f"{RAW}/SkeeveAN/Aion-DPS-Meter/main/Client/assets/aion2/bosses/field_bosses.json"
WAFFLE = f"{RAW}/Waffle-ens/waffle_meter/main/dotnet/src/WaffleMeter.Capture/FieldBossCatalog.cs"
LOOP = f"{RAW}/TheLoop705/aion2dps/main/data/timers.json"
ABYSS_MAPS = {"AbyssLowerMapId": 20, "AbyssMiddleMapId": 22}


def get(url):
    with urllib.request.urlopen(url, timeout=30) as r:
        return r.read().decode("utf-8-sig")


skeeve = json.loads(get(SKEEVE))
waffle = {}   # npc -> (slot, map)
for m in re.finditer(r"new\((\d+),\s*\"[^\"]*\",\s*(\d+),\s*FieldBossRegion\.\w+,\s*(\w+)\)", get(WAFFLE)):
    npc, slot, map_id = int(m[1]), int(m[2]), m[3]
    map_id = ABYSS_MAPS.get(map_id) or int(map_id)
    waffle[npc] = (slot or npc, map_id)   # 0 = das Paket trägt den NPC-Code selbst (Eltnen, Morheim)
loop = json.loads(get(LOOP))
by_place = {int(k): v for m, places in loop["fieldBossRespawnMinutes"].items() if not m.startswith("_")
            for k, v in [(int(m) * 100 + int(p), mins) for p, mins in places.items()]}
by_npc = {int(k): v for k, v in loop["bossRespawnMinutesByNpc"].items() if not k.startswith("_")}

maps = []
for m in skeeve["maps"]:
    bosses = []
    for b in m["bosses"]:
        slot, map_id = waffle.get(b["npc"], (None, m.get("id") or 0))
        if slot is None:
            print("ohne Slot:", b["npc"], b["names"].get("en"))
            continue
        entry = {"slot": slot, "npc": b["npc"], "x": b["x"], "y": b["y"]}
        respawn = by_place.get(slot) or by_npc.get(b["npc"])
        if respawn: entry["respawn"] = respawn
        if b.get("fixed"): entry["fixed"] = b["fixed"]
        bosses.append(entry)
    map_ids = {waffle[b["npc"]][1] for b in m["bosses"] if b["npc"] in waffle}
    for map_id in sorted(map_ids):
        maps.append({"map": map_id, "key": m["key"], "names": m["names"],
                     "bosses": sorted([b for b in bosses if waffle[b["npc"]][1] == map_id], key=lambda b: b["slot"])})

out = {
    "_source": "SkeeveAN/Aion-DPS-Meter (MIT, (c) 2026 SkeeveTV), Waffle-ens/waffle_meter (MIT, (c) TK-open-public), "
               "TheLoop705/aion2dps (GPL-3.0-or-later). Spieldaten (c) NCSOFT. Erzeugt von tools/build-field-bosses.py.",
    "maps": maps,
}
target = pathlib.Path(__file__).resolve().parent.parent / "AetherDPS.Engine" / "GameData" / "field_bosses.json"
target.write_text(json.dumps(out, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
print(f"{target.name}: {len(maps)} Karten, {sum(len(m['bosses']) for m in maps)} Bosse")
