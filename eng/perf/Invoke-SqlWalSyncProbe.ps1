#requires -Version 7.0
<#
.SYNOPSIS
    在独占临时卷上执行一次 pg_test_fsync，不启动数据库或修改业务持久化配置。
#>
[CmdletBinding()]
param([string]$ResultsDirectory, [string]$PostgresImage = 'postgres:16-alpine',
    [ValidateRange(1,10)][int]$SecondsPerTest = 1,
    [ValidateRange(30,300)][int]$TimeoutSeconds = 180)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
. "$PSScriptRoot/Get-SqlWalSyncSummary.ps1"

function Invoke-SqlWalSyncProbe {
    $ErrorActionPreference = 'Stop'
    $PSNativeCommandUseErrorActionPreference = $false
    if (!$ResultsDirectory) { throw 'ResultsDirectory is required.' }
    $root = [IO.Path]::GetFullPath($ResultsDirectory)
    if (Test-Path -LiteralPath $root) { throw 'ResultsDirectory must be new; no automatic retry.' }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $tag = 'sql-wal-' + [Guid]::NewGuid().ToString('N')
    $container = $null; $volume = $null
    $commands = [Collections.Generic.List[object]]::new()
    $receipt = [ordered]@{schema='SqlWalSyncEnvironment-v1'; status='RUNNING';
        expectedOutcome='EXPECTED_SUCCESS'; capacityBudgetEvaluated=$false; productionCapacityVerified=$false;
        startedAt=[DateTime]::UtcNow.ToString('o'); secondsPerTest=$SecondsPerTest; timeoutSeconds=$TimeoutSeconds;
        scope='isolated named disk volume on shared Docker host; no database or application workload';
        scriptSha256=(Get-FileHash -LiteralPath "$PSScriptRoot/Invoke-SqlWalSyncProbe.ps1").Hash;
        parserSha256=(Get-FileHash -LiteralPath "$PSScriptRoot/Get-SqlWalSyncSummary.ps1").Hash}
    function Invoke-WalDocker([string[]]$Arguments, [string]$LogName) {
        $output = & docker @Arguments 2>&1
        $code = $LASTEXITCODE
        $value = ($output -join "`n").Trim()
        $commands.Add(@{arguments=$Arguments; exitCode=$code; output=$value})
        if ($LogName) { [IO.File]::WriteAllText((Join-Path $root $LogName), $value + "`n", [Text.UTF8Encoding]::new($false)) }
        if ($code -ne 0) { throw "Docker $($Arguments[0]) failed: $code" }
        return $value
    }
    try {
        $hostInfo = Invoke-WalDocker @('info','--format','{{json .}}') | ConvertFrom-Json
        if ($hostInfo.OSType -cne 'linux' -or $hostInfo.NCPU -lt 1 -or $hostInfo.MemTotal -lt 768MB) {
            throw 'Linux Docker host with room for the 1CPU/512MiB probe is required.'
        }
        $receipt.host = @{cpu=$hostInfo.NCPU; memoryBytes=$hostInfo.MemTotal; version=$hostInfo.ServerVersion}
        # 只解析本地镜像为不可变ID；不拉取镜像，不挂载宿主或任何既有数据卷。
        $image = Invoke-WalDocker @('image','inspect','--format','{{.Id}}',$PostgresImage)
        $receipt.image = @{requested=$PostgresImage; id=$image}
        $volume = Invoke-WalDocker @('volume','create','--label',"bing.validation=$tag","$tag-data")
        $receipt.volume = Invoke-WalDocker @('volume','inspect',$volume) | ConvertFrom-Json
        $container = Invoke-WalDocker @('create','--pull','never','--name',$tag,'--label',"bing.validation=$tag",
            '--network','none','--cpus','1','--memory','512m','--memory-swap','512m','--read-only',
            '--cap-drop','ALL','--security-opt','no-new-privileges','--env','LC_ALL=C',
            '--mount',"type=volume,source=$volume,target=/var/lib/postgresql/data",'--entrypoint','sh',$image,'-c','sleep 600')
        $receipt.containerId = $container
        $null = Invoke-WalDocker @('start',$container)
        $inspection = @(Invoke-WalDocker @('inspect',$container) | ConvertFrom-Json)[0]
        $config = $inspection.HostConfig
        $mounts = @($inspection.Mounts)
        if ($inspection.Image -cne $image -or $config.NanoCpus -ne 1000000000 -or
            $config.Memory -ne 512MB -or $config.MemorySwap -ne 512MB -or
            $config.NetworkMode -cne 'none' -or !$config.ReadonlyRootfs -or $mounts.Count -ne 1 -or
            $mounts[0].Type -cne 'volume' -or $mounts[0].Name -cne $volume -or $mounts[0].Destination -cne '/var/lib/postgresql/data') {
            throw 'Container identity, isolation or resource configuration differs from the probe contract.'
        }
        $receipt.limits = @{
            cpuMax=(Invoke-WalDocker @('exec',$container,'cat','/sys/fs/cgroup/cpu.max'))
            memoryMax=(Invoke-WalDocker @('exec',$container,'cat','/sys/fs/cgroup/memory.max'))
            swapMax=(Invoke-WalDocker @('exec',$container,'cat','/sys/fs/cgroup/memory.swap.max'))
        }
        if ($receipt.limits.cpuMax -cne '100000 100000' -or $receipt.limits.memoryMax -cne '536870912' -or
            $receipt.limits.swapMax -cne '0') { throw 'Actual cgroup limits differ from the probe contract.' }
        $receipt.toolVersion = Invoke-WalDocker @('exec',$container,'pg_test_fsync','--version')
        if ($receipt.toolVersion -cnotmatch '^pg_test_fsync \(PostgreSQL\) 16\.') {
            throw 'The output parser supports PostgreSQL 16 only.'
        }
        $hash = Invoke-WalDocker @('exec',$container,'sh','-c','sha256sum "$(command -v pg_test_fsync)"')
        $receipt.toolSha256 = ($hash -split '\s+')[0]
        $receipt.fileSystem = Invoke-WalDocker @('exec',$container,'stat','-f','-c','%T','/var/lib/postgresql/data')
        $receipt.kernel = Invoke-WalDocker @('exec',$container,'uname','-a')
        $raw = Invoke-WalDocker @('exec',$container,'timeout','-k','5',"$TimeoutSeconds",'pg_test_fsync',
            '-f','/var/lib/postgresql/data/fsync.out','-s',"$SecondsPerTest") 'pg-test-fsync.log'
        $summary = ConvertFrom-SqlWalSyncOutput -Text $raw -ExpectedSecondsPerTest $SecondsPerTest
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$root/summary.json" -Encoding utf8
        $receipt.rawOutputSha256 = (Get-FileHash -LiteralPath "$root/pg-test-fsync.log").Hash
        $receipt.summarySha256 = (Get-FileHash -LiteralPath "$root/summary.json").Hash
        $receipt.status = 'DIAGNOSTIC_COMPLETE'
    }
    catch { $receipt.status='FAIL'; $receipt.error=$_.Exception.Message }
    finally {
        # 仅清理本次创建的具体资源ID；保留原始失败及独立清理退出码。
        $cleanup = @()
        foreach ($resource in @(@('container',$container),@('volume',$volume))) {
            if (!$resource[1]) { continue }
            $arguments = if ($resource[0] -ceq 'container') { @('rm','--force','--volumes',$resource[1]) } else { @('volume','rm',$resource[1]) }
            $output = & docker @arguments 2>&1
            $cleanup += @{kind=$resource[0]; id=$resource[1]; exitCode=$LASTEXITCODE; output=($output -join "`n")}
        }
        $receipt.cleanup=$cleanup
        if (@($cleanup | Where-Object exitCode -NE 0).Count) { $receipt.status='FAIL' }
        $receipt.finishedAt=[DateTime]::UtcNow.ToString('o')
        $receipt.commands=@($commands)
        $receipt | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$root/environment.json" -Encoding utf8
    }
    $receipt | ConvertTo-Json -Depth 10
    if ($receipt.status -cne 'DIAGNOSTIC_COMPLETE') { throw 'WAL sync diagnostic failed; inspect environment.json.' }
}
if ($MyInvocation.InvocationName -ne '.') { Invoke-SqlWalSyncProbe }
