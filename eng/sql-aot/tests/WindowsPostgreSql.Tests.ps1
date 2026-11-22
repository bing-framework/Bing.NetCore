#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'output/sql-aot-postgresql-tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$parent = if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repositoryRoot $ResultsDirectory }
$testRoot = Join-Path $parent ('run-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$results = @()
$loadError = $null
try { . (Join-Path $PSScriptRoot '../WindowsPostgreSql.ps1') } catch { $loadError = $_ }
$realTool = if (!$loadError) { (Get-Command Invoke-SqlAotPgTool).ScriptBlock }

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        if ($loadError) { throw $loadError }
        & $Body
        $script:results += [pscustomobject]@{ name = $Name; status = 'PASS' }
    }
    catch { $script:results += [pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message } }
}

function New-Scenario {
    $script:directory = Join-Path $testRoot ('isolated 中文 ' + [Guid]::NewGuid().ToString('N'))
    $script:environmentFile = Join-Path $testRoot ('env-' + [Guid]::NewGuid().ToString('N'))
    $script:calls = [Collections.Generic.List[object]]::new()
    $script:failAt = ''
    $script:failCleanup = $false
    $script:dataDirectoryExistedAtInitdb = $null
}

# 在外部工具边界替身；独立的真实进程测试另外覆盖参数、非零和超时。
function Invoke-SqlAotPgTool([string]$FilePath, [string[]]$Arguments, [string]$LogFile, [int]$TimeoutSeconds) {
    $name = [IO.Path]::GetFileNameWithoutExtension($FilePath)
    $script:calls.Add([pscustomobject]@{ name = $name; arguments = $Arguments; timeout = $TimeoutSeconds })
    [IO.File]::WriteAllText($LogFile, "工具日志 中文`n", $utf8)
    if ($Arguments -contains '--version') { return 'PostgreSQL fixture version' }
    if ($name -eq 'initdb') {
        $dataIndex = [array]::IndexOf($Arguments, '-D')
        if ($dataIndex -lt 0) { throw 'initdb data directory argument is missing' }
        $dataDirectory = $Arguments[$dataIndex + 1]
        $script:dataDirectoryExistedAtInitdb = Test-Path -LiteralPath $dataDirectory
        [IO.Directory]::CreateDirectory($dataDirectory) | Out-Null
    }
    if ($name -eq 'pg_ctl' -and $Arguments[-1] -eq 'start') {
        [IO.File]::WriteAllText((Join-Path $script:directory 'data/postmaster.pid'), '12345', $utf8)
    }
    if ($name -eq 'pg_ctl' -and $Arguments[-1] -eq 'stop') {
        if ($script:failCleanup) { throw 'cleanup failure' }
        [IO.File]::Delete((Join-Path $script:directory 'data/postmaster.pid'))
    }
    if ($name -eq $script:failAt -and $Arguments -notcontains '--version' -and
        ($name -ne 'pg_ctl' -or $Arguments[-1] -eq 'start')) { throw "$name original failure" }
}

Test-Case 'Start_CreatesOwnedUtf8Cluster_AndExportsOnlyTestDatabase' {
    New-Scenario
    $state = Start-SqlAotPostgreSql -BinDirectory 'C:/PostgreSQL tools/bin' -Directory $directory -EnvironmentFile $environmentFile
    if ($state.status -ne 'READY' -or $state.port -le 0 -or $state.version -ne 'PostgreSQL fixture version') { throw 'Missing ready identity' }
    $init = @($calls | Where-Object name -eq 'initdb')[0]
    $expected = @('-D', (Join-Path $directory 'data'), '--username=bing_aot', '--encoding=UTF8', '--locale=C', '--auth=trust', '--no-instructions')
    if (($init.arguments | ConvertTo-Json -Compress) -cne ($expected | ConvertTo-Json -Compress)) { throw 'initdb arguments differ' }
    $start = @($calls | Where-Object { $_.name -eq 'pg_ctl' -and $_.arguments[-1] -eq 'start' })[0]
    if ($start.arguments -notcontains '-w' -or $start.arguments -notcontains '60' -or $start.timeout -ne 75) { throw 'Readiness is not bounded' }
    if ($start.arguments -notcontains "-h 127.0.0.1 -p $($state.port)") { throw 'Server must bind loopback' }
    $create = @($calls | Where-Object name -eq 'createdb')[0]
    if ($create.arguments[-1] -ne 'bing_sql_aot_test' -or $create.arguments -notcontains '--maintenance-db=postgres') { throw 'Wrong test database' }
    $expectedEnv = "RUN_POSTGRESQL_INTEGRATION_TESTS=true`nConnectionStrings__PostgreSqlConnection=Host=127.0.0.1;Port=$($state.port);Database=bing_sql_aot_test;Username=bing_aot;Pooling=false`n"
    if ([IO.File]::ReadAllText($environmentFile, $utf8) -cne $expectedEnv) { throw 'Environment contract differs' }
}

Test-Case 'Start_LeavesDataDirectoryForInitdbToCreate' {
    New-Scenario
    Start-SqlAotPostgreSql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null
    if ($script:dataDirectoryExistedAtInitdb -ne $false) { throw 'initdb target directory must not exist before initialization' }
}

Test-Case 'Start_RejectsExistingDirectory_WithoutTouchingExistingCluster' {
    New-Scenario
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'sentinel'), 'keep', $utf8)
    $rejected = $false
    try { Start-SqlAotPostgreSql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null }
    catch { $rejected = $true }
    if (!$rejected -or $calls.Count -ne 0 -or [IO.File]::ReadAllText((Join-Path $directory 'sentinel'), $utf8) -ne 'keep') { throw 'Existing cluster was touched' }
}

foreach ($stage in @('initdb', 'pg_ctl', 'createdb')) {
    Test-Case "Start_${stage}Failure_PreservesFailureAndDoesNotExportEnvironment" {
        New-Scenario
        $script:failAt = $stage
        $message = ''
        try { Start-SqlAotPostgreSql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null }
        catch { $message = $_.Exception.Message }
        if ($message -ne "$stage original failure") { throw "Wrong original failure: $message" }
        if (Test-Path -LiteralPath $environmentFile) { throw 'Failed cluster exported an environment' }
        $state = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw -Encoding utf8 | ConvertFrom-Json
        if ($state.status -ne 'FAIL' -or $state.cleanupStatus -ne 'STOPPED') { throw 'Missing failure or cleanup receipt' }
        $stops = @($calls | Where-Object { $_.name -eq 'pg_ctl' -and $_.arguments[-1] -eq 'stop' })
        if ($stage -ne 'initdb' -and $stops.Count -ne 1) { throw 'Owned server was not stopped' }
        if ($stage -eq 'initdb' -and $stops.Count -ne 0) { throw 'Cleanup touched a server before creation' }
    }
}

Test-Case 'Start_CleanupFailure_DoesNotMaskOriginalFailure' {
    New-Scenario
    $script:failAt = 'createdb'
    $script:failCleanup = $true
    $message = ''
    try { Start-SqlAotPostgreSql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null }
    catch { $message = $_.Exception.Message }
    $state = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ($message -ne 'createdb original failure' -or $state.cleanupError -ne 'cleanup failure' -or $state.cleanupStatus -ne 'FAIL') { throw 'Failure identities were lost' }
}

Test-Case 'Stop_IsIdempotent_AndOnlyTargetsTheOwnedDataDirectory' {
    New-Scenario
    $unrelated = Join-Path $testRoot 'unrelated-postmaster.pid'
    [IO.File]::WriteAllText($unrelated, 'keep', $utf8)
    Start-SqlAotPostgreSql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null
    Stop-SqlAotPostgreSql -Directory $directory
    Stop-SqlAotPostgreSql -Directory $directory
    $stops = @($calls | Where-Object { $_.name -eq 'pg_ctl' -and $_.arguments[-1] -eq 'stop' })
    if ($stops.Count -ne 1 -or $stops[0].arguments[1] -ne (Join-Path $directory 'data')) { throw 'Cleanup ownership differs' }
    if ([IO.File]::ReadAllText($unrelated, $utf8) -ne 'keep') { throw 'Unrelated server was touched' }
    Stop-SqlAotPostgreSql -Directory (Join-Path $testRoot 'never-created')
}

Test-Case 'Stop_FailureRemainsVisible_AndCanRetryOwnedCleanup' {
    New-Scenario
    Start-SqlAotPostgreSql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null
    $script:failCleanup = $true
    $message = ''
    try { Stop-SqlAotPostgreSql -Directory $directory } catch { $message = $_.Exception.Message }
    $state = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ($message -ne 'cleanup failure' -or $state.cleanupStatus -ne 'FAIL') { throw 'Cleanup failure was swallowed' }
    $script:failCleanup = $false
    Stop-SqlAotPostgreSql -Directory $directory
    $state = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ($state.cleanupStatus -ne 'STOPPED' -or $state.PSObject.Properties.Name -contains 'cleanupError') { throw 'Successful retry kept a stale failure' }
}

if ($realTool) { Set-Item -LiteralPath Function:Invoke-SqlAotPgTool -Value $realTool }
$node = (Get-Command node).Source

Test-Case 'Tool_RealProcess_PreservesArgumentBoundariesAndUtf8' {
    $log = Join-Path $testRoot 'arguments.log'
    $arguments = @('-e', 'process.stdout.write(JSON.stringify(process.argv.slice(1)))', 'space name', '中文', 'quote"value', 'tail\')
    $actual = Invoke-SqlAotPgTool -FilePath $node -Arguments $arguments -LogFile $log -TimeoutSeconds 10
    $decoded = $actual | ConvertFrom-Json
    if (($decoded | ConvertTo-Json -Compress) -cne ($arguments[2..5] | ConvertTo-Json -Compress)) { throw 'Actual argument boundaries differ' }
}

Test-Case 'Tool_RealNonzeroExit_PreservesBothStreamsAndFails' {
    $log = Join-Path $testRoot 'nonzero.log'
    $message = ''
    try {
        Invoke-SqlAotPgTool -FilePath $node -Arguments @('-e', 'process.stdout.write("标准输出");process.stderr.write("失败原因");process.exitCode=23') -LogFile $log -TimeoutSeconds 10 | Out-Null
    }
    catch { $message = $_.Exception.Message }
    $content = [IO.File]::ReadAllText($log, $utf8)
    if ($message -notmatch '23' -or !$content.Contains('标准输出') -or !$content.Contains('失败原因')) { throw 'Nonzero exit was lost' }
}

Test-Case 'Tool_RealTimeout_TerminatesOwnedProcessAndRetainsLog' {
    $log = Join-Path $testRoot 'timeout.log'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $message = ''
    try {
        $program = 'console.log(process.pid);require("node:child_process").spawn(process.execPath,["-e","console.log(process.pid);setInterval(()=>{},1000)"],{stdio:["ignore","inherit","inherit"],windowsHide:true});setInterval(()=>{},1000)'
        Invoke-SqlAotPgTool -FilePath $node -Arguments @('-e', $program) -LogFile $log -TimeoutSeconds 2 | Out-Null
    }
    catch { $message = $_.Exception.Message }
    if ($message -notmatch 'timed out' -or $timer.Elapsed.TotalSeconds -gt 10) { throw 'Timeout was not bounded' }
    $childIds = @([IO.File]::ReadAllLines($log, $utf8) | Where-Object { $_.Trim() } | ForEach-Object { [int]$_ })
    if ($childIds.Count -ne 2) { throw 'Parent and descendant did not both start' }
    foreach ($childId in $childIds) {
        if (Get-Process -Id $childId -ErrorAction SilentlyContinue) { throw 'Owned timeout process is still alive' }
    }
}

$report = [ordered]@{ passed = @($results | Where-Object status -eq 'PASS').Count; failed = @($results | Where-Object status -eq 'FAIL').Count; tests = $results }
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testRoot 'tests.json') -Encoding utf8
$results | Format-Table -AutoSize | Out-Host
Write-Output "POSTGRESQL_LIFECYCLE_TESTS: $testRoot"
if ($report.failed -gt 0) { exit 1 }
