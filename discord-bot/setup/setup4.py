import dc, json, base64
cfg=json.load(open('cfg.json',encoding='utf-8')); G=cfg['guild']
b64=lambda p:'data:image/png;base64,'+base64.b64encode(open(p,'rb').read()).decode()
dc.req('PATCH','/users/@me',{'avatar':b64('img/avatar.png'),'banner':b64('img/bot_banner.png')}); print('bot avatar+banner')
desc=("✨ Der Helfer vom **AetherDPS**-Discord\n\n"
      "📊 AetherDPS ist der kostenlose, quelloffene DPS-Meter für AION 2 (EU), auf Deutsch und in 10 Sprachen\n"
      "📰 Bringt die offiziellen AION-2-Patchnotes automatisch in den Server\n"
      "🎭 Klassen-Rollen und Pings per Knopfdruck\n\n"
      "🌐 https://aetherdps.com\n💻 https://github.com/yykk62/AetherDPS")
print(len(desc))
dc.req('PATCH','/applications/@me',{'description':desc,'icon':b64('img/avatar.png'),'cover_image':b64('img/bot_banner.png'),
      'tags':['AION 2','DPS Meter','Patchnotes','MMORPG','Open Source']}); print('app')
dc.req('PATCH',f'/guilds/{G}',{'icon':b64('img/avatar.png')}); print('server icon')
