using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>固定规模的 Builder 资源探测。不是正式性能门槛，达到固定次数即停止。</summary>
internal static class SqlBuilderStateProbe
{
    internal static int Run(string output)
    {
        const int batch = 32, warmups = 2, samples = 5, renderIterations = 100;
        var results = new List<object>();
        foreach (var shape in new[]
                 {
                     "simple", "cte-union-depth6", "fragment-cte-union", "dynamic-filter-page", "cte-wide-1", "cte-wide-8",
                     "cte-wide-32", "cte-wide-128"
                 })
        {
            var expected = Build(shape);
            var candidate = Build(shape);
            var baseline = (SqlBuilderBase)expected.Clone();
            baseline.Where("Probe", 9);
            using (var checkpoint = candidate.BeginMutationCheckpoint())
            {
                if (checkpoint == null) throw new InvalidOperationException("Probe must exercise the built-in checkpoint.");
                candidate.Where("Probe", 9);
                checkpoint.Complete();
            }
            if (baseline.ToSql() != candidate.ToSql() ||
                !baseline.GetParams().SequenceEqual(candidate.GetParams()))
                throw new InvalidOperationException("SQL/parameter pairs differ.");

            foreach (var mode in new[] { "full-tree-clone", "clause-checkpoint" })
            {
                for (var sample = -warmups; sample < samples; sample++)
                {
                    var inputs = Enumerable.Range(0, batch).Select(_ => Build(shape)).ToArray();
                    CountedChild.Clones = 0;
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var timer = Stopwatch.StartNew();
                    foreach (var source in inputs)
                    {
                        if (mode == "full-tree-clone")
                            ((SqlBuilderBase)source.Clone()).Where("Probe", 9);
                        else
                        {
                            using var checkpoint = source.BeginMutationCheckpoint();
                            source.Where("Probe", 9);
                            checkpoint.Complete();
                        }
                    }
                    timer.Stop();
                    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (sample >= 0) results.Add(new
                    {
                        shape, operation = "draft-mutation", mode, sample, operations = batch,
                        nsPerOperation = timer.Elapsed.TotalNanoseconds / batch,
                        bytesPerOperation = (double)allocated / batch,
                        // 子树 Clone 为实际覆写计数；根 Clone 是此探测显式执行的调用。
                        rootCloneCalls = mode == "full-tree-clone" ? batch : 0,
                        childCloneCalls = CountedChild.Clones
                    });
                }
            }
            var render = Build(shape);
            var first = render.ToSql();
            for (var sample = -warmups; sample < samples; sample++)
            {
                CountedChild.Clones = 0;
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                for (var iteration = 0; iteration < renderIterations; iteration++)
                    if (render.ToSql() != first) throw new InvalidOperationException("Unstable render.");
                timer.Stop();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (sample >= 0) results.Add(new
                {
                    shape, operation = "repeat-render", mode = "current", sample, operations = renderIterations,
                    nsPerOperation = timer.Elapsed.TotalNanoseconds / renderIterations,
                    bytesPerOperation = (double)allocated / renderIterations,
                    childCloneCalls = CountedChild.Clones,
                    sqlCharacters = first.Length, parameterCount = render.GetParams().Count
                });
            }
        }
        ProbeFragmentMerges(results, warmups, samples);
        ProbeNestedBuilders(results, warmups, samples);
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            workload = "SqlBuilderStateProbe-v1", framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            warmups, samples, batch, renderIterations,
            comparison = "Same-candidate old full-tree strategy vs clause checkpoint; not a historical binary baseline",
            results
        }, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        return 0;
    }

    /// <summary>记录嵌套 Builder 合并和模板冻结的每次分配，分别测量有键与无键情况。</summary>
    private static void ProbeNestedBuilders(List<object> results, int warmups, int samples)
    {
        const int iterations = 1000;
        foreach (var keyed in new[] { false, true })
        {
            var key = new SqlParameterKey("request", "id");
            var fragment = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 7) });
            if (keyed) fragment = fragment.WithParameterKey("id", key);
            var child = new SqliteBuilder().Select("*").From(fragment, "leaf");
            for (var sample = -warmups; sample < samples; sample++)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    var template = new SqliteBuilder().Select("*").From(child, "outer").ToTemplate();
                    if (template.Sql != "Select * \r\nFrom (Select * \r\nFrom (SELECT @id AS value) As `leaf`) As `outer`" ||
                        template.Slots.Count != 1 || template.Slots[0] != (keyed ? key : new SqlParameterKey("id")))
                        throw new InvalidOperationException("Nested builder template differs.");
                }
                timer.Stop();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (sample >= 0) results.Add(new
                {
                    shape = keyed ? "keyed-nested-builder" : "nested-builder",
                    operation = "nested-builder-template", mode = "current", sample, operations = iterations,
                    nsPerOperation = timer.Elapsed.TotalNanoseconds / iterations,
                    bytesPerOperation = (double)allocated / iterations
                });
            }
        }
    }

    /// <summary>测量冻结片段合并时的常见名称和发生冲突时的开销。</summary>
    private static void ProbeFragmentMerges(List<object> results, int warmups, int samples)
    {
        const int iterations = 1000;
        var fragment = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("@id", 7) });
        foreach (var scenario in new[] { "fragment-no-collision", "fragment-collision", "keyed-fragment" })
        {
            var collision = scenario == "fragment-collision";
            var input = scenario == "keyed-fragment"
                ? fragment.WithParameterKey("id", new SqlParameterKey("request", "id")) : fragment;
            for (var sample = -warmups; sample < samples; sample++)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    var builder = new SqliteBuilder();
                    if (collision) builder.AddParam("id", 100);
                    builder.Select("*").From(input, "items");
                    if (builder.GetParams().Count != (collision ? 2 : 1))
                        throw new InvalidOperationException("Fragment merge parameter count differs.");
                }
                timer.Stop();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (sample >= 0) results.Add(new
                {
                    shape = scenario,
                    operation = "fragment-merge", mode = "current", sample, operations = iterations,
                    nsPerOperation = timer.Elapsed.TotalNanoseconds / iterations,
                    bytesPerOperation = (double)allocated / iterations
                });
            }
        }
        ProbeFragmentTemplates(results, warmups, samples);
    }

    /// <summary>分别记录普通与逻辑键片段冻结为模板的分配，不作为历史版本对照。</summary>
    private static void ProbeFragmentTemplates(List<object> results, int warmups, int samples)
    {
        const int iterations = 1000;
        foreach (var keyed in new[] { false, true })
        {
            var fragment = new SqlQueryFragment("SELECT @id AS value", new[] { new SqlParam("id", 7) });
            if (keyed) fragment = fragment.WithParameterKey("id", new SqlParameterKey("request", "id"));
            var builder = new SqliteBuilder().Select("*").From(fragment, "items");
            for (var sample = -warmups; sample < samples; sample++)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    var template = builder.ToTemplate();
                    if (template.Sql != "Select * \r\nFrom (SELECT @id AS value) As `items`" ||
                        template.Slots.Count != 1)
                        throw new InvalidOperationException("Fragment template differs.");
                }
                timer.Stop();
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                if (sample >= 0) results.Add(new
                {
                    shape = keyed ? "keyed-fragment" : "fragment-no-collision",
                    operation = "fragment-template", mode = "current", sample, operations = iterations,
                    nsPerOperation = timer.Elapsed.TotalNanoseconds / iterations,
                    bytesPerOperation = (double)allocated / iterations
                });
            }
        }
    }

    private static SqliteBuilder Build(string shape)
    {
        var services = shape == "dynamic-filter-page" || shape.StartsWith("cte-wide-", StringComparison.Ordinal)
            ? new SqlBuilderServices(filters: new ISqlFilter[]
            {
                shape.StartsWith("cte-wide-", StringComparison.Ordinal) ? new SnapshotFilter() : new ProbeFilter()
            }) : null;
        var root = new SqliteBuilder(services);
        if (shape == "dynamic-filter-page") root.Select<ProbeRow>(row => row.Id).From<ProbeRow>("r").Where("Enabled", true);
        else root.Select("Id").From("Rows").Where("Enabled", true);
        if (shape == "cte-union-depth6")
        {
            root.With("nested", Nested(6));
            root.Union(Nested(0));
            root.UnionAll(Nested(0));
        }
        if (shape == "fragment-cte-union")
        {
            root.With("fragment_cte", new SqlQueryFragment("SELECT @id AS Id", new[] { new SqlParam("id", 7) }));
            root.UnionAll(new SqlQueryFragment("SELECT @id AS Id", new[] { new SqlParam("id", 9) }));
        }
        if (shape.StartsWith("cte-wide-", StringComparison.Ordinal))
        {
            var width = int.Parse(shape.AsSpan("cte-wide-".Length));
            for (var index = 0; index < width; index++)
                root.With("wide" + index.ToString("D3"), Nested(0));
        }
        if (shape == "dynamic-filter-page") root.OrderBy("Id").Page(new Bing.Data.Pager(2, 20));
        return root;
    }

    private static CountedChild Nested(int depth)
    {
        var child = new CountedChild();
        child.Select("Id").From("Rows").Where("Depth", depth);
        if (depth != 0) child.With("level" + depth, Nested(depth - 1));
        return child;
    }

    /// <summary>仅测量可扩展子树边界的真实 Clone 次数，不修改生产热路径。</summary>
    private sealed class CountedChild : SqliteBuilder
    {
        internal static int Clones;
        public override ISqlBuilder Clone() { Clones++; return base.Clone(); }
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameters) => new CountedChild();
    }

    private sealed class ProbeFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context)
        {
            foreach (var source in context.Sources)
                context.AddPredicate(source, context.GetColumn(source, nameof(ProbeRow.Tenant)), 7);
        }
    }

    private sealed class SnapshotFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context) { }
    }

    private sealed class ProbeRow { public int Id { get; set; } public int Tenant { get; set; } }
}
