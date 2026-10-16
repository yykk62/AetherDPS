"""Bot-Avatar, App-Icon und Server-Icon auf das aktuelle Logo setzen (assets/brand/logo/logo-1024.png).

Baut discord-bot/assets/avatar.webp neu (Emblem auf dunklem Grund mit leichtem violettem Schein, Discord schneidet rund)
und lädt es hoch. Aufruf aus discord-bot/setup: python avatar.py   (Token aus ~/.secrets/aetherdps.env)
"""
import base64
import io
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

import dc

ROOT = Path(__file__).resolve().parents[2]
emblem = Image.open(ROOT / "assets" / "brand" / "logo" / "logo-1024.png").convert("RGBA")

S = 1024
bg = Image.new("RGBA", (S, S), (16, 19, 26, 255))
glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ImageDraw.Draw(glow).ellipse((S * 0.2, S * 0.22, S * 0.8, S * 0.82), fill=(201, 168, 242, 70))
bg = Image.alpha_composite(bg, glow.filter(ImageFilter.GaussianBlur(S * 0.12)))
em = emblem.resize((int(S * 0.78), int(S * 0.78)), Image.LANCZOS)
bg.paste(em, ((S - em.width) // 2, (S - em.height) // 2), em)
out = bg.convert("RGB")

asset = ROOT / "discord-bot" / "assets" / "avatar.webp"
out.save(asset, quality=90, method=6)
buf = io.BytesIO()
out.save(buf, "PNG", optimize=True)
data = "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()
print(asset.relative_to(ROOT), f"{asset.stat().st_size // 1024} KB, PNG {len(buf.getvalue()) // 1024} KB")

cfg = json.load(open(Path(__file__).with_name("cfg.json"), encoding="utf-8"))
dc.req("PATCH", "/users/@me", {"avatar": data}); print("bot avatar")
dc.req("PATCH", "/applications/@me", {"icon": data}); print("app icon")
dc.req("PATCH", f"/guilds/{cfg['guild']}", {"icon": data}); print("server icon")
