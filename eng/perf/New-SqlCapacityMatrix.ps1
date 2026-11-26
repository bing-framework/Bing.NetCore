#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$Rows,
    [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$Requests,
    [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$MaxPoolSize,
    [Parameter(Mandatory)][string]$OutputPath
)

[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$OutputEncoding = [Text.UTF8Encoding]::new($false)
$ErrorActionPreference = 'Stop'
# 固定相同的阶梯，避免不同硬件的请求条件不同而失去可比性。
$concurrencyLevels = @(1, 8, 16, 32, 64)
if ($Requests -lt $concurrencyLevels[-1]) { throw 'Requests must be at least 64 for the complete concurrency matrix.' }
$profiles = @(
    @{ name = '2C4G'; cpu = 2; memoryGiB = 4 },
    @{ name = '4C8G'; cpu = 4; memoryGiB = 8 },
    @{ name = '8C16G'; cpu = 8; memoryGiB = 16 }
)
$cells = @(
    foreach ($profile in $profiles) {
        foreach ($path in @('runtime', 'template')) {
            foreach ($concurrency in $concurrencyLevels) {
                $id = "$($profile.name)-$path-$concurrency"
                [ordered]@{
                    id = $id
                    profile = $profile.name
                    expectedWorkload = [ordered]@{
                        path = $path; rows = $Rows; requests = $Requests; concurrency = $concurrency
                        pooling = $true; maxPoolSize = $MaxPoolSize; observedServer = $false
                    }
                    # 直接传入 ArgumentList；由运行方将 {output} 替换为统一产物目录下的独立收据路径。
                    arguments = @('--capacity-probe', $path, "$Rows", "$Requests", "$concurrency", '{output}')
                }
            }
        }
    }
)
$matrix = [ordered]@{
    schema = 'SqlCapacityMatrix-v1'
    status = 'PLANNED'
    provider = 'PostgreSQL'
    workload = 'FixedReadProbe'
    hardwareScope = 'Application host; database host must be fixed and recorded separately'
    environmentVerification = 'REQUIRED'
    productionCapacityVerified = $false
    thresholds = $null
    profiles = $profiles
    cells = $cells
}
$file = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file)) | Out-Null
# 规划文件不能覆盖旧测量或已审批的配置。
$stream = [IO.File]::Open($file, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
try {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($matrix | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
    $stream.Write($bytes, 0, $bytes.Length)
}
finally { $stream.Dispose() }
