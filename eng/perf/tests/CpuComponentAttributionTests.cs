internal static class CpuComponentAttributionTests
{
    internal static void Run(Action<string, Action> test)
    {
        test("ComponentClassification_DriverAndNamespacePrefixesIgnoreSignatureTypes", () =>
        {
            Equal(CpuComponentAttribution.Classify("Npgsql.NpgsqlCommand.ExecuteReader"), "driver");
            Equal(CpuComponentAttribution.Classify("System.Collections.Generic.List<Npgsql.Type>.Add"), "runtime");
            Equal(CpuComponentAttribution.Classify("System.Collections.Generic.List<Bing.Data.Sql.Type>.Add"), "runtime");
            Equal(CpuComponentAttribution.Classify("Microsoft.Win32.Registry.GetValue"), "runtime");
            Equal(CpuComponentAttribution.Classify("Interop+Crypto.GetRandomBytes"), "runtime");
        });

        test("ComponentClassification_DapperMaterializationRequiresExactMethodShape", () =>
        {
            Equal(CpuComponentAttribution.Classify("Dapper.SqlMapper.GetSimpleValueDeserializer(System.Type)"), "dapper-materialization");
            Equal(CpuComponentAttribution.Classify("Dapper.SqlMapper+<>c__DisplayClass10_0.<GetSimpleValueDeserializer>b__0"), "dapper-materialization");
            Equal(CpuComponentAttribution.Classify("Dapper.SqlMapper.GetSimpleValueDeserializerWrapper"), "dapper");
            Equal(CpuComponentAttribution.Classify("Dapper.SqlMapper.Parse(System.GetSimpleValueDeserializer)"), "dapper");
            Equal(CpuComponentAttribution.Classify("Dapper.SqlMapper+<>c.<Other>b__0(System.Func.<GetSimpleValueDeserializer>)"), "dapper");
            Equal(CpuComponentAttribution.Classify("Dapper.SqlMapper.Parse"), "dapper");
        });

        test("ComponentClassification_PlanningMappingExecutionAndHarnessHaveBoundaries", () =>
        {
            Equal(CpuComponentAttribution.Classify("Bing.Data.Sql.Builders.SqlBuilder.Build"), "sql-planning");
            Equal(CpuComponentAttribution.Classify("Bing.Data.Sql.Runtime.Plans.QueryPlan.Create"), "sql-planning");
            Equal(CpuComponentAttribution.Classify("Bing.Data.Sql.SqlBuilderRuntimeBridge.Render"), "sql-planning");
            Equal(CpuComponentAttribution.Classify("Bing.Data.Sql.SqlQuery.GetPlan()"), "sql-planning");
            Equal(CpuComponentAttribution.Classify("Bing.Data.Sql.SqlQuery.GetPlanner"), "sql-execution");
            Equal(CpuComponentAttribution.Classify("Bing.Data.Sql.Metadata.EntityMap.Resolve"), "sql-mapping");
            Equal(CpuComponentAttribution.Classify("Bing.Dapper.QueryExecutor.Execute"), "sql-execution");
            Equal(CpuComponentAttribution.Classify("Bing.Data.Sql.Benchmarks.Probe.Run"), "harness");
            Equal(CpuComponentAttribution.Classify("Microsoft.Extensions.DependencyInjection.ServiceProvider.GetService"), "dependency-injection");
            Equal(CpuComponentAttribution.Classify("Sample.Application.Worker.Run"), "other");
        });

        test("ComponentSnapshot_ChoosesNearestMeaningfulOwnerAndFallsBackToLeaf", () =>
        {
            var report = new CpuComponentAttribution();
            report.Add(new[]
            {
                "System.Linq.Enumerable.ToArray",
                "Npgsql.NpgsqlCommand.ExecuteReader",
                "Bing.Data.Sql.Metadata.EntityMap.Resolve"
            });
            report.Add(new[] { "[unresolved 0x123]", "System.Runtime.SomeHelper" });

            var snapshot = report.Snapshot();
            Equal(snapshot.NearestOwners.Single(x => x.Method == "Npgsql.NpgsqlCommand.ExecuteReader").Component, "driver");
            Equal(snapshot.NearestOwners.Single(x => x.Method == "Npgsql.NpgsqlCommand.ExecuteReader").Samples, 1L);
            Equal(snapshot.NearestOwners.Single(x => x.Method == "[unresolved 0x123]").Component, "unresolved");
        });

        test("ComponentSnapshot_ConservesManagedSamplesAcrossIndependentViews", () =>
        {
            var report = new CpuComponentAttribution();
            report.Add(new[] { "Npgsql.NpgsqlCommand.ExecuteReader", "Bing.Data.Sql.Query.Execute" });
            report.Add(new[] { "System.Linq.Enumerable.ToArray", "Bing.Data.Sql.Query.Execute" });
            report.Add(new[] { "Bing.Data.Sql.Benchmarks.Probe.Run", "Bing.Data.Sql.Query.Execute" });
            report.Add(new[] { "[unresolved 0x123]" });
            report.Add(Array.Empty<string>());

            var snapshot = report.Snapshot();
            Equal(snapshot.ManagedSamples, 5L);
            Equal(snapshot.Components.Sum(x => x.LeafSamples), snapshot.ManagedSamples);
            Equal(snapshot.Components.Sum(x => x.NearestOwnerSamples), snapshot.ManagedSamples);
            Equal(snapshot.Components.Sum(x => x.LeafPercent ?? 0d), 100d);
            Equal(snapshot.Components.Sum(x => x.NearestOwnerPercent ?? 0d), 100d);
            Equal(snapshot.Components.Single(x => x.Component == "harness").LeafSamples, 1L);
            Equal(snapshot.Components.Single(x => x.Component == "sql-execution").NearestOwnerSamples, 1L);
            Equal(snapshot.Components.Single(x => x.Component == "harness").NearestOwnerSamples, 1L);
            Equal(snapshot.Components.Single(x => x.Component == "unresolved").LeafSamples, 2L);
            Equal(snapshot.NearestOwners.Single(x => x.Method == "[missing stack]").Component, "unresolved");
        });

        test("ComponentSnapshot_RecursiveFramesCountOncePerSample", () =>
        {
            var report = new CpuComponentAttribution();
            report.Add(new[] { "Bing.Data.Sql.Builders.QueryBuilder.Render", "Bing.Data.Sql.Builders.QueryBuilder.Render" });

            var snapshot = report.Snapshot();
            Equal(snapshot.ManagedSamples, 1L);
            Equal(snapshot.Components.Single(x => x.Component == "sql-planning").LeafSamples, 1L);
            Equal(snapshot.Components.Single(x => x.Component == "sql-planning").NearestOwnerSamples, 1L);
            Equal(snapshot.NearestOwners.Single().Samples, 1L);
        });

        test("ComponentSnapshot_EmptyReportHasFixedRowsAndNullPercentages", () =>
        {
            var snapshot = new CpuComponentAttribution().Snapshot();
            Equal(snapshot.ManagedSamples, 0L);
            SequenceEqual(snapshot.Components.Select(x => x.Component), new[]
            {
                "driver",
                "dapper-materialization",
                "dapper",
                "sql-planning",
                "sql-mapping",
                "sql-execution",
                "dependency-injection",
                "harness",
                "runtime",
                "unresolved",
                "other"
            });
            Equal(snapshot.Components.All(x => x.LeafSamples == 0 && x.NearestOwnerSamples == 0), true);
            Equal(snapshot.Components.All(x => x.LeafPercent is null && x.NearestOwnerPercent is null), true);
            Equal(snapshot.NearestOwners.Length, 0);
        });

        test("ComponentSnapshot_IsIsolatedFromSubsequentAdds", () =>
        {
            var report = new CpuComponentAttribution();
            report.Add(new[] { "Npgsql.NpgsqlCommand.ExecuteReader" });
            var first = report.Snapshot();
            report.Add(new[] { "Dapper.SqlMapper.Parse" });
            var second = report.Snapshot();

            Equal(first.ManagedSamples, 1L);
            Equal(first.Components.Sum(x => x.LeafSamples), 1L);
            Equal(second.ManagedSamples, 2L);
            Equal(second.Components.Sum(x => x.LeafSamples), 2L);
            Equal(first.NearestOwners.Length, 1);
            Equal(second.NearestOwners.Length, 2);
        });

        test("ComponentSnapshot_SortsOwnersByCountThenOrdinalMethod", () =>
        {
            var report = new CpuComponentAttribution();
            report.Add(new[] { "Bing.Data.Sql.Query.Alpha" });
            report.Add(new[] { "Bing.Data.Sql.Query.Alpha" });
            report.Add(new[] { "Bing.Data.Sql.Query.Zulu" });
            report.Add(new[] { "Bing.Data.Sql.Query.beta" });

            var methods = report.Snapshot().NearestOwners.Select(x => x.Method).ToArray();
            SequenceEqual(methods, new[]
            {
                "Bing.Data.Sql.Query.Alpha",
                "Bing.Data.Sql.Query.Zulu",
                "Bing.Data.Sql.Query.beta"
            });
        });
    }

    private static void Equal<T>(T actual, T expected)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new Exception($"Expected {expected}, got {actual}");
    }

    private static void SequenceEqual<T>(IEnumerable<T> actual, IEnumerable<T> expected)
    {
        if (!actual.SequenceEqual(expected))
            throw new Exception($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
    }
}
