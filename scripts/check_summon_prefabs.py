"""Verify the test fixture against local game assets; requires UnityPy (inspection only)."""
import argparse
import re
from pathlib import Path
import UnityPy

parser = argparse.ArgumentParser()
parser.add_argument('--bundle', required=True, type=Path)
parser.add_argument('--write-fixture', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
fixture = root / 'tests/SummonPrefabFacts.cs'
paths = {'Skeleton': 'Assets/Characters/Skeleton/Skeleton_Friendly.prefab',
         'Dummy': 'Assets/GameElements/Pieces/piece_TrainingDummy.prefab'}
env = UnityPy.load(str(args.bundle))
facts = {}
for label, asset_path in paths.items():
    prefab = env.container[asset_path].read()
    characters = []
    for pair in prefab.m_Component:
        obj = pair.component.deref()
        if obj.type.name != 'MonoBehaviour':
            continue
        data = obj.read_typetree()
        if 'm_faction' in data and 'm_aiSkipTarget' in data:
            characters.append(data)
    assert len(characters) == 1, asset_path
    facts[label + 'Name'] = prefab.m_Name
    facts[label + 'Faction'] = characters[0]['m_faction']
    facts[label + 'SkipTarget'] = bool(characters[0]['m_aiSkipTarget'])
if args.write_fixture:
    lines = ['// Extracted from installed Valheim prefab data with scripts/check_summon_prefabs.py.',
             '// Minimal factual fields only; no game assemblies or asset bundles are redistributed.',
             'internal static class SummonPrefabFacts', '{']
    for key, value in facts.items():
        if isinstance(value, bool):
            kind, text = 'bool', str(value).lower()
        elif isinstance(value, int):
            kind, text = 'int', str(value)
        else:
            kind, text = 'string', '"' + value + '"'
        lines.append('    internal const ' + kind + ' ' + key + ' = ' + text + ';')
    fixture.write_text('\n'.join(lines + ['}', '']), encoding='utf-8')
text = fixture.read_text(encoding='utf-8')
for key, value in facts.items():
    match = re.search(r'const (?:string|int|bool) ' + key + r' = ([^;]+);', text)
    assert match, key
    expected = str(value).lower() if isinstance(value, bool) else str(value)
    actual = match[1].strip('"')
    assert actual == expected, (key, actual, expected)
    print('PASS:', key, '=', actual)
print('PASS: six fixture fields match the actual installed prefabs; not a gameplay test.')
