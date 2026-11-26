using System.Diagnostics;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.EventPipe;

internal static class AttributionTests
{
    // 自测夹具由当前程序启动，避免依赖机器上的 SDK 输出量或 shell。
    internal static int RunChild(string mode)
    {
        if (mode == "large") { Console.Out.Write(new string('x', 256 * 1024)); return 0; }
        if (mode == "nonzero") { Console.Write("controlled failure"); return 23; }
        if (mode == "tree")
        {
            using var child = StartChild("hang");
            Console.WriteLine(child.Id);
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);
        }
        if (mode == "hang") { Thread.Sleep(Timeout.Infinite); }
        return 2;
    }

    private static Process StartChild(string mode)
    {
        var executable = Environment.ProcessPath ?? throw new Exception("Missing process path");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(AttributionTests).Assembly.Location);
        start.ArgumentList.Add("--self-test-child");
        start.ArgumentList.Add(mode);
        return Process.Start(start) ?? throw new Exception("Could not start controlled child process");
    }

    private static int WaitForChild(Process process, int timeoutMilliseconds, Func<Task>? startDrain = null)
    {
        try
        {
            // 子进程可能写满管道；必须在等待退出的同时持续消费 stdout。
            var drain = startDrain is null ? process.StandardOutput.BaseStream.CopyToAsync(Stream.Null) : startDrain();
            var exit = process.WaitForExitAsync();
            var timeout = Task.Delay(timeoutMilliseconds);
            var completed = Task.WhenAny(exit, drain, timeout).GetAwaiter().GetResult();
            if (completed == drain)
            {
                // 在子进程仍运行时也要立即观察读取故障。
                drain.GetAwaiter().GetResult();
                completed = Task.WhenAny(exit, timeout).GetAwaiter().GetResult();
            }
            if (completed == timeout)
            {
                if (drain.IsFaulted || drain.IsCanceled) drain.GetAwaiter().GetResult();
                throw new TimeoutException("Child process timed out");
            }
            exit.GetAwaiter().GetResult();
            drain.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit(5000)) throw new Exception("Child process cleanup timed out");
            }
        }
    }

    internal static int Run()
    {
        var cases = new List<object>();
        void Test(string name, Action body)
        {
            try { body(); cases.Add(new { name, status = "PASS" }); }
            catch (Exception ex) { cases.Add(new { name, status = "FAIL", error = ex.Message }); }
        }
        void Equal<T>(T actual, T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw new Exception($"Expected {expected}, got {actual}");
        }
        void SequenceEqual<T>(IEnumerable<T> actual, IEnumerable<T> expected)
        {
            if (!actual.SequenceEqual(expected)) throw new Exception($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
        }
        void CheckDrainFailure(string mode, Func<Exception, Task> startDrain, bool alreadyExited = false)
        {
            using var process = StartChild(mode);
            try
            {
                if (alreadyExited && !process.WaitForExit(10000)) throw new TimeoutException("Controlled child did not exit");
                var expected = new InvalidOperationException("controlled read failure");
                var watch = Stopwatch.StartNew();
                Exception? actual = null;
                try { WaitForChild(process, 5000, () => startDrain(expected)); }
                catch (Exception ex) { actual = ex; }
                if (!ReferenceEquals(actual, expected)) throw new Exception($"Expected original read failure, got {actual?.GetType().Name ?? "no exception"}");
                if (watch.ElapsedMilliseconds >= 3000) throw new Exception("Read failure was not reported promptly");
                Equal(process.HasExited, true);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(5000)) throw new Exception("Controlled child cleanup timed out");
                }
            }
        }
        Test("CaptureInvocation_FixedProbeKeepsExistingArgumentsAndScope", () =>
        {
            const string binary = @"C:\bench\Bing.Data.Sql.Benchmarks.dll";
            const string output = @"C:\results\run-123\probe.json";
            var invocation = SqlCpuTrace.CreateCaptureInvocation(binary, output, null);
            Equal(invocation.Workload, "SqlBuilderStateProbe-v1");
            SequenceEqual(invocation.ProcessArguments, new[] { binary, "--builder-state-probe", "--output", output });
            SequenceEqual(invocation.WorkloadArguments, new[] { "--builder-state-probe", "--output", output });
            Equal(invocation.Scope, "Entire fixed probe, including startup/JIT/validation/allocation measurement/JSON output; managed execution state only");
            Equal(invocation.ProductionCapacityVerified, false);
            Equal(invocation.RequestedWorkloadArguments, null);
        });
        Test("CaptureInvocation_CustomWorkloadReplacesOutputAndKeepsScopeBoundary", () =>
        {
            const string binary = @"C:\bench\Bing.Data.Sql.Benchmarks.dll";
            const string output = @"C:\results\run-456\probe.json";
            var requested = new[] { "--capacity-probe", "runtime", "10000", "8192", "64", "{output}", "--secondary={output}" };
            var effective = new[] { "--capacity-probe", "runtime", "10000", "8192", "64", output, "--secondary=" + output };
            var invocation = SqlCpuTrace.CreateCaptureInvocation(binary, output, requested);
            Equal(invocation.Workload, "CallerSpecifiedWorkload-v1");
            SequenceEqual(invocation.ProcessArguments, new[] { binary }.Concat(effective));
            SequenceEqual(invocation.WorkloadArguments, effective);
            SequenceEqual(invocation.RequestedWorkloadArguments!, requested);
            Equal(invocation.Scope, "Entire caller-specified workload, including startup/JIT and caller-defined warmup/measurement/output; managed execution state only");
            Equal(invocation.ProductionCapacityVerified, false);
        });
        Test("CaptureCpuTime_UnstartedProcessIsUnavailableNotZero", () =>
        {
            using var process = new Process();
            Equal(SqlCpuTrace.ReadProcessCpuSeconds(process), (double?)null);
        });
        Test("CaptureWindow_MixedCapacityAndStageRequireMeasurementMarkers", () =>
        {
            Equal(SqlCpuTrace.RequiresMeasurementWindow(new[] { "--mixed-capacity-probe", "workload.json" }), true);
            Equal(SqlCpuTrace.RequiresMeasurementWindow(new[] { "--mixed-stage-probe", "workload.json" }), true);
        });
        Test("CaptureWindow_OtherWorkloadsKeepAutomaticWindowDetection", () =>
        {
            Equal(SqlCpuTrace.RequiresMeasurementWindow(null), false);
            Equal(SqlCpuTrace.RequiresMeasurementWindow(Array.Empty<string>()), false);
            Equal(SqlCpuTrace.RequiresMeasurementWindow(new[] { "--builder-state-probe" }), false);
        });
        Test("CaptureCpuTime_ExitedLinuxProcessIsUnavailableNotZero", () =>
        {
            using var process = Process.Start(new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                ArgumentList = { "--info" }
            }) ?? throw new Exception("Could not start dotnet child process");
            Equal(WaitForChild(process, 15000), 0);
            var cpuSeconds = SqlCpuTrace.ReadProcessCpuSeconds(process);
            if (OperatingSystem.IsLinux()) Equal(cpuSeconds, (double?)null);
        });
        Test("ChildProcess_LargeRedirectedOutputCompletes", () =>
        {
            using var process = StartChild("large");
            Equal(WaitForChild(process, 15000), 0);
            Equal(process.HasExited, true);
        });
        Test("ChildProcess_NonzeroExitIsPreserved", () =>
        {
            using var process = StartChild("nonzero");
            Equal(WaitForChild(process, 15000), 23);
            Equal(process.HasExited, true);
        });
        Test("ChildProcess_TimeoutKillsAndReaps", () =>
        {
            using var process = StartChild("hang");
            var timedOut = false;
            try { WaitForChild(process, 1000); }
            catch (TimeoutException) { timedOut = true; }
            Equal(timedOut, true);
            Equal(process.HasExited, true);
        });
        Test("ChildProcess_OutputReadFailureKillsAndReaps", () =>
        {
            using var process = StartChild("hang");
            process.StandardOutput.Dispose();
            var failed = false;
            try { WaitForChild(process, 1000); }
            catch (ObjectDisposedException) { failed = true; }
            Equal(failed, true);
            Equal(process.HasExited, true);
        });
        Test("ChildProcess_SynchronousReadFailureKillsAndReaps", () =>
            CheckDrainFailure("hang", error => throw error));
        Test("ChildProcess_FaultedReadTaskKillsAndReaps", () =>
            CheckDrainFailure("hang", error => Task.FromException(error)));
        Test("ChildProcess_DelayedReadFailureKillsAndReaps", () =>
            CheckDrainFailure("hang", async error => { await Task.Delay(100); throw error; }));
        Test("ChildProcess_ExitedChildPreservesReadFailure", () =>
            CheckDrainFailure("nonzero", error => Task.FromException(error), alreadyExited: true));
        Test("ChildProcess_TimeoutKillsDescendant", () =>
        {
            using var process = StartChild("tree");
            try
            {
                var line = process.StandardOutput.ReadLineAsync();
                if (!line.Wait(10000)) throw new TimeoutException("Child tree did not start");
                var childId = int.Parse(line.GetAwaiter().GetResult() ?? throw new Exception("Missing child PID"));
                var timedOut = false;
                try { WaitForChild(process, 1000); }
                catch (TimeoutException) { timedOut = true; }
                Equal(timedOut, true);
                Equal(process.HasExited, true);
                try
                {
                    using var descendant = Process.GetProcessById(childId);
                    if (!descendant.WaitForExit(5000)) throw new Exception("Descendant survived timeout cleanup");
                }
                catch (ArgumentException) { /* 已回收的 PID 不再存在。 */ }
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(5000)) throw new Exception("Child tree cleanup timed out");
                }
            }
        });
        Test("Attribution_FiltersTargetPid_AndSeparatesExternalSamples", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(99, ClrThreadSampleType.Managed, new[] { "Other" });
            report.Add(42, ClrThreadSampleType.External, new[] { "Waiting" });
            report.Add(42, (ClrThreadSampleType)9, new[] { "UnknownState" });
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Builder.Render" });
            Equal(report.ManagedSamples, 1L); Equal(report.ExternalSamples, 1L);
            Equal(report.OtherSamples, 1L); Equal(report.Methods.Count, 1);
            Equal(report.Components.Snapshot().ManagedSamples, 1L);
            Equal(report.Components.Snapshot().NearestOwners.Single().Method, "Bing.Data.Sql.Builder.Render");
        });
        Test("Attribution_LeafAndInclusiveHaveSameManagedSampleDenominator", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(42, ClrThreadSampleType.Managed, new[] { "System.String.Concat", "Bing.Data.Sql.Builder.Render" });
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Builder.Render" });
            Equal(report.Methods["Bing.Data.Sql.Builder.Render"].SelfSamples, 1L);
            Equal(report.Methods["Bing.Data.Sql.Builder.Render"].InclusiveSamples, 2L);
            Equal(report.ManagedSamples, 2L); Equal(report.SqlStackSamples, 2L);
            Equal(report.Methods.Values.Sum(x => x.SelfSamples), 2L);
        });
        Test("Attribution_RecursiveMethodCountsOncePerInclusiveSample", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Builder.Render", "Bing.Data.Sql.Builder.Render" });
            Equal(report.Methods["Bing.Data.Sql.Builder.Render"].InclusiveSamples, 1L);
        });
        Test("Attribution_MissingStackRemainsInDenominator", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(42, ClrThreadSampleType.Managed, Array.Empty<string>());
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Builder.Render" });
            Equal(report.ManagedSamples, 2L); Equal(report.MissingStackSamples, 1L);
            Equal(report.Methods["[missing stack]"].SelfSamples, 1L);
            Equal(report.Components.Snapshot().Components.Single(x => x.Component == "unresolved").LeafSamples, 1L);
        });
        Test("Attribution_UnresolvedFrameIsReported_AndDoesNotBecomeSqlMethod", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(42, ClrThreadSampleType.Managed, new[] { "[unresolved 0x123]", "Bing.Data.Sql.Builder.Render" });
            Equal(report.UnresolvedStackSamples, 1L); Equal(report.SqlStackSamples, 1L);
            Equal(report.Methods["[unresolved 0x123]"].SelfSamples, 1L);
        });
        Test("Attribution_RejectsEmptyOrLostOrUnresolvedOnlyEvidence", () =>
        {
            var report = new CpuAttribution(42);
            Equal(report.IsVerified(0), false);
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Benchmarks.SqlBuilderStateProbe.Run" });
            Equal(report.IsVerified(0), false);
            report.Add(42, ClrThreadSampleType.Managed, new[] { "[unresolved 0x123]" });
            Equal(report.IsVerified(0), false);
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Builder.Render" });
            Equal(report.IsVerified(1), false); Equal(report.IsVerified(0), true);
        });
        Test("Attribution_CsvEscapesGenericMethodNames_AndUsesInvariantNumbers", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Map<System.String,\"中文\">" });
            var expected = "Method,SelfSamples,InclusiveSamples,SelfPercent,InclusivePercent\n\"Bing.Data.Sql.Map<System.String,\"\"中文\"\">\",1,1,100,100\n";
            Equal(report.ToCsv(), expected);
        });
        Test("Attribution_ComponentLabelDoesNotWidenExistingSqlEvidenceGate", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(42, ClrThreadSampleType.Managed, ["Bing.Dapper.QueryExecutor.Execute"]);
            Equal(report.Components.Snapshot().Components.Single(x => x.Component == "sql-execution").LeafSamples, 1L);
            Equal(report.SqlStackSamples, 0L);
            Equal(report.IsVerified(0), false);
        });
        Test("Window_UnmarkedTraceKeepsLegacyScope", () =>
        {
            var window = new CpuMeasurementWindow(42, false);
            Equal(window.IsValid, true); Equal(window.Contains(0), true);
            Equal(window.Scope, "entire-process");
        });
        Test("Window_RequiredMarkersCannotSilentlyFallBack", () =>
        {
            var window = new CpuMeasurementWindow(42, true);
            Equal(window.IsValid, false); Equal(window.Contains(100), false);
            window.AddMarker(42, 1, 10);
            Equal(window.IsValid, false); Equal(window.Contains(100), false);
            var missingStart = new CpuMeasurementWindow(42, true);
            missingStart.AddMarker(42, 2, 20);
            Equal(missingStart.IsValid, false); Equal(missingStart.Contains(15), false);
        });
        Test("Window_FiltersPidAndUsesHalfOpenMeasurementThroughDrain", () =>
        {
            var window = new CpuMeasurementWindow(42, true);
            window.AddMarker(99, 1, 0); window.AddMarker(99, 2, 100);
            window.AddMarker(42, 1, 10); window.AddMarker(42, 2, 20);
            Equal(window.IsValid, true); Equal(window.Contains(9.9), false);
            Equal(window.Contains(10), true); Equal(window.Contains(19.9), true);
            Equal(window.Contains(20), false); Equal(window.Scope, "measure-through-drain");
        });
        Test("Window_RejectsDuplicateReversedAndNonFiniteMarkers", () =>
        {
            foreach (var pair in new[] { (20d, 10d), (10d, 10d), (-1d, 20d), (double.NaN, 20d), (10d, double.PositiveInfinity) })
            {
                var window = new CpuMeasurementWindow(42, true);
                window.AddMarker(42, 1, pair.Item1); window.AddMarker(42, 2, pair.Item2);
                Equal(window.IsValid, false); Equal(window.Contains(15), false);
            }
            var duplicate = new CpuMeasurementWindow(42, false);
            duplicate.AddMarker(42, 1, 10); duplicate.AddMarker(42, 1, 11); duplicate.AddMarker(42, 2, 20);
            Equal(duplicate.IsValid, false); Equal(duplicate.Contains(15), false);
        });
        CpuComponentAttributionTests.Run(Test);
        var result = new { passed = cases.Count(x => JsonSerializer.Serialize(x).Contains("PASS")), failed = cases.Count(x => JsonSerializer.Serialize(x).Contains("FAIL")), cases };
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return result.failed == 0 ? 0 : 1;
    }
}
