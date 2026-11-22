#requires -Version 7.0

# 仅管理本次 CI 创建的数据目录，不启动、停止或修改机器上预装的服务。
function Invoke-SqlAotPgTool {
    param([string]$FilePath, [string[]]$Arguments, [string]$LogFile, [int]$TimeoutSeconds = 75)

    $utf8 = [Text.UTF8Encoding]::new($false)
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $FilePath
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $utf8
    $info.StandardErrorEncoding = $utf8
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (!$process.Start()) { throw "Could not start PostgreSQL tool: $FilePath" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = !$process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            # 只终止本调用启动的进程树，超时仍保留已经输出的诊断。
            if (!$process.HasExited) { $process.Kill($true) }
            if (!$process.WaitForExit(5000)) { throw "PostgreSQL tool did not terminate: $FilePath" }
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($LogFile, $output + $errorOutput, $utf8)
        if ($timedOut) { throw "PostgreSQL tool timed out: $FilePath; $LogFile" }
        if ($process.ExitCode -ne 0) { throw "PostgreSQL tool exit $($process.ExitCode): $FilePath; $LogFile" }
        return $output
    }
    finally { $process.Dispose() }
}

function Stop-SqlAotPostgreSql {
    param([string]$Directory)

    $stateFile = Join-Path $Directory 'state.json'
    if (!(Test-Path -LiteralPath $stateFile)) { return }
    $state = Get-Content -LiteralPath $stateFile -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    try {
        if (Test-Path -LiteralPath (Join-Path $state.dataDirectory 'postmaster.pid')) {
            Invoke-SqlAotPgTool -FilePath (Join-Path $state.binDirectory 'pg_ctl.exe') `
                -Arguments @('-D', $state.dataDirectory, '-m', 'immediate', '-w', '-t', '10', 'stop') `
                -LogFile (Join-Path $Directory 'stop.log') -TimeoutSeconds 20 | Out-Null
        }
        $state.cleanupStatus = 'STOPPED'
        $state.Remove('cleanupError') | Out-Null
    }
    catch {
        $state.cleanupStatus = 'FAIL'
        $state.cleanupError = $_.Exception.Message
        throw
    }
    finally {
        $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    }
}

function Start-SqlAotPostgreSql {
    param([string]$BinDirectory, [string]$Directory, [string]$EnvironmentFile)

    if ([string]::IsNullOrWhiteSpace($BinDirectory)) { throw 'PGBIN is required for Windows PostgreSQL Native CI.' }
    $Directory = [IO.Path]::GetFullPath($Directory)
    if (Test-Path -LiteralPath $Directory) { throw "PostgreSQL fixture directory already exists: $Directory" }
    $data = Join-Path $Directory 'data'
    # 让 Windows initdb 自己创建目标目录，避免对预先存在的目录修正 POSIX 权限。
    [IO.Directory]::CreateDirectory($Directory) | Out-Null
    $stateFile = Join-Path $Directory 'state.json'
    $state = [ordered]@{
        status = 'STARTING'; binDirectory = $BinDirectory; dataDirectory = $data
        port = 0; version = ''; cleanupStatus = 'NOT_RUN'; startedAt = [DateTime]::UtcNow.ToString('o')
    }
    # 在可能启动后台服务之前记录所有权，后续 always 步骤和失败路径共用这份记录。
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    try {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        try { $listener.Start(); $state.port = $listener.LocalEndpoint.Port }
        finally { $listener.Stop() }
        $state.version = (Invoke-SqlAotPgTool -FilePath (Join-Path $BinDirectory 'pg_ctl.exe') `
            -Arguments @('--version') -LogFile (Join-Path $Directory 'version.log') -TimeoutSeconds 10).Trim()
        Invoke-SqlAotPgTool -FilePath (Join-Path $BinDirectory 'initdb.exe') `
            -Arguments @('-D', $data, '--username=bing_aot', '--encoding=UTF8', '--locale=C', '--auth=trust', '--no-instructions') `
            -LogFile (Join-Path $Directory 'initdb.log') -TimeoutSeconds 75 | Out-Null
        Invoke-SqlAotPgTool -FilePath (Join-Path $BinDirectory 'pg_ctl.exe') `
            -Arguments @('-D', $data, '-l', (Join-Path $Directory 'server.log'), '-o', "-h 127.0.0.1 -p $($state.port)", '-w', '-t', '60', 'start') `
            -LogFile (Join-Path $Directory 'start.log') -TimeoutSeconds 75 | Out-Null
        Invoke-SqlAotPgTool -FilePath (Join-Path $BinDirectory 'createdb.exe') `
            -Arguments @('--host=127.0.0.1', "--port=$($state.port)", '--username=bing_aot', '--maintenance-db=postgres', '-w', 'bing_sql_aot_test') `
            -LogFile (Join-Path $Directory 'createdb.log') -TimeoutSeconds 30 | Out-Null
        $state.status = 'READY'
        $state.connectionString = "Host=127.0.0.1;Port=$($state.port);Database=bing_sql_aot_test;Username=bing_aot;Pooling=false"
        if ($EnvironmentFile) {
            [IO.File]::AppendAllText($EnvironmentFile,
                "RUN_POSTGRESQL_INTEGRATION_TESTS=true`nConnectionStrings__PostgreSqlConnection=$($state.connectionString)`n",
                [Text.UTF8Encoding]::new($false))
        }
    }
    catch {
        $original = $_
        $state.status = 'FAIL'
        $state.error = $_.Exception.Message
        try {
            Stop-SqlAotPostgreSql -Directory $Directory
            $state.cleanupStatus = 'STOPPED'
        }
        catch {
            $state.cleanupStatus = 'FAIL'
            $state.cleanupError = $_.Exception.Message
        }
        throw $original
    }
    finally {
        $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stateFile -Encoding utf8
    }
    return $state
}
