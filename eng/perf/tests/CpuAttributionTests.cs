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
        Test("Attribution_FiltersTargetPid_AndSeparatesExternalSamples", () =>
        {
            var report = new CpuAttribution(42);
            report.Add(99, ClrThreadSampleType.Managed, new[] { "Other" });
            report.Add(42, ClrThreadSampleType.External, new[] { "Waiting" });
            report.Add(42, (ClrThreadSampleType)9, new[] { "UnknownState" });
            report.Add(42, ClrThreadSampleType.Managed, new[] { "Bing.Data.Sql.Builder.Render" });
            Equal(report.ManagedSamples, 1L); Equal(report.ExternalSamples, 1L);
            Equal(report.OtherSamples, 1L); Equal(report.Methods.Count, 1);
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
        var result = new { passed = cases.Count(x => JsonSerializer.Serialize(x).Contains("PASS")), failed = cases.Count(x => JsonSerializer.Serialize(x).Contains("FAIL")), cases };
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return result.failed == 0 ? 0 : 1;
    }
}
