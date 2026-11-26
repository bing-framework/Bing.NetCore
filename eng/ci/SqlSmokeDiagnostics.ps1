# 仅记录 CPU 自测所在进程树的状态；不读取命令行或环境变量。
function Start-SqlSmokeCpuMonitor {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][int]$OwnerProcessId,
        [Parameter(Mandatory)][string[]]$Arguments
    )
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $start = @{ event = 'start'; utc = [DateTime]::UtcNow.ToString('o'); ownerPid = $OwnerProcessId; command = 'dotnet'; arguments = $Arguments }
    $start | ConvertTo-Json -Compress -Depth 6 | Set-Content -LiteralPath $Path -Encoding utf8
    $job = Start-Job -ArgumentList $Path, $OwnerProcessId -ScriptBlock {
        param($OutputPath, $OwnerPid)
        while ($true) {
            try {
                $all = @(Get-CimInstance Win32_Process -ErrorAction Stop)
                $ids = [Collections.Generic.HashSet[int]]::new()
                [void]$ids.Add([int]$OwnerPid)
                do {
                    $changed = $false
                    foreach ($process in $all) {
                        if ($ids.Contains([int]$process.ParentProcessId) -and $ids.Add([int]$process.ProcessId)) { $changed = $true }
                    }
                } while ($changed)
                $processes = @($all | Where-Object { $ids.Contains([int]$_.ProcessId) } | ForEach-Object {
                    @{ id = [int]$_.ProcessId; parentId = [int]$_.ParentProcessId; name = $_.Name;
                       workingSetBytes = [long]$_.WorkingSetSize; kernelTime100ns = [long]$_.KernelModeTime;
                       userTime100ns = [long]$_.UserModeTime }
                })
                $sample = @{ event = 'sample'; utc = [DateTime]::UtcNow.ToString('o'); processes = $processes }
            } catch {
                $sample = @{ event = 'sample-error'; utc = [DateTime]::UtcNow.ToString('o'); errorType = $_.Exception.GetType().FullName }
            }
            $sample | ConvertTo-Json -Compress -Depth 8 | Add-Content -LiteralPath $OutputPath -Encoding utf8
            Start-Sleep -Seconds 2
        }
    }
    return @{ Job = $job; Path = $Path; Clock = $clock }
}

function Stop-SqlSmokeCpuMonitor {
    param([Parameter(Mandatory)]$Monitor, [Parameter(Mandatory)][int]$ExitCode)
    Stop-Job -Job $Monitor.Job -ErrorAction SilentlyContinue
    Remove-Job -Job $Monitor.Job -Force -ErrorAction SilentlyContinue
    $Monitor.Clock.Stop()
    @{ event = 'finish'; utc = [DateTime]::UtcNow.ToString('o'); elapsedMilliseconds = $Monitor.Clock.Elapsed.TotalMilliseconds; exitCode = $ExitCode } |
        ConvertTo-Json -Compress | Add-Content -LiteralPath $Monitor.Path -Encoding utf8
}
