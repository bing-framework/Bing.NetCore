[CmdletBinding()]
param([string]$ResultsDirectory = 'output/round135-tests')

[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$helperPath = Join-Path $repositoryRoot "eng/perf/BuildProcessTree.cs"
$outputDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory, $repositoryRoot)
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$runId = [Guid]::NewGuid().ToString("N")
$summaryPath = Join-Path $outputDirectory "$runId-BuildProcessTree.Tests.json"
$runLogPath = Join-Path $outputDirectory "$runId-BuildProcessTree.Tests.log"
$script:caseResults = [System.Collections.Generic.List[object]]::new()
$script:measurements = [System.Collections.Generic.List[object]]::new()
$script:fixturePath = Join-Path $outputDirectory "$runId-BuildProcessTreeFixture.exe"
$fixtureSourcePath = Join-Path $outputDirectory "$runId-BuildProcessTreeFixture.cs"
$fixtureBuildLogPath = Join-Path $outputDirectory "$runId-BuildProcessTreeFixture-build.log"

function Write-TestSummary {
    param(
        [Parameter(Mandatory = $true)][string]$Status,
        [string]$ErrorMessage
    )

    $summary = [ordered]@{
        status = $Status
        runId = $runId
        tests = @($script:caseResults.ToArray())
        measurements = @($script:measurements.ToArray())
        error = $ErrorMessage
    }
    $json = $summary | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($summaryPath, $json, [System.Text.UTF8Encoding]::new($false))
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-TestCase {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Body
    )

    try {
        & $Body
        $script:caseResults.Add([ordered]@{ name = $Name; status = "PASS" })
        Write-Output "PASS $Name"
    }
    catch {
        $script:caseResults.Add([ordered]@{ name = $Name; status = "FAIL"; error = $_.Exception.Message })
        throw
    }
}

function Invoke-BuildProcessTree {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string[]]$CommandArguments,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
    )

    $logPath = Join-Path $outputDirectory "$runId-$Name.log"
    $result = [Bing.Sql.Performance.BuildProcessTree]::Run(
        $script:fixturePath,
        $CommandArguments,
        $outputDirectory,
        $logPath,
        $TimeoutMilliseconds)
    $script:measurements.Add([ordered]@{
        name = $Name
        exitCode = $result.ExitCode
        timedOut = $result.TimedOut
        totalProcesses = $result.TotalProcesses
        peakJobCommittedBytes = $result.PeakJobCommittedBytes
        peakProcessCommittedBytes = $result.PeakProcessCommittedBytes
        userTimeTicks = $result.UserTimeTicks
        kernelTimeTicks = $result.KernelTimeTicks
        elapsedMilliseconds = $result.ElapsedMilliseconds
        log = $logPath
    })
    return $result
}

function Wait-ForProcessesToExit {
    param(
        [Parameter(Mandatory = $true)][int[]]$ProcessIds,
        [Parameter(Mandatory = $true)][int]$TimeoutMilliseconds
    )

    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    while ($clock.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        $alive = @()
        foreach ($processId in $ProcessIds) {
            try {
                $process = [System.Diagnostics.Process]::GetProcessById($processId)
                try {
                    $process.Refresh()
                    if (-not $process.HasExited) {
                        $alive += $processId
                    }
                }
                finally {
                    $process.Dispose()
                }
            }
            catch [System.ArgumentException] {
            }
        }
        if ($alive.Count -eq 0) {
            return $true
        }
        Start-Sleep -Milliseconds 25
    }
    return $false
}

try {
    if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
        throw "RED: helper source is missing: $helperPath"
    }

    Add-Type -Path $helperPath -ErrorAction Stop

    $fixtureSource = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

internal static class BuildProcessTreeFixture
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length == 0)
            return 90;

        switch (args[0])
        {
            case "echo":
                for (var index = 1; index < args.Length; index++)
                    Console.WriteLine("ARG:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(args[index])));
                return 0;
            case "exit":
                Console.WriteLine("OUT-MARKER");
                Console.Error.WriteLine("ERR-MARKER");
                return Int32.Parse(args[1]);
            case "sleep":
                WriteProcessId(args[1]);
                Thread.Sleep(Int32.Parse(args[2]));
                return 0;
            case "root-tree":
                WriteProcessId(args[1]);
                StartDetached("child-tree", args[1], args[2]);
                return 0;
            case "child-tree":
                WriteProcessId(args[1]);
                StartDetached("sleep", args[1], args[2]);
                return 0;
            case "allocate-sequential":
                WriteProcessId(args[1]);
                StartAndWait("allocate", args[1], args[2], args[3], args[4]);
                StartAndWait("allocate", args[1], args[2], args[3], args[4]);
                return 0;
            case "allocate":
                WriteProcessId(args[1]);
                AllocateAndRecord(args[1], args[2], Int32.Parse(args[3]), Int32.Parse(args[4]));
                return 0;
            default:
                return 91;
        }
    }

    private static void WriteProcessId(string path)
    {
        File.AppendAllText(path, Process.GetCurrentProcess().Id + Environment.NewLine, new UTF8Encoding(false));
    }

    private static void AllocateAndRecord(string processListPath, string memoryListPath, int bytes, int delayMilliseconds)
    {
        var allocation = new byte[bytes];
        for (var index = 0; index < allocation.Length; index += 4096)
            allocation[index] = 1;
        allocation[allocation.Length - 1] = 1;

        using (var process = Process.GetCurrentProcess())
        {
            process.Refresh();
            File.AppendAllText(memoryListPath,
                process.Id + "|" + process.PrivateMemorySize64 + Environment.NewLine,
                new UTF8Encoding(false));
        }

        Thread.Sleep(delayMilliseconds);
        GC.KeepAlive(allocation);
    }

    private static void StartAndWait(string mode, params string[] arguments)
    {
        using (var process = Start(mode, arguments))
        {
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Fixture child exited with " + process.ExitCode + ".");
        }
    }

    private static void StartDetached(string mode, params string[] arguments)
    {
        using (Start(mode, arguments))
        {
        }
    }

    private static Process Start(string mode, string[] arguments)
    {
        var allArguments = new string[arguments.Length + 1];
        allArguments[0] = mode;
        Array.Copy(arguments, 0, allArguments, 1, arguments.Length);

        var startInfo = new ProcessStartInfo
        {
            FileName = Assembly.GetExecutingAssembly().Location,
            Arguments = JoinCommandLine(allArguments),
            UseShellExecute = false,
            CreateNoWindow = true
        };
        return Process.Start(startInfo);
    }

    private static string JoinCommandLine(string[] arguments)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < arguments.Length; index++)
        {
            if (index > 0)
                builder.Append(' ');
            builder.Append(QuoteArgument(arguments[index]));
        }
        return builder.ToString();
    }

    private static string QuoteArgument(string argument)
    {
        var builder = new StringBuilder();
        builder.Append('"');
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
            }
            else if (character == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
            }
            else
            {
                builder.Append('\\', backslashes);
                builder.Append(character);
                backslashes = 0;
            }
        }
        builder.Append('\\', backslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
'@
    [System.IO.File]::WriteAllText($fixtureSourcePath, $fixtureSource, [System.Text.UTF8Encoding]::new($false))
    $compilerPath = if ([IntPtr]::Size -eq 8) {
        "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    }
    else {
        "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    }
    if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) {
        throw "A Windows .NET Framework C# compiler is required for the process fixture: $compilerPath"
    }
    $compilerOutput = @(& $compilerPath "/nologo" "/target:exe" "/reference:System.dll" "/out:$script:fixturePath" $fixtureSourcePath 2>&1)
    $compilerExitCode = $LASTEXITCODE
    [System.IO.File]::WriteAllText($fixtureBuildLogPath, ($compilerOutput -join [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
    if ($compilerExitCode -ne 0) {
        throw "Process fixture compilation failed with exit code $compilerExitCode; see $fixtureBuildLogPath"
    }

    Invoke-TestCase -Name "root-exits-before-grandchild" -Body {
        $processList = Join-Path $outputDirectory "$runId-tree-processes.txt"
        $result = Invoke-BuildProcessTree -Name "tree" -CommandArguments ([string[]]@("root-tree", $processList, "650")) -TimeoutMilliseconds 5000
        $processIds = @(Get-Content -LiteralPath $processList -Encoding utf8 | ForEach-Object { [int]$_ })
        Assert-True ($result.ExitCode -eq 0) "Root process exit code was $($result.ExitCode)."
        Assert-True (-not $result.TimedOut) "Tree completed but was reported timed out."
        Assert-True ($result.TotalProcesses -ge 3) "Expected root, child and grandchild; job counted $($result.TotalProcesses)."
        Assert-True ($processIds.Count -eq 3) "Fixture recorded $($processIds.Count) process IDs."
        Assert-True ($result.ElapsedMilliseconds -ge 400) "Run returned before the grandchild completed ($($result.ElapsedMilliseconds) ms)."
    }

    Invoke-TestCase -Name "short-lived-child-commit-accounting" -Body {
        $processList = Join-Path $outputDirectory "$runId-allocation-processes.txt"
        $memoryList = Join-Path $outputDirectory "$runId-allocation-memory.txt"
        $allocationBytes = 100663296
        $result = Invoke-BuildProcessTree -Name "allocation" -CommandArguments ([string[]]@(
            "allocate-sequential", $processList, $memoryList, [string]$allocationBytes, "75")) -TimeoutMilliseconds 10000
        $processIds = @(Get-Content -LiteralPath $processList -Encoding utf8 | ForEach-Object { [int]$_ })
        $childCommittedBytes = @(Get-Content -LiteralPath $memoryList -Encoding utf8 | ForEach-Object { [ulong]($_ -split '\|')[1] })
        $sumOfChildCommittedBytes = [ulong]0
        foreach ($bytes in $childCommittedBytes) { $sumOfChildCommittedBytes += $bytes }

        Assert-True ($result.ExitCode -eq 0) "Allocation root process exit code was $($result.ExitCode)."
        Assert-True ($result.TotalProcesses -ge 3 -and $processIds.Count -eq 3) "Short-lived allocation children were not all counted."
        Assert-True ($result.PeakJobCommittedBytes -gt [ulong](64MB)) "Job peak commit omitted the allocation children."
        Assert-True ($result.PeakProcessCommittedBytes -gt [ulong](64MB)) "Peak process commit omitted an allocation child."
        Assert-True ($result.PeakJobCommittedBytes -ge $result.PeakProcessCommittedBytes) "Job peak commit was below the peak of one member process."
        Assert-True ($result.PeakJobCommittedBytes -lt $sumOfChildCommittedBytes) "Job peak appears to sum non-overlapping per-process peaks."
    }

    Invoke-TestCase -Name "nonzero-exit-preserves-log" -Body {
        $result = Invoke-BuildProcessTree -Name "nonzero" -CommandArguments ([string[]]@("exit", "23")) -TimeoutMilliseconds 5000
        $logPath = Join-Path $outputDirectory "$runId-nonzero.log"
        $log = Get-Content -LiteralPath $logPath -Raw -Encoding utf8
        Assert-True ($result.ExitCode -eq 23) "Expected exit code 23, received $($result.ExitCode)."
        Assert-True ($log.Contains("OUT-MARKER") -and $log.Contains("ERR-MARKER")) "Redirected stdout/stderr markers were absent from the log."
    }

    Invoke-TestCase -Name "timeout-terminates-entire-job" -Body {
        $processList = Join-Path $outputDirectory "$runId-timeout-processes.txt"
        $result = Invoke-BuildProcessTree -Name "timeout" -CommandArguments ([string[]]@("root-tree", $processList, "5000")) -TimeoutMilliseconds 500
        $processIds = @(Get-Content -LiteralPath $processList -Encoding utf8 | ForEach-Object { [int]$_ })
        Assert-True $result.TimedOut "A live descendant outlasted the deadline but TimedOut was false."
        Assert-True ($processIds.Count -eq 3) "Timeout fixture launched $($processIds.Count) of the expected processes."
        Assert-True (Wait-ForProcessesToExit -ProcessIds $processIds -TimeoutMilliseconds 3000) "One or more job members remained alive after timeout cleanup."
    }

    Invoke-TestCase -Name "windows-command-line-quoting" -Body {
        $expected = [string[]]@("", "two words", 'quote"inside', "trailing\", "two trailing\\", "汉字")
        $arguments = [System.Collections.Generic.List[string]]::new()
        $arguments.Add("echo")
        foreach ($item in $expected) { $arguments.Add($item) }
        $result = Invoke-BuildProcessTree -Name "arguments" -CommandArguments $arguments.ToArray() -TimeoutMilliseconds 5000
        $logPath = Join-Path $outputDirectory "$runId-arguments.log"
        $actual = @(Get-Content -LiteralPath $logPath -Encoding utf8 | Where-Object { $_.StartsWith("ARG:") } | ForEach-Object {
            [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($_.Substring(4)))
        })
        Assert-True ($result.ExitCode -eq 0) "Argument fixture exit code was $($result.ExitCode)."
        Assert-True ($actual.Count -eq $expected.Count) "Expected $($expected.Count) arguments; child observed $($actual.Count)."
        for ($index = 0; $index -lt $expected.Count; $index++) {
            Assert-True ($actual[$index] -ceq $expected[$index]) "Argument $index changed during command-line quoting."
        }
    }

    [System.IO.File]::WriteAllText($runLogPath, ($script:caseResults | ForEach-Object { "{0} {1}" -f $_.status, $_.name }) -join [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-TestSummary -Status "PASS"
}
catch {
    $errorText = $_ | Out-String
    [System.IO.File]::WriteAllText($runLogPath, $errorText, [System.Text.UTF8Encoding]::new($false))
    Write-TestSummary -Status "FAIL" -ErrorMessage $_.Exception.Message
    throw
}

Write-Output "Results: $summaryPath"
