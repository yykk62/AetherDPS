"""Macht aus dem rohen Logo (assets/brand/art/raw-logo.png, Higgsfield, auf reinem Magenta) das Emblem mit
durchsichtigem Hintergrund und schreibt es überall hin, wo es gebraucht wird:

- assets/brand/logo/logo-{1024,512,256}.png   Quelle für alles Weitere
- AetherDPS.App/Assets/icon.png (256) und icon.ico (16–256)   Fenster, Tray, Overlay, Toast
- docs/media/brand/logo.png (256)   README
- web bekommt icon.png beim Bauen (web/scripts/sync-assets.mjs -> gen/icon.png, favicon.png)

Aufruf: python tools/icons/logo.py
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "assets" / "brand" / "art" / "raw-logo.png"
OUT = ROOT / "assets" / "brand" / "logo"


def key_magenta(im: Image.Image, lo: float = 110, hi: float = 215) -> Image.Image:
    """Reines Magenta (#FF00FF) wird durchsichtig; Ränder werden entfärbt, damit kein rosa Saum bleibt."""
    a = np.asarray(im.convert("RGB"), dtype=np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = np.minimum(r, b) - g                      # hoch = Magenta
    alpha = np.clip((hi - m) / (hi - lo), 0, 1)
    # Farbe der halbdurchsichtigen Randpixel aus den deckenden Nachbarn holen (statt Magenta-Mischung)
    solid = (alpha > 0.95).astype(np.float32)
    rgb_solid = Image.fromarray((a * solid[..., None]).astype(np.uint8), "RGB").filter(ImageFilter.GaussianBlur(6))
    w = Image.fromarray((solid * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(6))
    wa = np.asarray(w, dtype=np.float32) / 255
    fill = np.asarray(rgb_solid, dtype=np.float32) / np.maximum(wa, 1e-3)[..., None]
    rgb = np.where(solid[..., None] > 0, a, np.clip(fill, 0, 255))
    out = np.dstack([rgb, alpha * 255]).astype(np.uint8)
    img = Image.fromarray(out, "RGBA")
    bbox = img.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
    return img.crop(bbox)


def square(im: Image.Image, size: int, pad: float = 0.04) -> Image.Image:
    """Mittig in ein Quadrat setzen, Rand `pad` je Seite, und auf `size` skalieren."""
    s = int(max(im.size) * (1 + 2 * pad))
    canvas = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    canvas.paste(im, ((s - im.width) // 2, (s - im.height) // 2), im)
    return canvas.resize((size, size), Image.LANCZOS)


def main() -> None:
    emblem = key_magenta(Image.open(RAW))
    OUT.mkdir(parents=True, exist_ok=True)
    for px in (1024, 512, 256):
        square(emblem, px).save(OUT / f"logo-{px}.png", optimize=True)
    square(emblem, 1024).save(OUT / "logo.webp", quality=92, method=6)

    app = ROOT / "AetherDPS.App" / "Assets"
    square(emblem, 256).save(app / "icon.png", optimize=True)
    # .ico mit allen gängigen Größen; kleine Stufen aus der großen Datei sauber herunterrechnen
    big = square(emblem, 256)
    big.save(app / "icon.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

    docs = ROOT / "docs" / "media" / "brand"
    docs.mkdir(parents=True, exist_ok=True)
    square(emblem, 256).save(docs / "logo.png", optimize=True)
    for p in [OUT / "logo-256.png", app / "icon.png", app / "icon.ico", docs / "logo.png"]:
        print(p.relative_to(ROOT), f"{p.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
