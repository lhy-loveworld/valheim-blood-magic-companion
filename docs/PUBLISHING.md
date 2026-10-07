# Publishing the prepared release

The source repository is https://github.com/lhy-loveworld/valheim-blood-magic-companion. Thunderstore publication is a separate step. The README labels this combined release experimental and untested in-game.

## GitHub

The clean source archive matches the public `lhy-loveworld/valheim-blood-magic-companion` project. It contains original mod source, the existing MIT license and notices, build and package scripts, regression tests, an offline-checks workflow, an issue template, and compatibility documentation. It excludes generated DLLs/EXEs, proprietary game assemblies, loader libraries, local profiles/configs, logs, and machine-specific paths.

Use GitHub for source review, issue tracking, contributions, tagged versions, and release downloads. Public CI tests only the pure code; it cannot assert that the Unity plugin works. Use a `v1.3.0` tag when creating a release from the reviewed source. Put built ZIPs in a GitHub Release instead of committing them into source history. The workflow never uploads or publishes packages.

The real repository URL is recorded in package/manifest.json. Use `scripts/package.py --website ...` only if the project moves. Do not upload the existing task folder; use the clean source archive.

## Thunderstore

Upload **BloodMagicCompanion-1.3.0.zip**, not the source archive. The package has a root manifest.json, README.md, CHANGELOG.md, 256Ã—256 PNG icon, license, validation notes, and plugins/BloodMagicCompanion.dll. The dependency is denikson-BepInExPack_Valheim-5.4.2350. The author/team is selected at upload; the public manifest does not claim a team.

1. Sign in and select the existing **qwertyzxcv** team. The listing is https://thunderstore.io/c/valheim/p/qwertyzxcv/BloodMagicCompanion/. Do not create a different team or rename the package for this update.
2. Open the upload page, select the public ZIP, select your team, choose **Valheim**, and choose the relevant available categories.
3. Review the description, README, dependency, and optional repository link. The local-import ZIP's temporary LocalMods author does not establish a public team.
4. Submit when ready. Later updates must use the same team and package name, with an increased version.

The pending playtest plan is in TESTING.md. Keep the experimental/unverified wording until those tests have actually been completed. If publishing before live testing, retain that wording in the public listing.

Official references:

- Package requirements: https://wiki.thunderstore.io/mods/creating-a-package
- BepInEx folder layout: https://wiki.thunderstore.io/mods/packaging-your-mods
- Upload page: https://thunderstore.io/package/create
- Updating an existing package: https://wiki.thunderstore.io/mods/updating-a-package

## Updating the existing listing to 1.3.0

Upload BloodMagicCompanion-1.3.0.zip at https://thunderstore.io/package/create, select team qwertyzxcv and community Valheim, retain appropriate categories, and submit. The manifest keeps name BloodMagicCompanion and raises version_number to 1.3.0, so this is an update to the existing package. Do not upload either the source ZIP or the LocalMods ZIP. Verify the live listing shows 1.3.0, then update the mod in r2modman and restart Valheim. To use skeleton training, set Summons/AttackTrainingDummy=true in the Config editor and restart; it defaults to false.

To use lava protection, set Summons/LavaDamageImmunity=true and restart on each computer that may simulate the summons. It defaults to false.
