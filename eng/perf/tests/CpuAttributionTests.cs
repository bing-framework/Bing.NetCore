using System.Diagnostics;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.EventPipe;

internal static class AttributionTests
{
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
            process.WaitForExit();
            var cpuSeconds = SqlCpuTrace.ReadProcessCpuSeconds(process);
            if (OperatingSystem.IsLinux()) Equal(cpuSeconds, (double?)null);
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
