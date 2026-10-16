"""Kopiert das Icon-Set aus assets/brand dorthin, wo es gebraucht wird (nach jedem slice.py-Lauf):

- README: docs/media/brand/icons/<name>.png           (Emblem-Set, 48 px, für die Funktionstabelle)
          docs/media/brand/ui/<name>.png              (UI-Set, 32 px, für die Bedienungstabelle)

Die App bindet die UI-Icons direkt aus assets/brand/icons/ui ein (AetherDPS.App.csproj, AvaloniaResource mit Link),
die Webseite holt sich die Embleme beim Bauen (web/scripts/sync-assets.mjs -> public/gen/brand).
"""
from __future__ import annotations

import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ICONS = ROOT / "assets" / "brand" / "icons"
DOCS = ROOT / "docs" / "media" / "brand" / "icons"


def copy_tree(src: Path, dst: Path) -> int:
    dst.mkdir(parents=True, exist_ok=True)
    n = 0
    for f in src.glob("*.png"):
        shutil.copy2(f, dst / f.name)
        n += 1
    return n


def main() -> None:
    if DOCS.exists():
        shutil.rmtree(DOCS)
    print(f"README: {copy_tree(ICONS / 'emblem' / '48', DOCS)} Dateien -> {DOCS.relative_to(ROOT)}")
    # Knöpfe der App, auf die das README verweist (Bedienung)
    ui = DOCS.parent / "ui"
    if ui.exists():
        shutil.rmtree(ui)
    print(f"README (UI): {copy_tree(ICONS / 'ui' / '32', ui)} Dateien -> {ui.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
