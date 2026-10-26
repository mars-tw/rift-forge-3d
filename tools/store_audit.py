"""Read-only sibling/native-kit audit; writes receipts only inside this repo."""
import hashlib
import json
import os
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ARCHIVED_PLANS = {'夜行列車_重製企劃_20261002': '_封存/20261022/企劃/夜行列車_重製企劃_20261002'}
OUT = ROOT / 'docs/evidence/r6-store'
OUT.mkdir(parents=True, exist_ok=True)
SKIP = {'.git', 'node_modules', 'Library', 'Logs', 'Temp', 'obj', 'bin', '.godot', 'build', 'Builds', 'dist', '.utmp', '.audit-tmp'}
APPS = ['rift-survivors','storm-apocalypse','doomsday-shooter','tower-defense','skill',
        'pixel-idle-farm-skill','village-siege','seven-district-reckoning','taiwan-island-drive',
        'taiwan-train-math','island-flight-school','taiwan-train-school','夜行列車_重製企劃_20261002']

def digest(p):
    h = hashlib.sha256()
    with p.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()

files = [p for folder in ('Assets', 'Packages') for p in (ROOT/folder).rglob('*')
         if p.is_file() and p.suffix != '.meta']
by_size = {}
for p in files:
    if p.is_file(): by_size.setdefault(p.stat().st_size, []).append((p, digest(p)))
matches, errors, counts = [], [], {}
for app in APPS:
    counts[app] = 0
    base = ROOT.parent / ARCHIVED_PLANS.get(app, app)
    if not base.exists(): errors.append({'path':str(base),'error':'missing'}); continue
    for directory, dirs, names in os.walk(base):
        dirs[:] = [d for d in dirs if d not in SKIP and not d.startswith('.')]
        for name in names:
            p = Path(directory) / name
            if p.suffix.lower() == '.meta': continue
            try:
                counts[app] += 1
                candidates = by_size.get(p.stat().st_size)
                if not candidates: continue
                hashed = digest(p)
                for own, sha in candidates:
                    if sha == hashed:
                        matches.append({'file':own.relative_to(ROOT).as_posix(),'app':app,'other':p.relative_to(base).as_posix(),'sha256':sha})
            except OSError as error: errors.append({'path':str(p),'error':type(error).__name__})
result = {'status':'VERIFIED' if not errors else 'EXECUTED','scope':'Tracked Assets and Packages exact bytes, all sibling files except generated/cache directories; size-filtered SHA-256',
          'scannedFiles':counts,'exactMatches':matches,'errors':errors}
(OUT/'shared-hashes.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print('shared matches',len(matches),'errors',len(errors))

runtime = list((ROOT/'Assets/_Project/Scripts/Runtime').glob('*.cs'))
network = re.compile(r'UnityWebRequest|HttpClient|WebRequest\.|Socket\(|OpenURL\(|Analytics\.|Unity\.Services|Advertisement\.')
findings = [{'file':p.relative_to(ROOT).as_posix(),'line':i,'text':s.strip()} for p in runtime
            for i,s in enumerate(p.read_text(encoding='utf8').splitlines(),1) if network.search(s)]
native = ROOT.parent/'game-store-publisher/native-kit'
snapshot = native/'unity-prepared/rift-forge-3d'
privacy = [str(p.relative_to(native)) for p in snapshot.rglob('*.xcprivacy')] if snapshot.exists() else []
(OUT/'privacy-source-audit.json').write_text(json.dumps({'status':'EXECUTED','runtimeNetworkCalls':findings,
    'nativeKitSnapshot':str(snapshot),'riftPrivacyManifests':privacy,'onlineDeclarationsRead':False,
    'appPrivacyManifest':digest(ROOT/'Assets/Plugins/iOS/PrivacyInfo.xcprivacy')},ensure_ascii=False,indent=2),encoding='utf8')
