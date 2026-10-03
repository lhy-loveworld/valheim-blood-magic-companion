# Validation â€” release 1.1.0

Validated on 2026-10-03 against the installed Valheim assembly used for the 1.0.16 build, with BepInExPack 5.4.2350 references. Its SHA256 is unchanged from the previously validated game build.

| Check | Result |
| --- | --- |
| Plugin compilation with warnings treated as errors | Passed |
| Executable stamina-cost assertions | 203 passed |
| Executable receipt/ownership/mixed-client routing assertions | 34 passed |
| Static cost/game/compiled-hook assertions | 64 passed |
| Static XP/game/compiled-hook assertions | 20 passed |
| Static combined-plugin integration assertions | 9 passed |
| Unity/Harmony loading, actual casting, multiplayer, legacy-peer interoperability | Not tested |
| Linux client or dedicated server | Not tested |
| Public CI | See repository Actions; not a substitute for game testing |

Total: **237 executable assertions and 93 static assertions passed**.

The cost checks cover skill scaling, per-staff/rate settings, existing native stamina, invalid values, overflow, unchanged health patch scope, native start and per-burst checks, and one native stamina payment per payment path. Configuration migration is inspected in code and included in the manual test plan; it has not been run inside BepInEx.

Plugin SHA256: 0710c50626fb5bd25e8cef3fc2db9088c030a1e209459e5fc24821d1842eb935.
Game assembly SHA256: 96cfc004f7f4a6f30d070bef39eafd79c466a137121c4665a2f19fb9c15c6127.

Balance remains untested. No game profile, save, or server installation was changed for these checks.
