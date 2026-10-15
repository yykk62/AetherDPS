"""README-Bilder bauen: Rohbilder aus dem UI-Test ReadmeMedia (doppelte Auflösung) auf eine Bühne setzen,
Schatten, GIFs. Braucht Pillow und ffmpeg (für GIFs mit guter Palette).

  set AETHERDPS_README=<rohordner>
  set AETHERDPS_README_FROM=%LOCALAPPDATA%\\AetherDPS     (optional: eigener Charakter, echtes Profil)
  dotnet test AetherDPS.UiTests --filter ReadmeMedia
  python tools/readme-media.py <rohordner>               → docs/media/
"""
import math
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
RAW = Path(sys.argv[1])
OUT = ROOT / "docs" / "media"
OUT.mkdir(parents=True, exist_ok=True)

BG_TOP, BG_BOTTOM = (20, 24, 34), (10, 12, 18)
GOLD = (224, 168, 74)


def load(name):
    return Image.open(RAW / f"{name}.png").convert("RGBA")


def font(size, bold=False):
    for name in (["segoeuib.ttf", "arialbd.ttf"] if bold else ["segoeui.ttf", "arial.ttf"]):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


_texture = None


def texture():
    """Weltkarte von Verteron, stark weichgezeichnet – ein Hauch Spielwelt im Hintergrund."""
    global _texture
    if _texture is None:
        tiles = sorted((ROOT / "AetherDPS.App" / "Assets" / "maps" / "verteron").glob("*.jpg"))
        coords = [tuple(int(v) for v in t.stem.split("_")) for t in tiles]
        cols = max(c for c, _ in coords) + 1
        rows = max(r for _, r in coords) + 1
        tile = Image.open(tiles[0]).width
        world = Image.new("RGB", (cols * tile, rows * tile), BG_BOTTOM)
        for t, (c, r) in zip(tiles, coords):
            world.paste(Image.open(t).convert("RGB"), (c * tile, r * tile))
        box = (int(world.width * 0.30), int(world.height * 0.30), int(world.width * 0.80), int(world.height * 0.75))
        _texture = world.crop(box)
    return _texture


def stage(w, h, glow=(0.5, 0.0)):
    """Dunkle Bühne: Verlauf, verschwommene Karte, goldener Schein, Vignette."""
    bg = Image.new("RGB", (w, h))
    px = ImageDraw.Draw(bg)
    for y in range(h):
        t = y / max(1, h - 1)
        px.line([(0, y), (w, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(BG_TOP, BG_BOTTOM)))
    tex = texture().resize((w, int(w * texture().height / texture().width)), Image.LANCZOS)
    if tex.height < h:
        tex = texture().resize((int(h * texture().width / texture().height), h), Image.LANCZOS)
    tex = tex.crop((0, 0, w, h)).filter(ImageFilter.GaussianBlur(max(w, h) / 90))
    bg = Image.blend(bg, tex, 0.22)
    glow_layer = Image.new("L", (w, h), 0)
    gx, gy = int(w * glow[0]), int(h * glow[1])
    ImageDraw.Draw(glow_layer).ellipse((gx - w * 0.6, gy - h * 0.7, gx + w * 0.6, gy + h * 0.7), fill=70)
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(w / 8))
    bg = Image.composite(Image.new("RGB", (w, h), GOLD), bg, glow_layer.point(lambda v: v * 0.35))
    vignette = Image.new("L", (w, h), 0)
    ImageDraw.Draw(vignette).rectangle((0, 0, w, h), outline=255, width=int(min(w, h) * 0.08))
    vignette = vignette.filter(ImageFilter.GaussianBlur(min(w, h) / 10))
    bg = Image.composite(Image.new("RGB", (w, h), (4, 5, 8)), bg, vignette.point(lambda v: v * 0.55))
    return bg.convert("RGBA")


def rounded(img, radius):
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, img.width - 1, img.height - 1), radius, fill=255)
    out = img.copy()
    out.putalpha(Image.composite(img.getchannel("A"), Image.new("L", img.size, 0), mask))
    return out


def place(bg, img, xy, shadow=True, radius=None, border=False):
    """Fenster mit weichem Schatten auf die Bühne setzen."""
    if radius:
        img = rounded(img, radius)
    if border:
        frame = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(frame).rounded_rectangle((0, 0, img.width - 1, img.height - 1), radius or 0,
                                                outline=(255, 255, 255, 34), width=2)
        img = Image.alpha_composite(img, frame)
    x, y = xy
    if shadow:
        pad = 60
        sh = Image.new("RGBA", (img.width + pad * 2, img.height + pad * 2), (0, 0, 0, 0))
        alpha = img.getchannel("A").point(lambda v: v * 0.75)
        sh.paste(Image.new("RGBA", img.size, (0, 0, 0, 255)), (pad, pad + 14), alpha)
        sh = sh.filter(ImageFilter.GaussianBlur(26))
        bg.alpha_composite(sh, (x - pad, y - pad))
    bg.alpha_composite(img, (x, y))


def fit(img, width=None, height=None):
    if width:
        return img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)
    return img.resize((round(img.width * height / img.height), height), Image.LANCZOS)


def card(img, name, pad=70, radius=28, out_width=1600, crop=None):
    """Ein Fenster mittig auf einer eigenen Bühne."""
    if crop:
        img = img.crop(crop)
    inner = out_width - pad * 2
    img = fit(img, width=inner)
    bg = stage(out_width, img.height + pad * 2)
    place(bg, img, (pad, pad), radius=radius, border=True)
    final = rounded(bg, 36)
    save(final, name)
    return final


def save(img, name):
    path = OUT / f"{name}.png"
    img.save(path, optimize=True)
    print(f"{path.relative_to(ROOT)}  {img.width}×{img.height}  {path.stat().st_size // 1024} KB")


def gif(frames, name, fps, width):
    """Bildfolge → GIF mit eigener Palette (ffmpeg)."""
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("ffmpeg fehlt – GIF übersprungen:", name)
        return
    with tempfile.TemporaryDirectory() as tmp:
        for i, f in enumerate(frames):
            f.convert("RGB").save(Path(tmp) / f"{i:04d}.png")
        path = OUT / f"{name}.gif"
        filters = (f"fps={fps},scale={width}:-1:flags=lanczos,split[a][b];"
                   "[a]palettegen=max_colors=256:stats_mode=full[p];[b][p]paletteuse=dither=sierra2_4a")
        subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-framerate", str(fps), "-i", str(Path(tmp) / "%04d.png"),
                        "-filter_complex", filters, "-loop", "0", str(path)], check=True)
        print(f"{path.relative_to(ROOT)}  {len(frames)} Bilder  {path.stat().st_size // 1024} KB")


def ease(t):
    return 0.5 - 0.5 * math.cos(math.pi * max(0.0, min(1.0, t)))


# ── Kampf-GIF ───────────────────────────────────────────────────────────────────────────────

def overlay_gif():
    files = sorted((RAW / "frames_overlay").glob("*.png"))
    frames = [Image.open(f).convert("RGBA") for f in files]
    w = max(f.width for f in frames) + 140
    h = max(f.height for f in frames) + 140
    bg = stage(w, h, glow=(0.2, 0.0))
    out = []
    for f in frames:
        canvas = bg.copy()
        place(canvas, f, (70, 70))
        out.append(rounded(canvas, 30))
    out += [out[-1]] * 10   # Ende kurz stehen lassen
    gif(out, "overlay", fps=4, width=520)


# ── Timer-Pills: hinlegen, Rechtsklick „Gruppieren“, fertig ─────────────────────────────────

def menu_image(items, highlight=0, scale=2):
    f = font(13 * scale)
    pad, line = 10 * scale, 30 * scale
    w = max(int(f.getlength(t)) for t in items) + pad * 4
    h = line * len(items) + pad
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, w - 1, h - 1), 8 * scale, fill=(32, 36, 46, 250), outline=(70, 76, 90, 255), width=scale)
    for i, t in enumerate(items):
        y = pad // 2 + i * line
        if i == highlight:
            d.rounded_rectangle((4 * scale, y + 2 * scale, w - 4 * scale, y + line - 2 * scale), 5 * scale, fill=(60, 66, 82, 255))
        d.text((pad * 1.5, y + line / 2), t, font=f, fill=(230, 232, 236), anchor="lm")
    return img


def cursor_image(scale=2):
    pts = [(0, 0), (0, 17), (4, 13), (7, 20), (10, 19), (7, 12), (12, 12)]
    img = Image.new("RGBA", (14 * scale, 22 * scale), (0, 0, 0, 0))
    ImageDraw.Draw(img).polygon([(x * scale, y * scale) for x, y in pts], fill=(255, 255, 255), outline=(0, 0, 0))
    return img


def pills_gif():
    singles = [Image.open(p).convert("RGBA") for p in sorted((RAW / "pills").glob("*.png"))[:4]]
    group = load("pills_group")
    pw, ph = singles[0].size
    w, h = 1000, 470
    gx, gy = (w - group.width) // 2, (h - group.height) // 2 + 20
    # Zielplätze im Block (2×2) und Startplätze verstreut
    targets = [(gx, gy), (gx + group.width - pw, gy), (gx, gy + group.height - ph), (gx + group.width - pw, gy + group.height - ph)]
    starts = [(40, 36), (w - pw - 50, 60), (70, h - ph - 36), (w - pw - 30, h - ph - 50)]
    rough = [(x + dx, y + dy) for (x, y), (dx, dy) in zip(targets, [(-14, -10), (18, 6), (-6, 14), (22, -8)])]
    bg = stage(w, h, glow=(0.5, 0.1))
    menu = menu_image(["Gruppieren (Pills in der Nähe)", "Entfernen", "Sperren (Klicks gehen ins Spiel)", "Alle Timer ausblenden"])
    cursor = cursor_image()
    out = []

    def frame(positions, extra=None):
        c = bg.copy()
        for img, (x, y) in zip(singles, positions):
            place(c, img, (int(x), int(y)))
        if extra:
            extra(c)
        out.append(rounded(c, 30))

    for _ in range(6):
        frame(starts)
    for i in range(4):   # nacheinander grob hinziehen
        for k in range(1, 9):
            pos = [rough[j] if j < i else starts[j] for j in range(4)]
            t = ease(k / 8)
            pos[i] = (starts[i][0] + (rough[i][0] - starts[i][0]) * t, starts[i][1] + (rough[i][1] - starts[i][1]) * t)
            frame(pos, lambda c, p=pos[i]: c.alpha_composite(cursor, (int(p[0] + pw * 0.5), int(p[1] + ph * 0.6))))
    click = (rough[0][0] + pw * 0.6, rough[0][1] + ph * 0.55)
    for _ in range(4):
        frame(rough, lambda c: c.alpha_composite(cursor, (int(click[0]), int(click[1]))))
    for _ in range(8):
        frame(rough, lambda c: (place(c, menu, (int(click[0]) + 6, int(click[1]) + 6)),
                                c.alpha_composite(cursor, (int(click[0]) + 30, int(click[1]) + 40))))
    for k in range(1, 6):   # einrasten
        t = ease(k / 5)
        frame([(r[0] + (g[0] - r[0]) * t, r[1] + (g[1] - r[1]) * t) for r, g in zip(rough, targets)])
    final = bg.copy()
    place(final, group, (gx, gy))
    final = rounded(final, 30)
    out += [final] * 16
    gif(out, "pills", fps=12, width=700)


# ── Pet-Radar: drei Zustände nebeneinander ──────────────────────────────────────────────────

def radar_trio():
    shots = [load("pet_radar"), load("pet_radar_species"), load("pet_radar_pick")]
    labels = ["Je Gattung das nächste", "Eine Gattung gewählt", "Pet angeklickt: alle Fundorte"]
    h = max(s.height for s in shots)
    gap, pad, top = 70, 80, 110
    w = sum(s.width for s in shots) + gap * 2 + pad * 2
    bg = stage(w, h + pad + top, glow=(0.5, 0.0))
    d = ImageDraw.Draw(bg)
    x = pad
    f = font(38, bold=True)
    for s, label in zip(shots, labels):
        d.text((x + s.width / 2, top / 2 + 20), label, font=f, fill=(230, 232, 236), anchor="mm")
        place(bg, s, (x, top))
        x += s.width + gap
    save(fit(rounded(bg, 36), width=1600), "pet-radar")


# ── Gruppen-Popup und Stagger-Hinweis ───────────────────────────────────────────────────────

def popups():
    join, toast = load("party_join"), load("toast_stagger")
    pad, gap = 80, 70
    h = max(join.height, toast.height) + pad * 2
    w = join.width + toast.width + gap + pad * 2
    bg = stage(w, h, glow=(0.3, 0.0))
    place(bg, join, (pad, (h - join.height) // 2))
    place(bg, toast, (pad + join.width + gap, (h - toast.height) // 2))
    save(fit(rounded(bg, 36), width=1600), "popups")


# ── Großes Titelbild ────────────────────────────────────────────────────────────────────────

def hero():
    w, h = 2400, 1340
    bg = stage(w, h, glow=(0.62, 0.0))
    hub = fit(load("hub_pets"), height=h - 150)
    place(bg, hub, (80, 75), radius=24, border=True)
    overlay = fit(Image.open(sorted((RAW / "frames_overlay").glob("*.png"))[30]).convert("RGBA"), width=w - hub.width - 230)
    ox = 80 + hub.width + 70
    place(bg, overlay, (ox, 75))
    group = fit(load("pills_group"), width=overlay.width)
    place(bg, group, (ox, 75 + overlay.height + 60))
    save(fit(rounded(bg, 40), width=1600), "hero")


def main():
    overlay_gif()
    pills_gif()
    hero()
    card(load("hub_runs"), "runs")
    # Ausschnitte des langen Profils (Fensterhöhe 2900 im Generator, doppelte Auflösung)
    profile = load("hub_profile")
    card(profile, "profile", crop=(0, 0, profile.width, 2770))
    card(profile, "profile-skills", crop=(300, 2760, profile.width - 20, 4190))
    item = load("hub_profile_item")
    card(item, "profile-item", crop=(300, 1700, item.width - 20, 2600))
    card(load("hub_pets"), "pets")
    radar_trio()
    timers = load("hub_timers")
    card(timers, "timers", crop=(0, 0, timers.width, min(timers.height, 2300)))
    popups()
    card(load("hub_connection"), "connection")
    card(load("overlay_skills"), "overlay-skills", out_width=900)


if __name__ == "__main__":
    main()
