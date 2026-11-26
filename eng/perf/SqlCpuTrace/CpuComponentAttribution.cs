/// <summary>离线采样的固定组件分类；叶帧和最近调用方是两种独立视图，不能相加。</summary>
internal sealed class CpuComponentAttribution
{
    private static readonly string[] ComponentNames =
    [
        "driver", "dapper-materialization", "dapper", "sql-planning", "sql-mapping",
        "sql-execution", "dependency-injection", "harness", "runtime", "unresolved", "other"
    ];

    private readonly Dictionary<string, long> _leaves = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _owners = new(StringComparer.Ordinal);
    private long _managedSamples;

    /// <summary>只匹配方法所属类型的前缀，避免把参数和泛型参数类型误认为调用方。</summary>
    internal static string Classify(string frame)
    {
        if (frame.StartsWith("Bing.Data.Sql.Benchmarks.", StringComparison.Ordinal)) return "harness";
        if (frame.StartsWith("Bing.Data.Sql.Builders.", StringComparison.Ordinal) ||
            frame.StartsWith("Bing.Data.Sql.Runtime.Plans.", StringComparison.Ordinal) ||
            frame.StartsWith("Bing.Data.Sql.SqlQuery.GetPlan(", StringComparison.Ordinal) ||
            frame.StartsWith("Bing.Data.Sql.SqlBuilderRuntimeBridge.", StringComparison.Ordinal)) return "sql-planning";
        if (frame.StartsWith("Bing.Data.Sql.Metadata.", StringComparison.Ordinal)) return "sql-mapping";
        if (frame.StartsWith("Bing.Data.Sql.", StringComparison.Ordinal) ||
            frame.StartsWith("Bing.Dapper.", StringComparison.Ordinal)) return "sql-execution";
        if (frame.StartsWith("Npgsql.", StringComparison.Ordinal)) return "driver";
        // 只识别已核实的简单值反序列化入口/闭包，不把 Dapper 整个查询过程称为物化。
        if (frame.StartsWith("Dapper.SqlMapper.GetSimpleValueDeserializer(", StringComparison.Ordinal)) return "dapper-materialization";
        if (frame.StartsWith("Dapper.SqlMapper+<>c", StringComparison.Ordinal))
        {
            var parameters = frame.IndexOf('(');
            var method = frame.AsSpan(0, parameters < 0 ? frame.Length : parameters);
            if (method.Contains(".<GetSimpleValueDeserializer>b__", StringComparison.Ordinal)) return "dapper-materialization";
        }
        if (frame.StartsWith("Dapper.", StringComparison.Ordinal)) return "dapper";
        if (frame.StartsWith("Microsoft.Extensions.DependencyInjection.", StringComparison.Ordinal)) return "dependency-injection";
        if (frame.StartsWith("System.", StringComparison.Ordinal) ||
            frame.StartsWith("Microsoft.Win32.", StringComparison.Ordinal) ||
            frame.StartsWith("Interop+", StringComparison.Ordinal)) return "runtime";
        if (frame.StartsWith("[unresolved", StringComparison.Ordinal) || frame == "[missing stack]") return "unresolved";
        return "other";
    }

    /// <summary>输入已过滤为目标进程及窗口内的托管样本；栈顺序为叶到根。</summary>
    internal void Add(string[] frames)
    {
        var leaf = frames.Length == 0 ? "[missing stack]" : frames[0];
        var leafComponent = Classify(leaf);
        _leaves[leafComponent] = _leaves.GetValueOrDefault(leafComponent) + 1;
        var owner = leaf;
        foreach (var frame in frames)
        {
            if (Classify(frame) is "runtime" or "unresolved" or "other") continue;
            owner = frame;
            break;
        }
        _owners[owner] = _owners.GetValueOrDefault(owner) + 1;
        _managedSamples++;
    }

    /// <summary>快照不持有活动计数集合；无样本时百分比保持未知。</summary>
    internal CpuComponentReport Snapshot()
    {
        double? Percent(long count) => _managedSamples == 0 ? null : 100d * count / _managedSamples;
        var owners = _owners.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new CpuOwnerCount(x.Key, Classify(x.Key), x.Value, Percent(x.Value))).ToArray();
        var components = ComponentNames.Select(name =>
        {
            var leafCount = _leaves.GetValueOrDefault(name);
            var ownerCount = owners.Where(x => x.Component == name).Sum(x => x.Samples);
            return new CpuComponentCount(name, leafCount, ownerCount, Percent(leafCount), Percent(ownerCount));
        }).ToArray();
        return new CpuComponentReport(_managedSamples, components, owners);
    }
}

internal sealed record CpuComponentCount(string Component, long LeafSamples, long NearestOwnerSamples,
    double? LeafPercent, double? NearestOwnerPercent);
internal sealed record CpuOwnerCount(string Method, string Component, long Samples, double? Percent);
internal sealed record CpuComponentReport(long ManagedSamples, CpuComponentCount[] Components, CpuOwnerCount[] NearestOwners);
