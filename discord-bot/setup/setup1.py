import dc, json, base64, os
G='1560474565916762253'
cfg={'guild':G}
OWNER=dc.req('GET',f'/guilds/{G}')['owner_id']
def b64(path,mime='image/png'): return f'data:{mime};base64,'+base64.b64encode(open(path,'rb').read()).decode()

# Bot-Name
try: dc.req('PATCH','/users/@me',{'username':'AetherDPS'}); print('bot umbenannt')
except Exception as e: print('bot-name:',e)
try: dc.req('PATCH','/applications/@me',{'name':'AetherDPS'}); print('app umbenannt')
except Exception as e: print('app-name:',e)

# Grundeinstellungen
dc.req('PATCH',f'/guilds/{G}',{'name':'AetherDPS','verification_level':2,'explicit_content_filter':2,
    'default_message_notifications':1,'preferred_locale':'de'},reason='Einrichtung')
print('guild ok')

# Rollen
roles={r['name']:r for r in dc.req('GET',f'/guilds/{G}/roles')}
P=lambda *bits: str(sum(1<<b for b in bits))
MOD_PERMS=P(1,2,13,40,10,11,14,15,16,6,18,34,35,38)  # kick, ban, manage msgs, timeout, view, send, embed, attach, history, reactions, ext emoji, threads...
want=[
 ('👑 Entwickler',0xE0A84A,True,False,str(1<<3)),       # Administrator
 ('🛡️ Moderator',0xC9A8F2,True,False,MOD_PERMS),
 ('🤝 Helfer',0x5B8DEF,True,False,'0'),
 ('🧪 Tester',0x5CC26A,False,True,'0'),
 ('🔔 Update-Ping',0,False,True,'0'),
 ('📰 AION-News-Ping',0,False,True,'0'),
 ('Gladiator',0x4FC1C9,False,False,'0'),('Templer',0x5B8DEF,False,False,'0'),('Assassine',0x8BC34A,False,False,'0'),
 ('Waldläufer',0x3FAE7A,False,False,'0'),('Magier',0xA15CE0,False,False,'0'),('Beschwörer',0xD65CC8,False,False,'0'),
 ('Kleriker',0xE0B84A,False,False,'0'),('Kantor',0xE08A3C,False,False,'0'),('Faustkämpfer',0xD64545,False,False,'0'),
]
cfg['roles']={}
for name,color,hoist,ment,perms in want:
    body={'name':name,'color':color,'hoist':hoist,'mentionable':ment,'permissions':perms}
    r=roles.get(name) or dc.req('POST',f'/guilds/{G}/roles',body,reason='Einrichtung')
    if name in roles: dc.req('PATCH',f'/guilds/{G}/roles/{r["id"]}',body)
    cfg['roles'][name]=r['id']; print('rolle',name,r['id'])
# Reihenfolge: Team oben, dann Pings, dann Klassen (Bot-Rolle bleibt darüber)
bot_role=[r for r in roles.values() if r['managed']][0]
order=[n for n,*_ in want]
top=len(order)+1
dc.req('PATCH',f'/guilds/{G}/roles',[{'id':bot_role['id'],'position':top+1}]+[{'id':cfg['roles'][n],'position':top-i} for i,n in enumerate(order)])
dc.req('PUT',f'/guilds/{G}/members/{OWNER}/roles/{cfg["roles"]["👑 Entwickler"]}',reason='Server-Inhaber')
print('owner = Entwickler')

# Klassen-Emojis aus der App
icons={'gladiator':'Gladiator','templar':'Templer','assassin':'Assassine','ranger':'Waldläufer','sorcerer':'Magier',
       'elementalist':'Beschwörer','cleric':'Kleriker','chanter':'Kantor','brawler':'Faustkämpfer'}
emo={e['name']:e for e in dc.req('GET',f'/guilds/{G}/emojis')}
cfg['emojis']={}
base=os.path.join(os.path.dirname(__file__),'..','..','AetherDPS.App','Assets','Classes')+os.sep
for f,de in icons.items():
    n='k_'+f
    e=emo.get(n) or dc.req('POST',f'/guilds/{G}/emojis',{'name':n,'image':b64(base+f+'.png')},reason='Klassen-Emoji')
    cfg['emojis'][de]=e['id']
print('emojis',len(cfg['emojis']))
json.dump(cfg,open('cfg.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
