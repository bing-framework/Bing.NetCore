using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>固定工作负载下测量实际存活堆，区分首次分配与长期保留，不执行数据库 I/O。</summary>
internal static class SqlQueryRetentionProbe
{
    private const int Count = 256;

    internal static int Run(string output)
    {
        using var services = new ServiceCollection().AddSqliteProvider().BuildServiceProvider();
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var executor = new SqliteSqlQuery(services, new SqlOptions<SqliteSqlQuery>
        {
            DatabaseType = DatabaseType.Sqlite, Connection = connection
        });
        var results = new List<object>();
        foreach (var shape in new[] { "simple", "cte-union" })
        foreach (var conditions in new[] { 1, 8 })
        foreach (var kind in new[] { "template", "explicit-plan", "query-after-first-terminal" })
            results.Add(Measure(kind, shape, conditions, executor));
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            workload = "SqlQueryRetentionProbe-v1", count = Count,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            method = "Full GC before/after; roots include weak-reference bookkeeping; shared executor pre-exists; no capacity/CPU threshold",
            results
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return 0;
    }

    // 独立栈帧保证上一个 cell 的根不跨越下一个 cell 的 GC 基线。
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object Measure(string kind, string shape, int conditions, ISqlQueryPlanExecutor executor)
    {
        Warmup(kind, shape, conditions, executor);
        Collect();
        var before = GC.GetTotalMemory(false);
        var allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var roots = Capture(kind, shape, conditions, executor, Count);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        Collect();
        var after = GC.GetTotalMemory(false);
        var result = new
        {
            kind, shape, conditions, instances = Count, beforeBytes = before, afterBytes = after,
            retainedBytes = after - before, retainedBytesPerInstance = (double)(after - before) / Count,
            // 包含快照校验和 WeakReference 记账，不能当作单个 GetPlan 的微基准。
            captureAllocatedBytesPerInstance = (double)allocated / Count,
            liveSourceBuilders = roots.Count(item => item.Source.IsAlive),
            liveBuilderServices = roots.Count(item => item.Services.IsAlive)
        };
        GC.KeepAlive(roots);
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Warmup(string kind, string shape, int conditions, ISqlQueryPlanExecutor executor)
    {
        var roots = Capture(kind, shape, conditions, executor, 8);
        GC.KeepAlive(roots);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Captured[] Capture(string kind, string shape, int conditions, ISqlQueryPlanExecutor executor, int count)
    {
        var roots = new Captured[count];
        var expected = Build(SqlBuilderServices.CreateDefault(), shape, conditions);
        var expectedSql = expected.ToSql();
        var expectedValues = ((ISqlParameter)expected).GetSqlParams().Values.Select(p => p.Value).ToArray();
        for (var index = 0; index < count; index++)
        {
            var builderServices = SqlBuilderServices.CreateDefault();
            var builder = Build(builderServices, shape, conditions);
            // 校验使用另一个草稿，不预先渲染被测实例的首次终结路径。
            object root;
            string sql;
            IEnumerable<SqlParam> parameters;
            if (kind == "template")
            {
                var template = builder.ToTemplate();
                root = template; sql = template.Sql; parameters = template.Parameters;
            }
            else
            {
                var query = new SqlQuery(executor, builder);
                var plan = kind == "explicit-plan" ? SqlQueryPlan.Create(builder).Freeze() : query.GetPlan();
                root = kind == "explicit-plan" ? plan : query;
                sql = plan.CommandText; parameters = (SqlParam[])plan.Parameters;
            }
            if (sql != expectedSql || !parameters.Select(p => p.Value).SequenceEqual(expectedValues))
                throw new InvalidOperationException("Frozen SQL/parameters differ from draft output.");
            roots[index] = new Captured(root, new WeakReference(builder), new WeakReference(builderServices));
        }
        return roots;
    }

    private static ISqlBuilder Build(SqlBuilderServices services, string shape, int conditions)
    {
        var builder = new SqliteBuilder(services).Select("Name").From(shape == "simple" ? "samples" : "recent");
        for (var i = 0; i < conditions; i++) builder.Where($"Status{i}", i);
        if (shape == "cte-union")
        {
            builder.With("recent", new SqliteBuilder(services).Select("Name").From("samples").Where("Kind", 9));
            builder.UnionAll(new SqliteBuilder(services).Select("Name").From("archive").Where("Kind", 10));
        }
        return builder.OrderBy("Name");
    }

    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    private sealed record Captured(object Root, WeakReference Source, WeakReference Services);
}
