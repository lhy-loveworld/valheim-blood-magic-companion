# Contributing

Run the standalone checks after changing cost math, ownership validation, or reward routing. Changes to game hooks also need local compilation and the static validators against the game build you support. Explain behavioral changes with a concrete example and list which checks actually ran.

Keep server installation optional. Preserve the documented vanilla fallback when the shield simulation owner is unmodded or a caster cannot receive a compatible message. Do not guess XP from visuals or award it on every hit.

Protocol changes need an explicit compatibility decision. The legacy RPC name and capability key are intentional, not obsolete names to clean up. Never bundle Valheim assemblies, loader binaries, profiles, saves, or logs in the source repository.

Issue reports should include a small reproduction, versions, relevant settings, and whether the recipient is simulated by a modded computer. Follow docs/BUILDING.md and docs/TESTING.md for build and validation steps.
