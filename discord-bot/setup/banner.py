from PIL import Image, ImageDraw, ImageFont, ImageFilter
GOLD=(224,168,74); VIOLET=(201,168,242); TEXT=(236,238,242); BG=(16,19,26)
def font(size, weight='Bold'):
    f=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf', size)
    try: f.set_variation_by_name(weight)
    except Exception: pass
    return f
def cover(im, w, h, fx=0.5, fy=0.5):
    r=max(w/im.width, h/im.height); im=im.resize((round(im.width*r), round(im.height*r)), Image.LANCZOS)
    x=int((im.width-w)*fx); y=int((im.height-h)*fy); return im.crop((x,y,x+w,y+h))
def shade(im, strength=0.85, until=0.62):
    w,h=im.size; g=Image.new('L',(w,1))
    for x in range(w):
        t=x/(w*until); g.putpixel((x,0), int(255*strength*max(0,1-t)**1.6))
    g=g.resize((w,h)); dark=Image.new('RGB',(w,h),BG); return Image.composite(dark, im, g)
def spaced(d, xy, text, f, fill, spacing):
    x,y=xy
    for ch in text:
        d.text((x,y), ch, font=f, fill=fill); x+=d.textlength(ch, font=f)+spacing
    return x
def banner(src, out, kicker, title, sub, w=1600, h=400, fx=1.0, fy=0.5, tsize=None):
    im=cover(Image.open(src).convert('RGB'), w, h, fx, fy); im=shade(im)
    # weicher Schatten hinter dem Text
    glow=Image.new('L',(w,h),0); gd=ImageDraw.Draw(glow)
    d=ImageDraw.Draw(im)
    pad=int(h*0.17); x=int(w*0.055)
    fk=font(int(h*0.075),'SemiBold'); ft=font(tsize or int(h*0.25),'Bold'); fs=font(int(h*0.085),'Regular')
    yk=pad; yt=yk+int(h*0.11); ys=yt+ft.size+int(h*0.06)
    # Gold-Linie + Kicker
    d.rounded_rectangle((x, yk+fk.size*0.35, x+int(h*0.09), yk+fk.size*0.35+4), 2, fill=GOLD)
    spaced(d,(x+int(h*0.12), yk), kicker.upper(), fk, GOLD, fk.size*0.22)
    for dx,dy in ((0,3),):
        d.text((x+dx,yt+dy), title, font=ft, fill=(0,0,0))
    # weicher dunkler Schein hinter Titel und Unterzeile (lesbar auf hellen Bildstellen)
    from PIL import ImageFilter
    m=Image.new('L',(w,h),0); md=ImageDraw.Draw(m)
    md.text((x,yt), title, font=ft, fill=200, stroke_width=int(h*0.02), stroke_fill=200)
    md.text((x,ys), sub, font=fs, fill=255, stroke_width=int(h*0.03), stroke_fill=255)
    m=m.filter(ImageFilter.GaussianBlur(h*0.03))
    im.paste(Image.composite(Image.new('RGB',(w,h),BG), im, m.point(lambda v:int(v*0.85))))
    d=ImageDraw.Draw(im)
    d.text((x,yt), title, font=ft, fill=TEXT)
    d.text((x,ys), sub, font=fs, fill=VIOLET)
    # dünne Goldkante unten
    d.rectangle((0,h-3,w,h), fill=GOLD)
    im.save(out, optimize=True); print(out, im.size)

banner('raw_0.png','welcome.png','AION 2 · DPS-Meter','AetherDPS','Kostenlos · Open Source · auf Deutsch',w=1600,h=686,fx=0.95,fy=0.5,tsize=150)
banner('raw_0.png','regeln.png','Willkommen','Regeln','Kurz, fair und für alle gleich',fx=0.7,fy=0.62)
banner('raw_5.png','rollen.png','Selbst wählen','Deine Rollen','Klasse zeigen · Pings nur, wenn du willst')
banner('raw_11.png','ankuendigungen.png','AetherDPS','Ankündigungen','Neue Versionen und wichtige Infos')
banner('raw_2.png','news.png','Offiziell · EU','AION 2 News','Patchnotes und Ankündigungen von NCSOFT')
banner('raw_3.png','anleitung.png','In 3 Minuten startklar','Anleitung','Download · Einrichtung · Hotkeys · FAQ')
banner('raw_4.png','feedback.png','Deine Ideen zählen','Ideen & Feedback','Vorschlagen · abstimmen · Fortschritt verfolgen')
# Bot-Banner (680x240) ohne Text, Server-/Bot-Icon
b=cover(Image.open('raw_0.png').convert('RGB'),1360,480,0.86,0.45); b.save('bot_banner.png',optimize=True)
a=Image.open('raw_10.png').convert('RGB').crop((471,251,1861,1641)).resize((1024,1024),Image.LANCZOS); a.save('avatar.png',optimize=True)
print('ok')
