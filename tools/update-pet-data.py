# Holt die Pet-Daten und die Weltkarten aus SkeeveAN/Aion-DPS-Meter (MIT, aus dem Spielclient gelesen):
# welche Monster welche Pet-Seele droppen, wo sie spawnen, die Gebiete je Pet und die Karten-Kalibrierung.
# Die Kartenkacheln werden verkleinert (512 px) und das Meer (Maske) dunkel eingefärbt, damit die exe klein bleibt.
# Aufruf aus dem Repo-Root:  python tools/update-pet-data.py [<commit>]
# Braucht Pillow (pip install pillow). Karten und Spieldaten sind Eigentum von NCSOFT.
import io, json, pathlib, sys, urllib.error, urllib.request
from PIL import Image

COMMIT = sys.argv[1] if len(sys.argv) > 1 else "968a35312deae2230dfe4146ad72e162a196e5cb"
BASE = f"https://raw.githubusercontent.com/SkeeveAN/Aion-DPS-Meter/{COMMIT}/Client/assets/aion2"
ROOT = pathlib.Path(__file__).resolve().parent.parent
DATA = ROOT / "AetherDPS.Engine" / "GameData" / "pets"
TILES = ROOT / "AetherDPS.App" / "Assets" / "maps"
TILE_SIZE = 512
SEA = (17, 20, 27)   # Hintergrund des Hub (#11141B)


def get(path):
    with urllib.request.urlopen(f"{BASE}/{path}", timeout=60) as r:
        return r.read()


def save_json(name, data):
    DATA.mkdir(parents=True, exist_ok=True)
    (DATA / name).write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print("aktualisiert:", name)


pets = json.loads(get("pets/pets.json"))
regions = json.loads(get("pets/regions.json"))
maps = json.loads(get("maps/maps.json"))
spawns = json.loads(get("pets/spawns.json"))["maps"]

# nur die Spawns der Monster, die eine Pet-Seele droppen, und nur auf den Karten, die wir zeigen
tables = {m["table"] for m in maps["maps"].values()}
pet_npcs = {int(n) for n in pets["monsters"]}
spawns = {t: {n: [[round(p[0]), round(p[1])] for p in pts] for n, pts in s.items() if int(n) in pet_npcs}
          for t, s in spawns.items() if t in tables}

save_json("pets.json", {"_source": f"SkeeveAN/Aion-DPS-Meter@{COMMIT[:10]} (MIT), Spieldaten von NCSOFT",
                        "pets": pets["pets"], "monsters": pets["monsters"]})
save_json("regions.json", regions)
save_json("maps.json", maps)
save_json("spawns.json", spawns)

for key, m in maps["maps"].items():
    folder = TILES / key
    folder.mkdir(parents=True, exist_ok=True)
    for col in range(m["grid"]):
        for row in range(m["grid"]):
            try:
                jpg = Image.open(io.BytesIO(get(f"maps/{key}/{col}_{row}.jpg"))).convert("RGB")
            except urllib.error.HTTPError:
                continue   # leere Kachel (nur Meer)
            mask = Image.open(io.BytesIO(get(f"maps/{key}/{col}_{row}_a.png"))).convert("L")
            jpg = jpg.resize((TILE_SIZE, TILE_SIZE), Image.LANCZOS)
            mask = mask.resize((TILE_SIZE, TILE_SIZE), Image.LANCZOS)
            tile = Image.composite(jpg, Image.new("RGB", jpg.size, SEA), mask)
            tile.save(folder / f"{col}_{row}.jpg", quality=82, optimize=True)
    print("Karte:", key)
