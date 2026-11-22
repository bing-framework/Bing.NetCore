#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BenchmarkAssembly,
    [Parameter(Mandatory = $true)][string]$ResultsDirectory,
    [switch]$RunPostgreSql
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$assemblyCandidate = if ([System.IO.Path]::IsPathRooted($BenchmarkAssembly)) {
    $BenchmarkAssembly
} else {
    Join-Path $repositoryRoot $BenchmarkAssembly
}
$assemblyPath = [System.IO.Path]::GetFullPath($assemblyCandidate)
if (!(Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "BenchmarkAssembly does not exist: $assemblyPath"
}
$dotnet = Get-Command dotnet -ErrorAction Stop
$resultCandidate = if ([System.IO.Path]::IsPathRooted($ResultsDirectory)) {
    $ResultsDirectory
} else {
    Join-Path $repositoryRoot $ResultsDirectory
}
$resultRoot = [System.IO.Path]::GetFullPath($resultCandidate)
[System.IO.Directory]::CreateDirectory($resultRoot) | Out-Null
$runRoot = Join-Path $resultRoot ('capacity-' + [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$cases = [System.Collections.Generic.List[object]]::new()
$oldGate = $env:RUN_POSTGRESQL_INTEGRATION_TESTS
$oldConnection = $env:ConnectionStrings__PostgreSqlConnection

function Invoke-CapacityProbe([string[]]$Arguments, [string]$LogPath, [int]$TimeoutSeconds = 45) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($dotnet.Source)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = $utf8
    $start.StandardErrorEncoding = $utf8
    $start.ArgumentList.Add($assemblyPath)
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (!$process.Start()) { throw 'Could not start the benchmark assembly.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = !$process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            if (!$process.HasExited) { $process.Kill($true) }
            $process.WaitForExit(5000) | Out-Null
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        [System.IO.File]::WriteAllText($LogPath, $output + $errorOutput, $utf8)
        if ($timedOut) { throw "Capacity probe exceeded $TimeoutSeconds seconds." }
        return $process.ExitCode
    }
    finally { $process.Dispose() }
}

function Assert-BuildIdentityGate([string]$ReceiptPath) {
    $gatePath = Join-Path $PSScriptRoot '../Invoke-SqlCapacityGate.ps1'
    $result = & $gatePath -ReceiptPath $ReceiptPath -ExpectedBuildDirectory (Split-Path -Parent $assemblyPath) `
        -MinRequestsPerSecond 0 -MaxP99Milliseconds 2147483647 -MaxFailureRate 1 | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $result.status -ne 'PASS' -or $result.identityScope -ne 'sql-assemblies') {
        throw "Build identity gate rejected the real receipt: $($result.error)"
    }
}

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $cases.Add([pscustomobject]@{ name = $Name; status = 'PASS' })
    }
    catch {
        $cases.Add([pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message })
    }
}

function Assert-Receipt([string]$Path, [string]$ExpectedPath, [int]$Rows, [int]$Requests, [int]$Concurrency, [string]$ConnectionString) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw 'Capacity report was not created.' }
    $raw = [System.IO.File]::ReadAllText($Path, $utf8)
    if ($raw.Contains($ConnectionString, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Capacity report contains the database connection string.'
    }
    $report = $raw | ConvertFrom-Json
    if ($report.probe -ne 'SqlQueryCapacityProbe-v2' -or $report.path -ne $ExpectedPath -or
        $report.rows -ne $Rows -or $report.requests -ne $Requests -or $report.concurrency -ne $Concurrency -or
        $null -eq $report.maxQueuedRequests -or $report.maxQueuedRequests -ne 0) {
        throw 'Capacity report identity or input values differ.'
    }
    $expectedAssemblies = @(
        'Bing.Data.Sql.Benchmarks.dll'
        'Bing.Data.dll'
        'Bing.Data.Sql.dll'
        'Bing.Dapper.Core.dll'
        'Bing.Dapper.PostgreSql.dll'
        'Dapper.dll'
        'Npgsql.dll'
    )
    if ($null -eq $report.assemblySha256) { throw 'Capacity report is missing assembly hashes.' }
    $actualAssemblyNames = @($report.assemblySha256.PSObject.Properties.Name | Sort-Object)
    $expectedAssemblyNames = @($expectedAssemblies | Sort-Object)
    if (($actualAssemblyNames -join "`n") -cne ($expectedAssemblyNames -join "`n")) {
        throw 'Capacity report assembly hash keys do not match the required build identity.'
    }
    $assemblyDirectory = Split-Path -Parent $assemblyPath
    foreach ($name in $expectedAssemblies) {
        $path = Join-Path $assemblyDirectory $name
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Build identity assembly is missing: $name." }
        $expectedHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($report.assemblySha256.$name -cne $expectedHash) {
            throw "Capacity report hash does not match the loaded build assembly: $name."
        }
    }
    if ($report.pooling -isnot [bool] -or $report.maxPoolSize -lt 1) {
        throw 'Capacity report is missing the configured connection pool settings.'
    }

    $measure = $report.measure
    $measureNames = @('successful', 'failed', 'peakInFlight', 'elapsedMilliseconds',
        'successfulRequestsPerSecond', 'p50Milliseconds', 'p95Milliseconds', 'p99Milliseconds',
        'allocatedBytes', 'gen0Collections', 'gen1Collections', 'gen2Collections', 'processCpuMilliseconds')
    foreach ($name in $measureNames) {
        if ($null -eq $measure.PSObject.Properties[$name]) { throw "Capacity report is missing measure.$name." }
    }
    if ($measure.successful -ne $Requests -or $measure.failed -ne 0) {
        throw 'Capacity report does not account for every successful request.'
    }
    if ($measure.peakInFlight -lt 1 -or $measure.peakInFlight -gt $Concurrency) {
        throw 'Observed in-flight work exceeds the configured concurrency.'
    }
    if ($measure.elapsedMilliseconds -le 0 -or $measure.successfulRequestsPerSecond -le 0 -or
        $measure.allocatedBytes -lt 0 -or $measure.processCpuMilliseconds -lt 0) {
        throw 'Capacity report contains invalid timing or resource measurements.'
    }
    if ($null -eq $measure.p50Milliseconds -or $null -eq $measure.p95Milliseconds -or $null -eq $measure.p99Milliseconds -or
        $measure.p50Milliseconds -lt 0 -or $measure.p50Milliseconds -gt $measure.p95Milliseconds -or
        $measure.p95Milliseconds -gt $measure.p99Milliseconds) {
        throw 'Latency percentiles are missing or out of order.'
    }
    foreach ($name in @('gen0Collections', 'gen1Collections', 'gen2Collections')) {
        if ($measure.$name -lt 0) { throw "Capacity report measure.$name is negative." }
    }
}

try {
    $env:RUN_POSTGRESQL_INTEGRATION_TESTS = $null
    $env:ConnectionStrings__PostgreSqlConnection = $null
    Test-Case 'CapacityProbe_RequiresExplicitDatabaseGate_AndWritesNoReceipt' {
        $report = Join-Path $runRoot 'gate-required.json'
        $code = Invoke-CapacityProbe @('--capacity-probe', 'runtime', '1', '1', '1', $report) (Join-Path $runRoot 'gate-required.log')
        if ($code -ne 2 -or (Test-Path -LiteralPath $report)) {
            throw 'Probe did not reject a missing integration gate before writing a receipt.'
        }
    }

    $env:RUN_POSTGRESQL_INTEGRATION_TESTS = 'true'
    $env:ConnectionStrings__PostgreSqlConnection = 'Host=127.0.0.1;Port=1;Database=postgres;Username=capacity_probe_test;Timeout=1'
    Test-Case 'CapacityProbe_RejectsNonFixtureDatabase_WithoutConnectingOrWritingReceipt' {
        $report = Join-Path $runRoot 'wrong-database.json'
        $log = Join-Path $runRoot 'wrong-database.log'
        $code = Invoke-CapacityProbe @('--capacity-probe', 'runtime', '1', '1', '1', $report) $log
        $diagnostic = [System.IO.File]::ReadAllText($log, $utf8)
        if ($code -ne 2 -or (Test-Path -LiteralPath $report) -or
            $diagnostic -notmatch 'bing_sql_aot_test|isolated.*database|test.*database') {
            throw 'Probe did not reject a database outside the isolated test fixture.'
        }
    }

    if ($RunPostgreSql) {
        if ($oldGate -ne 'true' -or [string]::IsNullOrWhiteSpace($oldConnection)) {
            throw 'PostgreSQL cases require the caller to set the integration gate and connection string.'
        }
        $connectionString = $oldConnection
        $env:RUN_POSTGRESQL_INTEGRATION_TESTS = $oldGate
        $env:ConnectionStrings__PostgreSqlConnection = $connectionString

        foreach ($path in @('runtime', 'template')) {
            Test-Case "CapacityProbe_$($path)_RecordsConcurrentSqlReceipt" {
                $report = Join-Path $runRoot "$path.json"
                $code = Invoke-CapacityProbe @('--capacity-probe', $path, '23', '128', '8', $report) `
                    (Join-Path $runRoot "$path.log") 90
                if ($code -ne 0) { throw "Capacity probe failed for $path." }
                Assert-Receipt -Path $report -ExpectedPath $path -Rows 23 -Requests 128 -Concurrency 8 -ConnectionString $connectionString
                if ($path -eq 'runtime') { Assert-BuildIdentityGate -ReceiptPath $report }
                $receipt = [System.IO.File]::ReadAllText($report, $utf8) | ConvertFrom-Json
                if ($null -ne $receipt.serverActivity) { throw 'Server sampling must be opt-in.' }
            }
        }

        Test-Case 'CapacityProbe_ObserveServer_SeparatesDatabaseActivityFromInFlightRequests' {
            $report = Join-Path $runRoot 'server-activity.json'
            $log = Join-Path $runRoot 'server-activity.log'
            $pooledConnection = "$connectionString;Pooling=true;Maximum Pool Size=2"
            $env:ConnectionStrings__PostgreSqlConnection = $pooledConnection
            try {
                $code = Invoke-CapacityProbe @('--capacity-probe', 'runtime', '67', '2048', '8', $report, '--observe-server') $log 90
                if ($code -ne 0) {
                    $diagnostic = [System.IO.File]::ReadAllText($log, $utf8).Trim()
                    throw "Probe with server sampling failed (exit $code). $diagnostic"
                }
                Assert-Receipt -Path $report -ExpectedPath 'runtime' -Rows 67 -Requests 2048 -Concurrency 8 -ConnectionString $pooledConnection
                $receipt = [System.IO.File]::ReadAllText($report, $utf8) | ConvertFrom-Json
                $activity = $receipt.serverActivity
                if ($receipt.maxPoolSize -ne 2 -or $null -eq $activity -or $activity.sampleCount -lt 1 -or
                    $activity.intervalMilliseconds -lt 1 -or $activity.peakActiveSessions -lt 1 -or
                    $activity.peakActiveSessions -gt 2 -or $receipt.measure.peakInFlight -le $activity.peakActiveSessions -or
                    $null -eq $activity.peakLockWaitingSessions -or $activity.peakLockWaitingSessions -lt 0 -or
                    $activity.peakLockWaitingSessions -gt $activity.peakActiveSessions) {
                    throw 'PostgreSQL activity must be observed independently of client concurrency and stay within the probe pool.'
                }
            }
            finally { $env:ConnectionStrings__PostgreSqlConnection = $connectionString }
        }
    }
}
finally {
    $env:RUN_POSTGRESQL_INTEGRATION_TESTS = $oldGate
    $env:ConnectionStrings__PostgreSqlConnection = $oldConnection
}

$result = [pscustomobject]@{
    passed = @($cases | Where-Object status -eq 'PASS').Count
    failed = @($cases | Where-Object status -eq 'FAIL').Count
    postgreSqlEnabled = [bool]$RunPostgreSql
    cases = $cases
}
[System.IO.File]::WriteAllText((Join-Path $runRoot 'integration-tests.json'), ($result | ConvertTo-Json -Depth 8), $utf8)
$result | ConvertTo-Json -Depth 8
if ($result.failed) { exit 1 }
