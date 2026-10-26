"""Final local source, privacy, version and required-pattern secret receipts. No external writes."""
import hashlib
import json
import plistlib
import re
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

root=Path(__file__).resolve().parents[1]
out=root/'docs/evidence/r6-store';out.mkdir(parents=True,exist_ok=True)
config=json.loads((root/'native/build-config.json').read_text(encoding='utf8'))
settings=(root/'ProjectSettings/ProjectSettings.asset').read_text(encoding='utf8')
export=(root/'Assets/_Project/Scripts/Editor/NativeExport.cs').read_text(encoding='utf8')
privacy=plistlib.loads((root/'Assets/Plugins/iOS/PrivacyInfo.xcprivacy').read_bytes())
version=config['nativeVersion']=='1.3.0' and config['nativeBuild']==4 and 'bundleVersion: 1.3.0' in settings and 'AndroidBundleVersionCode: 4' in settings and 'iPhone: 4' in settings and 'Version = "1.3.0"' in export and 'Build = 4' in export
assert version
assert not config['readyForStore']
assert privacy['NSPrivacyTracking'] is False and privacy['NSPrivacyCollectedDataTypes']==[]
assert privacy['NSPrivacyAccessedAPITypes'][0]['NSPrivacyAccessedAPITypeReasons']==['CA92.1']
assert 'SampleScene' not in (root/'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf8')
assert not (root/'Assets/_Project/Resources/Audio/rift_battle_loop.ogg').exists()
source_old=[]
for folder in ['Assets/_Project/Scripts','native']:
    for p in (root/folder).rglob('*'):
        if p.is_file() and p.suffix in ['.cs','.json'] and '1.2.0' in p.read_text(encoding='utf8'):
            source_old.append(p.relative_to(root).as_posix())
assert not source_old
tests={}
for name in ['editmode','playmode']:
    path=root/f'Logs/r6/{name}-final.xml'
    result=ET.parse(path).getroot()
    assert int(result.get('failed','-1'))==0
    assert int(result.get('passed','0'))>0
    tests[name]={key:result.get(key) for key in ['total','passed','failed','skipped','duration']}
    (out/f'{name}-final.xml').write_bytes(path.read_bytes())
pattern=r'sk-proj-[A-Za-z0-9_-]{20}|sk-[a-z0-9]{40}'
command=['rg','--hidden','--no-ignore','-l','-i',pattern,'.','-g','!.git/**','-g','!**/node_modules/**']
scan=subprocess.run(command,cwd=root,capture_output=True,text=True,encoding='utf8',errors='replace')
receipt={'status':'VERIFIED' if scan.returncode==1 and not scan.stderr else 'BLOCKED','command':command,
         'exitCode':scan.returncode,'matchedFiles':scan.stdout.splitlines(),'errors':scan.stderr.splitlines()}
(out/'secret-scan.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf8')
assert receipt['status']=='VERIFIED', 'Secret scan has hits or read errors; see local receipt.'
gates={'status':'VERIFIED','version':'1.3.0','build':4,'readyForStore':False,'privacyPlistParsed':True,
       'sampleSceneRemovedFromBuild':True,'unlicensedMusicRemovedFromResources':True,'oldProductionVersionHits':source_old,
       'tests':tests,'runtimeSourceSha256':{p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest()
           for p in sorted((root/'Assets/_Project/Scripts').rglob('*.cs'))}}
(out/'quality-gates.json').write_text(json.dumps(gates,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'version':gates['version'],'tests':tests,'secretScan':receipt['status']}))
