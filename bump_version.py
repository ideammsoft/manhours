"""
bump_version.py — manHours 버전 자동 증가
InformationalVersion: 1.00 -> 1.01 -> ... -> 1.99 -> 2.00
"""
import re, sys
from pathlib import Path

CSPROJ = Path('manHours/manHours.csproj')
text   = CSPROJ.read_text(encoding='utf-8')

m = re.search(r'<InformationalVersion>(\d+)\.(\d+)</InformationalVersion>', text)
if not m:
    print('[ERROR] InformationalVersion not found'); sys.exit(1)

major, minor = int(m.group(1)), int(m.group(2))
minor += 1
if minor >= 100:
    major += 1; minor = 0

new_ver = f'{major}.{minor:02d}'
new_text = re.sub(
    r'<InformationalVersion>[^<]+</InformationalVersion>',
    f'<InformationalVersion>{new_ver}</InformationalVersion>',
    text
)
CSPROJ.write_text(new_text, encoding='utf-8')
print(f'[OK] Version bumped to {new_ver}')

# 자동업데이트 서버 config.json 의 manHours 버전도 함께 맞춘다
# (a.bat 이 이 config.json 을 그대로 업로드 → 클라이언트가 새 버전 감지)
try:
    import json
    cfg_path = Path(__file__).resolve().parents[2] / 'autoupdate_new' / 'config.json'
    cfg = json.loads(cfg_path.read_text(encoding='utf-8'))
    cfg.setdefault('programs', {})
    prog = cfg['programs'].setdefault('manHours', {"required": False, "notes": "새 기능 및 버그 수정", "file": "manHours.exe"})
    prog['version'] = new_ver
    cfg['updated_at'] = __import__('datetime').date.today().isoformat()
    cfg_path.write_text(json.dumps(cfg, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'[OK] config.json manHours -> {new_ver}')
except Exception as e:
    print(f'[WARN] config.json 갱신 실패: {e}')
