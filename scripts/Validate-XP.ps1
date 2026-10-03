param(
    [string]$GameManaged = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed',
    [Parameter(Mandatory = $true)][string]$BepInExCore,
    [Parameter(Mandatory = $true)][string]$PluginDll
)
$ErrorActionPreference = 'Stop'
# Read metadata and IL without running Valheim, Unity, or Harmony.
Add-Type -Path "$BepInExCore\Mono.Cecil.dll"
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly("$GameManaged\assembly_valheim.dll")
function Get-GameMethod([string]$typeName, [string]$methodName) {
    $type = $assembly.MainModule.Types | Where-Object Name -EQ $typeName
    $matches = @($type.Methods | Where-Object Name -EQ $methodName)
    if ($matches.Count -ne 1) { throw "Expected one $typeName.$methodName; found $($matches.Count)" }
    return $matches[0]
}
function Assert-Contract([bool]$value, [string]$label) {
    if (!$value) { throw "FAIL: $label" }
    Write-Output "PASS: $label"
}
$damage = Get-GameMethod Character RPC_Damage
$done = Get-GameMethod SE_Shield IsDone
$setter = Get-GameMethod StatusEffect SetAttacker
$awake = Get-GameMethod Player Awake
$constructor = Get-GameMethod ZRoutedRpc .ctor
$damageCalls = @($damage.Body.Instructions | Where-Object { "$($_.Operand)" -eq 'System.Void StatusEffect::SetAttacker(Character)' })
$rewardCalls = @($done.Body.Instructions | Where-Object { "$($_.Operand)" -eq 'Skills Character::GetSkills()' })
Assert-Contract ($damageCalls.Count -eq 1) 'actual RPC_Damage contains exactly one caster hook'
Assert-Contract ($rewardCalls.Count -eq 1) 'actual IsDone contains exactly one recipient-skill hook'
Assert-Contract ($setter.Parameters.Count -eq 1 -and "$($setter.Parameters[0].ParameterType)" -eq 'Character') 'caster hook argument contract matches'
Assert-Contract ($constructor.Parameters.Count -eq 1 -and "$($constructor.Parameters[0].ParameterType)" -eq 'System.Boolean') 'router construction hook matches'
Assert-Contract ($awake.Parameters.Count -eq 0) 'player initialization hook matches'
$shield = $assembly.MainModule.Types | Where-Object Name -EQ SE_Shield
Assert-Contract (@($shield.Methods | Where-Object Name -EQ SetAttacker).Count -eq 0) 'shield uses base attacker setter'
Assert-Contract (@($done.Body.Instructions | Where-Object { "$($_.Operand)" -eq 'System.Void Skills::RaiseSkill(Skills/SkillType,System.Single)' }).Count -eq 1) 'vanilla shield reward uses Skills.RaiseSkill directly'
$route = Get-GameMethod ZRoutedRpc RouteRPC
Assert-Contract (@($route.Body.Instructions | Where-Object { "$($_.Operand)" -eq 'System.Int64 ZRoutedRpc/RoutedRPCData::m_targetPeerID' }).Count -gt 0) 'vanilla routing contains targeted peer forwarding'
$pluginPath = $PluginDll
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($pluginPath)
Assert-Contract ($plugin.Name.Name -eq 'BloodMagicCompanion') 'compiled plugin identity matches'
Assert-Contract (@($plugin.MainModule.AssemblyReferences | Where-Object Name -EQ 'assembly_valheim').Count -eq 1) 'plugin references actual Valheim assembly'
$pluginType = $plugin.MainModule.Types | Where-Object FullName -EQ 'BloodMagicCompanion.CasterXP'
$reward = $pluginType.Methods | Where-Object Name -EQ GetRewardSkills
$receive = $pluginType.Methods | Where-Object Name -EQ ReceiveBreak
Assert-Contract (@($reward.Body.Instructions | Where-Object { $_.OpCode.Name -in @('isinst','castclass') -and "$($_.Operand)" -eq 'Player' }).Count -eq 0) 'reward routing does not restrict recipients to players'
Assert-Contract (@($receive.Body.Instructions | Where-Object { "$($_.Operand)" -eq 'System.Int64 ZDOID::get_UserID()' }).Count -eq 0) 'network receiver does not confuse creature creator with owner'
Assert-Contract (@($receive.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'call' -and "$($_.Operand)" -match 'ReceiptLedger::IsAuthorizedSender' }).Count -eq 1) 'network receiver uses tested current-owner validation'
Assert-Contract (@($reward.Body.Instructions | Where-Object { "$($_.Operand)" -eq 'Player Player::m_localPlayer' }).Count -eq 1) 'locally simulated creatures have a caster-credit path'
Assert-Contract ($plugin.Name.Version.ToString() -eq '1.1.0.0') 'combined assembly version is 1.1.0.0'
Assert-Contract (@($reward.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'call' -and "$($_.Operand)" -match 'RewardRouting::Select' }).Count -eq 1) 'production reward dispatch uses the tested mixed-client routing policy'
Assert-Contract (@($reward.Body.Instructions | Where-Object { "$($_.Operand)" -eq 'System.Int64 ZDO::GetOwner()' }).Count -eq 1) 'caster peer is resolved at break time rather than cached at cast time'
Assert-Contract (@($route.Body.Instructions | Where-Object { "$($_.Operand)" -match 'ZRoutedRpc::m_functions' }).Count -eq 0) 'vanilla forwarding does not require the server to register a mod handler'
Assert-Contract (@($plugin.MainModule.AssemblyReferences | Where-Object Name -Match 'ServerSync|Jotunn').Count -eq 0) 'plugin has no external mandatory-mod synchronization dependency'
Assert-Contract (@($pluginType.CustomAttributes | Where-Object { $_.AttributeType.FullName -match 'NetworkCompatibility|BepInDependency' }).Count -eq 0) 'plugin declares no network compatibility gate or extra plugin requirement'
Write-Output '20 static game/build contract checks passed. Live joining and gameplay remain untested.' 
