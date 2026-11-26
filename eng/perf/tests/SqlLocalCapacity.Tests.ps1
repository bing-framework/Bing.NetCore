#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/r02-rc/tests')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$run = Join-Path ([IO.Path]::GetFullPath($ResultsDirectory)) ('local-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($run) | Out-Null
$cases = [Collections.Generic.List[object]]::new()
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $cases.Add(@{name=$Name; status='PASS'}) }
    catch { $cases.Add(@{name=$Name; status='FAIL'; error=$_.Exception.Message}) }
}
function Assert([bool]$Value, [string]$Message) { if (!$Value) { throw $Message } }
function Assert-Rejected([scriptblock]$Body, [string]$ExpectedError) {
    $rejected=$false
    try { & $Body | Out-Null } catch { $rejected=$_.Exception.Message -like $ExpectedError }
    Assert $rejected 'Expected invalid configuration to be rejected.'
}
$runner = Join-Path $PSScriptRoot '../Invoke-SqlLocalCapacity.ps1'
if (Test-Path -LiteralPath $runner) { . $runner }
$budget = Get-Content -LiteralPath "$PSScriptRoot/../r02-local-budget.json" -Raw -Encoding utf8 | ConvertFrom-Json
Test-Case 'ApprovedBudgetPreservesAllFiveCapacityCells' {
    $cells = @(Get-SqlLocalCapacityCells $budget @(1,8,16,32,64))
    Assert (($cells.concurrency -join ',') -ceq '1,8,16,32,64') 'Concurrency matrix changed.'
    Assert (($cells.minRequestsPerSecond -join ',') -ceq '2500,8000,10000,10000,10000') 'Throughput budgets changed.'
    Assert (($cells.maxP99Milliseconds -join ',') -ceq '5,20,50,60,80') 'Latency budgets changed.'
}
Test-Case 'UnapprovedBudgetIsRejectedBeforeProvisioning' {
    $copy=$budget | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $copy.approvalStatus='PENDING'
    Assert-Rejected { Get-SqlLocalCapacityCells $copy @(1) } '*approved 1C2G*'
}
Test-Case 'WrongTopologyIsRejectedBeforeProvisioning' {
    $copy=$budget | ConvertTo-Json -Depth 8 | ConvertFrom-Json
    $copy.profile='2C4G'
    Assert-Rejected { Get-SqlLocalCapacityCells $copy @(1) } '*approved 1C2G*'
}
Test-Case 'MissingConcurrencyBudgetIsRejected' {
    Assert-Rejected { Get-SqlLocalCapacityCells $budget @(2) } '*Missing approved capacity budget*'
}
Test-Case 'FormalCapacityCommandUsesFixedProbeWithoutDiagnosticCollector' {
    $actual=@(Get-SqlLocalCapacityCommand 8)
    $expected=@('timeout','240s','dotnet','/app/Bing.Data.Sql.Benchmarks.dll',
        '--mixed-capacity-probe','/results/workload.json','1C2G','8','/results/c8.json')
    Assert (($actual -join "`n") -ceq ($expected -join "`n")) 'Formal probe command differs.'
}
Test-Case 'SelectedCellRetainsItsApprovedBudget' {
    $cells=@(Get-SqlLocalCapacityCells $budget @(32))
    Assert ($cells.Count -eq 1 -and $cells[0].concurrency -eq 32 -and
        $cells[0].minRequestsPerSecond -eq 10000 -and $cells[0].maxP99Milliseconds -eq 60) 'Selected budget differs.'
}
Test-Case 'InvalidRuntimeCandidateStopsBeforeDockerProvisioning' {
    $fixture=Join-Path $run 'candidate'
    [IO.Directory]::CreateDirectory($fixture) | Out-Null
    Set-Content -LiteralPath "$fixture/Bing.Data.Sql.Benchmarks.dll" -Value 'entry' -Encoding utf8
    Set-Content -LiteralPath "$fixture/source.json" -Value '{"count":1,"inputs":{}}' -Encoding utf8
    Set-Content -LiteralPath "$fixture/runtime.json" -Value '{"count":0,"files":{}}' -Encoding utf8
    $receipt=@{status='PASS'; evidenceKind='CleanBuildProcessTree'; peakMetric='WindowsJobCommittedBytes';
        source=@{clean=$true; inputCount=1; manifest="$fixture/source.json"};
        binary=@{file="$fixture/Bing.Data.Sql.Benchmarks.dll"; sha256=(Get-FileHash "$fixture/Bing.Data.Sql.Benchmarks.dll").Hash};
        runtime=@{root=$fixture; manifest="$fixture/runtime.json"; count=0}}
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$fixture/build.json" -Encoding utf8
    # 真实调用预算门；候选无效必须在访问 Docker 之前结束，不依赖本机 Docker 权限。
    & (Get-Command pwsh).Source -NoProfile -File $runner -BuildReceiptPath "$fixture/build.json" `
        -ResultsDirectory "$run/rejected" *> "$run/rejected.log"
    $exit=$LASTEXITCODE
    $actual=Get-Content -LiteralPath "$run/rejected/environment.json" -Raw -Encoding utf8 | ConvertFrom-Json
    Assert ($exit -eq 2 -and $actual.status -ceq 'INVALID' -and
        $actual.error -like 'Build budget or full runtime identity check failed: before.*' -and
        !$actual.host -and !$actual.applicationId -and $actual.cleanup.Count -eq 0) 'Invalid candidate reached provisioning.'
}
$result=@{status=if (@($cases | Where-Object status -EQ FAIL).Count) {'FAIL'} else {'PASS'}; cases=@($cases)}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath "$run/result.json" -Encoding utf8
$result | ConvertTo-Json -Depth 6
if ($result.status -ne 'PASS') { exit 1 }
