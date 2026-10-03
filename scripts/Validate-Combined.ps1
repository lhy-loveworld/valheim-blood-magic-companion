param([Parameter(Mandatory=$true)][string]$BepInExCore,
      [Parameter(Mandatory=$true)][string]$PluginDll)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $BepInExCore 'Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$types = $assembly.MainModule.Types
function Assert($condition, $label) { if (!$condition) { throw "FAIL: $label" }; "PASS: $label" }
$entries = @($types | Where-Object { @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' }).Count -gt 0 })
Assert ($entries.Count -eq 1 -and $entries[0].FullName -eq 'BloodMagicCompanion.Plugin') 'Exactly one BepInEx plugin entry point'
$entry = $entries[0]
$incompatible = @($entry.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInIncompatibility' } | ForEach-Object { $_.ConstructorArguments[0].Value })
Assert ($incompatible.Count -eq 2 -and $incompatible -contains 'local.valheim.bloodmagiccasterxp' -and $incompatible -contains 'local.valheim.bloodmagichealthcost') 'Old standalone plugins declared incompatible locally'
$awake = $entry.Methods | Where-Object Name -eq 'Awake'
$body = $awake.Body.Instructions -join "`n"
Assert ($body.Contains('StaminaCosts::Patch') -and $body.Contains('CasterXP::Patch') -and !$body.Contains('PatchAll')) 'Feature-specific patch installation'
Assert ($body.Contains('StaminaCosts::Active') -and $body.Contains('CasterXP::SetEnabled') -and $body.Contains('UnpatchSelf')) 'Activation and failed-install cleanup present'
$xp = $types | Where-Object Name -eq 'CasterXP'
$rpc = $xp.Fields | Where-Object Name -eq 'RpcName'
$id = $xp.Fields | Where-Object Name -eq 'LegacyId'
Assert ($rpc.Constant -eq 'BloodMagicCasterXP_Break_v3' -and $id.Constant -eq 'local.valheim.bloodmagiccasterxp') 'Preserved standalone 1.2.0 XP wire identifiers'
$route = $types | Where-Object Name -eq 'RewardRouting'
Assert (($route.Fields | Where-Object Name -eq 'Protocol').Constant -eq 3) 'Preserved XP protocol 3'
$receive = $xp.Methods | Where-Object Name -eq 'ReceiveBreak'
Assert ($receive.Body.Instructions[0].Operand.Name -eq 'ready') 'Disabled XP rejects incoming messages'
$advertise = $xp.Methods | Where-Object Name -eq 'Advertise'
$text = $advertise.Body.Instructions -join "`n"
Assert ($text.Contains('CasterXP::ready') -and @($advertise.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ldc.i4.0'}).Count -ge 1) 'Disabled XP can clear advertised capability'
$nested = @($xp.NestedTypes | Where-Object { @($_.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'}).Count -gt 0 })
Assert ($nested.Count -eq 4) 'Four XP patch classes retained'
'9 combined-plugin static assertions passed. Runtime loading remains untested.'
