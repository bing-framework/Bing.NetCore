#requires -Version 7.0
<#
.SYNOPSIS
    在固定本地 1C2G 拓扑运行一次 C1 分阶段诊断，不作容量达标判定。
#>
[CmdletBinding()]
param(
    [string]$PublishDirectory,
    [string]$ResultsDirectory,
    [string]$WorkloadPath = "$PSScriptRoot/r02-workload.json",
    [string]$CpuCollectorDirectory,
    [string]$RuntimeImage = 'mcr.microsoft.com/dotnet/aspnet:8.0',
    [string]$PostgresImage = 'postgres:16-alpine'
)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)

function Get-SqlMixedStageArguments([string]$WorkloadPath, [string]$OutputPath = '/results/stages.json') {
    return @('--mixed-stage-probe', $WorkloadPath, '1C2G', '1', $OutputPath)
}

function Get-SqlMixedStageCommand([string]$WorkloadPath, [switch]$WithCpu) {
    if ($WithCpu) {
        return @('timeout','300s','dotnet','/results/cpu-tool/SqlCpuTrace.dll','capture-workload',
            '/app/Bing.Data.Sql.Benchmarks.dll','/results/cpu','240','--') +
            (Get-SqlMixedStageArguments $WorkloadPath '{output}')
    }
    return @('timeout','240s','dotnet','/app/Bing.Data.Sql.Benchmarks.dll') + (Get-SqlMixedStageArguments $WorkloadPath)
}

function Assert-SqlMixedStageCpuReport($Capture, $Summary, [string]$BinarySha256,
    [string]$CollectorSha256, [string]$TraceSha256) {
    if ($Capture.status -cne 'PASS' -or $Capture.exitCode -ne 0 -or
        $Capture.binarySha256 -cne $BinarySha256 -or $Capture.collectorSha256 -cne $CollectorSha256 -or
        $Capture.traceSha256 -cne $TraceSha256 -or $Summary.status -cne 'PASS' -or
        $Summary.processId -ne $Capture.processId -or $Summary.traceSha256 -cne $TraceSha256 -or
        $Summary.measurementWindowValid -ne $true -or $Summary.observationWindow -cne 'measure-through-drain' -or
        $null -eq $Summary.StartMilliseconds -or $Summary.StartMilliseconds -lt 0 -or
        $null -eq $Summary.StopMilliseconds -or $Summary.StopMilliseconds -le $Summary.StartMilliseconds) {
        throw 'CPU evidence must match the measured candidate, collector, trace, PID and measurement window.'
    }
}

function Assert-SqlMixedStageReport($Report) {
    if ($Report.schema -cnotin @('SqlMixedStageDiagnostic-v1','SqlMixedStageDiagnostic-v2') -or $Report.profile -cne '1C2G' -or
        $Report.concurrency -ne 1) { throw 'Expected a 1C2G C1 stage diagnostic report.' }
    $measure = $Report.measurement.measure
    $expected = @{
        'scope-and-compose' = $measure.attempted
        'read-freeze-plan' = $measure.readAttempts
        'read-execute-and-materialize' = $measure.readAttempts
        'write-execute' = $measure.writeAttempts
    }
    $stages = @($Report.measurement.diagnosticStages)
    if ($stages.Count -ne 4 -or $measure.attempted -ne $measure.readAttempts + $measure.writeAttempts) {
        throw 'Stage report does not contain consistent request totals.'
    }
    foreach ($name in $expected.Keys) {
        $stage = @($stages | Where-Object stage -CEQ $name)
        if ($stage.Count -ne 1 -or $null -eq $stage[0].count -or $stage[0].count -ne $expected[$name]) {
            throw "Stage count differs from completed request totals: $name"
        }
        if ($Report.schema -ceq 'SqlMixedStageDiagnostic-v2') { Assert-SqlStageLatencySummary $stage[0] }
    }
    if ($Report.schema -ceq 'SqlMixedStageDiagnostic-v2') {
        $waits = $Report.measurement.diagnosticDatabaseWaits
        if ($null -eq $waits -or $waits.intervalMilliseconds -ne 100) { throw 'Database wait observations are required.' }
        foreach ($metric in @('sampleCount','activeSessionObservations','lockObservations','walSyncObservations',
            'walWriteObservations','otherIoObservations','noWaitEventObservations','otherWaitObservations')) {
            if ($null -eq $waits.$metric -or ![double]::IsFinite([double]$waits.$metric) -or
                $waits.$metric -lt 0 -or $waits.$metric -ne [Math]::Truncate([double]$waits.$metric)) {
                throw "Invalid database wait observations: $metric"
            }
        }
        $sum = $waits.lockObservations + $waits.walSyncObservations + $waits.walWriteObservations +
            $waits.otherIoObservations + $waits.noWaitEventObservations + $waits.otherWaitObservations
        if ($sum -ne $waits.activeSessionObservations -or ($waits.sampleCount -eq 0 -and $sum -ne 0) -or
            $waits.activeSessionObservations -gt $waits.sampleCount) {
            throw 'Database wait observations must cover mutually exclusive C1 active sessions.'
        }
    }
    $open = $Report.measurement.diagnosticConnectionOpen
    $executionTicks = ($stages | Where-Object stage -CIn @('read-execute-and-materialize','write-execute') |
        Measure-Object -Property totalTicks -Sum).Sum
    if ($null -eq $open -or $null -eq $open.count -or $open.count -ne $measure.attempted -or
        $null -eq $open.failed -or $open.failed -ne 0 -or $null -eq $open.totalTicks -or
        $open.totalTicks -lt 0 -or $open.totalTicks -gt $executionTicks) {
        throw 'Connection-open samples must cover every successful request within execution stage totals.'
    }
    $commands = $Report.measurement.diagnosticDatabaseCommands
    if ($null -eq $commands -or [string]::IsNullOrWhiteSpace($commands.statementsReset) -or
        [string]::IsNullOrWhiteSpace($commands.wal.statsReset)) {
        throw 'Database statement and WAL snapshots with reset identities are required.'
    }
    foreach ($pair in @(@('read',$measure.readAttempts),@('write',$measure.writeAttempts))) {
        $value = $commands.($pair[0])
        foreach ($metric in @('calls','rows','totalExecutionMilliseconds','sharedBlocksHit','sharedBlocksRead',
            'sharedBlocksDirtied','sharedBlocksWritten','blockReadMilliseconds','blockWriteMilliseconds','walRecords','walBytes')) {
            if ($null -eq $value.$metric -or ![double]::IsFinite([double]$value.$metric) -or $value.$metric -lt 0) {
                throw "Invalid database statement metric: $($pair[0]).$metric"
            }
        }
        if ($value.calls -ne $pair[1] -or $value.rows -ne $pair[1]) {
            throw 'Database calls/rows differ from successful primary-key operations.'
        }
        if (($value.calls -eq 0 -and $null -ne $value.meanExecutionMilliseconds) -or
            ($value.calls -gt 0 -and ($null -eq $value.meanExecutionMilliseconds -or
                ![double]::IsFinite([double]$value.meanExecutionMilliseconds) -or
                [Math]::Abs($value.meanExecutionMilliseconds - $value.totalExecutionMilliseconds / $value.calls) -gt 1e-9))) {
            throw 'Database statement mean does not correspond to total time and calls.'
        }
    }
    foreach ($metric in @('records','bytes','writes','syncs','writeMilliseconds','syncMilliseconds')) {
        if ($null -eq $commands.wal.$metric -or ![double]::IsFinite([double]$commands.wal.$metric) -or
            $commands.wal.$metric -lt 0) { throw "Invalid cluster WAL metric: $metric" }
    }
}

function Assert-SqlStageLatencySummary($Stage) {
    $overflow = $Stage.latencyHistogramOverflowCount
    if ($Stage.latencyBucketWidthMilliseconds -ne 0.1 -or $Stage.latencyHistogramMaxMilliseconds -ne 100 -or
        $null -eq $overflow -or ![double]::IsFinite([double]$overflow) -or $overflow -lt 0 -or
        $overflow -gt $Stage.count -or $overflow -ne [Math]::Truncate([double]$overflow)) {
        throw 'Invalid stage latency histogram bounds or overflow count.'
    }
    $previous = 0
    foreach ($pair in @(@('p50Milliseconds',0.50),@('p95Milliseconds',0.95),@('p99Milliseconds',0.99))) {
        $value = $Stage.($pair[0])
        $hasValue = $Stage.count -gt 0 -and [Math]::Ceiling($Stage.count * $pair[1]) -le $Stage.count - $overflow
        if (!$hasValue) {
            if ($null -ne $value) { throw 'Overflow or empty stage latency percentile must be null.' }
        }
        elseif ($null -eq $value -or ![double]::IsFinite([double]$value) -or $value -lt $previous -or
            $value -gt 100 -or [Math]::Abs($value * 10 - [Math]::Round($value * 10)) -gt 1e-9) {
            throw 'Invalid stage latency percentile or ordering.'
        }
        else { $previous = $value }
    }
}

function Invoke-SqlMixedStageDiagnostic {
    $ErrorActionPreference = 'Stop'
    $PSNativeCommandUseErrorActionPreference = $false
    if (!$PublishDirectory -or !$ResultsDirectory) { throw 'PublishDirectory and ResultsDirectory are required.' }
    $publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
    $workload = (Resolve-Path -LiteralPath $WorkloadPath).Path
    $root = [IO.Path]::GetFullPath($ResultsDirectory)
    if (Test-Path -LiteralPath $root) { throw 'ResultsDirectory must be new; no automatic retry.' }
    $binary = Join-Path $publish 'Bing.Data.Sql.Benchmarks.dll'
    if (!(Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'Benchmark binary is missing.' }
    $collector = if ($CpuCollectorDirectory) { (Resolve-Path -LiteralPath $CpuCollectorDirectory).Path }
    if ($collector -and !(Test-Path -LiteralPath "$collector/SqlCpuTrace.dll" -PathType Leaf)) {
        throw 'CPU collector binary is missing.'
    }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $tag = 'sql-stage-' + [Guid]::NewGuid().ToString('N')
    $application = $null; $database = $null; $network = $null; $volume = $null
    $receipt = [ordered]@{schema='SqlMixedStageEnvironment-v1'; status='RUNNING';
        expectedOutcome='EXPECTED_SUCCESS'; productionCapacityVerified=$false; capacityBudgetEvaluated=$false;
        startedAt=[DateTime]::UtcNow.ToString('o'); profile='1C2G'; concurrency=1;
        databasePlacement='separate 1CPU/512MiB container on shared Docker host';
        binarySha256=(Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash;
        workloadSha256=(Get-FileHash -LiteralPath $workload -Algorithm SHA256).Hash}
    function Invoke-SqlStageDocker([string[]]$Arguments) {
        $value = & docker @Arguments
        if ($LASTEXITCODE -ne 0) { throw "Docker $($Arguments[0]) failed: $LASTEXITCODE" }
        return ($value -join "`n").Trim()
    }
    try {
        $engine = Invoke-SqlStageDocker @('info','--format','{{json .}}') | ConvertFrom-Json
        if ($engine.OSType -cne 'linux' -or $engine.NCPU -lt 2 -or $engine.MemTotal -lt 2560MB) {
            throw 'Linux host must accommodate both the 1C2G application and 1CPU/512MiB database.'
        }
        $receipt.host = @{cpu=$engine.NCPU; memoryBytes=$engine.MemTotal; version=$engine.ServerVersion}
        $network = Invoke-SqlStageDocker @('network','create','--internal','--label',"bing.validation=$tag",$tag)
        $volume = Invoke-SqlStageDocker @('volume','create','--label',"bing.validation=$tag","$tag-data")
        # 应用和数据库没有宿主发布端口；trust 仅用于本次独占内部网络的临时测试库。
        & pwsh -NoProfile -File "$PSScriptRoot/New-SqlCapacityEnvironment.ps1" -Profile 1C2G `
            -Image $RuntimeImage -PublishDirectory $publish -ResultsDirectory "$root/app" *> "$root/app-create.log"
        $createExit = $LASTEXITCODE
        $environment = Get-Content -LiteralPath "$root/app/environment.json" -Raw -Encoding utf8 | ConvertFrom-Json
        $application = $environment.containerId
        if ($createExit -ne 0 -or $environment.status -cne 'READY') { throw 'Application environment was not READY.' }
        $null = Invoke-SqlStageDocker @('network','connect',$tag,$application)
        $null = Invoke-SqlStageDocker @('network','disconnect','bridge',$application)
        if ($collector) {
            # 通过已有结果挂载提供采集器；docker cp 不支持只读根文件系统。
            Copy-Item -LiteralPath $collector -Destination "$root/app/cpu-tool" -Recurse
            $receipt.collectorSha256 = (Get-FileHash -LiteralPath "$collector/SqlCpuTrace.dll" -Algorithm SHA256).Hash
        }
        Copy-Item -LiteralPath $workload -Destination "$root/app/workload.json"
        Copy-Item -LiteralPath "$PSScriptRoot/Sample-SqlContainerResources.sh" -Destination "$root/app/sample.sh"
        $database = Invoke-SqlStageDocker @('run','--detach','--pull','never','--name',"$tag-pg",'--network',$tag,
            '--label',"bing.validation=$tag",'--cpus','1','--memory','512m','--memory-swap','512m',
            '--mount',"type=volume,source=$volume,target=/var/lib/postgresql/data",
            '--mount',"type=bind,source=$root/app,target=/results",'--env','POSTGRES_USER=bing_aot',
            '--env','POSTGRES_DB=bing_sql_aot_test','--env','POSTGRES_HOST_AUTH_METHOD=trust',
            $PostgresImage,'-c','shared_buffers=64MB','-c','max_connections=48',
            '-c','shared_preload_libraries=pg_stat_statements','-c','track_io_timing=on','-c','track_wal_io_timing=on')
        $receipt.applicationId=$application; $receipt.databaseId=$database
        $receipt.networkId=$network; $receipt.volume=$volume
        $ready = $false
        for ($attempt=0; $attempt -lt 20; $attempt++) {
            & docker exec $database pg_isready -h 127.0.0.1 -U bing_aot -d bing_sql_aot_test *> "$root/db-ready.log"
            if ($LASTEXITCODE -eq 0) { $ready=$true; break }
            Start-Sleep -Seconds 1
        }
        if (!$ready) { throw 'Temporary database readiness timed out.' }
        $receipt.databaseLimits = @{
            cpuMax=(Invoke-SqlStageDocker @('exec',$database,'cat','/sys/fs/cgroup/cpu.max'))
            memoryMax=(Invoke-SqlStageDocker @('exec',$database,'cat','/sys/fs/cgroup/memory.max'))
            swapMax=(Invoke-SqlStageDocker @('exec',$database,'cat','/sys/fs/cgroup/memory.swap.max'))
        }
        if ($receipt.databaseLimits.cpuMax -cne '100000 100000' -or
            $receipt.databaseLimits.memoryMax -cne '536870912' -or $receipt.databaseLimits.swapMax -cne '0') {
            throw 'Database cgroup limits differ from the fixed topology.'
        }
        $receipt.durability = Invoke-SqlStageDocker @('exec',$database,'psql','-U','bing_aot','-d','bing_sql_aot_test','-Atc',
            'SHOW fsync; SHOW synchronous_commit; SHOW full_page_writes;')
        if ($receipt.durability -cne "on`non`non") { throw 'Durable PostgreSQL defaults are required.' }
        $receipt.databaseInstrumentation = Invoke-SqlStageDocker @('exec',$database,'psql','-U','bing_aot','-d','bing_sql_aot_test','-Atc',
            'SHOW shared_preload_libraries; SHOW track_io_timing; SHOW track_wal_io_timing;')
        if ($receipt.databaseInstrumentation -cne "pg_stat_statements`non`non") {
            throw 'Server statement and WAL IO timing must be enabled for this diagnostic.'
        }
        $receipt.images = @{application=$environment.imageId; database=(Invoke-SqlStageDocker @('inspect','--format','{{.Image}}',$database))}
        $receipt.runtime = Invoke-SqlStageDocker @('exec',$application,'dotnet','--info')
        foreach ($pair in @(@($application,'app'),@($database,'db'))) {
            $null = Invoke-SqlStageDocker @('exec','-d',$pair[0],'sh','/results/sample.sh',"/results/$($pair[1]).jsonl",'/results/sample.stop')
        }
        $connection = "Host=$tag-pg;Username=bing_aot;Database=bing_sql_aot_test;SSL Mode=Disable;Pooling=true;Maximum Pool Size=32"
        $arguments = Get-SqlMixedStageCommand '/results/workload.json' -WithCpu:([bool]$collector)
        & docker exec --env RUN_POSTGRESQL_INTEGRATION_TESTS=true --env "ConnectionStrings__PostgreSqlConnection=$connection" `
            $application @arguments *> "$root/probe.log"
        $receipt.probeExitCode = $LASTEXITCODE
        Start-Sleep -Seconds 2
        [IO.File]::WriteAllText("$root/app/sample.stop",'stop',[Text.UTF8Encoding]::new($false))
        $stopped = $false
        for ($attempt=0; $attempt -lt 8; $attempt++) {
            if ((Test-Path -LiteralPath "$root/app/app.jsonl.done") -and (Test-Path -LiteralPath "$root/app/db.jsonl.done")) {
                $stopped=$true; break
            }
            Start-Sleep -Seconds 1
        }
        if (!$stopped -or $receipt.probeExitCode -ne 0) { throw 'Diagnostic or resource sampler failed; no automatic retry.' }
        $reportPath = "$root/app/stages.json"
        if ($collector) {
            $runs = @(Get-ChildItem -LiteralPath "$root/app/cpu" -Directory)
            if ($runs.Count -ne 1) { throw 'Expected exactly one CPU capture; no automatic retry.' }
            $cpuRun = $runs[0].FullName
            $capture = Get-Content -LiteralPath "$cpuRun/capture.json" -Raw -Encoding utf8 | ConvertFrom-Json
            $summary = Get-Content -LiteralPath "$cpuRun/report/summary.json" -Raw -Encoding utf8 | ConvertFrom-Json
            Assert-SqlMixedStageCpuReport $capture $summary $receipt.binarySha256 $receipt.collectorSha256 `
                (Get-FileHash -LiteralPath "$cpuRun/cpu.nettrace" -Algorithm SHA256).Hash
            $receipt.cpu = @{status='PASS'; run=$runs[0].Name; processId=$capture.processId;
                traceSha256=$capture.traceSha256; observationWindow=$summary.observationWindow;
                allocationScope='Process-wide allocated-byte delta in probe.json; no allocation stack events collected'}
            $reportPath = "$cpuRun/probe.json"
        }
        $report = Get-Content -LiteralPath $reportPath -Raw -Encoding utf8 | ConvertFrom-Json
        Assert-SqlMixedStageReport $report
        if ($report.measurement.warmup.failed -ne 0 -or $report.measurement.measure.failed -ne 0) { throw 'Requests failed.' }
        foreach ($name in $report.assemblySha256.PSObject.Properties.Name) {
            if ((Get-FileHash -LiteralPath (Join-Path $publish $name) -Algorithm SHA256).Hash -cne $report.assemblySha256.$name) {
                throw "Measured assembly identity differs: $name"
            }
        }
        $receipt.assemblyIdentityVerified = $true
        foreach ($kind in @('app','db')) {
            & pwsh -NoProfile -File "$PSScriptRoot/Get-SqlContainerResourceSummary.ps1" -SamplesPath "$root/app/$kind.jsonl" `
                -WindowStartUnixMilliseconds $report.measurement.measure.startedAtUnixMilliseconds `
                -WindowStopUnixMilliseconds $report.measurement.measure.stoppedAtUnixMilliseconds `
                -ResultPath "$root/$kind-resources.json" *> "$root/$kind-resources.log"
            if ($LASTEXITCODE -ne 0) { throw 'Resource summarization failed.' }
        }
        $receipt.remainingTables = Invoke-SqlStageDocker @('exec',$database,'psql','-U','bing_aot','-d','bing_sql_aot_test','-Atc',
            "SELECT count(*) FROM pg_tables WHERE schemaname='public' AND starts_with(tablename,'sql_mixed_')")
        if ($receipt.remainingTables -cne '0') { throw 'Diagnostic did not clean its fixture.' }
        $receipt.status = 'DIAGNOSTIC_COMPLETE'
    }
    catch { $receipt.status='FAIL'; $receipt.error=$_.Exception.Message }
    finally {
        # 只删除本次创建的资源；任一清理失败都保留失败状态与原始证据。
        $cleanup = @()
        foreach ($id in @($application,$database)) {
            if (!$id) { continue }
            $null = & docker rm --force --volumes $id 2>&1
            $cleanup += @{kind='container'; id=$id; exitCode=$LASTEXITCODE}
        }
        if ($network) { $null=& docker network rm $network 2>&1; $cleanup+=@{kind='network';id=$network;exitCode=$LASTEXITCODE} }
        if ($volume) { $null=& docker volume rm $volume 2>&1; $cleanup+=@{kind='volume';id=$volume;exitCode=$LASTEXITCODE} }
        $receipt.cleanup=$cleanup
        if (@($cleanup | Where-Object exitCode -NE 0).Count -gt 0) { $receipt.status='FAIL' }
        $receipt.finishedAt=[DateTime]::UtcNow.ToString('o')
        $receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath "$root/environment.json" -Encoding utf8
    }
    $receipt | ConvertTo-Json -Depth 12
    if ($receipt.status -cne 'DIAGNOSTIC_COMPLETE') { throw 'Stage diagnostic failed; inspect environment.json.' }
}
if ($MyInvocation.InvocationName -ne '.') { Invoke-SqlMixedStageDiagnostic }
