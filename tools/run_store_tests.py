"""Sequential Unity test runs. Always hidden, batchmode and Null graphics. No publishing."""
import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
editor = Path.home()/'Unity/Editors/6000.5.4f1/Editor/Unity.exe'
out = root/'docs/evidence/r6-store'; out.mkdir(parents=True, exist_ok=True)
receipts = []
for platform in ['EditMode', 'PlayMode']:
    name = platform.lower()
    command = [str(editor), '-batchmode', '-nographics', '-projectPath', str(root), '-runTests',
               '-testPlatform', platform, '-testResults', f'Logs/r6/{name}-final.xml', '-logFile', f'Logs/r6/{name}-final.log']
    process = subprocess.Popen(command, cwd=root, creationflags=subprocess.CREATE_NO_WINDOW)
    print(f'{platform} started pid={process.pid}', flush=True)
    process.wait(timeout=900)
    receipts.append({'platform': platform, 'pid': process.pid, 'exitCode': process.returncode,
                     'command': command, 'graphics': 'Null (-nographics)', 'visibleWindowRequested': False})
    (out/'execution.json').write_text(json.dumps(receipts,ensure_ascii=False,indent=2),encoding='utf8')
    print(f'{platform} exit={process.returncode}', flush=True)
    if process.returncode: raise SystemExit(process.returncode)
