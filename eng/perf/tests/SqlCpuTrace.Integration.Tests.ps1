[CmdletBinding()]
param(
    [string]$Collector = 'eng/perf/SqlCpuTrace/bin/Release/net8.0/SqlCpuTrace.dll',
    [string]$Fixture = 'eng/perf/tests/SqlCpuTraceFixture/bin/Release/net8.0/SqlCpuTraceFixture.dll',
    [string]$CapturedRun,
    [string]$ResultsDirectory = 'output/round136-tests'
)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([System.IO.Path]::GetFullPath($ResultsDirectory)) ('integration-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$cases = @()
$oldFixture = $env:BING_SQL_CPU_FIXTURE
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:cases += @{ name = $Name; status = 'PASS' } }
    catch { $script:cases += @{ name = $Name; status = 'FAIL'; error = $_.Exception.Message } }
}
function Invoke-TestCollector([string[]]$ToolArguments, [string]$Log) {
    $start = [System.Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add([System.IO.Path]::GetFullPath($Collector))
    foreach ($item in $ToolArguments) { $start.ArgumentList.Add($item) }
    $process = [System.Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $expired = -not $process.WaitForExit(30000)
    if ($expired) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null }
    [System.IO.File]::WriteAllText($Log, $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult(), [System.Text.UTF8Encoding]::new($false))
    $code = $process.ExitCode
    $process.Dispose()
    if ($expired) { throw 'Collector test process exceeded 30 second bound' }
    return $code
}
try {
    Test-Case 'Capture_FailingProbe_PreservesExitCode_TraceAndUtf8Logs' {
        $env:BING_SQL_CPU_FIXTURE = 'fail'
        $parent = Join-Path $root 'failure'
        $code = Invoke-TestCollector -ToolArguments @('capture', [System.IO.Path]::GetFullPath($Fixture), $parent, '15') -Log (Join-Path $root 'failure.log')
        if ($code -eq 0) { throw 'Failed probe reported success' }
        $run = @(Get-ChildItem -LiteralPath $parent -Directory)
        if ($run.Count -ne 1) { throw 'Missing unique receipt directory' }
        $receipt = Get-Content -LiteralPath (Join-Path $run[0].FullName 'capture.json') -Raw -Encoding utf8 | ConvertFrom-Json
        if ($receipt.status -ne 'FAIL' -or $receipt.exitCode -ne 23) { throw 'Lost failing exit code' }
        if (-not (Test-Path -LiteralPath (Join-Path $run[0].FullName 'cpu.nettrace'))) { throw 'Lost trace' }
        if ((Get-Content -LiteralPath (Join-Path $run[0].FullName 'probe.log') -Raw -Encoding utf8).Trim() -ne 'fixture-stdout-中文') { throw 'Lost stdout bytes' }
        if ((Get-Content -LiteralPath (Join-Path $run[0].FullName 'probe.stderr') -Raw -Encoding utf8).Trim() -ne 'fixture-stderr-中文') { throw 'Lost stderr bytes' }
    }
    Test-Case 'Capture_Timeout_KillsOwnedProcess_AndPersistsFailureReceipt' {
        $env:BING_SQL_CPU_FIXTURE = 'timeout'
        $parent = Join-Path $root 'timeout'
        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        $code = Invoke-TestCollector -ToolArguments @('capture', [System.IO.Path]::GetFullPath($Fixture), $parent, '2') -Log (Join-Path $root 'timeout.log')
        if ($code -eq 0) { throw 'Timeout reported success' }
        if ($watch.Elapsed.TotalSeconds -gt 12) { throw 'Timeout cleanup exceeded bounded test window' }
        $run = @(Get-ChildItem -LiteralPath $parent -Directory)[0]
        $receipt = Get-Content -LiteralPath (Join-Path $run.FullName 'capture.json') -Raw -Encoding utf8 | ConvertFrom-Json
        if ($receipt.status -ne 'FAIL' -or -not $receipt.timedOut -or -not $receipt.finishedAt) { throw 'Missing timeout receipt' }
        if (Get-Process -Id $receipt.processId -ErrorAction SilentlyContinue) { throw 'Owned process survived timeout' }
    }
    Test-Case 'Analyze_ReplaysRawTrace_AndRejectsWrongPid_WithoutOverwriting' {
        $receipt = Get-Content -LiteralPath (Join-Path $CapturedRun 'capture.json') -Raw -Encoding utf8 | ConvertFrom-Json
        $report = Join-Path $root 'replay'
        $code = Invoke-TestCollector -ToolArguments @('analyze', (Join-Path $CapturedRun 'cpu.nettrace'), [string]$receipt.processId, $report) -Log (Join-Path $root 'replay.log')
        if ($code -ne 0) { throw 'Replay failed' }
        $original = Get-Content -LiteralPath (Join-Path $CapturedRun 'report/summary.json') -Raw -Encoding utf8
        if ((Get-Content -LiteralPath (Join-Path $report 'summary.json') -Raw -Encoding utf8) -ne $original) { throw 'Replay summary differs' }
        $hash = (Get-FileHash -LiteralPath (Join-Path $report 'methods.csv')).Hash
        if ($hash -ne (Get-FileHash -LiteralPath (Join-Path $CapturedRun 'report/methods.csv')).Hash) { throw 'Replay method attribution differs' }
        $code = Invoke-TestCollector -ToolArguments @('analyze', (Join-Path $CapturedRun 'cpu.nettrace'), '2147483647', $report) -Log (Join-Path $root 'existing-report.log')
        if ($code -eq 0 -or (Get-FileHash -LiteralPath (Join-Path $report 'methods.csv')).Hash -ne $hash) { throw 'Overwrote existing report' }
        $wrong = Join-Path $root 'wrong-pid'
        $code = Invoke-TestCollector -ToolArguments @('analyze', (Join-Path $CapturedRun 'cpu.nettrace'), '2147483647', $wrong) -Log (Join-Path $root 'wrong-pid.log')
        $wrongSummary = Get-Content -LiteralPath (Join-Path $wrong 'summary.json') -Raw -Encoding utf8 | ConvertFrom-Json
        if ($code -eq 0 -or $wrongSummary.methodCpuVerified -or $wrongSummary.ManagedSamples -ne 0) { throw 'Wrong PID passed attribution gate' }
    }
} finally { $env:BING_SQL_CPU_FIXTURE = $oldFixture }
$result = @{ passed = @($cases | Where-Object status -eq 'PASS').Count; failed = @($cases | Where-Object status -eq 'FAIL').Count; cases = $cases }
[System.IO.File]::WriteAllText((Join-Path $root 'integration-tests.json'), ($result | ConvertTo-Json -Depth 6), [System.Text.UTF8Encoding]::new($false))
$result | ConvertTo-Json -Depth 6
if ($result.failed) { exit 1 }
