param([Parameter(Mandatory=$true)][string]$GameManaged,
      [Parameter(Mandatory=$true)][string]$BepInExCore,
      [Parameter(Mandatory=$true)][string]$PluginDll)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $BepInExCore 'Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameManaged 'assembly_valheim.dll'))
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$script:count = 0
function Assert($condition, $label) {
    if (!$condition) { throw "FAIL: $label" }
    $script:count++
}
$character = $game.MainModule.Types | Where-Object Name -eq 'Character'
$methods = @($character.Methods | Where-Object { $_.Name -eq 'UpdateHeatDamage' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.Single' })
Assert ($methods.Count -eq 1 -and !$methods[0].IsStatic -and $methods[0].ReturnType.FullName -eq 'System.Void') 'Native heat patch signature matches'
$method = $methods[0]
Assert ($method.Body.ExceptionHandlers.Count -eq 0) 'No heat exception boundaries cross guard'
$code = $method.Body.Instructions
$lava = @($code | Where-Object { $_.OpCode.Name -eq 'ldfld' -and [string]$_.Operand -eq 'System.Single Character::m_lavaHeatLevel' })
$ocean = @($code | Where-Object { $_.OpCode.Name -eq 'ldfld' -and [string]$_.Operand -eq 'System.Single Character::m_ashlandsOceanHeatLevel' })
Assert ($lava.Count -eq 2 -and $ocean.Count -eq 1) 'Two lava stages and one ocean stage match rewrite'
Assert ($lava[0].Previous.OpCode.Name -eq 'ldarg.0' -and $lava[1].Previous.OpCode.Name -eq 'ldarg.0' -and $ocean[0].Previous.OpCode.Name -eq 'ldarg.0') 'Heat reads use the current Character'
Assert ($lava[0].Offset -lt $lava[1].Offset -and $lava[1].Offset -lt $ocean[0].Offset) 'Both lava branches precede preserved ocean branch'
$hits = @($code | Where-Object { $_.OpCode.Name -eq 'stfld' -and $_.Operand.Name -eq 'm_hitType' })
Assert ($hits.Count -eq 3) 'Exactly three native heat hit packets'
Assert ($hits[0].Previous.Operand -eq 22 -and $hits[1].Previous.Operand -eq 22 -and $hits[2].Previous.Operand -eq 21) 'Native lava and ocean hit types match inspected contract'
$damage = @($code | Where-Object { [string]$_.Operand -eq 'System.Void Character::Damage(HitData)' })
Assert ($damage.Count -eq 3 -and $damage[1].Offset -lt $ocean[0].Offset -and $damage[2].Offset -gt $ocean[0].Offset) 'Only the two lava damage calls are bypassed'
$burn = @($code | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'AddStatusEffect' })
Assert ($burn.Count -eq 2 -and $burn[0].Offset -gt $lava[0].Offset -and $burn[1].Offset -lt $ocean[0].Offset) 'Lava-applied burning is contained in skipped branches'
$feature = $plugin.MainModule.Types | Where-Object Name -eq 'SummonLavaProtection'
$patches = @($feature.NestedTypes | Where-Object { @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' }).Count -gt 0 })
Assert ($patches.Count -eq 1) 'Exactly one lava hook compiled'
$target = $patches[0].CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' }
Assert ($target.ConstructorArguments[0].Value.FullName -eq 'Character' -and $target.ConstructorArguments[1].Value -eq 'UpdateHeatDamage') 'Only native heat damage is patched'
$guard = ($feature.Methods | Where-Object Name -eq 'Applies').Body.Instructions -join [Environment]::NewLine
Assert ($guard.Contains('Skeleton_Friendly') -and $guard.Contains('Skeleton_Friendly(Clone)')) 'Exact summoned-skeleton names compiled'
Assert ($guard.Contains('IsOwner') -and $guard.Contains('IsTamed') -and $guard.Contains('IsDead') -and $guard.Contains('GetFaction') -and $guard.Contains('::Active')) 'Owner, tame, life, faction and activation guards compiled'
$entry = $plugin.MainModule.Types | Where-Object Name -eq 'Plugin'
$awake = ($entry.Methods | Where-Object Name -eq 'Awake').Body.Instructions -join [Environment]::NewLine
Assert ($awake.Contains('LavaDamageImmunity') -and $awake.Contains('SummonLavaProtection::Patch') -and $awake.Contains('SummonLavaProtection::Active') -and $awake.Contains('UnpatchSelf')) 'Config, feature activation and failed-install cleanup compiled'
$destroy = ($entry.Methods | Where-Object Name -eq 'OnDestroy').Body.Instructions -join [Environment]::NewLine
Assert ($destroy.Contains('SummonLavaProtection::Active') -and $destroy.Contains('UnpatchSelf')) 'Unload disables lava feature and removes patches'
"PASS: $script:count static lava/game/compiled-hook assertions. Not a Unity playtest."
