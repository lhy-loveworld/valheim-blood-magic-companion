# Building from source

## Standalone checks (no game installation needed)

With the .NET 8 SDK:

```text
dotnet run --project tests/Checks.csproj --configuration Release
```

This executes the same cost, receipt, routing, and summon checks used by the production code. Summon checks execute the production selection, ray-filter and transpiler code with simulated Unity/Harmony APIs, including an emitted-IL stack probe. They do not load Unity or real Harmony. GitHub Actions runs this command on pushes and pull requests, plus source/package metadata validation. The workflow has no publishing credentials and does not publish anything.

## Build the plugin on Windows

Requires Python 3, Valheim, BepInExPack 5.4.2350 or a compatible later 5.x pack, and the Windows .NET Framework compiler. From the repository root, substitute your real profile path:

```powershell
python scripts/build.py --game-managed 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed' --bepinex-core 'C:\path\to\profile\BepInEx\core' --csc 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' --output build
```

If using an installed .NET runtime (8 or newer) to host the standalone checks, append `--dotnet 'C:\Program Files\dotnet\dotnet.exe'`. No SDK is required for that build route. The default still runs the Framework test executable directly.

The builder compiles `src/*.cs` into `build/BloodMagicCompanion.dll`, then compiles and executes the standalone checks. It treats warnings as errors. It does not copy the DLL into the game. WSL is also supported when Windows executable interop works; pass `/mnt/c/...` paths and invoke the Windows compiler directly.

## Inspect contracts against your installed game

In Windows PowerShell from the repository root:

```powershell
.\scripts\Validate-Costs.ps1 -GameManaged 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed' -BepInExCore 'C:\path\to\profile\BepInEx\core' -PluginDll '.\build\BloodMagicCompanion.dll'
.\scripts\Validate-XP.ps1 -GameManaged 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed' -BepInExCore 'C:\path\to\profile\BepInEx\core' -PluginDll '.\build\BloodMagicCompanion.dll'
.\scripts\Validate-Summons.ps1 -GameManaged 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed' -BepInExCore 'C:\path\to\profile\BepInEx\core' -PluginDll '.\build\BloodMagicCompanion.dll'
.\scripts\Validate-Combined.ps1 -BepInExCore 'C:\path\to\profile\BepInEx\core' -PluginDll '.\build\BloodMagicCompanion.dll'
```

These scripts use Mono.Cecil shipped with BepInEx to inspect metadata and selected IL contracts. They do not execute the game and do not replace playtesting. The public CI does not compile the plugin because it has no game assemblies.

## Verify summon prefab assumptions

The committed `tests/SummonPrefabFacts.cs` contains six factual fields extracted from installed Valheim 1.0.17 prefabs. The regression suite uses the extracted skeleton faction instead of inventing a faction for its test double.

For an independent asset check, use a separate Python environment with UnityPy installed and run:

```text
python scripts/check_summon_prefabs.py --bundle PATH_TO_SOFTREF_BUNDLE
```

Find the bundle containing `Assets/Characters/Skeleton/Skeleton_Friendly.prefab` and `Assets/GameElements/Pieces/piece_TrainingDummy.prefab` in the game's `valheim_Data/StreamingAssets/SoftRef/manifest_extended`. The checker compares all six committed fields to the actual assets. Use `--write-fixture` only when intentionally refreshing the fixture after reviewing a game update. UnityPy is an optional inspection dependency, not a plugin or standard CI dependency. Game bundles are not redistributed.

## Package

```text
python scripts/check_source.py
python scripts/package.py --dll build/BloodMagicCompanion.dll --output dist
```

The package script produces a Thunderstore ZIP, a local-import ZIP with the temporary LocalMods author, and a clean source ZIP. Use `--local-author YourTeam` to change local-import metadata, and `--website https://github.com/OWNER/REPOSITORY` if you fork or move the project. The manifest already points to the source repository. Do not use the source ZIP as the Thunderstore upload.

The DLL must be built from this source and version. The package tool checks its metadata and compiled-source fingerprint against the build receipt; rebuild after changing C# files. Update Plugin.Version, AssemblyVersion, manifest version, changelog, and documentation together for a release.
