using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
try
{
    if (args is ["--self-test"]) return AttributionTests.Run();
    if (args is ["--self-test-child", var childMode]) return AttributionTests.RunChild(childMode);
    if (args is ["capture", var binary, var parent, var timeoutText])
        return await SqlCpuTrace.Capture(Path.GetFullPath(binary), Path.GetFullPath(parent), int.Parse(timeoutText));
    if (args.Length >= 6 && args[0] == "capture-workload" && args[4] == "--")
        return await SqlCpuTrace.Capture(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]), args[5..]);
    if (args is ["analyze", var trace, var pidText, var folder])
        return SqlCpuTrace.Analyze(Path.GetFullPath(trace), int.Parse(pidText), Path.GetFullPath(folder));
    if (args is ["analyze-window", var windowTrace, var windowPid, var windowFolder])
        return SqlCpuTrace.Analyze(Path.GetFullPath(windowTrace), int.Parse(windowPid), Path.GetFullPath(windowFolder), true);
    Console.Error.WriteLine("Usage: SqlCpuTrace --self-test | capture <existing Release benchmark.dll> <results directory> <timeout seconds 1..300> | capture-workload <benchmark.dll> <results directory> <timeout seconds 1..300> -- <args...> | analyze[-window] <nettrace> <PID> <new report directory>");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

/// <summary>单用途 SQL 探针采样入口；使用现有 Diagnostics 程序集，不改业务或系统采样策略。</summary>
internal static class SqlCpuTrace
{
    internal sealed record CaptureInvocation(string Workload, string Scope, string[] ProcessArguments,
        string[] WorkloadArguments, string[]? RequestedWorkloadArguments, bool ProductionCapacityVerified);

    private static readonly UTF8Encoding Utf8 = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), Utf8);
    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    internal static CaptureInvocation CreateCaptureInvocation(string binary, string probePath, IReadOnlyList<string>? workloadArguments)
    {
        if (workloadArguments is null)
        {
            var fixedArguments = new[] { "--builder-state-probe", "--output", probePath };
            return new CaptureInvocation("SqlBuilderStateProbe-v1",
                "Entire fixed probe, including startup/JIT/validation/allocation measurement/JSON output; managed execution state only",
                new[] { binary }.Concat(fixedArguments).ToArray(), fixedArguments, null, false);
        }

        if (workloadArguments.Count == 0) throw new ArgumentException("At least one workload argument is required", nameof(workloadArguments));
        var requestedArguments = workloadArguments.ToArray();
        var effectiveArguments = requestedArguments
            .Select(argument => argument.Replace("{output}", probePath, StringComparison.Ordinal))
            .ToArray();
        return new CaptureInvocation("CallerSpecifiedWorkload-v1",
            "Entire caller-specified workload, including startup/JIT and caller-defined warmup/measurement/output; managed execution state only",
            new[] { binary }.Concat(effectiveArguments).ToArray(), effectiveArguments, requestedArguments, false);
    }

    // 两个混合入口使用相同的调度器窗口，不能在缺少标记时退回整段进程采样。
    internal static bool RequiresMeasurementWindow(IReadOnlyList<string>? arguments) =>
        arguments is { Count: > 0 } && arguments[0] is "--mixed-capacity-probe" or "--mixed-stage-probe";

    internal static async Task<int> Capture(string binary, string parent, int timeoutSeconds, IReadOnlyList<string>? workloadArguments = null)
    {
        if (timeoutSeconds is < 1 or > 300) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        if (!File.Exists(binary)) throw new FileNotFoundException("Existing benchmark binary required", binary);
        if (workloadArguments is { Count: 0 }) throw new ArgumentException("At least one workload argument is required", nameof(workloadArguments));
        var run = Path.Combine(parent, "run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run);
        var probePath = Path.Combine(run, "probe.json");
        var invocation = CreateCaptureInvocation(binary, probePath, workloadArguments);
        var receipt = new Dictionary<string, object?>
        {
            ["status"] = "FAIL", ["kind"] = "EventPipeManagedExecutionSamples", ["workload"] = invocation.Workload,
            ["startedAt"] = DateTime.UtcNow, ["binary"] = binary, ["binarySha256"] = Hash(binary),
            ["collectorSha256"] = Hash(typeof(SqlCpuTrace).Assembly.Location), ["timeoutSeconds"] = timeoutSeconds,
            ["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ["os"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ["architecture"] = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            ["logicalProcessors"] = Environment.ProcessorCount, ["bufferMB"] = 64,
            ["scope"] = invocation.Scope, ["productionCapacityVerified"] = invocation.ProductionCapacityVerified
        };
        if (invocation.RequestedWorkloadArguments is not null)
        {
            receipt["mode"] = "caller-specified-workload-arguments";
            receipt["requestedWorkloadArguments"] = invocation.RequestedWorkloadArguments;
            receipt["workloadArguments"] = invocation.WorkloadArguments;
        }
        var manifest = Directory.EnumerateFiles(Path.GetDirectoryName(binary)!)
            .Where(x => Path.GetExtension(x) is ".dll" or ".pdb" or ".json")
            .OrderBy(x => x, StringComparer.Ordinal).ToDictionary(x => Path.GetFileName(x), Hash);
        WriteJson(Path.Combine(run, "binary-manifest.json"), manifest);
        using var process = new Process();
        Task<string>? stdout = null, stderr = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            process.StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in invocation.ProcessArguments)
                process.StartInfo.ArgumentList.Add(arg);
            // 在运行任何托管业务代码前接入诊断端口，避免按 PID 轮询丢失启动和 JIT 方法事件。
            process.StartInfo.Environment["DOTNET_DefaultDiagnosticPortSuspend"] = "1";
            if (!process.Start()) throw new InvalidOperationException("Could not start benchmark process");
            receipt["processId"] = process.Id;
            receipt["arguments"] = process.StartInfo.ArgumentList.ToArray();
            WriteJson(Path.Combine(run, "capture.json"), receipt);
            stdout = process.StandardOutput.ReadToEndAsync();
            stderr = process.StandardError.ReadToEndAsync();
            var client = new DiagnosticsClient(process.Id);
            while (!DiagnosticsClient.GetPublishedProcesses().Contains(process.Id))
                await Task.Delay(10, timeout.Token);
            var providers = new[]
            {
                new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational),
                new EventPipeProvider(CpuMeasurementWindow.ProviderName, EventLevel.Informational),
                // Loader/JIT/NGen 方法事件；不采集无关的 GC 分配或任务事件。
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Verbose, 0x38)
            };
            receipt["providers"] = providers.Select(x => new { x.Name, level = (int)x.EventLevel, keywords = x.Keywords }).ToArray();
            using var session = await Task.Run(() => client.StartEventPipeSession(providers, true, 64))
                .WaitAsync(timeout.Token);
            var trace = Path.Combine(run, "cpu.nettrace");
            await using (var output = new FileStream(trace, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                var copy = session.EventStream.CopyToAsync(output, timeout.Token);
                await Task.Run(client.ResumeRuntime).WaitAsync(timeout.Token);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                    receipt["exitCode"] = process.ExitCode;
                    var processCpuSeconds = ReadProcessCpuSeconds(process);
                    receipt["processCpuSeconds"] = processCpuSeconds;
                    receipt["processCpuSecondsStatus"] = processCpuSeconds is null ? "NOT_AVAILABLE" : "AVAILABLE";
                    await copy;
                }
                catch
                {
                    // 先结束本轮子进程，解除 EventPipe 的同步关闭等待，再释放会话。
                    await StopOwnedProcess(process);
                    throw;
                }
            }
            if (process.ExitCode != 0) throw new InvalidOperationException($"Probe exited with {process.ExitCode}");
            receipt["traceSha256"] = Hash(trace);
            receipt["status"] = Analyze(trace, process.Id, Path.Combine(run, "report"),
                RequiresMeasurementWindow(workloadArguments)) == 0 ? "PASS" : "NOT_VERIFIED";
            return receipt["status"]!.Equals("PASS") ? 0 : 1;
        }
        catch (Exception ex)
        {
            receipt["error"] = ex.ToString();
            receipt["timedOut"] = ex is OperationCanceledException;
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (receipt.ContainsKey("processId")) await StopOwnedProcess(process);
            if (stdout != null) await File.WriteAllTextAsync(Path.Combine(run, "probe.log"), await stdout, Utf8);
            if (stderr != null) await File.WriteAllTextAsync(Path.Combine(run, "probe.stderr"), await stderr, Utf8);
            receipt["finishedAt"] = DateTime.UtcNow;
            WriteJson(Path.Combine(run, "capture.json"), receipt);
            Console.WriteLine($"CPU capture receipt: {run}");
        }
    }

    private static async Task StopOwnedProcess(Process process)
    {
        if (process.HasExited) return;
        process.Kill(entireProcessTree: true);
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await process.WaitForExitAsync(cleanup.Token);
    }

    internal static double? ReadProcessCpuSeconds(Process process)
    {
        try { return process.TotalProcessorTime.TotalSeconds; }
        catch (InvalidOperationException) { return null; }
    }

    internal static int Analyze(string trace, int targetPid, string folder, bool requireMeasurementWindow = false)
    {
        // 每次独立输出，保留原始 nettrace，不让解析缓存或覆盖历史报告影响结论。
        if (Directory.Exists(folder)) throw new IOException($"Report directory already exists: {folder}");
        Directory.CreateDirectory(folder);
        using var conversion = new StreamWriter(Path.Combine(folder, "conversion.log"), false, Utf8);
        var options = new TraceLogOptions { ConversionLog = conversion, ContinueOnError = false };
        var etlx = TraceLog.CreateFromEventPipeDataFile(trace, Path.Combine(folder, "cpu.etlx"), options);
        using var log = new TraceLog(etlx);
        var window = new CpuMeasurementWindow(targetPid, requireMeasurementWindow);
        foreach (var marker in log.Events)
            if (marker.ProviderName == CpuMeasurementWindow.ProviderName)
                window.AddMarker(marker.ProcessID, (int)marker.ID, marker.TimeStampRelativeMSec);
        var attribution = new CpuAttribution(targetPid);
        using var raw = new StreamWriter(Path.Combine(folder, "samples.jsonl"), false, Utf8);
        foreach (var sample in log.Events)
        {
            if (sample.ProcessID != targetPid || sample.ProviderName != "Microsoft-DotNETCore-SampleProfiler" ||
                sample.EventName != "Thread/Sample" || !window.Contains(sample.TimeStampRelativeMSec)) continue;
            var frames = new List<string>();
            for (var stack = sample.CallStack(); stack != null; stack = stack.Caller)
            {
                var address = stack.CodeAddress;
                frames.Add(string.IsNullOrEmpty(address.FullMethodName) ? $"[unresolved 0x{address.Address:x}]" : address.FullMethodName);
            }
            var kind = (ClrThreadSampleType)Convert.ToInt32(sample.PayloadValue(0), System.Globalization.CultureInfo.InvariantCulture);
            raw.WriteLine(JsonSerializer.Serialize(new { processId = sample.ProcessID, threadId = sample.ThreadID,
                timeMilliseconds = sample.TimeStampRelativeMSec, kind, frames }));
            attribution.Add(sample.ProcessID, kind, frames.ToArray());
        }
        File.WriteAllText(Path.Combine(folder, "methods.csv"), attribution.ToCsv(), Utf8);
        var verified = window.IsValid && attribution.IsVerified(log.EventsLost);
        var traceHash = Hash(trace);
        var components = attribution.Components.Snapshot();
        WriteJson(Path.Combine(folder, "components.json"), new
        {
            schema = "SqlCpuComponents-v1", classificationVersion = 1,
            status = verified ? "PASS" : "NOT_VERIFIED", processId = targetPid, traceSha256 = traceHash,
            observationWindow = window.Scope, measurementWindowValid = window.IsValid,
            window.StartMilliseconds, window.StopMilliseconds,
            components.ManagedSamples, components.Components, components.NearestOwners,
            denominator = "Same target PID/window/managed execution samples as methods.csv; missing and unresolved stacks remain in denominator",
            leafScope = "One fixed category of the sampled leaf per managed sample",
            nearestOwnerScope = "First recognized owner from leaf to root, skipping runtime/unresolved/other; leaf fallback if no owner. Call context, not inclusive time",
            weights = "Sample counts only; not exact CPU milliseconds, wall time, allocation bytes or host CPU percentage",
            classificationLimits = "Fixed namespace/method rules; inlining, async and truncated stacks may hide owners. dapper-materialization covers only GetSimpleValueDeserializer setup/closures",
            twoViewsNotAdditive = true,
            allocationAttributionVerified = false, allocationAttributionStatus = "NOT_COLLECTED",
            readWriteSplitVerified = false, nativeKernelAttributionVerified = false, productionCapacityVerified = false
        });
        WriteJson(Path.Combine(folder, "summary.json"), new
        {
            status = verified ? "PASS" : "NOT_VERIFIED", methodCpuVerified = verified,
            kind = "EventPipeManagedExecutionSamples", processId = targetPid, traceSha256 = traceHash,
            observationWindow = window.Scope, measurementWindowValid = window.IsValid,
            window.StartMilliseconds, window.StopMilliseconds,
            attribution.ManagedSamples, attribution.ExternalSamples, attribution.OtherSamples,
            attribution.MissingStackSamples, attribution.UnresolvedStackSamples, attribution.SqlStackSamples,
            hasCallStacks = log.HasCallStacks, eventsLost = log.EventsLost, traceDurationMilliseconds = log.SessionDuration.TotalMilliseconds,
            denominator = "Target PID managed-execution sample count; external/unknown-state samples excluded",
            weights = "One per recorded sample. Approximate relative managed execution attribution; not exact CPU milliseconds or host CPU percentage",
            inclusive = "Count a method once per sample even in recursion. Inclusive percentages across methods must not be summed",
            nativeKernelAttributionVerified = false, productionCapacityVerified = false,
            componentsReport = "components.json", componentAttributionVerified = verified,
            methodCount = attribution.Methods.Count
        });
        return verified ? 0 : 1;
    }
}
