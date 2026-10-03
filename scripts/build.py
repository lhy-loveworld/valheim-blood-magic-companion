"""Build with a Windows .NET Framework compiler, from Windows or WSL. Requires Python 3."""
import argparse
from pathlib import Path
import subprocess
import os
parser = argparse.ArgumentParser()
parser.add_argument('--game-managed', required=True, type=Path)
parser.add_argument('--bepinex-core', required=True, type=Path)
parser.add_argument('--csc', required=True, type=Path)
parser.add_argument('--output', type=Path, default=Path('build'))
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
source = Path(__file__).resolve().parent.parent / 'src'
tests = source.parent / 'tests'

def compiler_path(path):
    path = str(path.resolve())
    if os.name != 'nt' and path.startswith('/mnt/') and len(path) > 7 and path[6] == '/':
        return path[5].upper() + ':' + path[6:].replace('/', '\\')
    return path

references = [args.game_managed / name for name in [
    'mscorlib.dll', 'System.dll', 'System.Core.dll', 'netstandard.dll',
    'assembly_valheim.dll', 'assembly_utils.dll', 'UnityEngine.dll',
    'UnityEngine.CoreModule.dll', 'UnityEngine.PhysicsModule.dll', 'SoftReferenceableAssets.dll']]
references += [args.bepinex_core / name for name in ['BepInEx.dll', '0Harmony.dll']]
subprocess.run([str(args.csc), '/noconfig', '/nologo', '/target:library', '/optimize+',
    '/nostdlib+', '/warnaserror+', '/out:' + compiler_path(args.output / 'BloodMagicCompanion.dll')]
    + ['/reference:' + compiler_path(ref) for ref in references]
    + [compiler_path(path) for path in sorted(source.glob('*.cs'))], check=True)
checks = args.output / 'BloodMagicCompanionChecks.exe'
subprocess.run([str(args.csc), '/nologo', '/target:exe', '/optimize+', '/warnaserror+',
    '/out:' + compiler_path(checks), compiler_path(source / 'CostModel.cs'), compiler_path(source / 'ReceiptLedger.cs')]
    + [compiler_path(path) for path in sorted(tests.glob('*.cs'))], check=True)
subprocess.run([str(checks.resolve())], check=True)
print('Built:', (args.output / 'BloodMagicCompanion.dll').resolve())

from release_common import manifest, source_fingerprint, digest
import json
receipt = {"version": manifest()["version_number"],
           "source_sha256": source_fingerprint(),
           "dll_sha256": digest(args.output / "BloodMagicCompanion.dll"),
           "standalone_checks_passed": True}
(args.output / "build-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
