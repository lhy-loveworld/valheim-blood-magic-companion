# Validation - release 1.3.0

Validated on 2026-10-06 against installed Valheim 1.0.17 (Steam build 25730771), using BepInExPack 5.4.2350 references. The installed game has updated since the 1.1.0 mod release; static contracts were rechecked.

| Check | Result |
| --- | --- |
| Plugin compilation with warnings treated as errors | Passed |
| Executable stamina-cost assertions | 203 passed |
| Executable receipt/ownership/mixed-client routing assertions | 34 passed |
| Executable summon behavior and emitted-IL assertions with simulated APIs | 42 passed |
| Executable lava eligibility and emitted-branch assertions with simulated APIs | 36 passed |
| Static lava/game/compiled-hook assertions | 15 passed |
| Static cost/game/compiled-hook assertions | 64 passed |
| Static XP/game/compiled-hook assertions | 20 passed |
| Static combined-plugin integration assertions | 9 passed |
| Static summon/game/compiled-hook assertions | 17 passed |
| Installed-prefab fixture comparisons | 6 passed |
| 1.2.0 game loading | User profile log confirms loaded with training enabled |
| 1.2.0 skeleton training | User reports both melee and archer fail to attack |
| 1.3.0 Unity/Harmony loading, attacks and lava survival | Not tested |
| Linux client or dedicated server gameplay | Not tested |
| Public CI | See repository Actions; not a substitute for game testing |

Total: **315 executable assertions and 125 static assertions passed**.

The new tests execute production summon selection, observer filtering, obstruction filtering and IL rewriting against small game/physics/Harmony test doubles. They cover nearest visible dummy selection, existing/new ordinary-enemy priority, dead targets, ownership migration, wrong creature/faction, disabled behavior, target-only collider exemption, walls and other dummies remaining blockers, native ray parameters, unsupported rewrite rejection, branch labels, and execution of the rewritten call in a generated method. They do not test actual Unity raycasts or real Harmony installation. The installed-game validator separately checks the method signatures and obstruction-call layout used by the rewrite.

The 1.2.0 simulated tests used an incorrect PlayerSpawned faction. Version 1.2.1 added a minimal fixture extracted from the actual Skeleton_Friendly and piece_TrainingDummy prefabs: the skeleton faction is Players (0), and the dummy faction is TrainingDummy (12). The new actual-faction eligibility assertion was run against the unchanged 1.2.0 implementation and failed, then passed after correcting the production filter. Both target selection and visibility filtering use the corrected guard. All six fixture values were independently compared with the installed assets by check_summon_prefabs.py. The Character faction enum was also checked in the installed game assembly. No compiled game method assigns m_faction beyond Character construction; the inspected GetFaction method returns that field directly.

The existing cost and XP tests still pass. The stamina and XP implementation files are unchanged from 1.1.0. Configuration and native leash/range/attack gates still need the in-game acceptance checks in TESTING.md.

The plugin and standalone test assemblies were compiled with the Windows Framework compiler. The tests ran through the installed .NET host using build.py --dotnet; direct standalone executable launch was blocked by Windows Application Control. No .NET SDK was installed locally for these checks.

Plugin SHA256: 53f5e4f4c292c5a1e98517a1127a72c5b2689e6440154e8c856a1849327d8141.
Game assembly SHA256: 25a0a107dce4d834c44c2b72d0eafd5cb7793933bda81816accfa1ea9543dace.

No game profile, save, or server installation was changed. Training defaults to off; enable Summons/AttackTrainingDummy and restart to test it. The new feature is experimental pending live melee, archer, obstacle and ownership-transfer testing.

## Lava protection evidence and limits

The installed Character.UpdateHeatDamage(float) has two lava heat branches followed by the boiling-ocean branch. Each lava branch applies an AshlandsLava hit and may add Burning; the ocean branch uses AshlandsOcean. The new guard jumps over both lava branches for eligible owned summons while keeping native tick timing and ocean processing. It does not change UpdateLava, generic Damage/RPC_Damage, fire resistance, existing status effects or AI/pathfinding.

The 36 new executable assertions run the production guard and transpiler against a generated heat-branch fixture with simulated Character/Harmony APIs. They verify vanilla controls, full/partial lava protection, native ocean damage, unchanged heat fields, independent feature toggles, owner/tame/alive/faction/prefab scope, unsupported-layout rejection and incoming branch labels. The 15 installed-game/compiled-plugin checks separately verify the actual method signature, field-read order, heat hit types, damage and burning calls, hook scope and lifecycle cleanup. Neither test layer installs Harmony inside Unity or proves multiplayer lava survival.

Summons/LavaDamageImmunity defaults to false. Enable it and restart on the current simulation owner to test. Ownership transfer to an unmodded or disabled peer restores vanilla lava behavior. The existing six prefab comparisons from the 1.2.1 validation remain applicable: the fixture and inspected game assets are unchanged.

Shield XP code is unchanged. Offline routing checks do not establish that the reported Blood Magic level-1 popup was caused by shield attribution, nor verify the caster's saved skill progress. That multiplayer investigation remains pending separately.
