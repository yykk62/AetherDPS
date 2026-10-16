import json, os, sys, time, urllib.request, urllib.error
def _env():
    d={}
    for l in open(os.path.expanduser('~/.secrets/aetherdps.env'),encoding='utf-8'):
        l=l.strip()
        if '=' in l and not l.startswith('#'):
            k,v=l.split('=',1); d[k.strip()]=v.strip().strip('"').strip("'")
    return d
ENV=_env()
TOKEN=ENV['DISCORD_BOT_TOKEN']
API='https://discord.com/api/v10'
def req(method, path, body=None, files=None, reason=None):
    url=API+path
    headers={'Authorization':'Bot '+TOKEN,'User-Agent':'DiscordBot (https://aetherdps.com, 1.0)'}
    if reason: headers['X-Audit-Log-Reason']=urllib.parse.quote(reason)
    data=None
    if files:
        import uuid
        b='----b'+uuid.uuid4().hex
        parts=[]
        parts.append(f'--{b}\r\nContent-Disposition: form-data; name="payload_json"\r\nContent-Type: application/json\r\n\r\n'.encode()+json.dumps(body or {}).encode()+b'\r\n')
        for i,(name,content,ctype) in enumerate(files):
            parts.append(f'--{b}\r\nContent-Disposition: form-data; name="files[{i}]"; filename="{name}"\r\nContent-Type: {ctype}\r\n\r\n'.encode()+content+b'\r\n')
        parts.append(f'--{b}--\r\n'.encode())
        data=b''.join(parts); headers['Content-Type']='multipart/form-data; boundary='+b
    elif body is not None:
        data=json.dumps(body).encode(); headers['Content-Type']='application/json'
    for attempt in range(6):
        r=urllib.request.Request(url,data=data,method=method,headers=headers)
        try:
            with urllib.request.urlopen(r) as resp:
                t=resp.read()
                return json.loads(t) if t else None
        except urllib.error.HTTPError as e:
            t=e.read().decode()
            if e.code==429:
                wait=json.loads(t).get('retry_after',1); time.sleep(float(wait)+0.3); continue
            raise RuntimeError(f'{method} {path} -> {e.code}: {t[:800]}')
    raise RuntimeError('rate limited')
import urllib.parse
