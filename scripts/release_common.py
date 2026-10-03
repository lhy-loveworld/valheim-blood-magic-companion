"""Shared local release metadata. Standard library only."""
from pathlib import Path
import hashlib
import json

ROOT = Path(__file__).resolve().parent.parent

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def source_fingerprint():
    combined = hashlib.sha256()
    for path in sorted((ROOT / 'src').glob('*.cs')):
        combined.update(path.name.encode('utf-8') + b'\0' + path.read_bytes() + b'\0')
    return combined.hexdigest()

def manifest():
    return json.loads((ROOT / 'package/manifest.json').read_text(encoding='utf-8'))

def source_files():
    files = [ROOT / name for name in ['README.md', 'CHANGELOG.md', 'LICENSE',
             'CONTRIBUTING.md', '.gitignore', '.gitattributes']]
    extensions = {'.cs', '.csproj', '.py', '.ps1', '.md', '.json', '.png', '.yml', '.yaml'}
    for directory in ['src', 'tests', 'scripts', 'docs', 'package', '.github']:
        files.extend(path for path in (ROOT / directory).rglob('*') if path.is_file()
                     and path.suffix in extensions
                     and not any(part in {'bin', 'obj', '__pycache__', '.git'} for part in path.relative_to(ROOT).parts))
    return sorted(set(files))
