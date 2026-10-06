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
$ai = $game.MainModule.Types | Where-Object Name -eq 'BaseAI'
$character = $game.MainModule.Types | Where-Object Name -eq 'Character'
$sight = @($ai.Methods | Where-Object { $_.Name -eq 'CanSeeTarget' -and $_.Parameters.Count -eq 7 })
Assert ($sight.Count -eq 1 -and $sight[0].IsStatic) 'Visibility target exists and is static'
Assert (($sight[0].Parameters.ParameterType.FullName -join ',') -eq 'UnityEngine.Transform,UnityEngine.Vector3,System.Single,System.Single,System.Boolean,System.Boolean,Character') 'Observer and target argument indexes match transpiler'
$rays = @($sight[0].Body.Instructions | Where-Object { [string]$_.Operand -eq 'System.Boolean UnityEngine.Physics::Raycast(UnityEngine.Vector3,UnityEngine.Vector3,System.Single,System.Int32)' })
Assert ($rays.Count -eq 1 -and $rays[0].OpCode.Name -eq 'call') 'Exactly one matching native obstruction ray'
Assert ($sight[0].Body.ExceptionHandlers.Count -eq 0) 'No exception boundaries cross rewrite'
$il = $sight[0].Body.Instructions -join [Environment]::NewLine
Assert ($il.Contains('Vector3::Distance') -and $il.Contains('Vector3::Angle') -and $il.Contains('Character::GetStealthFactor') -and $il.Contains('ParticleMist::IsMistBlocked')) 'Native range, view cone, stealth and mist checks remain available'
$find = @($ai.Methods | Where-Object { $_.Name -eq 'FindEnemy' -and $_.Parameters.Count -eq 0 })
Assert ($find.Count -eq 1 -and $find[0].ReturnType.FullName -eq 'Character') 'Fallback patch target matches'
Assert (@($ai.Fields | Where-Object { $_.Name -eq 'm_character' -and $_.FieldType.FullName -eq 'Character' }).Count -eq 1) 'Injected observer field matches'
$faction = $character.NestedTypes | Where-Object Name -eq 'Faction'
Assert (@($faction.Fields | Where-Object Name -in @('PlayerSpawned','TrainingDummy')).Count -eq 2) 'Narrow faction scope available'
$feature = $plugin.MainModule.Types | Where-Object Name -eq 'SummonTraining'
$patches = @($feature.NestedTypes | Where-Object { @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' }).Count -gt 0 })
Assert ($patches.Count -eq 2) 'Exactly two summon hooks'
$targets = @($patches | ForEach-Object { $_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' } })
Assert (@($targets | Where-Object { $_.ConstructorArguments[0].Value.FullName -ne 'BaseAI' }).Count -eq 0) 'No global Character or Physics patch'
Assert (($targets | ForEach-Object { $_.ConstructorArguments[1].Value } | Sort-Object) -join ',' -eq 'CanSeeTarget,FindEnemy') 'No damage, attack or ownership mutations patched'
$eligible = ($feature.Methods | Where-Object Name -eq 'Eligible').Body.Instructions -join [Environment]::NewLine
Assert ($eligible.Contains('IsOwner') -and $eligible.Contains('IsTamed') -and $eligible.Contains('IsDead') -and $eligible.Contains('GetFaction')) 'Owner, tame, life and faction guards compiled'
$blocked = ($feature.Methods | Where-Object Name -eq 'SightBlocked').Body.Instructions -join [Environment]::NewLine
Assert ($blocked.Contains('Physics::Raycast(') -and $blocked.Contains('Physics::RaycastAll(') -and $blocked.Contains('GetComponentInParent<Character>')) 'Native fallback and per-character obstruction filtering compiled'
$select = ($feature.Methods | Where-Object Name -eq 'SelectTarget').Body.Instructions -join [Environment]::NewLine
Assert ($select.Contains('GetTargetCreature') -and $select.Contains('CanSeeTarget') -and $select.Contains('Vector3::Distance')) 'Current target priority and native visibility used for selection'
$entry = $plugin.MainModule.Types | Where-Object Name -eq 'Plugin'
$awake = ($entry.Methods | Where-Object Name -eq 'Awake').Body.Instructions -join [Environment]::NewLine
Assert ($awake.Contains('SummonTraining::Patch') -and $awake.Contains('SummonTraining::Active') -and $awake.Contains('UnpatchSelf')) 'Feature participates in activation and failed-install cleanup'
$destroy = ($entry.Methods | Where-Object Name -eq 'OnDestroy').Body.Instructions -join [Environment]::NewLine
Assert ($destroy.Contains('SummonTraining::Active') -and $destroy.Contains('UnpatchSelf')) 'Unload cleanup present'
"PASS: $script:count static summon/game/compiled-hook assertions. Not a Unity playtest."
