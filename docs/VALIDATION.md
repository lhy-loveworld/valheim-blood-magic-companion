# Validation - release 1.2.0

Validated on 2026-10-06 against installed Valheim 1.0.17 (Steam build 25730771), using BepInExPack 5.4.2350 references. The installed game has updated since the 1.1.0 mod release; static contracts were rechecked.

| Check | Result |
| --- | --- |
| Plugin compilation with warnings treated as errors | Passed |
| Executable stamina-cost assertions | 203 passed |
| Executable receipt/ownership/mixed-client routing assertions | 34 passed |
| Executable summon behavior and emitted-IL assertions with simulated APIs | 40 passed |
| Static cost/game/compiled-hook assertions | 64 passed |
| Static XP/game/compiled-hook assertions | 20 passed |
| Static combined-plugin integration assertions | 9 passed |
| Static summon/game/compiled-hook assertions | 16 passed |
| Unity/Harmony loading, actual casting or skeleton attacks, multiplayer | Not tested |
| Linux client or dedicated server gameplay | Not tested |
| Public CI | See repository Actions; not a substitute for game testing |

Total: **277 executable assertions and 109 static assertions passed**.

The new tests execute production summon selection, observer filtering, obstruction filtering and IL rewriting against small game/physics/Harmony test doubles. They cover nearest visible dummy selection, existing/new ordinary-enemy priority, dead targets, ownership migration, wrong creature/faction, disabled behavior, target-only collider exemption, walls and other dummies remaining blockers, native ray parameters, unsupported rewrite rejection, branch labels, and execution of the rewritten call in a generated method. They do not test actual Unity raycasts or real Harmony installation. The installed-game validator separately checks the method signatures and obstruction-call layout used by the rewrite.

The existing cost and XP tests still pass. The stamina and XP implementation files are unchanged from 1.1.0. Configuration and native leash/range/attack gates still need the in-game acceptance checks in TESTING.md.

The plugin and standalone test assemblies were compiled with the Windows Framework compiler. The tests ran through the installed .NET host using build.py --dotnet; direct standalone executable launch was blocked by Windows Application Control. No .NET SDK was installed locally for these checks.

Plugin SHA256: fcfe27a926149c0f990aed93a237a27d98fa568c0a74448c5324b9e38884fbeb.
Game assembly SHA256: 25a0a107dce4d834c44c2b72d0eafd5cb7793933bda81816accfa1ea9543dace.

No game profile, save, or server installation was changed. Training defaults to off; enable Summons/AttackTrainingDummy and restart to test it. The new feature is experimental pending live melee, archer, obstacle and ownership-transfer testing.
