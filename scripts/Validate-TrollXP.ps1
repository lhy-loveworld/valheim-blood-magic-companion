param([Parameter(Mandatory=$true)][string]$GameManaged,
      [Parameter(Mandatory=$true)][string]$BepInExCore,
      [Parameter(Mandatory=$true)][string]$PluginDll)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $BepInExCore 'Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameManaged 'assembly_valheim.dll'))
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$script:count = 0
function Assert($value, $label) { if (!$value) { throw "FAIL: $label" }; $script:count++ }
$spawn = $game.MainModule.Types | Where-Object Name -eq 'SpawnAbility'
$setup = @($spawn.Methods | Where-Object Name -eq 'SetupAoe')
Assert ($setup.Count -eq 1 -and !$setup[0].IsStatic -and ($setup[0].Parameters.ParameterType.FullName -join ',') -eq 'Character,UnityEngine.Vector3') 'Spawn hook signature'
Assert ($setup[0].Parameters[0].Name -eq 'owner') 'Spawned character argument name'
Assert (@($spawn.Fields | Where-Object { $_.Name -eq 'm_owner' -and $_.FieldType.FullName -eq 'Character' }).Count -eq 1) 'Original caster field'
$coroutine = $spawn.NestedTypes | Where-Object Name -like '*Spawn*d__*'
$move = $coroutine.Methods | Where-Object Name -eq 'MoveNext'
$calls = @($move.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'SetupAoe' })
Assert ($calls.Count -eq 1 -and [string]$calls[0].Previous.Previous.Previous.Operand -eq '!!0 UnityEngine.GameObject::GetComponent<Character>()') 'Coroutine passes newly spawned Character to hook'
$aoeIL = $setup[0].Body.Instructions -join [Environment]::NewLine
Assert ($aoeIL.Contains('Aoe::Setup(Character') -and $aoeIL.Contains('Instantiate<UnityEngine.GameObject>')) 'Spawn hook precedes creation and setup of arrival AoE'
$character = $game.MainModule.Types | Where-Object Name -eq 'Character'
$damage = @($character.Methods | Where-Object Name -eq 'ApplyDamage')
Assert ($damage.Count -eq 1 -and ($damage[0].Parameters.ParameterType.FullName -join ',') -eq 'HitData,System.Boolean,System.Boolean,HitData/DamageModifier') 'Damage hook signature'
Assert ($damage[0].Parameters[0].Name -eq 'hit') 'Damage argument name'
$damageIL = $damage[0].Body.Instructions -join [Environment]::NewLine
Assert ($damageIL.Contains('Character::SetHealth(System.Single)') -and $damageIL.Contains('Character::GetHealth()') -and $damageIL.Contains('HitData::GetAttacker()')) 'Health-loss and attacker observations supported'
$feature = $plugin.MainModule.Types | Where-Object Name -eq 'TrollXP'
$patches = @($feature.NestedTypes | Where-Object { @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' }).Count -gt 0 })
Assert ($patches.Count -eq 3) 'Exactly spawn, damage and router hooks'
$spawnPatch = $feature.NestedTypes | Where-Object Name -eq 'CaptureSpawn'
$capture = ($spawnPatch.Methods | Where-Object Name -eq 'Prefix').Body.Instructions -join [Environment]::NewLine
Assert ($capture.Contains('TrollXpPolicy::IsSpawn') -and $capture.Contains('TrollXpPolicy::IsTroll') -and $capture.Contains('ZDO::Set(System.String,ZDOID)')) 'Exact spawn scope and persistent caster identity'
Assert ($capture.Contains('Player::m_localPlayer') -and $capture.Contains('Character::IsOwner')) 'Only local caster and owned spawn can record identity'
$damagePatch = $feature.NestedTypes | Where-Object Name -eq 'RewardDamage'
$prefix = ($damagePatch.Methods | Where-Object Name -eq 'Prefix').Body.Instructions -join [Environment]::NewLine
$postfix = ($damagePatch.Methods | Where-Object Name -eq 'Postfix').Body.Instructions -join [Environment]::NewLine
Assert ($prefix.Contains('TrollXpPolicy::Eligible') -and $prefix.Contains('BaseAI::IsEnemy') -and $prefix.Contains('Character::IsOwner')) 'Owned enemy eligibility checked before damage'
Assert ($postfix.Contains('TrollXpPolicy::LostHealth') -and $postfix.Contains('TrollXP::Report')) 'Reward requires actual health loss'
$award = ($feature.Methods | Where-Object Name -eq 'Award').Body.Instructions -join [Environment]::NewLine
Assert ($award.Contains('Player::m_localPlayer') -and $award.Contains('Character::IsOwner') -and $award.Contains('Skills::RaiseSkill')) 'Only local owning player receives XP'
Assert (!$award.Contains('TrollXpCooldown') -and !$award.Contains('realtimeSinceStartup') -and $award.Contains('CostModel::Setting')) 'Each eligible event uses validated caster factor without time throttling'
$receive = ($feature.Methods | Where-Object Name -eq 'Receive').Body.Instructions -join [Environment]::NewLine
Assert ($receive.Contains('ReceiptLedger::IsAuthorizedSender') -and $receive.Contains('ReceiptLedger::Accept') -and $receive.Contains('ZDO::GetPrefab')) 'Remote sender, duplicate and troll-prefab validation'
$report = ($feature.Methods | Where-Object Name -eq 'Report').Body.Instructions -join [Environment]::NewLine
Assert ($report.Contains('RewardRouting::Select') -and $report.Contains('ZRoutedRpc::InvokeRoutedRPC')) 'Capability-gated targeted reporting'
$pluginType = $plugin.MainModule.Types | Where-Object Name -eq 'Plugin'
$awake = ($pluginType.Methods | Where-Object Name -eq 'Awake').Body.Instructions -join [Environment]::NewLine
Assert ($awake.Contains('TrollXP::Patch') -and $awake.Contains('TrollXP::SetEnabled') -and $awake.Contains('UnpatchSelf')) 'Independent feature activation and failed-install cleanup'
$destroy = ($pluginType.Methods | Where-Object Name -eq 'OnDestroy').Body.Instructions -join [Environment]::NewLine
Assert ($destroy.Contains('TrollXP::SetEnabled')) 'Unload disables extra XP'
"PASS: $script:count static Trollstav/game/compiled-hook assertions. Not a Unity playtest."
