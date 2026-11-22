#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot '../Invoke-WindowsSqlServer.ps1'
if (!(Test-Path -LiteralPath $scriptPath)) { throw 'Missing Windows SQL Server lifecycle script.' }
. $scriptPath

$root = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path ('output/sql-aot-sqlserver-tests/run-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
$results = @()

function Test-Case([string]$Name, [scriptblock]$Body) {
    $script:directory = Join-Path $root ($Name + " 中文 ' path")
    $script:environmentFile = Join-Path $root ($Name + '.env')
    $script:commands = [Collections.Generic.List[object]]::new()
    $script:failAt = ''
    try {
        & $Body
        $script:results += [pscustomobject]@{ name = $Name; status = 'PASS' }
    }
    catch { $script:results += [pscustomobject]@{ name = $Name; status = 'FAIL'; error = $_.Exception.Message } }
}

# 替换原生工具，保留环境文件与实例记录的实际读写。
function Find-SqlAotLocalDbTool([string]$Name) { return $Name }
function Invoke-SqlAotLocalDbTool([string]$FilePath, [string[]]$Arguments, [string]$LogFile) {
    $stage = if ($FilePath -eq 'sqlcmd.exe') { 'database' } else { $Arguments[0] }
    $script:commands.Add([pscustomobject]@{ stage = $stage; arguments = $Arguments })
    if ($stage -eq 'start' -and !(Test-Path -LiteralPath (Join-Path $directory 'instance.txt'))) {
        throw 'The cleanup receipt must exist before startup.'
    }
    if ($stage -eq $failAt) { throw "$stage original failure" }
    Set-Content -LiteralPath $LogFile -Value "fixture $stage" -Encoding utf8
}

Test-Case 'Start_UsesOwnedInstanceAndPublishesEnvironmentAfterDatabaseCreation' {
    Start-SqlAotLocalDb -Directory $directory -EnvironmentFile $environmentFile
    $name = (Get-Content -LiteralPath (Join-Path $directory 'instance.txt') -Raw -Encoding utf8).Trim()
    if ($name -cnotmatch '^BingSqlAot[0-9a-f]{32}$' -or ($commands.stage -join ',') -cne 'create,start,database') {
        throw 'Expected a private instance and create/start/database in order.'
    }
    $data = (Join-Path $directory 'fixture.mdf').Replace("'", "''")
    $log = (Join-Path $directory 'fixture.ldf').Replace("'", "''")
    $sql = "CREATE DATABASE bing_sql_aot_test ON (NAME=N'bing_sql_aot_test', FILENAME=N'$data') LOG ON (NAME=N'bing_sql_aot_test_log', FILENAME=N'$log'); SELECT CONVERT(varchar(50), SERVERPROPERTY('ProductVersion'));"
    if ($commands[2].arguments[-1] -cne $sql -or $commands[2].arguments[1] -cne "(localdb)\$name") {
        throw 'The isolated database command or endpoint differs.'
    }
    $expected = "RUN_SQLSERVER_INTEGRATION_TESTS=true`nConnectionStrings__SqlServerConnection=Server=(localdb)\$name;Database=bing_sql_aot_test;Integrated Security=true;TrustServerCertificate=true;Pooling=false`n"
    if ((Get-Content -LiteralPath $environmentFile -Raw -Encoding utf8) -cne $expected) { throw 'Unexpected SQL Server integration environment.' }
    [IO.File]::WriteAllBytes((Join-Path $directory 'fixture.mdf'), [byte[]]@(1))
    [IO.File]::WriteAllBytes((Join-Path $directory 'fixture.ldf'), [byte[]]@(1))
    Stop-SqlAotLocalDb -Directory $directory
    Stop-SqlAotLocalDb -Directory $directory
    if (($commands.stage -join ',') -cne 'create,start,database,stop,delete' -or $commands[4].arguments[1] -cne $name) {
        throw 'Expected the owned instance to be stopped and deleted once.'
    }
    if (Test-Path -LiteralPath (Join-Path $directory 'instance.txt'), (Join-Path $directory 'fixture.mdf'), (Join-Path $directory 'fixture.ldf') | Where-Object { $_ }) {
        throw 'Owned database files or active receipt remained after cleanup.'
    }
}

Test-Case 'Start_WhenDatabaseCreationFails_PreservesFailureAndRemovesOwnedInstance' {
    $script:failAt = 'database'
    try {
        Start-SqlAotLocalDb -Directory $directory -EnvironmentFile $environmentFile
        throw 'Expected startup to fail.'
    }
    catch { if ($_.Exception.Message -cne 'database original failure') { throw } }
    if (($commands.stage -join ',') -cne 'create,start,database,stop,delete' -or (Test-Path -LiteralPath $environmentFile)) {
        throw 'Startup failure must clean up without enabling integration.'
    }
    if (Test-Path -LiteralPath (Join-Path $directory 'instance.txt')) { throw 'The active receipt remained.' }
}

Test-Case 'Stop_WithUnownedReceipt_DoesNotInvokeTools' {
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    Set-Content -LiteralPath (Join-Path $directory 'instance.txt') -Value 'MSSQLLocalDB' -Encoding utf8
    try {
        Stop-SqlAotLocalDb -Directory $directory
        throw 'Expected an invalid receipt to fail.'
    }
    catch { if ($_.Exception.Message -notlike 'Invalid LocalDB instance receipt*') { throw } }
    if ($commands.Count -ne 0) { throw 'Cleanup touched an unowned instance.' }
}

$results | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding utf8
$results | Format-Table -AutoSize
if ($results.status -contains 'FAIL') { exit 1 }
