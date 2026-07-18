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
