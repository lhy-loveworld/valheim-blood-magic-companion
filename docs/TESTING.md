# Manual acceptance tests (not yet run)

Use a modded profile with only this combined plugin and BepInEx first. Disable the two standalone predecessors. Record the game build, configuration, which computer simulates each shield recipient, and the feature states from the startup log.

1. **Feature switches:** test both enabled, stamina only, XP only, and both disabled, restarting after each change. Disabled stamina conversion should be vanilla. Disabled XP should retain vanilla rewards and advertise no XP capability.
2. **Stamina and health:** remove eitr food. For a 60-base-eitr attack at skill 0, verify +60 stamina and unchanged original health cost. Repeat at skill 50 and 100 (+50.1 and +40.2), at high and low health. Confirm no cast at zero stamina or just below the native start margin. No materials or health should be consumed on a rejected start. Check a non-default rate and per-staff multiplier, and distinguish native stamina costs from the surcharge.
3. **Timing and compatibility:** take damage or heal during wind-up; health must follow vanilla timing. Exercise per-burst attacks with enough stamina for one burst but not the next. Verify one native stamina deduction per payment point, normal cancellation, and unchanged attack fields when switching weapons. Check a native stamina equipment modifier: it should not discount the converted surcharge. Test both attack modes, food expiry, exhaustion, and a staff whose total cost exceeds maximum stamina.
4. **Unrelated combat:** melee and elemental magic retain their normal costs. Summon behavior and shield strength remain unchanged.
5. **Only caster modded:** join an unmodded server with an unmodded friend. Both should connect without a mod-version requirement. Self-shield XP stays local. A shield on the unmodded friend follows vanilla recipient XP; no fabricated extra caster reward.
6. **Both relevant clients modded:** with caster XP enabled and the server unmodded, break the friend's shield. Recipient log should show redirection, caster log should show receipt, and only the caster should gain that reward.
7. **Summons:** test a skeleton controlled locally, then one controlled by another modded client. Test an unmodded simulation owner as a fallback case. Separate skeleton attack XP from shield-break XP.
8. **Legacy peer:** use BloodMagicCasterXP 1.2.0 on one computer and this combined mod on the other. Check both casting directions. Never install both on one computer.
9. **Lifecycle:** recasts attribute to the latest caster; ordinary expiry gives no shield XP. Test disconnect/reconnect, respawn, and creature ownership transfer. The documented lack of retry may cause a dropped reward, but must not duplicate it.
10. **Optional server installation:** if desired, test a server-owned creature with the mod on the server. This is not required for normal joining and does not fix an unmodded client that processes the break.

Enable Logging/Diagnostics to distinguish local reward, redirected reward, received reward, and vanilla fallback. A startup success log or a skill integer that did not change is not proof of correct XP delivery. Record results and limitations in VALIDATION.md instead of marking unrun scenarios passed.

11. **Configuration upgrade:** start with a 1.0.0 config with HealthCosts/Enabled=false, then verify the first new StaminaCosts/Enabled is false. Set the new entry true and verify it overrides the legacy false. Old health rate/multiplier keys must have no gameplay effect. Verify invalid numeric/per-staff values warn and fall back.


## 1.2.0 skeleton training acceptance (not yet run)

- Leave Summons/AttackTrainingDummy=false: verify melee and archer AI matches vanilla, including retaliation.
- Enable it and restart. In open terrain with no ordinary enemies, summon one melee skeleton and one archer near T.W.I.G. Confirm both acquire and repeatedly attack it without requiring provocation.
- Put a wall or another dummy between the skeleton and target: confirm it does not see through the obstacle. Remove the obstacle and confirm attacks resume. Repeat with terrain, view range, mist and follow/patrol leash boundaries.
- Add an ordinary hostile: verify training yields to combat, including when already attacking a dummy. Remove the hostile and verify training resumes.
- Check damage and normal skeleton attack XP; this feature must not multiply rewards or attack speed.
- Try a wolf, wild skeleton, and ordinary enemy: confirm their AI is unchanged.
- Transfer skeleton ownership to an unmodded or disabled peer and back: expect vanilla behavior there and training only under the enabled owner. Test an unmodded dedicated server and optional modded Linux owner.
- Toggle stamina and caster XP independently, with training both enabled and disabled. Confirm existing behavior remains independent.
- Inspect BepInEx logs for patch errors. Static and simulated-API checks do not establish actual Harmony/Unity compatibility.
