# Prüft die Sprachdateien gegen de.json: gleiche Schlüssel, gleiche Platzhalter ({0}, {1:HH:mm} …).
# Aufruf aus dem Repo-Root:  python tools/check-lang.py
import json, re, sys, pathlib

lang_dir = pathlib.Path(__file__).resolve().parent.parent / "AetherDPS.App" / "Lang"
base = json.loads((lang_dir / "de.json").read_text(encoding="utf-8"))
placeholder = re.compile(r"\{\d+(?::[^}]*)?\}")
ok = True
for path in sorted(lang_dir.glob("*.json")):
    if path.name == "de.json":
        continue
    other = json.loads(path.read_text(encoding="utf-8"))
    missing = [k for k in base if k not in other]
    extra = [k for k in other if k not in base]
    wrong = [k for k in base if k in other and sorted(placeholder.findall(base[k])) != sorted(placeholder.findall(other[k]))]
    status = "ok" if not (missing or extra or wrong) else "FEHLER"
    ok &= status == "ok"
    print(f"{path.name}: {len(other)} Texte, {status}")
    for k in missing: print(f"  fehlt: {k}")
    for k in extra: print(f"  überzählig: {k}")
    for k in wrong: print(f"  Platzhalter: {k}: {base[k]!r} ↔ {other[k]!r}")
sys.exit(0 if ok else 1)
