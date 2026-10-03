"""Check distributable metadata/source without requiring game assemblies."""
import ast
import re
import struct
import xml.etree.ElementTree as ET
from release_common import ROOT, manifest, source_files

def check():
    data = manifest()
    assert set(data) == {'name', 'version_number', 'description', 'website_url', 'dependencies'}
    assert data['name'] == 'BloodMagicCompanion'
    assert re.fullmatch(r'[A-Za-z0-9_]{1,128}', data['name'])
    assert re.fullmatch(r'[0-9]+[.][0-9]+[.][0-9]+', data['version_number'])
    assert len(data['description']) <= 250
    assert data['website_url'] == '' or data['website_url'].startswith('https://')
    assert data['dependencies'] == ['denikson-BepInExPack_Valheim-5.4.2350']
    plugin = (ROOT / 'src/Plugin.cs').read_text(encoding='utf-8')
    assert 'Version = "' + data['version_number'] + '"' in plugin
    assert 'AssemblyVersion("' + data['version_number'] + '.0")' in plugin
    assert sum(path.read_text(encoding='utf-8').count('[BepInPlugin(') for path in (ROOT / 'src').glob('*.cs')) == 1
    png = (ROOT / 'package/icon.png').read_bytes()
    assert png[:8] == bytes([137, 80, 78, 71, 13, 10, 26, 10])
    assert struct.unpack('>II', png[16:24]) == (256, 256)
    assert 'MIT License' in (ROOT / 'LICENSE').read_text(encoding='utf-8')
    for path in source_files():
        if path.suffix == '.png': continue
        text = path.read_text(encoding='utf-8')
        assert not any(ord(c) < 32 and ord(c) not in {9, 10, 13} for c in text), path
        # Distributable sources must not carry the development machine's private paths.
        assert ('/mnt/c/' + 'Users/') not in text and ('C:' + chr(92) + 'Users' + chr(92)) not in text, path
        if path.suffix == '.py': ast.parse(text, filename=str(path))
    project = ET.parse(ROOT / 'tests/Checks.csproj')
    for item in project.findall('.//Compile'):
        assert (ROOT / 'tests' / item.attrib['Include']).is_file()
    print('PASS: source versions, manifest, icon, license, distributable-file hygiene, Python syntax, and test-project inputs.')

if __name__ == '__main__': check()
