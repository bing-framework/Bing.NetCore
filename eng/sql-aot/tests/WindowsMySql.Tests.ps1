#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'output/sql-aot-mysql-tests')

[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$parent = if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repositoryRoot $ResultsDirectory }
$testRoot = Join-Path $parent ('run-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$node = (Get-Command node).Source
$results = @()
$owned = [Collections.Generic.List[Diagnostics.Process]]::new()
$loadError = $null
try { . (Join-Path $PSScriptRoot '../WindowsMySql.ps1') } catch { $loadError = $_ }
$realTool = if (!$loadError) { (Get-Command Invoke-SqlAotMySqlTool).ScriptBlock }
$realStart = if (!$loadError) { (Get-Command Start-SqlAotMySqlProcess).ScriptBlock }

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        if ($loadError) { throw $loadError }
        & $Body
        $script:results += [pscustomobject]@{ name = $Name; status = 'PASS' }
    }
    catch { $script:results += [pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message } }
    finally {
        foreach ($process in $owned) {
            if (!$process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null }
            $process.Dispose()
        }
        $owned.Clear()
    }
}

function New-Scenario {
    $script:directory = Join-Path $testRoot ('isolated 中文 ' + [Guid]::NewGuid().ToString('N'))
    $script:environmentFile = Join-Path $testRoot ('env-' + [Guid]::NewGuid().ToString('N'))
    $script:calls = [Collections.Generic.List[object]]::new()
    $script:failAt = ''
    $script:shutdownFailure = $false
    $script:server = $null
}

# 仅替换原生数据库工具；后台进程的启动、身份核对和回收使用真实 Node 进程。
function Invoke-SqlAotMySqlTool([string]$FilePath, [string[]]$Arguments, [string]$LogFile, [int]$TimeoutSeconds) {
    $name = [IO.Path]::GetFileNameWithoutExtension($FilePath)
    $stage = if ($Arguments -contains '--version') { 'version' }
        elseif ($Arguments -contains '--initialize-insecure') { 'initialize' }
        elseif ($Arguments[-1] -eq 'ping') { 'ready' }
        elseif ($Arguments[-1] -eq 'shutdown') { 'shutdown' }
        else { 'create' }
    $script:calls.Add([pscustomobject]@{ name = $name; stage = $stage; arguments = $Arguments; timeout = $TimeoutSeconds })
    [IO.File]::WriteAllText($LogFile, "MySQL 工具 中文`n", $utf8)
    if ($stage -eq $script:failAt) { throw "$stage original failure" }
    if ($stage -eq 'version') { return 'MySQL fixture version' }
    if ($stage -eq 'shutdown') {
        if ($script:shutdownFailure) { throw 'shutdown failure' }
        $script:server.Kill($true)
        $script:server.WaitForExit(5000) | Out-Null
    }
}

function Start-SqlAotMySqlProcess([string]$FilePath, [string[]]$Arguments) {
    $script:calls.Add([pscustomobject]@{ name = 'server'; stage = 'start'; arguments = $Arguments })
    if ($script:failAt -eq 'start') { throw 'start original failure' }
    $script:server = & $realStart -FilePath $node -Arguments @('-e', 'setInterval(()=>{},1000)')
    $owned.Add($script:server)
    return [Diagnostics.Process]::GetProcessById($script:server.Id)
}

Test-Case 'Start_CreatesIsolatedUtf8Database_AndExportsExactEnvironment' {
    New-Scenario
    $bin = Join-Path $testRoot 'MySQL tools/bin'
    $state = Start-SqlAotMySql -BinDirectory $bin -Directory $directory -EnvironmentFile $environmentFile
    if ($state.status -ne 'READY' -or $state.port -le 0 -or $state.version -ne 'MySQL fixture version') { throw 'Missing ready identity' }
    $data = Join-Path $directory 'data'
    $base = Split-Path $bin -Parent
    $expectedInit = @('--no-defaults', "--basedir=$base", "--datadir=$data", '--initialize-insecure', '--console')
    $init = @($calls | Where-Object stage -eq initialize)[0]
    if (($init.arguments | ConvertTo-Json -Compress) -cne ($expectedInit | ConvertTo-Json -Compress)) { throw 'Initialization arguments differ' }
    $expectedStart = @('--no-defaults', "--basedir=$base", "--datadir=$data", '--bind-address=127.0.0.1', "--port=$($state.port)", '--mysqlx=0', '--character-set-server=utf8mb4', "--log-error=$(Join-Path $directory 'server.log')")
    if ((@($calls | Where-Object stage -eq start)[0].arguments | ConvertTo-Json -Compress) -cne ($expectedStart | ConvertTo-Json -Compress)) { throw 'Server arguments differ' }
    $connection = @('--no-defaults', '--no-login-paths', '--protocol=TCP', '--host=127.0.0.1', "--port=$($state.port)", '--user=root', '--skip-password', '--connect-timeout=2')
    $ready = @($calls | Where-Object stage -eq ready)[0]
    if (($ready.arguments | ConvertTo-Json -Compress) -cne (($connection + @('--wait=30', 'ping')) | ConvertTo-Json -Compress) -or $ready.timeout -ne 75) { throw 'Readiness arguments differ' }
    $sql = "CREATE DATABASE ``bing_sql_aot_test`` CHARACTER SET utf8mb4; CREATE USER 'bing_aot'@'127.0.0.1' IDENTIFIED BY 'local_aot_test_password'; GRANT ALL PRIVILEGES ON ``bing_sql_aot_test``.* TO 'bing_aot'@'127.0.0.1';"
    $create = @($calls | Where-Object stage -eq create)[0]
    if (($create.arguments | ConvertTo-Json -Compress) -cne (($connection + @('--default-character-set=utf8mb4', "--execute=$sql")) | ConvertTo-Json -Compress)) { throw 'Complete database/account SQL differs' }
    $expectedEnv = "RUN_MYSQL_INTEGRATION_TESTS=true`nConnectionStrings__MySqlConnection=Server=127.0.0.1;Port=$($state.port);Database=bing_sql_aot_test;User ID=bing_aot;Password=local_aot_test_password;Pooling=false;SslMode=None;AllowPublicKeyRetrieval=true`n"
    if ([IO.File]::ReadAllText($environmentFile, $utf8) -cne $expectedEnv) { throw 'Environment differs' }
    $saved = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ($saved.processId -ne $server.Id -or $saved.processStartedUtcTicks -ne $server.StartTime.ToUniversalTime().Ticks) { throw 'Process ownership not persisted' }
    Stop-SqlAotMySql -Directory $directory
}

Test-Case 'Start_RejectsExistingDirectory_WithoutLaunchingOrChangingData' {
    New-Scenario
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    [IO.File]::WriteAllText((Join-Path $directory 'sentinel'), 'keep', $utf8)
    $message = ''
    try { Start-SqlAotMySql -BinDirectory 'tools' -Directory $directory | Out-Null } catch { $message = $_.Exception.Message }
    if (!$message -or $calls.Count -ne 0 -or [IO.File]::ReadAllText((Join-Path $directory 'sentinel'), $utf8) -cne 'keep') { throw 'Existing directory was touched' }
}

foreach ($stage in @('initialize', 'start', 'ready', 'create')) {
    Test-Case "Start_${stage}Failure_PreservesOriginalError_CleansOwnedProcess_AndDoesNotExport" {
        New-Scenario
        $script:failAt = $stage
        $message = ''
        try { Start-SqlAotMySql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null } catch { $message = $_.Exception.Message }
        $state = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw -Encoding utf8 | ConvertFrom-Json
        if ($message -cne "$stage original failure" -or $state.status -ne 'FAIL' -or $state.cleanupStatus -ne 'STOPPED') { throw 'Original failure or final cleanup state was lost' }
        if (Test-Path -LiteralPath $environmentFile) { throw 'Failed start exported an environment' }
        if ($server -and !$server.HasExited) { throw 'Owned process leaked' }
        if ($stage -in @('initialize', 'start') -and @($calls | Where-Object stage -eq shutdown).Count) { throw 'Shutdown ran before server creation' }
    }
}

Test-Case 'Start_ShutdownFailure_StillTerminatesOwnedProcess_AndPreservesOriginalError' {
    New-Scenario
    $script:failAt = 'create'
    $script:shutdownFailure = $true
    $message = ''
    try { Start-SqlAotMySql -BinDirectory 'tools' -Directory $directory -EnvironmentFile $environmentFile | Out-Null } catch { $message = $_.Exception.Message }
    $state = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw -Encoding utf8 | ConvertFrom-Json
    if ($message -cne 'create original failure' -or $state.cleanupStatus -ne 'FORCED' -or $state.shutdownError -cne 'shutdown failure' -or !$server.HasExited) { throw 'Failure cleanup lost ownership or error' }
}

Test-Case 'Stop_IsIdempotent_AndOnlyStopsTheOwnedProcess' {
    New-Scenario
    $other = & $realStart -FilePath $node -Arguments @('-e', 'setInterval(()=>{},1000)')
    $owned.Add($other)
    Start-SqlAotMySql -BinDirectory 'tools' -Directory $directory | Out-Null
    Stop-SqlAotMySql -Directory $directory
    Stop-SqlAotMySql -Directory $directory
    Stop-SqlAotMySql -Directory (Join-Path $testRoot 'never-created')
    if (!$server.HasExited -or $other.HasExited -or @($calls | Where-Object stage -eq shutdown).Count -ne 1) { throw 'Stop ownership differs' }
}

Test-Case 'Stop_RejectsMismatchedProcessIdentity_AndCanRetryWithCorrectIdentity' {
    New-Scenario
    Start-SqlAotMySql -BinDirectory 'tools' -Directory $directory | Out-Null
    $stateFile = Join-Path $directory 'state.json'
    $state = Get-Content -LiteralPath $stateFile -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $originalTicks = $state.processStartedUtcTicks
    $state.processStartedUtcTicks++
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    $message = ''
    try { Stop-SqlAotMySql -Directory $directory } catch { $message = $_.Exception.Message }
    $failed = Get-Content -LiteralPath $stateFile -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    if (!$message -or $server.HasExited -or $failed.cleanupStatus -ne 'FAIL' -or @($calls | Where-Object stage -eq shutdown).Count) { throw 'Identity mismatch touched a process' }
    $failed.processStartedUtcTicks = $originalTicks
    $failed | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    Stop-SqlAotMySql -Directory $directory
    $saved = Get-Content -LiteralPath $stateFile -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    if (!$server.HasExited -or $saved.cleanupStatus -ne 'STOPPED' -or $saved.ContainsKey('cleanupError')) { throw 'Retry kept stale failure' }
}

if ($realTool) { Set-Item -LiteralPath Function:Invoke-SqlAotMySqlTool -Value $realTool }
if ($realStart) { Set-Item -LiteralPath Function:Start-SqlAotMySqlProcess -Value $realStart }

Test-Case 'Server_RealProcess_SurvivesLauncherExit_WithoutHoldingOutputPipes' {
    $launcher = Join-Path $testRoot 'launcher.ps1'
    $pidFile = Join-Path $testRoot 'server-pid.txt'
    $argsFile = Join-Path $testRoot 'server-arguments.json'
    $source = @'
param($HelperPath, $NodePath, $PidFile, $ArgsFile)
$ErrorActionPreference = 'Stop'
. $HelperPath
$program = 'require("node:fs").writeFileSync(process.argv[1],JSON.stringify(process.argv.slice(2)),"utf8");setInterval(()=>{},1000)'
$server = Start-SqlAotMySqlProcess -FilePath $NodePath -Arguments @('-e', $program, $ArgsFile, 'space name', '中文', 'quote"value', 'tail\')
$server.Id | Set-Content -LiteralPath $PidFile -Encoding utf8
$server.Dispose()
'@
    [IO.File]::WriteAllText($launcher, $source, $utf8)
    $info = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh).Source)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $launcher, '-HelperPath', (Join-Path $PSScriptRoot '../WindowsMySql.ps1'), '-NodePath', $node, '-PidFile', $pidFile, '-ArgsFile', $argsFile)) { $info.ArgumentList.Add($argument) }
    $hostProcess = [Diagnostics.Process]::new()
    $hostProcess.StartInfo = $info
    try {
        $hostProcess.Start() | Out-Null
        $stdout = $hostProcess.StandardOutput.ReadToEndAsync()
        $stderr = $hostProcess.StandardError.ReadToEndAsync()
        if (!$hostProcess.WaitForExit(5000)) { throw 'Launcher did not exit independently' }
        if (!$stdout.Wait(1000) -or !$stderr.Wait(1000)) { throw 'Background process kept launcher output pipes open' }
        if ($hostProcess.ExitCode -ne 0) { throw $stderr.GetAwaiter().GetResult() }
        $background = [Diagnostics.Process]::GetProcessById([int][IO.File]::ReadAllText($pidFile, $utf8))
        $owned.Add($background)
        $deadline = [DateTime]::UtcNow.AddSeconds(2)
        while (!(Test-Path -LiteralPath $argsFile) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 20 }
        $actual = [IO.File]::ReadAllText($argsFile, $utf8) | ConvertFrom-Json
        if ($background.HasExited -or ($actual | ConvertTo-Json -Compress) -cne (@('space name', '中文', 'quote"value', 'tail\') | ConvertTo-Json -Compress)) { throw 'Background lifetime or arguments differ' }
    }
    finally {
        if (!$hostProcess.HasExited) { $hostProcess.Kill($true); $hostProcess.WaitForExit(5000) | Out-Null }
        if (Test-Path -LiteralPath $pidFile) {
            $background = Get-Process -Id ([int][IO.File]::ReadAllText($pidFile, $utf8)) -ErrorAction SilentlyContinue
            if ($background) { $background.Kill($true); $background.WaitForExit(5000) | Out-Null; $background.Dispose() }
        }
        $hostProcess.Dispose()
    }
}

Test-Case 'Tool_RealProcess_PreservesArgumentBoundariesAndUtf8' {
    $arguments = @('-e', 'process.stdout.write(JSON.stringify(process.argv.slice(1)))', 'space name', '中文', 'quote"value', 'tail\')
    $actual = Invoke-SqlAotMySqlTool -FilePath $node -Arguments $arguments -LogFile (Join-Path $testRoot 'arguments.log') -TimeoutSeconds 10
    if ((($actual | ConvertFrom-Json) | ConvertTo-Json -Compress) -cne ($arguments[2..5] | ConvertTo-Json -Compress)) { throw 'Argument boundaries differ' }
}

Test-Case 'Tool_RealNonzeroExit_PreservesBothUtf8StreamsAndFails' {
    $log = Join-Path $testRoot 'nonzero.log'
    $message = ''
    try { Invoke-SqlAotMySqlTool -FilePath $node -Arguments @('-e', 'process.stdout.write("标准输出");process.stderr.write("失败原因");process.exitCode=23') -LogFile $log -TimeoutSeconds 10 | Out-Null } catch { $message = $_.Exception.Message }
    if ($message -notmatch '23' -or [IO.File]::ReadAllText($log, $utf8) -cne '标准输出失败原因') { throw 'Nonzero exit or complete log differs' }
}

Test-Case 'Tool_RealTimeout_TerminatesParentAndDescendant_AndRetainsLog' {
    $log = Join-Path $testRoot 'timeout.log'
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $message = ''
    $program = 'console.log(process.pid);require("node:child_process").spawn(process.execPath,["-e","console.log(process.pid);setInterval(()=>{},1000)"],{stdio:["ignore","inherit","inherit"],windowsHide:true});setInterval(()=>{},1000)'
    try { Invoke-SqlAotMySqlTool -FilePath $node -Arguments @('-e', $program) -LogFile $log -TimeoutSeconds 2 | Out-Null } catch { $message = $_.Exception.Message }
    if ($message -notmatch 'timed out' -or $timer.Elapsed.TotalSeconds -gt 10) { throw 'Timeout was not bounded' }
    $childIds = @([IO.File]::ReadAllLines($log, $utf8) | Where-Object { $_.Trim() } | ForEach-Object { [int]$_ })
    if ($childIds.Count -ne 2) { throw 'Parent and descendant did not both run' }
    foreach ($childId in $childIds) { if (Get-Process -Id $childId -ErrorAction SilentlyContinue) { throw 'Owned process leaked' } }
}

$report = [ordered]@{ passed = @($results | Where-Object status -eq 'PASS').Count; failed = @($results | Where-Object status -eq 'FAIL').Count; tests = $results }
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testRoot 'tests.json') -Encoding utf8
$results | Format-Table -AutoSize | Out-Host
Write-Output "MYSQL_LIFECYCLE_TESTS: $testRoot"
if ($report.failed -gt 0) { exit 1 }
