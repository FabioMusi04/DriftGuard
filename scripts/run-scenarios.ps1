$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$cli = @('run', '--configuration', 'Release', '--no-build', '--project', 'src/DriftGuard.Cli', '--')
$policy = 'policies/production.json'

function Invoke-Scenario([int]$Expected, [string]$Label, [string[]]$CommandArgs) {
    Write-Host "`n===== $Label (expected exit $Expected) ====="
    & dotnet @CommandArgs
    $actual = $LASTEXITCODE
    if ($actual -ne $Expected) { throw "$Label returned $actual, expected $Expected" }
    Write-Host "PASS: exit code matched ($actual)"
}

Invoke-Scenario 0 '01 no semantic drift' (@($cli) + @('compare','scenarios/01-no-op/baseline.json','scenarios/01-no-op/current.json'))
Invoke-Scenario 1 '02 debug enabled' (@($cli) + @('check','scenarios/02-debug-enabled/baseline.json','scenarios/02-debug-enabled/current.json','--policy',$policy))
Invoke-Scenario 1 '03 database TLS disabled' (@($cli) + @('check','scenarios/03-tls-disabled/baseline.json','scenarios/03-tls-disabled/current.json','--policy',$policy))
Invoke-Scenario 1 '04 secret change blocks at warning threshold' (@($cli) + @('check','scenarios/04-secret-rotation/baseline.json','scenarios/04-secret-rotation/current.json','--policy',$policy,'--fail-on','warning'))
Invoke-Scenario 1 '05 critical feature removed' (@($cli) + @('check','scenarios/05-feature-removed/baseline.json','scenarios/05-feature-removed/current.json','--policy',$policy))
Invoke-Scenario 1 '06 type drift blocks at warning threshold' (@($cli) + @('check','scenarios/06-type-drift/baseline.json','scenarios/06-type-drift/current.json','--policy',$policy,'--fail-on','warning'))
Invoke-Scenario 1 '07 YAML debug enabled' (@($cli) + @('check','scenarios/07-yaml-debug/baseline.yaml','scenarios/07-yaml-debug/current.yaml','--policy',$policy))
Invoke-Scenario 0 '08 safe additive change' (@($cli) + @('check','scenarios/08-safe-addition/baseline.json','scenarios/08-safe-addition/current.json','--policy',$policy))
Invoke-Scenario 1 '09 single replica blocks at warning threshold' (@($cli) + @('check','scenarios/09-single-replica/baseline.json','scenarios/09-single-replica/current.json','--policy',$policy,'--fail-on','warning'))
Invoke-Scenario 2 '10 malformed JSON' (@($cli) + @('compare','scenarios/10-invalid-json/baseline.json','scenarios/10-invalid-json/current.json'))
Invoke-Scenario 1 '11 public ingress enabled' (@($cli) + @('check','scenarios/11-public-ingress/baseline.json','scenarios/11-public-ingress/current.json','--policy',$policy))
Invoke-Scenario 0 '12 array change is report-only' (@($cli) + @('compare','scenarios/12-array-change/baseline.json','scenarios/12-array-change/current.json'))
Write-Host "`nAll scenario exit-code assertions passed."
