param(
    [Parameter(Mandatory=$true)][string]$GameManaged,
    [Parameter(Mandatory=$true)][string]$BepInExCore,
    [Parameter(Mandatory=$true)][string]$PluginDll
)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $BepInExCore 'Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameManaged 'assembly_valheim.dll'))
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($PluginDll)
$script:checks = 0
function Assert($condition, [string]$message) {
    if (!$condition) { throw "FAIL: $message" }
    $script:checks++
}
function Method($type, [string]$name, [int]$arity) {
    $found = @($type.Methods | Where-Object { $_.Name -eq $name -and $_.Parameters.Count -eq $arity })
    Assert ($found.Count -eq 1) "$($type.FullName)::$name/$arity exists uniquely"
    return $found[0]
}
$attack = $game.MainModule.Types | Where-Object Name -eq 'Attack'
$character = $game.MainModule.Types | Where-Object Name -eq 'Character'
$item = ($game.MainModule.Types | Where-Object Name -eq 'ItemDrop').NestedTypes | Where-Object Name -eq 'ItemData'
$patchCount = 0
foreach ($type in $plugin.MainModule.Types) {
    foreach ($attr in $type.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' }) {
        $patchCount++
        $targetName = $attr.ConstructorArguments[0].Value.FullName
        $methodName = $attr.ConstructorArguments[1].Value
        $targetType = if ($targetName -eq 'Attack') { $attack } elseif ($targetName -eq 'ItemDrop/ItemData') { $item } else { throw "Unexpected patch target $targetName" }
        $candidates = @($targetType.Methods | Where-Object Name -eq $methodName)
        if ($attr.ConstructorArguments.Count -eq 3) {
            $signature = (@($attr.ConstructorArguments[2].Value | ForEach-Object { $_.Value.FullName }) -join ',')
            $candidates = @($candidates | Where-Object { (@($_.Parameters | ForEach-Object {$_.ParameterType.FullName}) -join ',') -eq $signature })
        }
        Assert ($candidates.Count -eq 1) "Harmony target $($type.Name) resolves uniquely"
        $target = $candidates[0]
        foreach ($hook in $type.Methods | Where-Object { $_.Name -in @('Prefix','Postfix') }) {
            foreach ($parameter in $hook.Parameters) {
                $name = $parameter.Name
                if ($name.StartsWith('___')) {
                    $field = @($targetType.Fields | Where-Object Name -eq $name.Substring(3))
                    Assert ($field.Count -eq 1 -and $field[0].FieldType.FullName -eq $parameter.ParameterType.FullName) "Injected field $name matches"
                } elseif ($name -eq '__result') {
                    Assert ($parameter.ParameterType.FullName -eq ($target.ReturnType.FullName + '&')) 'Result injection matches'
                } elseif ($name -eq '__instance') {
                    Assert (!$target.IsStatic -and $parameter.ParameterType.FullName -eq $targetType.FullName) 'Instance injection matches'
                } else {
                    $arg = if ($name -match '^__(\d+)$') { $target.Parameters[[int]$Matches[1]] } else { $target.Parameters | Where-Object Name -eq $name }
                    Assert ($null -ne $arg -and $arg.ParameterType.FullName -eq $parameter.ParameterType.FullName) "Argument $name matches"
                }
            }
        }
    }
}
Assert ($patchCount -eq 4) 'Only four cost and tooltip hooks present'
$update = Method $attack 'Update' 1
$burst = Method $attack 'FireProjectileBurst' 0
$start = Method $attack 'Start' 9
$null = Method $attack 'Stop' 0
$null = Method $attack 'Abort' 0
$null = Method $attack 'OnAttackTrigger' 0
$payments = @($attack.Methods | Where-Object { $_.HasBody -and @($_.Body.Instructions | Where-Object { [string]$_.Operand -eq 'System.Void Character::UseHealth(System.Single)' }).Count -gt 0 })
Assert (($payments.Name | Sort-Object) -join ',' -eq 'FireProjectileBurst,Update') 'Both native Attack health payment paths identified'
foreach ($m in @($update,$burst)) {
    $il = $m.Body.Instructions
    Assert (@($il | Where-Object { [string]$_.Operand -eq 'System.Void Character::UseHealth(System.Single)' }).Count -eq 1) "$($m.Name) pays health once"
    Assert (@($il | Where-Object { [string]$_.Operand -eq 'System.Single Attack::GetAttackHealth()' }).Count -eq 1) "$($m.Name) uses patched health getter"
    Assert (@($il | Where-Object { [string]$_.Operand -eq 'System.Single Attack::GetAttackEitr()' }).Count -eq 1) "$($m.Name) uses patched eitr getter"
    Assert (@($il | Where-Object { [string]$_.Operand -eq 'System.Single UnityEngine.Mathf::Min(System.Single,System.Single)' }).Count -ge 1) "$($m.Name) native clamp checked"
}
$updateText = $update.Body.Instructions -join "`n"
Assert ($updateText.Contains('Attack::m_wasInAttack') -and $updateText.Contains('Attack::m_perBurstResourceUsage') -and $updateText.Contains('Attack::m_attackDone')) 'Update payment gates present'
Assert (($start.Body.Instructions -join "`n").Contains('Character::TryUseEitr(System.Single)')) 'Start checks patched eitr cost'
$tryEitr = Method $character 'TryUseEitr' 1
$il = $tryEitr.Body.Instructions
Assert ($il[0].OpCode.Name -eq 'ldarg.1' -and $il[1].OpCode.Name -eq 'ldc.r4' -and $il[1].Operand -eq 0 -and $il[2].OpCode.Name -eq 'bne.un.s' -and $il[3].OpCode.Name -eq 'ldc.i4.1' -and $il[4].OpCode.Name -eq 'ret') 'Zero eitr accepted before max-eitr requirement'
$health = Method $attack 'GetAttackHealth' 0
$eitr = Method $attack 'GetAttackEitr' 2
foreach ($m in @($health,$eitr)) {
    Assert (@($m.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldc.r4' -and [Math]::Abs([double]$_.Operand - 0.33) -lt 0.000001 }).Count -eq 1) "$($m.Name) vanilla 33 percent skill coefficient"
}
$pluginType = $plugin.MainModule.Types | Where-Object Name -eq 'StaminaCosts'
$applies = Method $pluginType 'Applies' 3
$appliesIL = $applies.Body.Instructions -join "`n"
Assert ($appliesIL.Contains('Player::m_localPlayer') -and $appliesIL.Contains('Character::IsOwner()')) 'Local owner scope present'
Assert ($appliesIL.Contains('Attack::m_attackKillsSelf')) 'Self-killing attacks excluded'
$allPluginIL = ($plugin.MainModule.Types.Methods | Where-Object HasBody | ForEach-Object {$_.Body.Instructions}) -join "`n"
Assert (!$allPluginIL.Contains('ServerSync')) 'No mandatory server synchronization'
Assert ($plugin.MainModule.AssemblyReferences.Name -notcontains 'Mono.Cecil') 'Validation dependency not shipped in plugin'
$cost = Method $pluginType 'Surcharge' 3
Assert (!(($cost.Body.Instructions -join "`n").Contains('GetMaxHealth'))) 'No maximum-health dependency in payment'
Assert (@($plugin.MainModule.Types | Where-Object { $_.Name -match 'StartPatch|GuardPatch' }).Count -eq 0) 'No custom cast-start or payment patches'
Assert (@($pluginType.Methods | Where-Object { $_.Name -in @('Affordable','Cancel','Notice') }).Count -eq 0) 'Old affordability and cancellation logic removed'
$staminaPatch = $plugin.MainModule.Types | Where-Object Name -eq 'StaminaCostPatch'
Assert ((($staminaPatch.Methods | Where-Object Name -eq 'Postfix').Body.Instructions -join [Environment]::NewLine).Contains('StaminaCosts::Surcharge')) 'Stamina getter adds converted surcharge'
Assert (@($plugin.MainModule.Types | Where-Object Name -eq 'HealthCostPatch').Count -eq 0) 'Original health getter is not patched'
$stamina = Method $attack 'GetAttackStamina' 0
foreach ($m in @($start,$update,$burst)) {
    $text = $m.Body.Instructions -join [Environment]::NewLine
    Assert ($text.Contains('Attack::GetAttackStamina()')) "$($m.Name) uses converted stamina getter"
}
Assert (($start.Body.Instructions -join [Environment]::NewLine).Contains('Character::HaveStamina(System.Single)')) 'Native start stamina affordability check retained'
Assert (($burst.Body.Instructions -join [Environment]::NewLine).Contains('Character::HaveStamina(System.Single)')) 'Native per-burst stamina affordability check retained'
foreach ($m in @($update,$burst)) {
    Assert (@($m.Body.Instructions | Where-Object { [string]$_.Operand -eq 'System.Void Character::UseStamina(System.Single)' }).Count -eq 1) "$($m.Name) deducts stamina once"
}
$total = Method ($plugin.MainModule.Types | Where-Object Name -eq 'CostModel') 'Total' 2
$allCostIL = (($plugin.MainModule.Types | Where-Object Name -in @('StaminaCosts','StaminaCostPatch','CostModel')).Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions }) -join [Environment]::NewLine
Assert (!$allCostIL.Contains('Character::UseStamina') -and !$allCostIL.Contains('Character::UseHealth')) 'No manual or duplicate resource deduction'
Assert (@(($plugin.MainModule.Types | Where-Object Name -eq 'StaminaCostPatch').Methods | Where-Object Name -eq 'Prefix').Count -eq 0) 'Vanilla stamina calculation executes before additive postfix'
"PASS: $script:checks static game and compiled-patch contract assertions."
"Game assembly SHA256: $((Get-FileHash (Join-Path $GameManaged 'assembly_valheim.dll') -Algorithm SHA256).Hash)"
"Plugin SHA256: $((Get-FileHash $PluginDll -Algorithm SHA256).Hash)"
'Not a Unity/Harmony runtime or multiplayer test.'
