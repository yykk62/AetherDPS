import dc, json
cfg=json.load(open('cfg.json',encoding='utf-8')); G=cfg['guild']; R=cfg['roles']
B=lambda *bits: str(sum(1<<b for b in bits))
EVERY=G
chs={c['id']:c for c in dc.req('GET',f'/guilds/{G}/channels')}
byname={c['name']:c for c in chs.values()}
def upsert(name, body, old=None):
    c=byname.get(name) or (chs.get(old) if old else None)
    body={'name':name,**body}
    if c: c=dc.req('PATCH',f'/channels/{c["id"]}',body)
    else: c=dc.req('POST',f'/guilds/{G}/channels',body)
    byname[name]=c; return c
team_allow=[{'id':R['👑 Entwickler'],'type':0,'allow':B(10,11,13,16,34,35,38),'deny':'0'},
            {'id':R['🛡️ Moderator'],'type':0,'allow':B(10,11,13,16,34,35,38),'deny':'0'}]
info_ow=[{'id':EVERY,'type':0,'allow':B(6,10,16),'deny':B(11,35,36,38)}]+team_allow
# vorhandene Standard-Kanäle umnutzen statt löschen
old_txt_cat=next(c['id'] for c in chs.values() if c['type']==4 and c['name'].startswith('Text'))
old_voice_cat=next(c['id'] for c in chs.values() if c['type']==4 and c['name'].startswith('Sprach'))
old_txt=next(c['id'] for c in chs.values() if c['type']==0 and c['name']=='Allgemein')
old_voice=next(c['id'] for c in chs.values() if c['type']==2)

cat_info=upsert('📌 AetherDPS',{'type':4,'position':0,'permission_overwrites':info_ow},old=old_txt_cat)
cat_com=upsert('💬 Community',{'type':4,'position':1,'permission_overwrites':[]},old=old_voice_cat)
cat_team=upsert('🔒 Team',{'type':4,'position':2,'permission_overwrites':[{'id':EVERY,'type':0,'allow':'0','deny':B(10)}]+team_allow})
willkommen=upsert('👋・willkommen',{'type':0,'parent_id':cat_info['id'],'position':0,'permission_overwrites':info_ow,
    'topic':'Start hier: was AetherDPS ist, Regeln und deine Rollen · Start here: rules & roles'})
anleitung=upsert('📖・anleitung',{'type':0,'parent_id':cat_info['id'],'position':3,'permission_overwrites':info_ow,
    'topic':'Download, Einrichtung, Hotkeys und Häufige Fragen · Download, setup, hotkeys & FAQ'})
allgemein=upsert('💬・allgemein',{'type':0,'parent_id':cat_com['id'],'position':0,'permission_overwrites':[],'rate_limit_per_user':0,
    'topic':'Quatschen über AION 2, Builds, Läufe und den Meter · Deutsch & English welcome'},old=old_txt)
voice=upsert('🔊 Gruppen-Talk',{'type':2,'parent_id':cat_com['id'],'position':2,'permission_overwrites':[]},old=old_voice)
teamlog=upsert('🛠️・team-log',{'type':0,'parent_id':cat_team['id'],'position':0,'topic':'Discord-Hinweise, AutoMod-Meldungen'})

# Community einschalten
dc.req('PATCH',f'/guilds/{G}',{'features':['COMMUNITY'],'rules_channel_id':willkommen['id'],'public_updates_channel_id':teamlog['id'],
   'verification_level':2,'explicit_content_filter':2,'preferred_locale':'de',
   'description':'Der kostenlose, quelloffene DPS-Meter für AION 2 – Updates, Hilfe, Ideen und offizielle AION-2-News auf Deutsch.',
   'system_channel_id':allgemein['id'],'system_channel_flags':(1<<2)|(1<<3)|(1<<1)},reason='Community-Modus')
print('community an')

ann=upsert('📢・ankündigungen',{'type':5,'parent_id':cat_info['id'],'position':1,'permission_overwrites':info_ow,
    'topic':'Neue AetherDPS-Versionen und wichtige Infos · Folgen-Button: in deinen Gilden-Discord holen'})
news=upsert('📰・aion2-news',{'type':5,'parent_id':cat_info['id'],'position':2,'permission_overwrites':info_ow,
    'topic':'Offizielle AION-2-Patchnotes und Ankündigungen (EU), automatisch von aion2.plaync.com · Folgen-Button verfügbar'})
forum_tags=[
 {'name':'Idee','emoji_name':'💡'},{'name':'Verbesserung','emoji_name':'✨'},{'name':'Fehler','emoji_name':'🐞'},{'name':'Frage','emoji_name':'❓'},
 {'name':'Wird geprüft','emoji_name':'👀','moderated':True},{'name':'Geplant','emoji_name':'📌','moderated':True},
 {'name':'In Arbeit','emoji_name':'🛠️','moderated':True},{'name':'Umgesetzt','emoji_name':'✅','moderated':True},
 {'name':'Gelöst','emoji_name':'🟢','moderated':True},{'name':'Abgelehnt','emoji_name':'🚫','moderated':True},
]
forum=upsert('💡・ideen-und-feedback',{'type':15,'parent_id':cat_com['id'],'position':1,'permission_overwrites':[],
    'topic':('**So geht’s:** Ein Beitrag pro Idee oder Fehler · Tag wählen · mit 👍 abstimmen.\n'
             'Den Stand siehst du am Status-Tag: 👀 Wird geprüft → 📌 Geplant → 🛠️ In Arbeit → ✅ Umgesetzt.\n'
             'Bei Fehlern bitte Version, was passiert ist und ggf. „Problem melden“ aus der App.\n'
             'One post per idea or bug · pick a tag · vote with 👍 · English welcome.'),
    'available_tags':forum_tags,'default_reaction_emoji':{'emoji_name':'👍'},'default_sort_order':0,'default_forum_layout':1,
    'flags':1<<4,'default_thread_rate_limit_per_user':0})
cfg['channels']={k:v['id'] for k,v in {'willkommen':willkommen,'ankuendigungen':ann,'news':news,'anleitung':anleitung,'allgemein':allgemein,
   'forum':forum,'voice':voice,'teamlog':teamlog,'cat_info':cat_info,'cat_com':cat_com,'cat_team':cat_team}.items()}
cfg['forum_tags']={t['name']:t['id'] for t in dc.req('GET',f'/channels/{forum["id"]}')['available_tags']}
json.dump(cfg,open('cfg.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
for c in sorted(dc.req('GET',f'/guilds/{G}/channels'),key=lambda c:(c['type']!=4,c['position'])): print(c['type'],c['position'],c['name'])
