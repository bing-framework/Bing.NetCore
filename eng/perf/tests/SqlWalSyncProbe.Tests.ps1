#requires -Version 7.0
[CmdletBinding()]
param([string]$ResultsDirectory = 'artifacts/validation/sql-wal-sync/probe-tests')
[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$testRoot = [IO.Path]::GetFullPath($ResultsDirectory)
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$runner = Join-Path $PSScriptRoot '../Invoke-SqlWalSyncProbe.ps1'
$cases = [Collections.Generic.List[object]]::new()

function Assert([bool]$Value, [string]$Message) { if (!$Value) { throw $Message } }
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $cases.Add(@{name=$Name; status='PASS'}) }
    catch { $cases.Add(@{name=$Name; status='FAIL'; error=$_.Exception.Message}) }
}
function New-WalOutput {
    $lines = @('1 second per test')
    foreach ($writes in @('one','two')) {
        $suffix = if ($writes -eq 'one') { 'write' } else { 'writes' }
        $lines += "Compare file sync methods using $writes 8kB ${suffix}:"
        foreach ($method in @('open_datasync','fdatasync','fsync','fsync_writethrough','open_sync')) {
            $lines += "  $method 1000.000 ops/sec 1000 usecs/op"
        }
    }
    $lines += @("Non-sync'ed 8kB writes:", '  write 10000.000 ops/sec 100 usecs/op')
    return $lines -join "`n"
}
# 替身只覆盖 Docker 协议，正常职责测试不连接引擎。
function docker {
    $a = @($args)
    $script:calls.Add($a)
    $global:LASTEXITCODE = 0
    if ($a[0] -eq 'info') { return '{"OSType":"linux","NCPU":4,"MemTotal":4108902400,"ServerVersion":"fixture"}' }
    if ($a[0] -eq 'image') {
        if ($script:mode -eq 'image-failure') { $global:LASTEXITCODE=1; return 'image unavailable' }
        return 'sha256:fixture-image'
    }
    if ($a[0] -eq 'volume' -and $a[1] -eq 'create') { return 'fixture-volume' }
    if ($a[0] -eq 'volume' -and $a[1] -eq 'inspect') { return '[{"Name":"fixture-volume","Driver":"local","Scope":"local","Options":null}]' }
    if ($a[0] -eq 'create') {
        if ($script:mode -eq 'create-failure') { $global:LASTEXITCODE=1; return 'create failed' }
        return 'fixture-container'
    }
    if ($a[0] -eq 'inspect') {
        return '[{"Id":"fixture-container","Image":"sha256:fixture-image","HostConfig":{"NanoCpus":1000000000,"Memory":536870912,"MemorySwap":536870912,"NetworkMode":"none","ReadonlyRootfs":true},"Mounts":[{"Type":"volume","Name":"fixture-volume","Destination":"/var/lib/postgresql/data"}]}]'
    }
    if ($a[0] -eq 'exec') {
        $command = $a -join ' '
        if ($command -like '*cpu.max*') { return '100000 100000' }
        if ($command -like '*memory.max*') { if ($script:mode -eq 'bad-limit') { return 'max' }; return '536870912' }
        if ($command -like '*memory.swap.max*') { return '0' }
        if ($command -like '*--version*') { if ($script:mode -eq 'bad-version') { return 'pg_test_fsync (PostgreSQL) 17.0' }; return 'pg_test_fsync (PostgreSQL) 16.14' }
        if ($command -like '*sha256sum*') { return (('a' * 64) + '  /usr/local/bin/pg_test_fsync') }
        if ($command -like '*stat -f*') { return 'ext2/ext3' }
        if ($command -like '*uname*') { return 'Linux fixture' }
        if ($command -like '*pg_test_fsync -f*') {
            if ($script:mode -eq 'probe-failure') { $global:LASTEXITCODE=124; return 'timed out' }
            if ($script:mode -eq 'bad-output') { return '1 second per test' }
            return New-WalOutput
        }
    }
    if ($a[0] -eq 'volume' -and $a[1] -eq 'rm' -and $script:mode -eq 'cleanup-failure') {
        $global:LASTEXITCODE=1; return 'volume busy'
    }
}
function Invoke-Fixture([string]$Name, [string]$Mode = 'success') {
    $script:mode = $Mode
    $script:calls = [Collections.Generic.List[object]]::new()
    . $runner -ResultsDirectory "$testRoot/$Name"
    $script:invocationError = $null
    try { Invoke-SqlWalSyncProbe | Out-Null } catch { $script:invocationError = $_.Exception.Message }
    return Get-Content -LiteralPath "$testRoot/$Name/environment.json" -Raw -Encoding utf8 | ConvertFrom-Json
}

Test-Case 'CompleteProbeRetainsRawOutputAndTenMeasurements' {
    $r=Invoke-Fixture 'complete'
    Assert (!$script:invocationError -and $r.status -ceq 'DIAGNOSTIC_COMPLETE') 'Successful probe failed.'
    $summary=Get-Content -LiteralPath "$testRoot/complete/summary.json" -Raw -Encoding utf8 | ConvertFrom-Json
    Assert ($summary.syncMethods.Count -eq 10 -and !$summary.capacityBudgetEvaluated) 'Summary contract differs.'
    Assert ((Get-FileHash -LiteralPath "$testRoot/complete/pg-test-fsync.log").Hash -ceq $r.rawOutputSha256) 'Raw output identity missing.'
    Assert ($r.cleanup.Count -eq 2 -and $r.toolSha256 -ceq ('a' * 64)) 'Identity or cleanup is missing.'
}
Test-Case 'ProbeUsesIsolatedLimitedContainerAndNamedVolume' {
    $null=Invoke-Fixture 'isolation'
    $a=@($script:calls | Where-Object { $_[0] -eq 'create' })[0]
    $pairs = @{
        '--pull'='never'; '--network'='none'; '--cpus'='1'; '--memory'='512m';
        '--memory-swap'='512m'; '--env'='LC_ALL=C'; '--cap-drop'='ALL';
        '--security-opt'='no-new-privileges';
        '--mount'='type=volume,source=fixture-volume,target=/var/lib/postgresql/data'
    }
    foreach ($key in $pairs.Keys) {
        $positions = @(for ($i=0; $i -lt $a.Count; $i++) { if ($a[$i] -ceq $key) { $i } })
        Assert ($positions.Count -eq 1) "Expected one argument $key"
        $position = $positions[0]
        Assert ($position + 1 -lt $a.Count -and $a[$position + 1] -ceq $pairs[$key]) "Unexpected value for $key"
    }
    Assert ($a -ccontains '--read-only') 'Read-only root filesystem missing.'
    Assert (@($a | Where-Object { $_ -cmatch '^(--publish(?:=|$)|-p(?:$|[0-9])|--volume(?:=|$)|-v$|type=bind)' }).Count -eq 0) 'Unexpected external mount or port.'
    $probe=@($script:calls | Where-Object { ($_ -join ' ') -like '*pg_test_fsync -f*' })
    $expected=@('exec','fixture-container','timeout','-k','5','180','pg_test_fsync','-f','/var/lib/postgresql/data/fsync.out','-s','1')
    Assert ($probe.Count -eq 1) 'Probe must run once.'
    Assert (($probe[0] -join "`n") -ceq ($expected -join "`n")) 'Probe arguments or bounded duration differ.'

}
foreach ($mode in @('probe-failure','bad-output','bad-limit','bad-version')) {
    Test-Case "FailureCleansOwnedResources_$mode" {
        $r=Invoke-Fixture $mode $mode
        Assert ($script:invocationError -and $r.status -ceq 'FAIL' -and $r.cleanup.Count -eq 2) 'Failed probe or cleanup was hidden.'
        Assert (@($script:calls | Where-Object { $_[0] -eq 'rm' -and $_ -contains 'fixture-container' }).Count -eq 1) 'Container cleanup missing.'
        Assert (@($script:calls | Where-Object { $_[0] -eq 'volume' -and $_[1] -eq 'rm' -and $_ -contains 'fixture-volume' }).Count -eq 1) 'Volume cleanup missing.'
        if ($mode -in @('bad-limit','bad-version')) {
            Assert (@($script:calls | Where-Object { ($_ -join ' ') -like '*pg_test_fsync -f*' }).Count -eq 0) 'Invalid setup reached measurement.'
        }
    }
}
Test-Case 'FailedContainerCreationStillRemovesOwnedVolume' {
    $r=Invoke-Fixture 'create-failure' 'create-failure'
    Assert ($r.status -ceq 'FAIL' -and $r.cleanup.Count -eq 1 -and $r.cleanup[0].kind -ceq 'volume') 'Partial setup leaked volume.'
}
Test-Case 'MissingImageDoesNotCreateOrPullResources' {
    $r=Invoke-Fixture 'image-failure' 'image-failure'
    Assert ($r.status -ceq 'FAIL' -and $r.cleanup.Count -eq 0) 'Missing image created resources.'
    Assert (@($script:calls | Where-Object { $_[0] -in @('pull','create','volume') }).Count -eq 0) 'Missing image reached provisioning.'
}
Test-Case 'CleanupFailureCannotProduceSuccessfulReceipt' {
    $r=Invoke-Fixture 'cleanup-failure' 'cleanup-failure'
    Assert ($r.status -ceq 'FAIL' -and $script:invocationError -and @($r.cleanup | Where-Object exitCode -NE 0).Count -eq 1) 'Cleanup failure was hidden.'
}
Test-Case 'ExistingResultsAreRejectedBeforeDockerCalls' {
    $script:calls=[Collections.Generic.List[object]]::new()
    . $runner -ResultsDirectory "$testRoot/complete"
    $rejected=$false
    try { Invoke-SqlWalSyncProbe | Out-Null } catch { $rejected=$_.Exception.Message -like '*must be new*' }
    Assert ($rejected -and $script:calls.Count -eq 0) 'Existing evidence was overwritten or Docker was reached.'
}
$report=@{passed=@($cases | Where-Object status -EQ PASS).Count; failed=@($cases | Where-Object status -EQ FAIL).Count; cases=@($cases)}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$testRoot/probe-tests.json" -Encoding utf8
$report | ConvertTo-Json -Depth 8
if ($report.failed) { exit 1 }
exit 0
