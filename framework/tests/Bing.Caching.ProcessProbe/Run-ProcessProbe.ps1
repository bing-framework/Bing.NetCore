[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Connection,

    [ValidateSet('freeredis', 'csredis', 'easyredis', 'both')]
    [string] $Provider = 'both',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $NoBuild
)

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$projectPath = Join-Path $PSScriptRoot 'Bing.Caching.ProcessProbe.csproj'
$assemblyPath = Join-Path $PSScriptRoot "bin\$Configuration\net8.0\Bing.Caching.ProcessProbe.dll"
$startedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

function Get-ProbeText {
    param([System.Collections.Concurrent.ConcurrentQueue[string]] $Lines)

    return [string]::Join([Environment]::NewLine, [string[]]$Lines.ToArray())
}

function Start-Probe {
    param(
        [string] $Name,
        [string[]] $Arguments
    )

    $state = [pscustomobject]@{
        Output = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
        Error = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
        OutputReader = $null
        ErrorReader = $null
        OutputTask = $null
        ErrorTask = $null
    }
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'dotnet'
    $startInfo.WorkingDirectory = $PSScriptRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments)
    {
        [void]$startInfo.ArgumentList.Add($argument)
    }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start())
    {
        throw "Could not start probe '$Name'."
    }
    $state.OutputReader = $process.StandardOutput
    $state.ErrorReader = $process.StandardError
    $state.OutputTask = $state.OutputReader.ReadLineAsync()
    $state.ErrorTask = $state.ErrorReader.ReadLineAsync()
    $startedProcesses.Add($process)
    return [pscustomobject]@{
        Name = $Name
        Process = $process
        State = $state
    }
}

function Drain-ProbeOutput {
    param([pscustomobject] $Probe)

    foreach ($stream in @(
            @{ Queue = $Probe.State.Output; Reader = 'OutputReader'; Task = 'OutputTask' },
            @{ Queue = $Probe.State.Error; Reader = 'ErrorReader'; Task = 'ErrorTask' }))
    {
        while ($null -ne $Probe.State.($stream.Task) -and $Probe.State.($stream.Task).IsCompleted)
        {
            $line = $Probe.State.($stream.Task).GetAwaiter().GetResult()
            if ($null -eq $line)
            {
                $Probe.State.($stream.Task) = $null
                break
            }
            $stream.Queue.Enqueue($line)
            $Probe.State.($stream.Task) = $Probe.State.($stream.Reader).ReadLineAsync()
        }
    }
}

function Wait-ProbeOutput {
    param(
        [pscustomobject] $Probe,
        [string] $Pattern,
        [int] $TimeoutSeconds = 15
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do
    {
        Drain-ProbeOutput $Probe
        $text = Get-ProbeText $Probe.State.Output
        if ($text -match [Regex]::Escape($Pattern))
        {
            return $text
        }
        Start-Sleep -Milliseconds 25
    }
    while ([DateTime]::UtcNow -lt $deadline)

    $errorText = Get-ProbeText $Probe.State.Error
    throw "Timed out waiting for '$Pattern' from $($Probe.Name). stdout=$text stderr=$errorText"
}

function Wait-Probe {
    param(
        [pscustomobject] $Probe,
        [int] $TimeoutSeconds = 30
    )

    if (-not $Probe.Process.WaitForExit($TimeoutSeconds * 1000))
    {
        $Probe.Process.Kill($true)
        throw "Probe '$($Probe.Name)' did not exit within $TimeoutSeconds seconds."
    }
    # 等待读取任务排空，确保末尾的错误和结果也进入断言队列。
    $drainDeadline = [DateTime]::UtcNow.AddSeconds(2)
    do
    {
        Drain-ProbeOutput $Probe
        if ($null -eq $Probe.State.OutputTask -and $null -eq $Probe.State.ErrorTask)
        {
            break
        }
        Start-Sleep -Milliseconds 25
    }
    while ([DateTime]::UtcNow -lt $drainDeadline)
    $Probe.Process.Refresh()
    return [pscustomobject]@{
        Name = $Probe.Name
        ExitCode = $Probe.Process.ExitCode
        Stdout = Get-ProbeText $Probe.State.Output
        Stderr = Get-ProbeText $Probe.State.Error
    }
}

function Assert-Text {
    param(
        [string] $Text,
        [string] $Pattern,
        [string] $Description
    )

    if ($Text -notmatch $Pattern)
    {
        throw "Assertion failed: $Description. Pattern '$Pattern' was not found in: $Text"
    }
}

function Assert-ExitCode {
    param(
        [pscustomobject] $Result,
        [int] $Expected
    )

    if ($Result.ExitCode -ne $Expected)
    {
        throw "Assertion failed: $($Result.Name) exit code $($Result.ExitCode), expected $Expected. stdout=$($Result.Stdout) stderr=$($Result.Stderr)"
    }
}

function New-Namespace {
    param([string] $ProviderName, [string] $Scenario)

    return "process-probe-$ProviderName-$Scenario-$([Guid]::NewGuid().ToString('N'))"
}

function New-ProbeArguments {
    param(
        [string] $ProviderName,
        [string] $Namespace,
        [long] $StartTicks,
        [string] $Mode,
        [string[]] $Options = @()
    )

    $arguments = @($assemblyPath, $ProviderName, $Connection, $Namespace,
        $StartTicks.ToString([Globalization.CultureInfo]::InvariantCulture), $Mode)
    return $arguments + $Options
}

function Run-ProviderProbe {
    param([string] $ProviderName)

    Write-Host "[$ProviderName] normal same-key coalescing"
    $namespace = New-Namespace $ProviderName 'normal'
    $startTicks = [DateTime]::UtcNow.AddMilliseconds(800).Ticks
    $left = Start-Probe 'normal-left' (New-ProbeArguments $ProviderName $namespace $startTicks 'normal' @('--factory-ms=300', '--value=42'))
    $right = Start-Probe 'normal-right' (New-ProbeArguments $ProviderName $namespace $startTicks 'normal' @('--factory-ms=300', '--value=42'))
    $leftResult = Wait-Probe $left
    $rightResult = Wait-Probe $right
    Assert-ExitCode $leftResult 0
    Assert-ExitCode $rightResult 0
    $normalOutput = "$($leftResult.Stdout)`n$($rightResult.Stdout)"
    Assert-Text $normalOutput 'RESULT=42' 'normal mode must return 42 from both processes'
    if (([Regex]::Matches($normalOutput, '(?m)^FACTORY\r?$')).Count -ne 1)
    {
        throw "Assertion failed: normal mode must execute one factory. output=$normalOutput"
    }

    Write-Host "[$ProviderName] abnormal holder exit and takeover"
    $namespace = New-Namespace $ProviderName 'crash'
    $holder = Start-Probe 'crash-holder' (New-ProbeArguments $ProviderName $namespace 0 'crash-holder' @(
            '--lease-ms=700', '--renew-ms=150', '--wait-ms=4000', '--retrieval-ms=5000',
            '--crash-after-ms=500', '--value=1'))
    Wait-ProbeOutput $holder 'FACTORY' | Out-Null
    $holderResult = Wait-Probe $holder
    Assert-ExitCode $holderResult 17
    Assert-Text $holderResult.Stdout 'HOLDER_EXIT=17' 'crash holder must exit nonzero while holding the lease'
    $waiter = Start-Probe 'crash-waiter' (New-ProbeArguments $ProviderName $namespace 0 'normal' @(
            '--lease-ms=700', '--renew-ms=150', '--wait-ms=4000', '--retrieval-ms=5000',
            '--factory-ms=20', '--value=2'))
    $waiterResult = Wait-Probe $waiter
    Assert-ExitCode $waiterResult 0
    Assert-Text $waiterResult.Stdout 'RESULT=2' 'new holder must take over after an abnormal exit'

    Write-Host "[$ProviderName] old holder timeout cannot write back"
    $namespace = New-Namespace $ProviderName 'timeout'
    $oldHolder = Start-Probe 'timeout-holder' (New-ProbeArguments $ProviderName $namespace 0 'timeout-holder' @(
            '--lease-ms=2000', '--renew-ms=500', '--wait-ms=4000', '--retrieval-ms=150',
            '--factory-ms=700', '--linger-ms=900', '--value=1'))
    Wait-ProbeOutput $oldHolder 'FACTORY' | Out-Null
    $oldHolderResult = Wait-Probe $oldHolder
    Assert-ExitCode $oldHolderResult 1
    Assert-Text $oldHolderResult.Stdout 'ERROR=TimeoutException' 'old holder must report a retrieval timeout'
    Assert-Text $oldHolderResult.Stdout 'FACTORY_RETURNED' 'late factory completion must be observed'
    Assert-Text $oldHolderResult.Stdout 'CACHE_PRESENT=False' 'timed-out holder must not write a cache value'
    $newHolder = Start-Probe 'timeout-takeover' (New-ProbeArguments $ProviderName $namespace 0 'normal' @(
            '--lease-ms=2000', '--renew-ms=500', '--wait-ms=4000', '--retrieval-ms=5000',
            '--factory-ms=20', '--value=2'))
    $newHolderResult = Wait-Probe $newHolder
    Assert-ExitCode $newHolderResult 0
    Assert-Text $newHolderResult.Stdout 'RESULT=2' 'new holder must fill the timed-out miss'

    Write-Host "[$ProviderName] lost lease rejects old writeback"
    $namespace = New-Namespace $ProviderName 'lost-lease'
    $oldHolder = Start-Probe 'lost-lease-holder' (New-ProbeArguments $ProviderName $namespace 0 'lost-lease' @(
            '--lease-ms=5000', '--renew-ms=1000', '--wait-ms=4000', '--retrieval-ms=5000',
            '--drop-lease-after-ms=150', '--factory-ms=700', '--value=1'))
    Wait-ProbeOutput $oldHolder 'LEASE_DELETED' | Out-Null
    $oldHolderResult = Wait-Probe $oldHolder
    Assert-ExitCode $oldHolderResult 1
    Assert-Text $oldHolderResult.Stdout 'ERROR=InvalidOperationException' 'lost lease must reject the old commit'
    Assert-Text $oldHolderResult.Stdout 'CACHE_PRESENT=False' 'old holder must not create a value after losing its lease'
    $newHolder = Start-Probe 'lost-lease-takeover' (New-ProbeArguments $ProviderName $namespace 0 'normal' @(
            '--lease-ms=5000', '--renew-ms=1000', '--wait-ms=4000', '--retrieval-ms=5000',
            '--factory-ms=20', '--value=2'))
    $newHolderResult = Wait-Probe $newHolder
    Assert-ExitCode $newHolderResult 0
    Assert-Text $newHolderResult.Stdout 'RESULT=2' 'new holder must fill the lost-lease miss'

    Write-Host "[$ProviderName] waiter timeout while holder remains active"
    $namespace = New-Namespace $ProviderName 'waiter-timeout'
    $holder = Start-Probe 'wait-timeout-holder' (New-ProbeArguments $ProviderName $namespace 0 'normal' @(
            '--lease-ms=5000', '--renew-ms=1000', '--wait-ms=5000', '--retrieval-ms=5000',
            '--factory-ms=1200', '--value=1'))
    Wait-ProbeOutput $holder 'FACTORY' | Out-Null
    $waiter = Start-Probe 'wait-timeout-waiter' (New-ProbeArguments $ProviderName $namespace 0 'waiter-timeout' @(
            '--lease-ms=5000', '--renew-ms=1000', '--wait-ms=250', '--retrieval-ms=5000',
            '--factory-ms=10', '--value=2'))
    $waiterResult = Wait-Probe $waiter
    Assert-ExitCode $waiterResult 1
    Assert-Text $waiterResult.Stdout 'ERROR=TimeoutException' 'waiter must time out while the lease is held'
    $holderResult = Wait-Probe $holder
    Assert-ExitCode $holderResult 0
    Assert-Text $holderResult.Stdout 'RESULT=1' 'original holder must complete after the waiter timeout'

    Write-Host "[$ProviderName] PASS"
}

try
{
    if (-not $NoBuild)
    {
        & dotnet build $projectPath '--no-restore' '--nologo' '-c' $Configuration
        if ($LASTEXITCODE -ne 0)
        {
            throw "ProcessProbe build failed with exit code $LASTEXITCODE."
        }
    }
    if (-not (Test-Path -LiteralPath $assemblyPath))
    {
        throw "ProcessProbe assembly was not found: $assemblyPath"
    }

    $providers = if ($Provider -eq 'both') { @('freeredis', 'csredis', 'easyredis') } else { @($Provider) }
    foreach ($providerName in $providers)
    {
        Run-ProviderProbe $providerName
    }
}
finally
{
    foreach ($process in $startedProcesses)
    {
        try
        {
            if (-not $process.HasExited)
            {
                $process.Kill($true)
            }
        }
        catch { }
        $process.Dispose()
    }
}
