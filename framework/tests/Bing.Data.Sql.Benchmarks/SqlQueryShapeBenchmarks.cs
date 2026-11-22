using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量共享查询模板命中与大量路由导致未命中的本地渲染成本。</summary>
[MemoryDiagnoser]
public class SqlQueryShapeBenchmarks
{
    private SqlQueryShapeTemplate _template;
    private SqlQueryShapeTemplate _suffixTemplate;
    private SqlQueryShapeTemplate _orderedTemplate;
    private SqlQueryShapeTemplate _sharedSlotTemplate;
    private SqlQueryShapeTemplate _keyedTemplate;
    private SqlQueryShapeKey _shape;
    private Dictionary<string, object> _values;
    private Dictionary<string, object> _suffixValues;
    private Dictionary<SqlParameterKey, object> _keyedValues;
    private SqlQueryScope _scope;
    private string[] _routes;
    private int _nextRoute;

    /// <summary>可选条件数量。</summary>
    [Params(1, 4)]
    public int OptionalCount { get; set; }

    /// <summary>创建固定形状、参数值及 512 个预先分配的路由。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _values = new Dictionary<string, object> { ["tenant"] = 7 };
        var optional = Enumerable.Range(0, OptionalCount).Select(index =>
        {
            var name = "p" + index;
            _values[name] = index;
            return new SqlQueryOptionalFragment(index, "AND c" + index + "=@" + name,
                new[] { new SqlParam("@" + name, 0, DbType.Int32) });
        }).ToArray();
        _suffixValues = new Dictionary<string, object>(_values) { ["limit"] = 10 };
        _template = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT id FROM rows WHERE tenant=@tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }), optional);
        _suffixTemplate = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT id FROM rows WHERE tenant=@tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }), optional,
            " ", new SqlQueryFragment("ORDER BY id LIMIT @limit", new[] { new SqlParam("@limit", 0, DbType.Int32) }));
        var ordered = new List<(SqlQueryFragment Fragment, int? OptionalBit)>();
        var whereCount = (OptionalCount + 1) / 2;
        for (var index = 0; index < whereCount; index++)
            ordered.Add((optional[index].Fragment, index));
        ordered.Add((new SqlQueryFragment("GROUP BY category"), null));
        for (var index = whereCount; index < OptionalCount; index++)
        {
            var prefix = index == whereCount ? "HAVING" : "AND";
            ordered.Add((new SqlQueryFragment(prefix + " COUNT(*)>=@p" + index,
                new[] { new SqlParam("@p" + index, 0, DbType.Int32) }), index));
        }
        ordered.Add((new SqlQueryFragment("ORDER BY category LIMIT @limit", new[] { new SqlParam("@limit", 0, DbType.Int32) }), null));
        _orderedTemplate = SqlQueryShapeTemplate.CreateOrdered(
            new SqlQueryFragment("SELECT category FROM rows WHERE tenant=@tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }), ordered);
        _sharedSlotTemplate = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @tenant", new[] { new SqlParam("@tenant", 0, DbType.Int32) }),
            new[] { new SqlQueryOptionalFragment(0, "WHERE @tenant > 0", new[] { new SqlParam("@tenant", 1, DbType.Int32) }) });
        var firstKey = new SqlParameterKey("first", "id");
        var secondKey = new SqlParameterKey("second", "id");
        _keyedValues = new Dictionary<SqlParameterKey, object> { [firstKey] = 7, [secondKey] = 9 };
        _keyedTemplate = new SqlQueryShapeTemplate(
            new SqlQueryFragment("SELECT @id", new[] { new SqlParam("@id", 0, DbType.Int32) }).WithParameterKey("id", firstKey),
            new[] { new SqlQueryOptionalFragment(0, new SqlQueryFragment("UNION ALL SELECT @id",
                new[] { new SqlParam("@id", 0, DbType.Int32) }).WithParameterKey("id", secondKey)) });
        _shape = new SqlQueryShapeKey("rows", "sqlite", "v1", "stable", (1 << OptionalCount) - 1, string.Empty);
        _scope = new SqlQueryScope("consumer");
        _routes = Enumerable.Range(0, 512).Select(index => "route-" + index).ToArray();
        _template.Render(_shape, _values, null);
        _suffixTemplate.Render(_shape, _suffixValues, null);
        _orderedTemplate.Render(_shape, _suffixValues, null);
        _sharedSlotTemplate.Render(1, _values);
        _keyedTemplate.RenderWithKeys(1, _keyedValues);
    }

    /// <summary>重复渲染同一 SQL 形状，每次创建独立参数快照。</summary>
    [Benchmark]
    public SqlQueryFragment StableShape() => _template.Render(_shape, _values, null);

    /// <summary>包含固定排序和分页尾部的缓存命中成本。</summary>
    [Benchmark]
    public SqlQueryFragment SuffixStableShape() => _suffixTemplate.Render(_shape, _suffixValues, null);

    /// <summary>包含固定尾部、路由超过缓存容量时的形状编译成本。</summary>
    [Benchmark]
    public SqlQueryFragment SuffixChurningRoutes() =>
        _suffixTemplate.Render(_shape with { RouteKey = _routes[_nextRoute++ & 511] }, _suffixValues, null);

    /// <summary>固定分组两侧存在可选条件时的缓存命中成本。</summary>
    [Benchmark]
    public SqlQueryFragment OrderedStableShape() => _orderedTemplate.Render(_shape, _suffixValues, null);

    /// <summary>交错片段在路由超过缓存容量时的形状编译成本。</summary>
    [Benchmark]
    public SqlQueryFragment OrderedChurningRoutes() =>
        _orderedTemplate.Render(_shape with { RouteKey = _routes[_nextRoute++ & 511] }, _suffixValues, null);

    /// <summary>重复槽采用请求值时的缓存命中成本。</summary>
    [Benchmark]
    public SqlQueryFragment SharedSlot() => _sharedSlotTemplate.Render(1, _values);

    /// <summary>显式逻辑键区分同名参数时的缓存命中成本。</summary>
    [Benchmark]
    public SqlQueryFragment KeyedShape() => _keyedTemplate.RenderWithKeys(1, _keyedValues);

    /// <summary>重复渲染同一 SQL 形状并为所有参数增加作用域。</summary>
    [Benchmark]
    public SqlQueryFragment ScopedShape() => _template.Render(_shape, _values, _scope);

    /// <summary>路由数量超过缓存容量，持续构建不同形状。</summary>
    [Benchmark]
    public SqlQueryFragment ChurningRoutes() =>
        _template.Render(_shape with { RouteKey = _routes[_nextRoute++ & 511] }, _values, null);
}

/// <summary>测量渲染时二进制请求参数的快照开销。</summary>
[MemoryDiagnoser]
public class SqlQueryShapePayloadBenchmarks
{
    private SqlQueryShapeTemplate _template;
    private Dictionary<string, object> _values;

    /// <summary>请求参数字节数。</summary>
    [Params(4096, 1048576)]
    public int PayloadBytes { get; set; }

    /// <summary>创建模板与调用方拥有的二进制参数。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT @payload",
            new[] { new SqlParam("@payload", Array.Empty<byte>(), DbType.Binary) }));
        _values = new Dictionary<string, object> { ["payload"] = new byte[PayloadBytes] };
        _template.Render(0, _values);
    }

    /// <summary>创建独立片段与参数快照。</summary>
    [Benchmark]
    public SqlQueryFragment RenderPayload() => _template.Render(0, _values);
}

/// <summary>比较相同模板热命中时名称字典与顺序槽位的绑定成本。</summary>
/// <remarks>输入已装箱，分别测量复用输入和每次创建输入容器；不包含数据库 I/O。</remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SqlQueryShapeValueBindingBenchmarks
{
    private SqlQueryShapeTemplate _template;
    private SqlQueryShapeKey _shape;
    private Dictionary<string, object> _namedValues;
    private object[] _slotValues;
    private string[] _names;

    /// <summary>全部启用的可选条件数量。</summary>
    [Params(0, 2, 8, 16)]
    public int OptionalCount { get; set; }

    /// <summary>预热同一缓存形状，并逐字核对两种绑定的 SQL、参数顺序、值和类型。</summary>
    [GlobalSetup]
    public void Setup()
    {
        _names = new string[OptionalCount + 1];
        _slotValues = new object[OptionalCount + 1];
        _namedValues = new Dictionary<string, object>(OptionalCount + 1);
        _names[0] = "tenant";
        _slotValues[0] = 7;
        _namedValues.Add(_names[0], _slotValues[0]);
        var optional = new SqlQueryOptionalFragment[OptionalCount];
        for (var index = 0; index < OptionalCount; index++)
        {
            var name = "p" + index;
            _names[index + 1] = name;
            _slotValues[index + 1] = index;
            _namedValues.Add(name, _slotValues[index + 1]);
            optional[index] = new SqlQueryOptionalFragment(index, "AND c" + index + "=@" + name,
                new[] { new SqlParam("@" + name, 0, DbType.Int32) });
        }
        _template = new SqlQueryShapeTemplate(new SqlQueryFragment("SELECT id FROM rows WHERE tenant=@tenant",
            new[] { new SqlParam("@tenant", 0, DbType.Int32) }), optional);
        _shape = new SqlQueryShapeKey("value-binding", "sqlite", "v1", "default", (1 << OptionalCount) - 1, "");
        var expectedSql = "SELECT id FROM rows WHERE tenant=@tenant" +
            string.Concat(Enumerable.Range(0, OptionalCount).Select(index => " AND c" + index + "=@p" + index));
        var fragments = new[] { ReuseDictionary(), ReuseSlots(), CreateDictionary(), CreateSlots() };
        foreach (var fragment in fragments)
        {
            var parameters = fragment.Parameters;
            if (fragment.Sql != expectedSql || parameters.Count != _slotValues.Length)
                throw new InvalidOperationException("模板绑定基准的 SQL 或参数数量不一致。");
            for (var index = 0; index < parameters.Count; index++)
                if (parameters[index].Name != "@" + _names[index] ||
                    !Equals(parameters[index].Value, _slotValues[index]) || parameters[index].DbType != DbType.Int32)
                    throw new InvalidOperationException("模板绑定基准的参数布局不一致。");
        }
    }

    /// <summary>复用输入字典，仅测量模板查找和名称绑定。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("ReuseInput")]
    public SqlQueryFragment ReuseDictionary() => _template.Render(_shape, _namedValues, null);

    /// <summary>复用输入数组，按已编译槽位直接绑定。</summary>
    [Benchmark, BenchmarkCategory("ReuseInput")]
    public SqlQueryFragment ReuseSlots() => _template.RenderValues(_shape, _slotValues);

    /// <summary>计入每次创建字典及填入已装箱请求值的成本。</summary>
    [Benchmark(Baseline = true), BenchmarkCategory("CreateInput")]
    public SqlQueryFragment CreateDictionary()
    {
        var values = new Dictionary<string, object>(_slotValues.Length);
        for (var index = 0; index < _slotValues.Length; index++)
            values.Add(_names[index], _slotValues[index]);
        return _template.Render(_shape, values, null);
    }

    /// <summary>计入每次创建数组及填入相同请求值的成本。</summary>
    [Benchmark, BenchmarkCategory("CreateInput")]
    public SqlQueryFragment CreateSlots()
    {
        var values = new object[_slotValues.Length];
        _slotValues.CopyTo(values, 0);
        return _template.RenderValues(_shape, values);
    }
}
