#requires -Version 7.0

function Invoke-SqlAotMySqlTool {
    param([string]$FilePath, [string[]]$Arguments, [string]$LogFile, [int]$TimeoutSeconds = 75)

    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FilePath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $info.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (!$process.Start()) { throw "Could not start MySQL tool: $FilePath" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = !$process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            if (!$process.HasExited) { $process.Kill($true) }
            if (!$process.WaitForExit(5000)) { throw "MySQL tool did not terminate: $FilePath" }
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($LogFile, $output + $errorOutput, [Text.UTF8Encoding]::new($false))
        if ($timedOut) { throw "MySQL tool timed out: $FilePath; $LogFile" }
        if ($process.ExitCode -ne 0) { throw "MySQL tool exit $($process.ExitCode): $FilePath; $LogFile" }
        return $output
    }
    finally { $process.Dispose() }
}

# 服务端通过 Windows shell 隐藏启动，避免继承 CI 步骤的输出管道；日志由 mysqld 自行写入。
function Start-SqlAotMySqlProcess {
    param([string]$FilePath, [string[]]$Arguments)

    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FilePath
    $info.UseShellExecute = $true
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (!$process.Start()) { throw "Could not start MySQL server: $FilePath" }
        return $process
    }
    catch { $process.Dispose(); throw }
}

function Stop-SqlAotMySql {
    param([string]$Directory)

    $stateFile = Join-Path $Directory 'state.json'
    if (!(Test-Path -LiteralPath $stateFile)) { return }
    $state = Get-Content -LiteralPath $stateFile -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    $process = $null
    try {
        if ($state.processId) {
            try { $process = [Diagnostics.Process]::GetProcessById($state.processId) }
            catch [ArgumentException] { $process = $null }
        }
        if ($process) {
            # PID 可能复用，启动身份不一致时不得向端口发 shutdown 或终止该进程。
            if ($process.StartTime.ToUniversalTime().Ticks -ne $state.processStartedUtcTicks) { throw 'MySQL process identity changed; refusing cleanup.' }
            try {
                Invoke-SqlAotMySqlTool -FilePath (Join-Path $state.binDirectory 'mysqladmin.exe') `
                    -Arguments @('--no-defaults', '--no-login-paths', '--protocol=TCP', '--host=127.0.0.1', "--port=$($state.port)", '--user=root', '--skip-password', '--connect-timeout=2', '--shutdown-timeout=10', 'shutdown') `
                    -LogFile (Join-Path $Directory 'stop.log') -TimeoutSeconds 15 | Out-Null
            }
            catch { $state.shutdownError = $_.Exception.Message }
            if (!$process.WaitForExit(10000)) {
                $process.Kill($true)
                if (!$process.WaitForExit(5000)) { throw 'Owned MySQL server did not terminate.' }
                $state.cleanupStatus = 'FORCED'
            }
            else { $state.cleanupStatus = 'STOPPED' }
        }
        elseif ($state.cleanupStatus -ne 'FORCED') { $state.cleanupStatus = 'STOPPED' }
        $state.Remove('cleanupError') | Out-Null
    }
    catch {
        $state.cleanupStatus = 'FAIL'
        $state.cleanupError = $_.Exception.Message
        throw
    }
    finally {
        if ($process) { $process.Dispose() }
        $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    }
}

function Start-SqlAotMySql {
    param([string]$BinDirectory, [string]$Directory, [string]$EnvironmentFile)

    if (!$BinDirectory) {
        $command = Get-Command mysqld.exe -CommandType Application -ErrorAction SilentlyContinue
        if (!$command) { throw 'MySQL bin directory is required; mysqld.exe was not found on PATH.' }
        $BinDirectory = Split-Path $command.Source -Parent
    }
    $BinDirectory = [IO.Path]::GetFullPath($BinDirectory)
    $Directory = [IO.Path]::GetFullPath($Directory)
    if (Test-Path -LiteralPath $Directory) { throw "MySQL fixture directory already exists: $Directory" }
    $data = Join-Path $Directory 'data'
    [IO.Directory]::CreateDirectory($data) | Out-Null
    $stateFile = Join-Path $Directory 'state.json'
    $state = [ordered]@{
        status = 'STARTING'; binDirectory = $BinDirectory; dataDirectory = $data
        port = 0; version = ''; processId = 0; processStartedUtcTicks = 0
        cleanupStatus = 'NOT_RUN'; startedAt = [DateTime]::UtcNow.ToString('o')
    }
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    $server = $null
    try {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        try { $listener.Start(); $state.port = $listener.LocalEndpoint.Port }
        finally { $listener.Stop() }
        $mysqld = Join-Path $BinDirectory 'mysqld.exe'
        $base = Split-Path $BinDirectory -Parent
        $state.version = (Invoke-SqlAotMySqlTool -FilePath $mysqld -Arguments @('--no-defaults', '--version') `
            -LogFile (Join-Path $Directory 'version.log') -TimeoutSeconds 10).Trim()
        Invoke-SqlAotMySqlTool -FilePath $mysqld `
            -Arguments @('--no-defaults', "--basedir=$base", "--datadir=$data", '--initialize-insecure', '--console') `
            -LogFile (Join-Path $Directory 'initialize.log') -TimeoutSeconds 75 | Out-Null
        $server = Start-SqlAotMySqlProcess -FilePath $mysqld `
            -Arguments @('--no-defaults', "--basedir=$base", "--datadir=$data", '--bind-address=127.0.0.1', "--port=$($state.port)", '--mysqlx=0', '--character-set-server=utf8mb4', "--log-error=$(Join-Path $Directory 'server.log')")
        $state.processId = $server.Id
        $state.processStartedUtcTicks = $server.StartTime.ToUniversalTime().Ticks
        # 进入等待或建库前保存进程所有权；失败路径和 always Stop 均使用该记录。
        $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
        $connection = @('--no-defaults', '--no-login-paths', '--protocol=TCP', '--host=127.0.0.1', "--port=$($state.port)", '--user=root', '--skip-password', '--connect-timeout=2')
        Invoke-SqlAotMySqlTool -FilePath (Join-Path $BinDirectory 'mysqladmin.exe') -Arguments ($connection + @('--wait=30', 'ping')) `
            -LogFile (Join-Path $Directory 'ready.log') -TimeoutSeconds 75 | Out-Null
        if ($server.HasExited) { throw "Owned MySQL server exited before database setup; $(Join-Path $Directory 'server.log')" }
        $sql = "CREATE DATABASE ``bing_sql_aot_test`` CHARACTER SET utf8mb4; CREATE USER 'bing_aot'@'127.0.0.1' IDENTIFIED BY 'local_aot_test_password'; GRANT ALL PRIVILEGES ON ``bing_sql_aot_test``.* TO 'bing_aot'@'127.0.0.1';"
        Invoke-SqlAotMySqlTool -FilePath (Join-Path $BinDirectory 'mysql.exe') -Arguments ($connection + @('--default-character-set=utf8mb4', "--execute=$sql")) `
            -LogFile (Join-Path $Directory 'create.log') -TimeoutSeconds 30 | Out-Null
        $state.status = 'READY'
        $state.connectionString = "Server=127.0.0.1;Port=$($state.port);Database=bing_sql_aot_test;User ID=bing_aot;Password=local_aot_test_password;Pooling=false;SslMode=None;AllowPublicKeyRetrieval=true"
        if ($EnvironmentFile) {
            [IO.File]::AppendAllText($EnvironmentFile, "RUN_MYSQL_INTEGRATION_TESTS=true`nConnectionStrings__MySqlConnection=$($state.connectionString)`n", [Text.UTF8Encoding]::new($false))
        }
    }
    catch {
        $original = $_
        $state.status = 'FAIL'
        $state.error = $_.Exception.Message
        try {
            Stop-SqlAotMySql -Directory $Directory
            $cleanup = Get-Content -LiteralPath $stateFile -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
            $state.cleanupStatus = $cleanup.cleanupStatus
            if ($cleanup.ContainsKey('shutdownError')) { $state.shutdownError = $cleanup.shutdownError }
        }
        catch { $state.cleanupStatus = 'FAIL'; $state.cleanupError = $_.Exception.Message }
        throw $original
    }
    finally {
        if ($server) { $server.Dispose() }
        $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    }
    return $state
}
