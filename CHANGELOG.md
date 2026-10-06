# Changelog

## 1.2.0

- Add opt-in `Summons/AttackTrainingDummy` for summoned melee and archer skeletons.
- Prefer ordinary enemies; allow a nearby visible T.W.I.G. as a fallback target.
- Exclude only the selected dummy's own colliders from the skeleton's visibility ray. Preserve other obstacles and native attack gates.
- Apply only on the skeleton's simulation owner, without requiring installation on other peers or changing ownership.
- Default remains off. Built against Valheim 1.0.17; live gameplay remains untested.

## 1.1.0

- Replace converted eitr health payments with fixed stamina costs; original health cost and its native skill scaling remain unchanged.
- Default conversion: 1 stamina per base eitr, reduced once by Blood Magic skill (33% at level 100).
- Use native stamina checks and deductions, including per-burst checks; no free casting when exhausted.
- Add StaminaCosts settings for enabling conversion, conversion rate, skill discount, per-staff multipliers, and tooltips.
- Retain an explicit old HealthCosts/Enabled=false as the initial new enable setting. Old health balance settings are ignored.
- Preserve caster shield XP behavior and protocol, optional server installation, and mixed-client fallback.

## 1.0.0

First combined release, incorporating BloodMagicCasterXP 1.2.0 and BloodMagicHealthCost 1.1.0 into one plugin.

- Independent health-only casting and caster-shield-XP switches in one configuration file.
- Both health percentage portions scale with current health; the entire cost scales with skill. No added low-health casting threshold.
- Preserve XP protocol 3 for compatibility with peers using BloodMagicCasterXP 1.2.0.
- Optional server and other-client installation, with explicit mixed-client reward limitations.
- Prevent loading alongside either standalone predecessor on the same computer.
- Include source, standalone regression checks, static validators, and documented build steps.

Experimental: no in-game or multiplayer playtest has been completed for this combined release.
