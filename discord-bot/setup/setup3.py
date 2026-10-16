import dc, json
cfg=json.load(open('cfg.json',encoding='utf-8')); G=cfg['guild']; R=cfg['roles']; C=cfg['channels']
exempt=[R['👑 Entwickler'],R['🛡️ Moderator']]
alert={'type':2,'metadata':{'channel_id':C['teamlog']}}
block=lambda msg:{'type':1,'metadata':{'custom_message':msg}}
rules=[
 {'name':'Spam','trigger_type':3,'actions':[block('Das sah nach Spam aus und wurde blockiert.'),alert]},
 {'name':'Massen-Erwähnungen','trigger_type':5,'trigger_metadata':{'mention_total_limit':5,'mention_raid_protection_enabled':True},
  'actions':[block('Zu viele Erwähnungen auf einmal.'),alert,{'type':3,'metadata':{'duration_seconds':600}}]},
 {'name':'Anstößige Inhalte','trigger_type':4,'trigger_metadata':{'presets':[2,3]},'actions':[block('Diese Wortwahl ist hier nicht erlaubt.'),alert]},
 {'name':'Einladungen & Betrug','trigger_type':1,'trigger_metadata':{
   'keyword_filter':['*free nitro*','*nitro gratis*','*steam gift*','*airdrop*','*@everyone*'],
   'regex_patterns':[r'(discord\.gg|discord(app)?\.com/invite)/[a-z0-9-]+',r'steamcommun[^i]ty|stearncommunity|steamcomnunity'],
   'allow_list':['discord.gg/aetherdps']},
  'actions':[block('Fremde Einladungen und Gewinnspiel-Links sind hier blockiert. Downloads nur über GitHub oder aetherdps.com.'),alert]},
]
have={r['name']:r for r in dc.req('GET',f'/guilds/{G}/auto-moderation/rules')}
for r in rules:
    body={**r,'event_type':1,'enabled':True,'exempt_roles':exempt}
    if r['name'] in have: dc.req('PATCH',f'/guilds/{G}/auto-moderation/rules/{have[r["name"]]["id"]}',{k:v for k,v in body.items() if k!='trigger_type'})
    else: dc.req('POST',f'/guilds/{G}/auto-moderation/rules',body)
    print('automod',r['name'])
