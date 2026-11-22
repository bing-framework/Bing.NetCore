[CmdletBinding()]
param([string]$ResultsDirectory = "output/sql-regression-contract-tests")

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$runner = Join-Path $PSScriptRoot "../Invoke-SqlRegressionTests.ps1"
$results = Join-Path ([IO.Path]::GetFullPath($ResultsDirectory, $repositoryRoot)) ("contract-" + [Guid]::NewGuid().ToString("N"))
$invocations = [System.Collections.Generic.List[string]]::new()
$workflowValues = [ordered]@{
    GITHUB_REPOSITORY = "contract/sql-regression"
    GITHUB_RUN_ID = "run-123"
    GITHUB_SHA = "abcdef0123456789"
    GITHUB_RUN_ATTEMPT = "2"
}

# 模拟 dotnet test 的结束状态和 TRX。真实程序集由同一回归入口另行执行。
function dotnet {
    $project = [IO.Path]::GetFileNameWithoutExtension($args[1])
    $framework = $args[[Array]::IndexOf($args, "--framework") + 1]
    $directory = $args[[Array]::IndexOf($args, "--results-directory") + 1]
    $name = $args[[Array]::IndexOf($args, "--logger") + 1].Split("=")[-1]
    $caseName = "$project/$framework"
    $invocations.Add($caseName)

    $global:LASTEXITCODE = 0
    $executed = 1
    if ($failureMode) {
        switch ($caseName) {
            "Bing.Data.Sql.Tests/net6.0" { $global:LASTEXITCODE = 1 }
            "Bing.Data.Sql.Generators.Tests/net8.0" { $executed = 0 }
            "Bing.Dapper.Core.Tests/net8.0" { return }
        }
    }
    $trx = if ($caseName -eq $overrideCase) { $overrideReport } else {
        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary outcome="Completed"><Counters executed="{0}" failed="0" /></ResultSummary></TestRun>' -f $executed
    }
    Set-Content -LiteralPath (Join-Path $directory $name) -Value $trx -Encoding utf8
}

$overrideCase = $null
$failureMode = $false
$originalWorkflowValues = [ordered]@{}
foreach ($name in $workflowValues.Keys) {
    $originalWorkflowValues[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
    [Environment]::SetEnvironmentVariable($name, $workflowValues[$name], "Process")
}
try {
    & $runner -ResultsDirectory (Join-Path $results "pass")
}
finally {
    foreach ($name in $originalWorkflowValues.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalWorkflowValues[$name], "Process")
    }
}
$passTrx = @(Get-ChildItem -LiteralPath (Join-Path $results "pass") -Recurse -File -Filter "*.trx")
if ($passTrx.Count -ne 22 -or @($invocations | Sort-Object -Unique).Count -ne 22 -or
    "Bing.Dapper.Sqlite.Tests.Integration/net6.0" -notin $invocations -or
    "Bing.Dapper.Sqlite.Tests.Integration/net8.0" -notin $invocations -or
    "Bing.Data.Sql.Generators.Tests/net8.0" -notin $invocations -or
    "Bing.Data.Sql.Generators.Tests/net6.0" -in $invocations) {
    throw "应输出 22 份不同测试格的 TRX，SQLite 覆盖双框架，生成器覆盖 .NET 8。"
}
Write-Output "PASS SQL 回归执行 22 格并保留 TRX"

$passRunDirectory = $passTrx[0].Directory.FullName
$receiptPath = Join-Path $passRunDirectory "reports.json"
if (!(Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
    throw "成功运行应在同一 run 目录写出 reports.json。"
}
$receipt = Get-Content -LiteralPath $receiptPath -Encoding utf8 -Raw | ConvertFrom-Json
$expectedNames = @($passTrx | ForEach-Object Name | Sort-Object)
$reportedNames = @($receipt.reports.PSObject.Properties.Name | Sort-Object)
$workflowNames = @($receipt.workflow.PSObject.Properties.Name | Sort-Object)
if (($expectedNames -join "`n") -ne ($reportedNames -join "`n") -or
    $reportedNames.Count -ne 22 -or
    ($workflowNames -join ",") -ne "attempt,repository,runId,sha" -or
    $receipt.workflow.repository -ne $workflowValues.GITHUB_REPOSITORY -or
    $receipt.workflow.runId -ne $workflowValues.GITHUB_RUN_ID -or
    $receipt.workflow.sha -ne $workflowValues.GITHUB_SHA -or
    $receipt.workflow.attempt -ne $workflowValues.GITHUB_RUN_ATTEMPT) {
    throw "reports.json 应包含四个工作流字段，以及恰好 22 份已验证 TRX。"
}
foreach ($trx in $passTrx) {
    $reportedHash = [string]$receipt.reports.($trx.Name)
    $actualHash = (Get-FileHash -LiteralPath $trx.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($reportedHash -cne $actualHash -or $reportedHash -cnotmatch '^[0-9a-f]{64}$') {
        throw "reports.json 中 $($trx.Name) 的 SHA256 与实际文件不符。"
    }
}
Write-Output "PASS 成功收据匹配 22 份 TRX 摘要与 SHA256"

$failureMode = $true
$invocations.Clear()
$failure = $null
try {
    & $runner -ResultsDirectory (Join-Path $results "fail")
}
catch {
    $failure = $_.Exception.Message
}
foreach ($failedCase in @("Bing.Data.Sql.Tests/net6.0", "Bing.Data.Sql.Generators.Tests/net8.0", "Bing.Dapper.Core.Tests/net8.0")) {
    if (!$failure -or !$failure.Contains($failedCase)) {
        throw "应报告失败、零执行或缺失 TRX 的格：$failedCase"
    }
}
$failedTrx = @(Get-ChildItem -LiteralPath (Join-Path $results "fail") -Recurse -File -Filter "*.trx")
if ($invocations.Count -ne 22 -or $failedTrx.Count -ne 21 -or
    @(Get-ChildItem -LiteralPath (Join-Path $results "fail") -Recurse -File -Filter "reports.json").Count -ne 0) {
    throw "某一格失败后应继续执行剩余测试并保留已产生的 TRX。"
}
Write-Output "PASS SQL 回归报告进程失败、零执行及缺失 TRX，继续收集且不发布成功清单"

# 将异常报告放在第一格，验证失败归属和后续 21 格的报告收集。
$failureMode = $false
$overrideCase = "Bing.Data.Sql.Tests/net6.0"
$reports = @(
    @{ name = 'FailedCounter'; content = '<TestRun><ResultSummary outcome="Completed"><Counters executed="2" failed="1" /></ResultSummary></TestRun>'; diagnostic = 'failed=1' }
    @{ name = 'FailedRun'; content = '<TestRun><ResultSummary outcome="Failed"><Counters executed="1" failed="0" /></ResultSummary></TestRun>'; diagnostic = 'outcome=Failed' }
    @{ name = 'TruncatedTrx'; content = '<TestRun><ResultSummary'; diagnostic = 'invalid TRX' }
)
$contractErrors = [System.Collections.Generic.List[string]]::new()
foreach ($report in $reports) {
    $overrideReport = $report.content
    $invocations.Clear()
    $caseRoot = Join-Path $results $report.name
    $failure = ''
    try { & $runner -ResultsDirectory $caseRoot | Out-Null }
    catch { $failure = $_.Exception.Message }
    $saved = @(Get-ChildItem -LiteralPath $caseRoot -Recurse -File -Filter '*.trx').Count
    $receipts = @(Get-ChildItem -LiteralPath $caseRoot -Recurse -File -Filter 'reports.json').Count
    if (!$failure.Contains($overrideCase) -or !$failure.Contains($report.diagnostic) -or
        $invocations.Count -ne 22 -or $saved -ne 22 -or $receipts -ne 0) {
        $contractErrors.Add("$($report.name): 期望报告 $overrideCase ($($report.diagnostic))，保留 22 份 TRX 且不生成清单。实际执行 $($invocations.Count) 格，保留 $saved 份，清单 $receipts 份。错误为 [$failure]")
        continue
    }
    Write-Output "PASS $($report.name)"
}
if ($contractErrors.Count) { throw ($contractErrors -join [Environment]::NewLine) }
