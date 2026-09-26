# Blood Magic Companion

Two configurable Blood Magic changes in one Valheim mod:

- **Health-only casting:** replace eitr with an extra current-health cost, scaled by Blood Magic skill, without adding a low-health casting barrier.
- **Caster shield XP:** credit the Staff of Protection's shield-break XP to the caster when the computer processing the break and the caster support the feature.

Both features are enabled by default and can be switched off independently. There is one DLL and one configuration file.

**Status:** experimental first combined release. Compiled against Valheim 1.0.16 and BepInExPack 5.4.2350. Offline arithmetic, routing, and static checks pass. Unity loading, in-game behavior, and multiplayer have not yet been playtested. Balance defaults are a starting point, not a claim of equivalence to the original eitr-food tradeoff.

## Install and upgrade

Use r2modman or Thunderstore Mod Manager with BepInExPack_Valheim 5.4.2350 or newer. For an unpublished local ZIP, select **Settings → Profile → Import local mod**, choose `LocalMods-BloodMagicCompanion-1.0.0.zip`, and launch with **Start modded**. For manual installation, place `BloodMagicCompanion.dll` inside the profile or game's `BepInEx/plugins` directory.

**Remove or disable BOTH standalone predecessors before enabling this mod:** BloodMagicCasterXP and BloodMagicHealthCost. They are replaced by this single plugin. BepInEx will refuse to load the combined plugin alongside those old plugin IDs, preventing double patches. Restart after making changes.

After the first launch, edit `BepInEx/config/local.valheim.bloodmagiccompanion.cfg` through the mod manager's Config editor. Restart to apply gameplay settings. Old standalone configuration files are left alone and are not imported automatically; copy any custom values into the new settings described below. There are no save migrations.

## Health-only costs

At the defaults, every point of **base eitr** becomes **0.5% of current health**. Add the original health cost, then discount the entire amount with Blood Magic skill: 0% at skill 0, 16.5% at skill 50, and 33% at skill 100. The discount is applied once.

```
base = flat health cost + current health × original health percentage / 100
extra = current health × base eitr cost × conversion percentage / 100
cost = (base × existing-health multiplier + extra)
       × item multiplier × (1 − skill reduction × skill level / 100)
payment = min(cost, max(0, current health − 1))
```

Both percentage portions use **current health**, never maximum health. Original flat health costs remain flat before the multiplier and discount. Skill is clamped to levels 0–100.

For a spell with 40% health and 60 base eitr, the total becomes 70% of current health at skill 0. At 200 health that is 140; at 20 health it is 14. At skill 100 those costs are 93.8 and 9.38. This is an example calculation; each attack uses its actual configured cost fields.

There is **no added health eligibility check**. Payment is capped to leave 1 health, including costs above 100%. At 1 health the health payment is zero. This deliberately allows low-health emergency casting. Normal materials, ammunition, durability, and other non-eitr conditions still apply. Damage taken after casting can still kill you.

The added tooltip section displays the payable total at your current health and skill. Vanilla health lines above it describe the base cost. The actual cost is recalculated at the normal payment time, including each burst of a per-burst attack.

Applies to the owning local player's attacks whose item uses Blood Magic and whose base eitr cost is positive. Primary and secondary attacks use their own fields. Explicitly self-killing attacks and already zero-eitr attacks are left unchanged. Elemental magic, melee, and creature attacks are unchanged.

## Caster shield XP and multiplayer

XP is awarded when damage **breaks** a Staff of Protection shield, not on cast, every hit, or ordinary expiry. The original XP factor and vanilla skill-reward call are preserved. A refreshed shield is attributed to its latest tracked caster. Skeleton attack XP, shield strength, duration, and which targets can receive shields are unchanged.

**The server and other players may remain unmodded.** This does not mean all remote shields can be corrected by one modded client.

| Situation | Result |
| --- | --- |
| Only you install the mod | Your own health-only casting works. |
| You shield yourself | Your shield-break XP remains yours. |
| You shield a skeleton currently simulated by your modded client | Shield-break XP can go to you. |
| You shield an unmodded friend | Their client uses vanilla XP rules; this mod cannot redirect that reward. |
| You shield a skeleton simulated by an unmodded computer | That computer uses vanilla XP rules. |
| Caster and computer processing the break both enable compatible caster XP | The shield-break reward is routed to the caster. |
| A caster is unmodded, XP-disabled, or incompatible | Modded recipients use vanilla reward fallback. |
| Dedicated server remains unmodded | Supported design; targeted XP messages use ordinary Valheim forwarding. |
| Dedicated server runs this mod with caster XP enabled | Optional; it can report breaks it actually processes. |

The computer simulating a creature can change; summoning it does not guarantee that your client always controls it. A modded server cannot fix a break processed by an unmodded client.

The XP protocol intentionally matches **BloodMagicCasterXP 1.2.0**. A friend using that standalone version can exchange XP messages with this combined mod, though this interoperability has not been tested live. Do not install both on the same client. Standalone 1.0/1.1 or unknown protocols use vanilla fallback.

Capability is advertised only while caster XP is enabled. No mandatory-mod network handshake, forced version matching, or mod-version kick is added. Targeted remote rewards require known current ownership; disconnects, respawns, ownership transitions, or missing network data may drop rewards. There is no offline queue or delivery retry. Receipts have bounded duplicate protection; this is a cooperative mod, not an anti-cheat system. Other mods replacing shield, cost, or network methods may interfere.

## Configuration

File: `local.valheim.bloodmagiccompanion.cfg`. Gameplay settings apply at startup.

| Section / setting | Default | Purpose |
| --- | --- | --- |
| HealthCosts / Enabled | true | Enable health-only casting. Disable for vanilla costs while retaining the XP feature. |
| CasterXP / Enabled | true | Enable caster-attributed shield XP. Disable independently of health costs. |
| HealthCosts / EitrAsCurrentHealthPercentPerPoint | 0.5 | Percentage of current health per base eitr point; range 0.01–10. |
| HealthCosts / ExistingHealthCostMultiplier | 1 | Multiplier on the original health portion; range 0.01–10. |
| HealthCosts / CostReductionAtSkill100 | 0.33 | Discount fraction at skill 100; range 0–0.9. |
| HealthCosts / ItemCostMultipliers | StaffShield=1;StaffSkeleton=1;StaffTroll=1 | Semicolon-separated prefab-name multipliers for the entire cost; each 0.01–100. Unlisted items use 1. |
| HealthCosts / ShowCostInTooltip | true | Display the payable total. |
| Logging / Diagnostics | false | Log shield attribution and XP delivery for troubleshooting. |

Example: `StaffShield=1.2;StaffSkeleton=1` raises total shield cost by 20%. A conversion rate of 0.35 is gentler; 0.75 is harsher. Large costs still use the leave-one-health cap. Use decimal points. Invalid settings fall back to defaults or ignore invalid item entries with a warning.

When migrating, copy the old health mod's Balance values into HealthCosts, its General/Enabled into HealthCosts/Enabled, and its tooltip switch into HealthCosts/ShowCostInTooltip. Copy the XP mod's Logging/Diagnostics if desired. The obsolete maximum-health conversion and minimum-health reserve settings from BloodMagicHealthCost 1.0.0 have no equivalent and are ignored. All configuration is local; it is not enforced on friends.

## Troubleshooting and removal

Check BepInEx/LogOutput.log for `Blood Magic Companion 1.0.0 loaded` and the two feature states. If loading is rejected, remove the old standalone plugins and restart. If patch installation fails, the combined mod removes its patches rather than leaving a partially enabled feature.

Before filing a bug, report the game and mod versions, the relevant configuration, other mods changing combat, and which computer was processing the shield break. Share only relevant log excerpts and remove personal information. The source project includes an issue template and a manual multiplayer test plan.

To remove the mod, disable or uninstall it and restart. The config may remain. Existing earned XP stays part of ordinary character progress. No coordinated server uninstall is required.

## Source and validation

The [source repository](https://github.com/lhy-loveworld/valheim-blood-magic-companion) contains all C# source, the MIT license, build scripts, standalone tests, static game-contract validators, and a GitHub Actions workflow for tests that require no game files. Game assemblies and loader libraries are not redistributed. The source and this release were developed with AI assistance.

This combined release passed 250 executable assertions (216 costs and 34 XP/ownership/routing) and 81 static assertions (52 cost contracts, 20 XP contracts, and 9 integration checks). These are offline checks, not proof of in-game or multiplayer behavior. See the included VALIDATION.md for details and the source project's docs/TESTING.md for the playtest plan.
