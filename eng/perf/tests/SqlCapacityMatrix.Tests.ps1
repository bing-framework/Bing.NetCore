#requires -Version 7.0
param([string]$ResultsDirectory = 'artifacts/validation/r02/matrix')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$generator = Join-Path $PSScriptRoot '../New-SqlCapacityMatrix.ps1'
$root = [IO.Path]::GetFullPath($ResultsDirectory)
[IO.Directory]::CreateDirectory($root) | Out-Null
$cases = [Collections.Generic.List[object]]::new()
function Check([string]$Name, [scriptblock]$Test) {
    try { & $Test; $cases.Add(@{ name = $Name; status = 'PASS' }) }
    catch { $cases.Add(@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message }) }
}
function Assert($Condition) { if (!$Condition) { throw 'Assertion failed' } }
$file = Join-Path $root ('matrix-' + [Guid]::NewGuid().ToString('N') + '.json')
Check 'ThreeHardwareProfilesAndThirtyCells' {
    & $generator -Rows 10000 -Requests 8192 -MaxPoolSize 32 -OutputPath $file
    $script:matrix = Get-Content $file -Raw -Encoding utf8 | ConvertFrom-Json
    Assert (($matrix.profiles.name -join ',') -ceq '2C4G,4C8G,8C16G')
    Assert (($matrix.profiles.cpu -join ',') -eq '2,4,8')
    Assert (($matrix.profiles.memoryGiB -join ',') -eq '4,8,16')
    Assert ($matrix.cells.Count -eq 30)
}
Check 'SameWorkloadAcrossHardware' {
    Assert ($null -ne $matrix -and $matrix.profiles.Count -eq 3)
    foreach ($profile in $matrix.profiles) {
        $cells = @($matrix.cells | Where-Object profile -eq $profile.name)
        Assert ($cells.Count -eq 10)
        foreach ($cell in $cells) {
            Assert ($cell.expectedWorkload.rows -eq 10000 -and $cell.expectedWorkload.requests -eq 8192)
            Assert ($cell.expectedWorkload.maxPoolSize -eq 32 -and $cell.expectedWorkload.pooling)
            Assert (!$cell.expectedWorkload.observedServer)
            Assert (@($cell.expectedWorkload.PSObject.Properties).Count -eq 7)
            Assert ($cell.expectedWorkload.concurrency -in @(1,8,16,32,64))
            Assert ($cell.arguments[0] -ceq '--capacity-probe')
        }
    }
}
Check 'PlanningIsNotCapacityApproval' {
    Assert ($matrix.status -ceq 'PLANNED')
    Assert (!$matrix.productionCapacityVerified -and $null -eq $matrix.thresholds)
    Assert ($matrix.environmentVerification -ceq 'REQUIRED')
}
Check 'RejectRequestsBelowConcurrency' {
    $failed = $false
    try { & $generator -Rows 10 -Requests 2 -MaxPoolSize 32 -OutputPath (Join-Path $root 'invalid.json') }
    catch { $failed = $_.Exception.Message -eq 'Requests must be at least 64 for the complete concurrency matrix.' }
    Assert $failed
    Assert (!(Test-Path (Join-Path $root 'invalid.json')))
}
Check 'RejectOverwrite' {
    $before = (Get-FileHash $file).Hash
    $failed = $false
    try { & $generator -Rows 10 -Requests 100 -MaxPoolSize 32 -OutputPath $file }
    catch { $failed = $_.Exception.InnerException -is [IO.IOException] }
    Assert $failed
    Assert ((Get-FileHash $file).Hash -eq $before)
}
$report = @{ passed = @($cases | Where-Object status -eq PASS).Count; failed = @($cases | Where-Object status -eq FAIL).Count; cases = $cases }
$report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'tests.json') -Encoding utf8
$report | ConvertTo-Json -Depth 8
if ($report.failed) { exit 1 }
exit 0
