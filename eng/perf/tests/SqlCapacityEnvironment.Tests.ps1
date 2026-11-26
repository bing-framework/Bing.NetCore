#requires -Version 7.0
param([string]$ResultsDirectory = 'artifacts/validation/r02/environment')
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
$testResultsDirectory=$ResultsDirectory
$cases = [Collections.Generic.List[object]]::new()
function Check($name, [scriptblock]$test) {
    try { & $test; $cases.Add(@{name=$name; status='PASS'}) }
    catch { $cases.Add(@{name=$name; status='FAIL'; error=$_.Exception.Message}) }
}
function Assert($condition) { if (!$condition) { throw 'Assertion failed' } }
Check 'LoadEnvironmentFunctions' { . "$PSScriptRoot/../New-SqlCapacityEnvironment.ps1"; Assert ($null -ne (Get-Command Get-SqlCapacityProfile -ErrorAction Stop)) }
# 脚本在此作用域导入，以便每项测试能独立验证函数行为。
if (Test-Path "$PSScriptRoot/../New-SqlCapacityEnvironment.ps1") { . "$PSScriptRoot/../New-SqlCapacityEnvironment.ps1" }
Check 'ProfilesUseBinaryGiB' {
    Assert ((Get-SqlCapacityProfile '1C2G').cpu -eq 1)
    Assert ((Get-SqlCapacityProfile '1C2G').memoryBytes -eq 2147483648)
    Assert ((Get-SqlCapacityProfile '2C4G').memoryBytes -eq 4294967296)
    Assert ((Get-SqlCapacityProfile '4C8G').cpu -eq 4)
    Assert ((Get-SqlCapacityProfile '8C16G').memoryBytes -eq 17179869184)
}
Check 'OneCoreProfileFitsCurrentDockerHostAndKeepsExactLimits' {
    $limits=Get-SqlCapacityProfile '1C2G'
    Assert-SqlCapacityHost @{OSType='linux'; NCPU=4; MemTotal=4108902400} $limits
    Assert-SqlCapacityLimits '100000 100000' '2147483648' $limits
    foreach ($limit in @(@('200000 100000','2147483648'), @('100000 100000','1073741824'))) {
        $failed=$false
        try { Assert-SqlCapacityLimits $limit[0] $limit[1] $limits }
        catch { $failed=$true }
        Assert $failed
    }
}
Check 'RejectUndersizedHost' {
    $failed=$false
    try { Assert-SqlCapacityHost @{OSType='linux'; NCPU=4; MemTotal=4108902400} (Get-SqlCapacityProfile '2C4G') }
    catch { $failed=$_.Exception.Message -like '*insufficient*' }
    Assert $failed
}
Check 'RejectWindowsEngine' {
    $failed=$false
    try { Assert-SqlCapacityHost @{OSType='windows'; NCPU=16; MemTotal=32GB} (Get-SqlCapacityProfile '8C16G') }
    catch { $failed=$_.Exception.Message -like '*Linux*' }
    Assert $failed
}
Check 'AcceptSufficientLinuxHost' { Assert-SqlCapacityHost @{OSType='linux'; NCPU=16; MemTotal=32GB} (Get-SqlCapacityProfile '8C16G') }
Check 'VerifyEffectiveCgroupLimits' {
    Assert-SqlCapacityLimits '200000 100000' '4294967296' (Get-SqlCapacityProfile '2C4G')
    Assert-SqlCapacityLimits '400000 200000' '4294967296' (Get-SqlCapacityProfile '2C4G')
}
Check 'RejectUnlimitedOrWrongCgroupLimits' {
    foreach ($limit in @(@('max 100000','4294967296'), @('200000 100000','max'), @('400000 100000','4294967296'))) {
        $failed=$false
        try { Assert-SqlCapacityLimits $limit[0] $limit[1] (Get-SqlCapacityProfile '2C4G') }
        catch { $failed=$_.Exception.Message -eq 'Effective cgroup limits do not match the requested profile.' }
        Assert $failed
    }
}
Check 'ProvisionPassesLimitsAndVerifiesEffectiveValues' {
    # Docker 替身只验证编排合同；它不构成真实容器或容量证据。
    $calls=[Collections.Generic.List[object]]::new()
    function Invoke-SqlCapacityDocker([string[]]$Arguments) {
        $calls.Add($Arguments)
        switch ($Arguments[0]) {
            'info' { return '{"OSType":"linux","NCPU":16,"MemTotal":34359738368,"ServerVersion":"fixture"}' }
            'create' { return 'fixture-container' }
            'start' { return 'fixture-container' }
            'inspect' { return '[{"Image":"sha256:fixture","HostConfig":{"NanoCpus":2000000000,"Memory":4294967296}}]' }
            'exec' {
                switch ($Arguments[-1]) {
                    '/sys/fs/cgroup/cpu.max' { return '200000 100000' }
                    '/sys/fs/cgroup/memory.max' { return '4294967296' }
                    '/sys/fs/cgroup/memory.swap.max' { return '0' }
                    default { throw 'Unexpected exec' }
                }
            }
            default { throw 'Unexpected Docker command' }
        }
    }
    $Profile='2C4G'; $Image='local-fixture'; $PublishDirectory=$PSScriptRoot
    $ResultsDirectory=Join-Path $testResultsDirectory ('provision-' + [Guid]::NewGuid().ToString('N'))
    $CheckHostOnly=$false
    New-SqlCapacityEnvironment | Out-Null
    $receipt=Get-Content "$ResultsDirectory/environment.json" -Raw -Encoding utf8 | ConvertFrom-Json
    Assert ($receipt.status -ceq 'READY' -and $receipt.effectiveLimitsVerified -and !$receipt.productionCapacityVerified)
    $create=@($calls | Where-Object { $_[0] -eq 'create' })[0]
    Assert ($create[$create.IndexOf('--cpus')+1] -eq '2')
    Assert ($create[$create.IndexOf('--memory')+1] -eq '4294967296')
    Assert ($create[$create.IndexOf('--memory-swap')+1] -eq '4294967296')
    Assert ($create -contains '--read-only')
}
$report=@{passed=@($cases | Where-Object status -eq PASS).Count; failed=@($cases | Where-Object status -eq FAIL).Count; cases=$cases}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($testResultsDirectory)) | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content "$testResultsDirectory/tests.json" -Encoding utf8
$report | ConvertTo-Json -Depth 8
if ($report.failed) { exit 1 }; exit 0
