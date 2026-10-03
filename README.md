# Blood Magic Companion

Two configurable Blood Magic changes in one Valheim mod:

- **Stamina-powered casting:** replace eitr with fixed stamina costs, scaled by Blood Magic skill, while preserving the original health cost.
- **Caster shield XP:** credit Staff of Protection shield-break XP to the caster when the computer processing the break and the caster support the feature.

Both features can be switched off independently. One DLL, one configuration file.

**Status:** experimental release 1.1.0. Compiled against Valheim 1.0.16 and BepInExPack 5.4.2350. Offline arithmetic, routing, and static checks pass. Unity loading, actual casting, and multiplayer have not been playtested. The default conversion rate is a starting point for balancing, not a claim of equivalence to vanilla eitr.

## Install and upgrade

Use r2modman or Thunderstore Mod Manager with BepInExPack_Valheim 5.4.2350 or newer. Update the existing **qwertyzxcv-BloodMagicCompanion** package after 1.1.0 is published, then launch with **Start modded**. For a local ZIP, use **Settings → Profile → Import local mod** and select LocalMods-BloodMagicCompanion-1.1.0.zip. Avoid enabling both a local import and the public package at once.

For manual installation, replace BloodMagicCompanion.dll inside BepInEx/plugins with the new DLL. Keep only one copy. Close Valheim before updating.

**Remove or disable both standalone predecessors:** BloodMagicCasterXP and BloodMagicHealthCost. The combined plugin declares those IDs incompatible to avoid double patches.

After the first launch, edit BepInEx/config/local.valheim.bloodmagiccompanion.cfg through the mod manager's Config editor. Restart to apply settings.

**Upgrading from 1.0.0 changes casting balance:** the extra health payment is replaced by stamina. The original health cost is restored. If the old HealthCosts/Enabled setting was false, it seeds StaminaCosts/Enabled=false when the new setting is first created. An existing StaminaCosts/Enabled always takes precedence. Other old HealthCosts settings are ignored; configure the new StaminaCosts section instead. Old per-staff health multipliers are not copied because the units and meaning changed. CasterXP and Logging settings are retained.

## Stamina and health costs

At the defaults, each point of **base eitr** becomes **1 stamina**, discounted once by Blood Magic skill: 0% at skill 0, 16.5% at skill 50, and 33% at skill 100.

    converted stamina = base eitr × StaminaPerEitr × item multiplier
                        × (1 − CostReductionAtSkill100 × skill level / 100)
    total stamina = max(0, native stamina cost) + converted stamina

Skill is clamped to levels 0–100. Primary and secondary attacks use their own base eitr fields. Any original stamina cost retains its native modifiers and is added separately. Equipment and status-effect stamina discounts do not reduce the converted surcharge. A negative native stamina refund cannot cancel the surcharge.

| Base eitr | Skill 0 | Skill 50 | Skill 100 |
| --- | --- | --- | --- |
| 60 | 60 stamina | 50.1 stamina | 40.2 stamina |
| 100 | 100 stamina | 83.5 stamina | 67 stamina |
| 120 | 120 stamina | 100.2 stamina | 80.4 stamina |

These are conversion examples; each attack uses its actual configured base eitr cost.

**Health remains vanilla.** Original flat health plus the percentage of current health, its native skill discount, eligibility checks, and payment cap are unchanged. There is no extra health payment and no new health threshold. The conversion does not scale with current or maximum health.

**Enough stamina is required.** Native attack-start checks use the total stamina cost (including the game's small 0.1 stamina start margin). Spending down to zero does not make casting free. Native per-burst checks also apply, so a burst attack stops when it cannot afford another burst. The game controls deduction timing; this mod does not reserve stamina during wind-up or add a separate payment.

The tooltip shows the additional converted stamina, including current Blood Magic skill. It is added to any native stamina cost shown elsewhere. Eitr becomes zero; normal materials, ammunition, durability, and other conditions still apply.

Applies only to the owning local player's Blood Magic attacks with positive base eitr cost. Self-killing attacks and already zero-eitr attacks are excluded. Elemental magic, melee, creature attacks, shield strength, and summon behavior are unchanged.

Removing eitr also removes its food requirement. Pre-casting shields or summons and recovering stamina remains possible. This is a configurable alternative balance model, not vanilla difficulty parity.

## Caster shield XP and multiplayer

XP is awarded when damage **breaks** a Staff of Protection shield, not on cast, every hit, or ordinary expiry. The original XP factor and vanilla skill-reward call are preserved. A refreshed shield is attributed to its latest tracked caster. Skeleton attack XP, shield strength, duration, and which targets can receive shields are unchanged.

**The server and other players may remain unmodded.** This does not mean all remote shields can be corrected by one modded client.

| Situation | Result |
| --- | --- |
| Only you install the mod | Your own stamina-powered casting works. |
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

File: local.valheim.bloodmagiccompanion.cfg. Settings apply at startup.

| Section / setting | Default | Purpose |
| --- | --- | --- |
| StaminaCosts / Enabled | true | Replace eitr with stamina. False restores vanilla costs. An old explicit HealthCosts/Enabled=false seeds this false on upgrade. |
| StaminaCosts / StaminaPerEitr | 1 | Stamina per base eitr point; range 0.01–100. |
| StaminaCosts / CostReductionAtSkill100 | 0.33 | Converted stamina discount at skill 100; range 0–0.9. Does not change health scaling. |
| StaminaCosts / ItemCostMultipliers | StaffShield=1;StaffSkeleton=1;StaffTroll=1 | Per-prefab multipliers on converted stamina only; range 0.01–100. Unlisted items use 1. |
| StaminaCosts / ShowCostInTooltip | true | Show the converted stamina surcharge. |
| CasterXP / Enabled | true | Enable caster-attributed shield-break XP independently. |
| Logging / Diagnostics | false | Log shield attribution and XP delivery. |

Example: StaminaPerEitr=0.75 changes a 60-eitr spell to 45 stamina before skill scaling. StaffShield=1.2 makes shield conversion 20% more expensive without changing its original health cost. Use decimal points. Invalid settings fall back to defaults or ignore invalid item entries with a warning.

HealthCosts/Enabled is retained only as a migration seed. To disable conversion now, use StaminaCosts/Enabled. There is no health-only mode in 1.1.0. All configuration is local; it is not enforced on friends.

## Troubleshooting and removal

Check BepInEx/LogOutput.log for Blood Magic Companion 1.1.0 loaded and both feature states. If loading is rejected, remove the old standalone plugins. If patch installation fails, the mod removes its patches instead of leaving a partially enabled feature.

Report game and mod versions, relevant configuration, other combat mods, and which computer processed the shield break. Share only relevant log excerpts after removing personal information.

Disable or uninstall the mod and restart to remove it. Existing earned XP remains ordinary character progress. No coordinated server uninstall is required.

## Source and validation

The [source repository](https://github.com/lhy-loveworld/valheim-blood-magic-companion) includes C# source, MIT licensing, build scripts, standalone tests, static validators, and an offline GitHub Actions workflow. Game assemblies and loader libraries are not redistributed. This mod was developed with AI assistance.

See docs/VALIDATION.md for the exact build and offline checks, and docs/TESTING.md for the unrun in-game acceptance plan. Offline tests do not prove Unity loading, multiplayer behavior, or balance.
