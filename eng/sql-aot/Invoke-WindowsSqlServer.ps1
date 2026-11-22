#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Start', 'Stop')][string]$Action,
    [string]$Directory = (Join-Path $PSScriptRoot '../../output/sql-aot-sqlserver'),
    [string]$EnvironmentFile = $env:GITHUB_ENV
)

function Find-SqlAotLocalDbTool([string]$Name) {
    $tool = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($tool) { return $tool.Source }
    $installation = Join-Path $env:ProgramFiles 'Microsoft SQL Server'
    $tool = Get-ChildItem -LiteralPath $installation -Filter $Name -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($tool) { return $tool.FullName }
    throw "Required Windows SQL Server tool was not found: $Name"
}

function Invoke-SqlAotLocalDbTool([string]$FilePath, [string[]]$Arguments, [string]$LogFile) {
    $PSNativeCommandUseErrorActionPreference = $false
    $previousEncoding = [Console]::OutputEncoding
    try {
        [Console]::OutputEncoding = [Text.Encoding]::GetEncoding((Get-Culture).TextInfo.OEMCodePage)
        & $FilePath @Arguments 2>&1 | Out-File -LiteralPath $LogFile -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw "SQL Server tool exit ${LASTEXITCODE}: $FilePath. See $LogFile" }
    }
    finally { [Console]::OutputEncoding = $previousEncoding }
}

function Start-SqlAotLocalDb([string]$Directory, [string]$EnvironmentFile) {
    $Directory = [IO.Path]::GetFullPath($Directory)
    if (!$EnvironmentFile) { throw 'GITHUB_ENV or -EnvironmentFile is required for Windows SQL Server Native CI.' }
    if (Test-Path -LiteralPath $Directory) { throw "SQL Server fixture directory already exists: $Directory" }
    $localDb = Find-SqlAotLocalDbTool 'SqlLocalDB.exe'
    $sqlcmd = Find-SqlAotLocalDbTool 'sqlcmd.exe'
    [IO.Directory]::CreateDirectory($Directory) | Out-Null
    $name = 'BingSqlAot' + [Guid]::NewGuid().ToString('N')
    $endpoint = "(localdb)\$name"
    $receipt = Join-Path $Directory 'instance.txt'
    $data = (Join-Path $Directory 'fixture.mdf').Replace("'", "''")
    $log = (Join-Path $Directory 'fixture.ldf').Replace("'", "''")
    $sql = "CREATE DATABASE bing_sql_aot_test ON (NAME=N'bing_sql_aot_test', FILENAME=N'$data') LOG ON (NAME=N'bing_sql_aot_test_log', FILENAME=N'$log'); SELECT CONVERT(varchar(50), SERVERPROPERTY('ProductVersion'));"

    Invoke-SqlAotLocalDbTool $localDb @('create', $name) (Join-Path $Directory 'create.log')
    # 在启动前记录实例名，失败路径和 CI 的 always 步骤使用同一记录。
    Set-Content -LiteralPath $receipt -Value $name -Encoding utf8
    try {
        Invoke-SqlAotLocalDbTool $localDb @('start', $name) (Join-Path $Directory 'start.log')
        Invoke-SqlAotLocalDbTool $sqlcmd @('-S', $endpoint, '-E', '-C', '-b', '-l', '15', '-t', '15', '-Q', $sql) (Join-Path $Directory 'database.log')
        $connection = "Server=$endpoint;Database=bing_sql_aot_test;Integrated Security=true;TrustServerCertificate=true;Pooling=false"
        [IO.File]::AppendAllText($EnvironmentFile,
            "RUN_SQLSERVER_INTEGRATION_TESTS=true`nConnectionStrings__SqlServerConnection=$connection`n",
            [Text.UTF8Encoding]::new($false))
    }
    catch {
        try { Stop-SqlAotLocalDb -Directory $Directory }
        catch { Write-Warning $_.Exception.Message }
        throw
    }
}

function Stop-SqlAotLocalDb([string]$Directory) {
    $receipt = Join-Path $Directory 'instance.txt'
    if (!(Test-Path -LiteralPath $receipt)) { return }
    $name = (Get-Content -LiteralPath $receipt -Raw -Encoding utf8).Trim()
    if ($name -cnotmatch '^BingSqlAot[0-9a-f]{32}$') { throw "Invalid LocalDB instance receipt: $receipt" }
    $localDb = Find-SqlAotLocalDbTool 'SqlLocalDB.exe'
    Invoke-SqlAotLocalDbTool $localDb @('stop', $name, '-i') (Join-Path $Directory 'stop.log')
    Invoke-SqlAotLocalDbTool $localDb @('delete', $name) (Join-Path $Directory 'delete.log')
    Remove-Item -LiteralPath $receipt
    foreach ($file in 'fixture.mdf', 'fixture.ldf') {
        $path = Join-Path $Directory $file
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    switch ($Action) {
        'Start' { Start-SqlAotLocalDb -Directory $Directory -EnvironmentFile $EnvironmentFile }
        'Stop' { Stop-SqlAotLocalDb -Directory $Directory }
        default { throw 'Specify -Action Start or -Action Stop.' }
    }
}
