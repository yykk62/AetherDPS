"""Macht aus den rohen Higgsfield-Grafiken (assets/brand/art/raw-*.png, nicht im Repo) die fertigen Dateien:

- README-Banner mit lokal gesetztem Titel (Bahnschrift) nach docs/media/brand/banner-*.webp
- Hintergründe und Zierleiste für die Webseite nach web/public/img/
- Zierleiste und Kopf-Banner nach docs/media/brand/

Aufruf: python tools/icons/art.py   (Rohbilder müssen lokal liegen; die Ergebnisse sind im Repo)
"""
from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
RAW = ROOT / "assets" / "brand" / "art"
DOCS = ROOT / "docs" / "media" / "brand"
WEB = ROOT / "web" / "public" / "img"
GOLD, VIOLET, TEXT, BG = (224, 168, 74), (201, 168, 242), (236, 238, 242), (16, 19, 26)


def font(size: int, weight: str = "Bold") -> ImageFont.FreeTypeFont:
    f = ImageFont.truetype("C:/Windows/Fonts/bahnschrift.ttf", size)
    try:
        f.set_variation_by_name(weight)
    except Exception:
        pass
    return f


def cover(im: Image.Image, w: int, h: int, fx: float = 0.5, fy: float = 0.5) -> Image.Image:
    r = max(w / im.width, h / im.height)
    im = im.resize((round(im.width * r), round(im.height * r)), Image.LANCZOS)
    x, y = int((im.width - w) * fx), int((im.height - h) * fy)
    return im.crop((x, y, x + w, y + h))


def shade_left(im: Image.Image, strength: float = 0.8, until: float = 0.6) -> Image.Image:
    """Dunkelt die linke Seite ab, damit Text lesbar ist."""
    w, h = im.size
    t = np.linspace(0, 1, w) / until
    g = (255 * strength * np.clip(1 - t, 0, 1) ** 1.5).astype(np.uint8)
    mask = Image.fromarray(np.tile(g, (h, 1)), "L")
    return Image.composite(Image.new("RGB", (w, h), BG), im, mask)


def spaced(d: ImageDraw.ImageDraw, xy, text, f, fill, spacing):
    x, y = xy
    for ch in text:
        d.text((x, y), ch, font=f, fill=fill)
        x += d.textlength(ch, font=f) + spacing
    return x


def banner(src: Path, out: Path, kicker: str, title: str, sub: str, w: int, h: int, fx: float = 1.0, fy: float = 0.5,
           tsize: int | None = None, until: float = 0.6) -> None:
    im = shade_left(cover(Image.open(src).convert("RGB"), w, h, fx, fy), until=until)
    d = ImageDraw.Draw(im)
    x = int(w * 0.04)
    fk, ft, fs = font(int(h * 0.09), "SemiBold"), font(tsize or int(h * 0.26), "Bold"), font(int(h * 0.1), "Regular")
    yk = int(h * 0.2)
    yt = yk + int(h * 0.12)
    ys = yt + ft.size + int(h * 0.05)
    # weicher dunkler Schein hinter Titel und Unterzeile
    m = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(m)
    md.text((x, yt), title, font=ft, fill=200, stroke_width=int(h * 0.025), stroke_fill=200)
    md.text((x, ys), sub, font=fs, fill=255, stroke_width=int(h * 0.03), stroke_fill=255)
    m = m.filter(ImageFilter.GaussianBlur(h * 0.04))
    im = Image.composite(Image.new("RGB", (w, h), BG), im, m.point(lambda v: int(v * 0.8)))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((x, yk + fk.size * 0.38, x + int(h * 0.12), yk + fk.size * 0.38 + max(2, h // 90)), 2, fill=GOLD)
    spaced(d, (x + int(h * 0.16), yk), kicker.upper(), fk, GOLD, fk.size * 0.2)
    d.text((x + 1, yt + 2), title, font=ft, fill=(0, 0, 0))
    d.text((x, yt), title, font=ft, fill=TEXT)
    d.text((x, ys), sub, font=fs, fill=VIOLET)
    d.rectangle((0, h - max(2, h // 100), w, h), fill=GOLD)
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out, quality=86, method=6)
    print(out.relative_to(ROOT), im.size)


def background(src: Path, out: Path, w: int = 1920, darken: float = 0.0) -> None:
    im = Image.open(src).convert("RGB")
    im = im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)
    if darken:
        im = Image.blend(im, Image.new("RGB", im.size, BG), darken)
    out.parent.mkdir(parents=True, exist_ok=True)
    im.save(out, quality=84, method=6)
    print(out.relative_to(ROOT), im.size, f"{out.stat().st_size // 1024} KB")


def divider(src: Path, outs: list[Path], w: int = 1400) -> None:
    """Goldene Zierleiste auf Schwarz -> transparent (Alpha aus Helligkeit)."""
    a = np.asarray(Image.open(src).convert("RGB"), dtype=np.float32)
    m = a.max(axis=2)
    alpha = np.clip((m - 10) * (255.0 / 245), 0, 255)
    rgb = np.clip(a * np.where(m > 0, 255.0 / np.maximum(m, 1), 0)[..., None], 0, 255)
    im = Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA")
    im = im.crop(im.getchannel("A").point(lambda v: 255 if v > 30 else 0).getbbox())
    im = im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)
    for out in outs:
        out.parent.mkdir(parents=True, exist_ok=True)
        im.save(out, quality=90, method=6)
        print(out.relative_to(ROOT), im.size)


def main() -> None:
    W, H = 1600, 200
    sections = [
        ("meter", "Overlay", "Live-DPS-Meter", "Nur deine Gruppe · Boss-Karte · Stagger-Leiste · Skill-Details", 0.85),
        ("runs", "Archiv", "Läufe-Archiv", "Boss-Zeiten · Bestzeit · Kampfanalyse · als Bild für Discord", 0.75),
        ("profile", "Charakter", "Profil", "Ausrüstung · Werte · Daevanion · Skills, auch ohne laufendes Spiel", 0.8),
        ("party", "Gruppe", "Neu in der Gruppe", "Kampfkraft · Itemstufe · Verzauberung · Waffe, sobald jemand beitritt", 0.7),
        ("pets", "Pets", "Pet-Finder & Pet-Radar", "Alle Pets mit deiner Stufe · Spawnpunkte · Radar über der Spielkarte", 0.7),
        ("timers", "Timer", "Timer & Feldbosse", "Echte Respawn-Zeiten vom Server · Risse · Belagerung · eigene Pills", 0.9),
        ("connection", "Netzwerk", "Verbindung & mehr", "Ping-Verlauf · Lag-Spitzen · liegt es an dir, am Anbieter oder am Server?", 0.8),
        ("checklist", "Alltag", "Checkliste", "Tägliches und Wöchentliches · automatischer Reset · Erinnerung", 0.8),
    ]
    for name, kicker, title, sub, fx in sections:
        banner(RAW / f"raw-banner-{name}.png", DOCS / f"banner-{name}.webp", kicker, title, sub, W, H, fx=fx)
    banner(RAW / "raw-banner-hero.png", DOCS / "hero.webp", "AION 2 · EU · DPS-Meter", "AetherDPS",
           "Kostenlos · Open Source · Deine Kampfdaten bleiben auf deinem PC", 1600, 400, fx=0.6, fy=0.5, tsize=120, until=0.55)
    background(RAW / "raw-bg-aether.png", WEB / "bg-aether.webp")
    background(RAW / "raw-bg-wings.png", WEB / "bg-wings.webp", darken=0.25)
    background(RAW / "raw-bg-stars.png", WEB / "bg-stars.webp")
    background(RAW / "raw-banner-hero.png", WEB / "hero-wide.webp", w=2000)
    divider(RAW / "raw-divider.png", [WEB / "divider.webp", DOCS / "divider.webp"])
    # Hauptfenster der App: Flügel als sehr dunkles Wasserzeichen hinter den Karten
    app_bg = ROOT / "AetherDPS.App" / "Assets" / "art" / "hub-bg.jpg"
    app_bg.parent.mkdir(parents=True, exist_ok=True)
    im = Image.open(RAW / "raw-bg-wings.png").convert("RGB")
    im = im.resize((1280, round(im.height * 1280 / im.width)), Image.LANCZOS)
    Image.blend(im, Image.new("RGB", im.size, (17, 20, 27)), 0.1).save(app_bg, quality=82, optimize=True)
    print(app_bg.relative_to(ROOT), im.size, f"{app_bg.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
