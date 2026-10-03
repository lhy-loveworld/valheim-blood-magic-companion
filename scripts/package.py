"""Create local artifacts only. Never uploads or publishes."""
import argparse
import json
import re
import zipfile
from pathlib import Path
from urllib.parse import urlparse
from release_common import ROOT, manifest, source_fingerprint, digest, source_files
from check_source import check

parser = argparse.ArgumentParser()
parser.add_argument('--dll', required=True, type=Path)
parser.add_argument('--output', type=Path, default=Path('dist'))
parser.add_argument('--local-author', default='LocalMods')
parser.add_argument('--website', default=None)
args = parser.parse_args()
check()
if not re.fullmatch(r'[A-Za-z0-9_]+', args.local_author):
    parser.error('Local author must contain only letters, numbers, and underscores.')
data = manifest()
if args.website is not None:
    url = urlparse(args.website)
    if args.website and (url.scheme != 'https' or not url.netloc):
        parser.error('Website must be an HTTPS URL or an empty string.')
    data['website_url'] = args.website
receipt = json.loads(args.dll.with_name('build-receipt.json').read_text(encoding='utf-8'))
assert receipt['version'] == data['version_number'], 'Rebuild after changing version.'
assert receipt['source_sha256'] == source_fingerprint(), 'C# sources changed: rebuild first.'
assert receipt['dll_sha256'] == digest(args.dll), 'DLL does not match the successful build receipt.'
assert receipt['standalone_checks_passed'] is True
assert args.dll.read_bytes()[:2] == b'MZ'
assert digest(args.dll) in (ROOT / 'docs/VALIDATION.md').read_text(encoding='utf-8'), 'Update validation hash to match this DLL.'
args.output.mkdir(parents=True, exist_ok=True)
name, version = data['name'], data['version_number']
common = {'README.md': ROOT / 'README.md', 'CHANGELOG.md': ROOT / 'CHANGELOG.md',
          'LICENSE': ROOT / 'LICENSE', 'VALIDATION.md': ROOT / 'docs/VALIDATION.md',
          'icon.png': ROOT / 'package/icon.png',
          'plugins/BloodMagicCompanion.dll': args.dll}
archives = []
for local in [False, True]:
    metadata = dict(data)
    prefix = args.local_author + '-' if local else ''
    if local: metadata['author'] = args.local_author
    archive = args.output / (prefix + name + '-' + version + '.zip')
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('manifest.json', json.dumps(metadata, indent=2) + '\n')
        for arcname, path in common.items(): z.write(path, arcname)
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert set(z.namelist()) == set(common) | {'manifest.json'}
        assert z.read('plugins/BloodMagicCompanion.dll') == args.dll.read_bytes()
    archives.append(archive)
source = args.output / (name + '-' + version + '-source.zip')
with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED) as z:
    for path in source_files(): z.write(path, path.relative_to(ROOT).as_posix())
with zipfile.ZipFile(source) as z:
    assert z.testzip() is None
    assert not any(Path(n).suffix in {'.dll', '.exe', '.cfg', '.log', '.zip'} for n in z.namelist())
archives.append(source)
(args.output / 'SHA256SUMS.txt').write_text(''.join(digest(path) + '  ' + path.name + '\n' for path in archives))
for archive in archives: print(archive.resolve())
print('PASS: archive integrity, runtime allowlist, clean source distribution, and successful-build fingerprint.')
