"""Zerlegt Icon-Sheets (Raster aus Higgsfield, nano_banana_2_1) in einzelne, freigestellte PNGs.

Aufruf:  python tools/icons/slice.py [assets/brand/sheets/<sheet>.json ...]
Ohne Argumente werden alle *.json in assets/brand/sheets verarbeitet.

Ein Manifest je Sheet:
{
  "image": "ui-1.png",            # Sheet im selben Ordner (4k-PNG lokal; fehlt es, die 2k-WebP-Kopie)
  "grid": [5, 5],                 # Spalten, Zeilen
  "key": "black" | "magenta",     # Hintergrund: Schwarz -> Alpha aus Helligkeit, Magenta -> Chroma-Key
  "set": "ui" | "emblem",         # Zielordner assets/brand/icons/<set>/
  "sizes": [16, 24, 32, 48, 64],  # Ausgabegrößen (Pixel)
  "icons": ["close", "reset", ...] # Namen zeilenweise, "" = Zelle überspringen
}

Ausgabe: assets/brand/icons/<set>/<size>/<name>.png (quadratisch, transparent),
dazu die Übersichten assets/brand/uebersicht-<set>.png.
Von jedem 4k-Sheet wird eine 2k-WebP-Kopie neben das Manifest gelegt (die 4k-PNGs sind in .gitignore).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SHEETS = ROOT / "assets" / "brand" / "sheets"
ICONS = ROOT / "assets" / "brand" / "icons"


def key_black(cell: Image.Image) -> Image.Image:
    """Schwarzer Hintergrund: Alpha = Helligkeit, Farben entpremultiplizieren.

    Leuchtende Glyphen auf Schwarz sind additiv: ein Pixel (r,g,b) entspricht
    Farbe (r,g,b)/max mit Alpha max. So bleibt der violette Schein halbtransparent."""
    a = np.asarray(cell.convert("RGB"), dtype=np.float32)
    m = a.max(axis=2)
    floor = 14.0  # der Schein füllt die ganze Zelle schwach; alles darunter ist Hintergrund
    alpha = np.clip((m - floor) * (255.0 / (255 - floor)) * 1.08, 0, 255)
    scale = np.where(m > 0, 255.0 / np.maximum(m, 1), 0)[..., None]
    rgb = np.clip(a * scale, 0, 255)
    # Schein nur in der Nähe der Glyphe behalten (sonst bleibt ein schwaches Quadrat je Zelle)
    core = Image.fromarray((alpha > 110).astype(np.uint8) * 255, "L")
    small = core.resize((max(1, core.width // 8), max(1, core.height // 8)), Image.BOX)
    near = small.filter(ImageFilter.MaxFilter(11)).filter(ImageFilter.GaussianBlur(4)).resize(core.size, Image.BILINEAR)
    alpha = alpha * (np.asarray(near, dtype=np.float32) / 255.0)
    out = np.dstack([rgb, alpha]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def key_magenta(cell: Image.Image) -> Image.Image:
    """Magenta-Hintergrund (#FF00FF): Chroma-Key mit weichem Rand und Entfärbung des Saums."""
    a = np.asarray(cell.convert("RGB"), dtype=np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    k = np.minimum(r, b) - g  # "Magenta-Grad": 255 = reiner Hintergrund
    lo, hi = 110.0, 215.0
    bg = np.clip((k - lo) / (hi - lo), 0, 1)  # Anteil Hintergrund im Pixel
    fg = np.maximum(1 - bg, 1e-3)
    rr = np.clip((r - bg * 255) / fg, 0, 255)
    gg = np.clip(g / fg, 0, 255)
    bb = np.clip((b - bg * 255) / fg, 0, 255)
    alpha = (1 - bg) * 255
    out = np.dstack([rr, gg, bb, alpha]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def trim_square(im: Image.Image, pad: float = 0.06) -> Image.Image:
    """Auf den Inhalt beschneiden und quadratisch mit Rand zentrieren."""
    bbox = im.getchannel("A").point(lambda v: 255 if v > 24 else 0).getbbox()
    if not bbox:
        return im
    im = im.crop(bbox)
    side = int(max(im.size) * (1 + 2 * pad))
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(im, ((side - im.width) // 2, (side - im.height) // 2))
    return sq


def touches_edge(icon: Image.Image) -> bool:
    """Ragt die Glyphe an den Zellrand? (dann stimmt das Raster nicht)"""
    a = np.asarray(icon.getchannel("A"))
    edge = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    return int((edge > 40).sum()) > 4


def load_sheet(manifest: Path, m: dict) -> Image.Image:
    png = manifest.parent / m["image"]
    webp = png.with_suffix(".webp")
    if png.exists():
        sheet = Image.open(png).convert("RGB")
        if not webp.exists() or webp.stat().st_mtime < png.stat().st_mtime:
            copy = sheet if sheet.width <= 2048 else sheet.resize((2048, round(sheet.height * 2048 / sheet.width)), Image.LANCZOS)
            copy.save(webp, quality=92, method=6)
        return sheet
    if webp.exists():
        return Image.open(webp).convert("RGB")
    raise FileNotFoundError(png)


def process(manifest: Path) -> list[str]:
    m = json.loads(manifest.read_text(encoding="utf-8"))
    sheet = load_sheet(manifest, m)
    cols, rows = m["grid"]
    cw, ch = sheet.width / cols, sheet.height / rows
    sizes = m.get("sizes", [16, 24, 32, 48, 64])
    out_dir = ICONS / m["set"]
    done: list[str] = []
    for i, name in enumerate(m["icons"]):
        if not name:
            continue
        c, r = i % cols, i // cols
        cell = sheet.crop((int(c * cw), int(r * ch), int((c + 1) * cw), int((r + 1) * ch)))
        icon = key_black(cell) if m["key"] == "black" else key_magenta(cell)
        if touches_edge(icon):
            print(f"  ! {name}: berührt den Zellrand, bitte Sheet prüfen")
        icon = trim_square(icon)
        for s in sizes:
            small = icon.resize((s, s), Image.LANCZOS)
            if s <= 24:
                small = small.filter(ImageFilter.UnsharpMask(radius=0.6, percent=60, threshold=2))
            (out_dir / str(s)).mkdir(parents=True, exist_ok=True)
            small.save(out_dir / str(s) / f"{name}.png", optimize=True)
        done.append(name)
    print(f"{manifest.name}: {len(done)} Icons -> {out_dir.relative_to(ROOT)}")
    return done


def contact_sheet(sets: dict[str, list[str]]) -> None:
    """Übersicht für assets/brand/README.md: alle Icons bei 48 px mit Namen."""
    from PIL import ImageDraw, ImageFont

    try:
        font = ImageFont.truetype("C:/Windows/Fonts/bahnschrift.ttf", 13)
    except OSError:
        font = ImageFont.load_default()
    for set_name, names in sets.items():
        names = sorted(set(names))
        per_row, cell_w, cell_h = 8, 140, 92
        rows = (len(names) + per_row - 1) // per_row
        im = Image.new("RGB", (per_row * cell_w + 16, rows * cell_h + 16), (16, 19, 26))
        d = ImageDraw.Draw(im)
        for i, name in enumerate(names):
            x, y = 8 + (i % per_row) * cell_w, 8 + (i // per_row) * cell_h
            ic = Image.open(ICONS / set_name / "48" / f"{name}.png").convert("RGBA")
            im.paste(ic, (x + (cell_w - 48) // 2, y + 8), ic)
            tw = d.textlength(name, font=font)
            d.text((x + (cell_w - tw) / 2, y + 64), name, font=font, fill=(142, 150, 163))
        im.save(ICONS.parent / f"uebersicht-{set_name}.png", optimize=True)
        print(f"uebersicht-{set_name}.png: {len(names)} Icons")


def main(argv: list[str]) -> None:
    manifests = [Path(a) for a in argv] or sorted(SHEETS.glob("*.json"))
    sets: dict[str, list[str]] = {}
    for mf in manifests:
        m = json.loads(mf.read_text(encoding="utf-8"))
        sets.setdefault(m["set"], []).extend(process(mf))
    if not argv:
        contact_sheet({s: [p.stem for p in (ICONS / s / "48").glob("*.png")] for s in sets})
    else:
        contact_sheet(sets)


if __name__ == "__main__":
    main(sys.argv[1:])
