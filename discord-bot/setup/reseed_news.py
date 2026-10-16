import dc, json, re, urllib.request
cfg = json.load(open('cfg.json', encoding='utf-8')); C = cfg['channels']
NC = 'https://api-global-community.plaync.com/aion2_global/board'
GOLD, VIOLET = 0xE0A84A, 0xC9A8F2

def clean(s):
    for a, b in (('&nbsp;', ' '), ('&amp;', '&'), ('&lt;', '<'), ('&gt;', '>'), ('&quot;', '"'), ('&#39;', "'")): s = s.replace(a, b)
    s = re.sub('[​‌‍﻿]', '', s)
    s = re.sub(r'([,.!?:;])(?=[A-ZÄÖÜ])', r'\1 ', s)
    s = re.sub(r'([a-zäöüß])(?=[A-ZÄÖÜ][a-zäöüß])', r'\1 ', s)
    return re.sub(r'\s+', ' ', s).strip()

def cut(s, n):
    return s if len(s) <= n else re.sub(r'\s+\S*$', '', s[:n - 1]) + ' …'

def board(alias, patch):
    req = urllib.request.Request(f'{NC}/{alias}/article/search/moreArticle?isVote=true&moreSize=10&moreDirection=BEFORE&previousArticleId=0',
                                 headers={'User-Agent': 'AetherDPS-Discord (+https://aetherdps.com)'})
    out = []
    for a in json.load(urllib.request.urlopen(req))['contentList']:
        url = a['rootBoard']['board']['boardUrlPattern'].replace('{articleId}', a['id'])
        out.append(dict(url=url, title=clean(a['title']), summary=clean(a.get('summary') or ''), image=a.get('thumbnailUrl') or None,
                        at=a['timestamps']['postedAt'], patch=patch))
    return out

items = sorted(board('update_de', True) + board('notice_de', False), key=lambda a: a['at'])
latest_patch = [a for a in items if a['patch']][-1]
pick = sorted([latest_patch] + [a for a in items if not a['patch']][-3:], key=lambda a: a['at'])

def embed(a):
    e = {'author': {'name': '🛠️ AION 2 · Patch-Notizen' if a['patch'] else '📣 AION 2 · Ankündigung'},
         'title': cut(a['title'], 250), 'url': a['url'],
         'description': f"{cut(a['summary'], 320)}\n\n**[➜ Ganzen Beitrag lesen]({a['url']})**",
         'color': GOLD if a['patch'] else VIOLET,
         'footer': {'text': 'Offiziell von aion2.plaync.com · automatisch geteilt'}, 'timestamp': a['at']}
    if a['image']: e['image'] = {'url': a['image']}
    return e

posted = [m for m in reversed(dc.req('GET', f"/channels/{C['news']}/messages?limit=20")) if any(e.get('url') for e in m.get('embeds', []))]
for i, a in enumerate(pick):
    body = {'content': '', 'embeds': [embed(a)], 'allowed_mentions': {'parse': []}}
    if i < len(posted):
        dc.req('PATCH', f"/channels/{C['news']}/messages/{posted[i]['id']}", body)
    else:
        m = dc.req('POST', f"/channels/{C['news']}/messages", body)
        dc.req('POST', f"/channels/{C['news']}/messages/{m['id']}/crosspost")
    print(a['at'][:16], 'PATCH' if a['patch'] else 'NEWS ', a['title'][:70])
