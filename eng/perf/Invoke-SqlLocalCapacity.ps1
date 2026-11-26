#requires -Version 7.0
<#
.SYNOPSIS
    使用同一干净构建候选执行已批准的本地容量矩阵；首个失败即停止后续档位。
#>
[CmdletBinding()]
param(
    [string]$BuildReceiptPath,
    [string]$ResultsDirectory,
    [string]$BudgetPath = "$PSScriptRoot/r02-local-budget.json",
    [string]$WorkloadPath = "$PSScriptRoot/r02-workload.json",
    [int[]]$Concurrency = @(1,8,16,32,64),
    [string]$RuntimeImage = 'mcr.microsoft.com/dotnet/aspnet:8.0',
    [string]$PostgresImage = 'postgres:16-alpine'
)
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)

function Get-SqlLocalCapacityCells($Budget, [int[]]$Concurrency) {
    if ($Budget.schema -cne 'SqlLocalRegressionBudget-v1' -or $Budget.approvalStatus -cne 'APPROVED' -or
        !$Budget.approvalId -or $Budget.profile -cne '1C2G' -or $Budget.maxFailureRate -ne 0 -or
        $Budget.maxWarmupFailures -ne 0 -or $Budget.application.cpus -ne 1 -or
        $Budget.application.memoryBytes -ne 2GB -or $Budget.application.swapBytes -ne 0 -or
        $Budget.database.cpus -ne 1 -or $Budget.database.memoryBytes -ne 512MB -or $Budget.database.swapBytes -ne 0) {
        throw 'An approved 1C2G local regression budget with the fixed topology is required.'
    }
    if (!$Concurrency.Count -or @($Concurrency | Select-Object -Unique).Count -ne $Concurrency.Count) {
        throw 'Select at least one distinct concurrency cell.'
    }
    foreach ($value in $Concurrency) {
        $cell=@($Budget.capacity | Where-Object concurrency -EQ $value)
        if ($cell.Count -ne 1 -or $cell[0].minRequestsPerSecond -le 0 -or $cell[0].maxP99Milliseconds -le 0) {
            throw "Missing approved capacity budget for concurrency $value."
        }
        $cell[0]
    }
}

function Get-SqlLocalCapacityCommand([int]$Concurrency) {
    return @('timeout','240s','dotnet','/app/Bing.Data.Sql.Benchmarks.dll',
        '--mixed-capacity-probe','/results/workload.json','1C2G',"$Concurrency","/results/c$Concurrency.json")
}

function Invoke-SqlLocalCapacity {
    $ErrorActionPreference='Stop'
    $PSNativeCommandUseErrorActionPreference=$false
    if (!$BuildReceiptPath -or !$ResultsDirectory) { throw 'BuildReceiptPath and ResultsDirectory are required.' }
    $buildFile=(Resolve-Path -LiteralPath $BuildReceiptPath).Path
    $budgetFile=(Resolve-Path -LiteralPath $BudgetPath).Path
    $workload=(Resolve-Path -LiteralPath $WorkloadPath).Path
    $budget=Get-Content -LiteralPath $budgetFile -Raw -Encoding utf8 | ConvertFrom-Json
    $cells=@(Get-SqlLocalCapacityCells $budget $Concurrency)
    $build=Get-Content -LiteralPath $buildFile -Raw -Encoding utf8 | ConvertFrom-Json
    if (!$build.runtime.manifest) { throw 'A clean-build receipt with a complete runtime manifest is required.' }
    $publish=(Get-Item -LiteralPath $build.binary.file).DirectoryName
    $sourceHash=(Get-FileHash -LiteralPath $build.source.manifest -Algorithm SHA256).Hash
    $runtimeHash=(Get-FileHash -LiteralPath $build.runtime.manifest -Algorithm SHA256).Hash
    $root=[IO.Path]::GetFullPath($ResultsDirectory)
    if (Test-Path -LiteralPath $root) { throw 'ResultsDirectory must be new; no automatic retry.' }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    $tag='sql-local-' + [Guid]::NewGuid().ToString('N')
    $application=$null; $database=$null; $network=$null; $volume=$null
    $receipt=[ordered]@{schema='SqlLocalCapacityEnvironment-v1'; status='RUNNING'; capacityStatus='NOT_RUN';
        productionCapacityVerified=$false; startedAt=[DateTime]::UtcNow.ToString('o'); profile='1C2G';
        selectedConcurrency=@($cells.concurrency); cells=@(); buildReceiptSha256=(Get-FileHash -LiteralPath $buildFile).Hash;
        sourceManifestSha256=$sourceHash; runtimeManifestSha256=$runtimeHash;
        budgetSha256=(Get-FileHash -LiteralPath $budgetFile).Hash; approvalId=$budget.approvalId;
        workloadSha256=(Get-FileHash -LiteralPath $workload).Hash;
        databasePlacement='separate 1CPU/512MiB container on shared Docker host'}
    function Invoke-SqlLocalDocker([string[]]$Arguments) {
        $value=& docker @Arguments
        if ($LASTEXITCODE -ne 0) { throw "Docker $($Arguments[0]) failed: $LASTEXITCODE" }
        return ($value -join "`n").Trim()
    }
    function Assert-BuildCandidate([string]$Phase) {
        & pwsh -NoProfile -File "$PSScriptRoot/Invoke-SqlBuildBudgetGate.ps1" -ReceiptPath $buildFile `
            -ExpectedSourceManifestSha256 $sourceHash -ExpectedBinarySha256 $build.binary.sha256 `
            -ExpectedRuntimeManifestSha256 $runtimeHash -BudgetApprovalId $budget.approvalId `
            -MaxCommittedMiB $budget.build.maxCommittedMiB -ResultPath "$root/build-$Phase.json" *> "$root/build-$Phase.log"
        if ($LASTEXITCODE -ne 0) { throw "Build budget or full runtime identity check failed: $Phase." }
    }
    try {
        Assert-BuildCandidate 'before'
        $engine=Invoke-SqlLocalDocker @('info','--format','{{json .}}') | ConvertFrom-Json
        if ($engine.OSType -cne 'linux' -or $engine.NCPU -lt 2 -or $engine.MemTotal -lt 2560MB) {
            throw 'Linux host must accommodate both the 1C2G application and 1CPU/512MiB database.'
        }
        $receipt.host=@{cpu=$engine.NCPU; memoryBytes=$engine.MemTotal; version=$engine.ServerVersion}
        $network=Invoke-SqlLocalDocker @('network','create','--internal','--label',"bing.validation=$tag",$tag)
        $volume=Invoke-SqlLocalDocker @('volume','create','--label',"bing.validation=$tag","$tag-data")
        & pwsh -NoProfile -File "$PSScriptRoot/New-SqlCapacityEnvironment.ps1" -Profile 1C2G `
            -Image $RuntimeImage -PublishDirectory $publish -ResultsDirectory "$root/app" *> "$root/app-create.log"
        $createExit=$LASTEXITCODE
        $environment=Get-Content -LiteralPath "$root/app/environment.json" -Raw -Encoding utf8 | ConvertFrom-Json
        $application=$environment.containerId
        if ($createExit -ne 0 -or $environment.status -cne 'READY') { throw 'Application environment was not READY.' }
        $null=Invoke-SqlLocalDocker @('network','connect',$tag,$application)
        $null=Invoke-SqlLocalDocker @('network','disconnect','bridge',$application)
        Copy-Item -LiteralPath $workload -Destination "$root/app/workload.json"
        Copy-Item -LiteralPath "$PSScriptRoot/Sample-SqlContainerResources.sh" -Destination "$root/app/sample.sh"
        # 临时数据库仅接入本次内部网络，不发布宿主端口。
        $database=Invoke-SqlLocalDocker @('run','--detach','--pull','never','--name',"$tag-pg",'--network',$tag,
            '--label',"bing.validation=$tag",'--cpus','1','--memory','512m','--memory-swap','512m',
            '--mount',"type=volume,source=$volume,target=/var/lib/postgresql/data",
            '--mount',"type=bind,source=$root/app,target=/results",'--env','POSTGRES_USER=bing_aot',
            '--env','POSTGRES_DB=bing_sql_aot_test','--env','POSTGRES_HOST_AUTH_METHOD=trust',
            $PostgresImage,'-c','shared_buffers=64MB','-c','max_connections=48')
        $receipt.applicationId=$application; $receipt.databaseId=$database; $receipt.networkId=$network; $receipt.volume=$volume
        $ready=$false
        for ($attempt=0; $attempt -lt 20; $attempt++) {
            & docker exec $database pg_isready -h 127.0.0.1 -U bing_aot -d bing_sql_aot_test *> "$root/db-ready.log"
            if ($LASTEXITCODE -eq 0) { $ready=$true; break }
            Start-Sleep -Seconds 1
        }
        if (!$ready) { throw 'Temporary database readiness timed out.' }
        $receipt.databaseLimits=@{
            cpuMax=(Invoke-SqlLocalDocker @('exec',$database,'cat','/sys/fs/cgroup/cpu.max'))
            memoryMax=(Invoke-SqlLocalDocker @('exec',$database,'cat','/sys/fs/cgroup/memory.max'))
            swapMax=(Invoke-SqlLocalDocker @('exec',$database,'cat','/sys/fs/cgroup/memory.swap.max'))
        }
        if ($receipt.databaseLimits.cpuMax -cne '100000 100000' -or $receipt.databaseLimits.memoryMax -cne '536870912' -or
            $receipt.databaseLimits.swapMax -cne '0') { throw 'Database cgroup limits differ from the fixed topology.' }
        $receipt.durability=Invoke-SqlLocalDocker @('exec',$database,'psql','-U','bing_aot','-d','bing_sql_aot_test','-Atc',
            'SHOW fsync; SHOW synchronous_commit; SHOW full_page_writes;')
        if ($receipt.durability -cne "on`non`non") { throw 'Durable PostgreSQL defaults are required.' }
        $receipt.images=@{application=$environment.imageId; database=(Invoke-SqlLocalDocker @('inspect','--format','{{.Image}}',$database))}
        $receipt.runtime=Invoke-SqlLocalDocker @('exec',$application,'dotnet','--info')
        $receipt.databaseVersion=Invoke-SqlLocalDocker @('exec',$database,'psql','-U','bing_aot','-d','bing_sql_aot_test','-Atc','SELECT version()')
        $connection="Host=$tag-pg;Username=bing_aot;Database=bing_sql_aot_test;SSL Mode=Disable;Pooling=true;Maximum Pool Size=32"
        foreach ($cell in $cells) {
            $n=$cell.concurrency
            foreach ($pair in @(@($application,'app'),@($database,'db'))) {
                $null=Invoke-SqlLocalDocker @('exec','-d',$pair[0],'sh','/results/sample.sh',"/results/c$n-$($pair[1]).jsonl","/results/c$n.stop")
            }
            $arguments=Get-SqlLocalCapacityCommand $n
            & docker exec --env RUN_POSTGRESQL_INTEGRATION_TESTS=true --env "ConnectionStrings__PostgreSqlConnection=$connection" `
                $application @arguments *> "$root/c$n-probe.log"
            $probeExit=$LASTEXITCODE
            Start-Sleep -Seconds 2
            [IO.File]::WriteAllText("$root/app/c$n.stop",'stop',[Text.UTF8Encoding]::new($false))
            $stopped=$false
            for ($attempt=0; $attempt -lt 8; $attempt++) {
                if ((Test-Path -LiteralPath "$root/app/c$n-app.jsonl.done") -and (Test-Path -LiteralPath "$root/app/c$n-db.jsonl.done")) {
                    $stopped=$true; break
                }
                Start-Sleep -Seconds 1
            }
            if (!$stopped -or $probeExit -ne 0) { throw "C$n probe or resource sampler failed; no automatic retry." }
            $reportPath="$root/app/c$n.json"
            $report=Get-Content -LiteralPath $reportPath -Raw -Encoding utf8 | ConvertFrom-Json
            & pwsh -NoProfile -File "$PSScriptRoot/Invoke-SqlMixedCapacityGate.ps1" -ReceiptPath $reportPath `
                -ExpectedBuildDirectory $publish -ExpectedWorkloadPath $workload -ExpectedProfile 1C2G -ExpectedConcurrency $n `
                -BudgetApprovalId $budget.approvalId -MinRequestsPerSecond $cell.minRequestsPerSecond `
                -MaxP99Milliseconds $cell.maxP99Milliseconds -MaxFailureRate $budget.maxFailureRate *> "$root/c$n-gate.json"
            $gateExit=$LASTEXITCODE
            $gate=Get-Content -LiteralPath "$root/c$n-gate.json" -Raw -Encoding utf8 | ConvertFrom-Json
            if ($gateExit -notin @(0,1) -or $gate.status -notin @('PASS','FAIL')) { throw "C$n capacity evidence is invalid." }
            foreach ($kind in @('app','db')) {
                & pwsh -NoProfile -File "$PSScriptRoot/Get-SqlContainerResourceSummary.ps1" -SamplesPath "$root/app/c$n-$kind.jsonl" `
                    -WindowStartUnixMilliseconds $report.measurement.measure.startedAtUnixMilliseconds `
                    -WindowStopUnixMilliseconds $report.measurement.measure.stoppedAtUnixMilliseconds `
                    -ResultPath "$root/c$n-$kind-resources.json" *> "$root/c$n-$kind-resources.log"
                if ($LASTEXITCODE -ne 0) { throw "C$n $kind resource summary is invalid." }
                $resources=Get-Content -LiteralPath "$root/c$n-$kind-resources.json" -Raw -Encoding utf8 | ConvertFrom-Json
                if ($resources.oomKillDelta -ne 0) { throw "C$n $kind had an OOM kill." }
            }
            $receipt.cells+=@{concurrency=$n; status=$gate.status; observed=$gate.observed; violations=@($gate.violations)}
            # 保留失败档位的测量证据，不以自动重跑或后续档位掩盖失败。
            if ($gate.status -ceq 'FAIL') { break }
        }
        Assert-BuildCandidate 'after'
        $receipt.remainingTables=Invoke-SqlLocalDocker @('exec',$database,'psql','-U','bing_aot','-d','bing_sql_aot_test','-Atc',
            "SELECT count(*) FROM pg_tables WHERE schemaname='public' AND starts_with(tablename,'sql_mixed_')")
        if ($receipt.remainingTables -cne '0') { throw 'Capacity fixture tables were not cleaned.' }
        $receipt.finalContainers=@()
        foreach ($id in @($application,$database)) {
            $state=(Invoke-SqlLocalDocker @('inspect',$id) | ConvertFrom-Json)[0]
            $receipt.finalContainers+=@{id=$id; oomKilled=$state.State.OOMKilled; restartCount=$state.RestartCount; running=$state.State.Running}
            if ($state.State.OOMKilled -or $state.RestartCount -ne 0 -or !$state.State.Running) { throw 'Unexpected container state.' }
        }
        $receipt.capacityStatus=if (@($receipt.cells | Where-Object status -EQ FAIL).Count) {'FAIL'} else {'PASS'}
        $receipt.status=if ($receipt.capacityStatus -ceq 'PASS') {'PASS'} else {'CAPACITY_FAIL'}
    }
    catch { $receipt.status='INVALID'; $receipt.error=$_.Exception.Message }
    finally {
        $cleanup=@()
        foreach ($id in @($application,$database)) {
            if (!$id) { continue }
            $null=& docker rm --force --volumes $id 2>&1
            $cleanup+=@{kind='container'; id=$id; exitCode=$LASTEXITCODE}
        }
        if ($network) { $null=& docker network rm $network 2>&1; $cleanup+=@{kind='network'; id=$network; exitCode=$LASTEXITCODE} }
        if ($volume) { $null=& docker volume rm $volume 2>&1; $cleanup+=@{kind='volume'; id=$volume; exitCode=$LASTEXITCODE} }
        $receipt.cleanup=$cleanup
        if (@($cleanup | Where-Object exitCode -NE 0).Count) { $receipt.status='INVALID' }
        $receipt.finishedAt=[DateTime]::UtcNow.ToString('o')
        $receipt | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath "$root/environment.json" -Encoding utf8
    }
    $receipt | ConvertTo-Json -Depth 12
    if ($receipt.status -ceq 'CAPACITY_FAIL') { exit 1 }
    if ($receipt.status -cne 'PASS') { exit 2 }
}
if ($MyInvocation.InvocationName -ne '.') { Invoke-SqlLocalCapacity }
