using System.Data;
using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>
    /// 验证双精度聚合生成代码可编译并读取可空结果。
    /// </summary>
    /// <param name="type">用于生成测试代码的类型名称。</param>
    /// <param name="syntax">用于选择聚合表达式写法的场景序号。</param>
    [Theory]
    [InlineData("double", 0)]
    [InlineData("double?", 0)]
    [InlineData("double", 1)]
    [InlineData("double?", 1)]
    [InlineData("double", 2)]
    [InlineData("double?", 2)]
    public void Generate_WhenDoubleAggregatesAreSelected_ShouldCompileAndReadNullableResults(string type, int syntax)
    {
        var projection = syntax switch
        {
            0 => "r => new Result { Count = SqlFn.Count(r.Value), Total = SqlFn.Sum(r.Value) }",
            1 => "(r, f) => new Result { Count = f.Count(r.Value), Total = f.Sum(r.Value) }",
            _ => "(r, f) => new Result { Count = f.Count(x => x.Value), Total = f.Sum(x => x.Value) }"
        };
        var source = "using Bing.Data.Sql; public class Row { public " + type + " Value { get; set; } } " +
            "public class Result { public long Count { get; set; } public double? Total { get; set; } } " +
            "public static class Consumer { public static object Load(SqlLambdaQuery q) => q.From<Row>().Select<Row>(" + projection + ").ToList<Result>(); }";
        var result = RunGenerator(source, analyzerConfigOptionsProvider: new TestAnalyzerConfigOptionsProvider(
            new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" }));
        Assert.DoesNotContain(result.Diagnostics, item => item.Id is "BINGSQLG001" or "BINGSQLG002");
        var assembly = LoadGeneratedAssembly(result);
        using var table = new DataTable();
        table.Columns.Add("Count", typeof(long));
        table.Columns.Add("Total", typeof(double));
        table.Rows.Add(2L, 12.5d);
        table.Rows.Add(0L, DBNull.Value);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var first = ReadFloatingResult(assembly, reader, new Dictionary<string, int> { ["Count"] = 0, ["Total"] = 1 });
        Assert.Equal(2L, first.GetType().GetProperty("Count")!.GetValue(first));
        Assert.Equal(12.5d, first.GetType().GetProperty("Total")!.GetValue(first));
        Assert.True(reader.Read());
        var empty = ReadFloatingResult(assembly, reader, new Dictionary<string, int> { ["Count"] = 0, ["Total"] = 1 });
        Assert.Equal(0L, empty.GetType().GetProperty("Count")!.GetValue(empty));
        Assert.Null(empty.GetType().GetProperty("Total")!.GetValue(empty));
    }

    /// <summary>
    /// 验证生成读取器处理浮点列及其数据库空值。
    /// </summary>
    /// <param name="type">用于生成测试代码的类型名称。</param>
    /// <param name="nullable">是否使用可空类型或可空阈值。</param>
    [Theory]
    [InlineData("float", false)]
    [InlineData("float?", true)]
    [InlineData("double", false)]
    [InlineData("double?", true)]
    public void Generate_WhenFloatingColumnIsSelected_ShouldReadColumnAndNull(string type, bool nullable)
    {
        var source = "using Bing.Data.Sql; public class Row { public " + type + " Value { get; set; } } " +
            "public static class Consumer { public static object Load(SqlLambdaQuery q) => q.From<Row>().Select<Row>(r => new { r.Value }).ToList<Row>(); }";
        var result = RunGenerator(source);
        Assert.DoesNotContain(result.Diagnostics, item => item.Id is "BINGSQLG001" or "BINGSQLG002");
        var assembly = LoadGeneratedAssembly(result);
        var valueType = type.StartsWith("float", StringComparison.Ordinal) ? typeof(float) : typeof(double);
        var expected = Convert.ChangeType(5.5d, valueType);
        using var table = new DataTable();
        table.Columns.Add("Value", valueType);
        table.Rows.Add(expected);
        if (nullable) table.Rows.Add(DBNull.Value);
        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());
        var first = ReadFloatingResult(assembly, reader, new Dictionary<string, int> { ["Value"] = 0 });
        Assert.Equal(expected, first.GetType().GetProperty("Value")!.GetValue(first));
        if (!nullable) return;
        Assert.True(reader.Read());
        var empty = ReadFloatingResult(assembly, reader, new Dictionary<string, int> { ["Value"] = 0 });
        Assert.Null(empty.GetType().GetProperty("Value")!.GetValue(empty));
    }

    /// <summary>
    /// 调用生成的浮点结果读取方法。
    /// </summary>
    /// <param name="assembly">包含生成代码的程序集。</param>
    /// <param name="reader">提供查询结果的数据库读取器。</param>
    /// <param name="ordinals">结果列名称到序号的映射。</param>
    /// <returns>由生成的读取方法构造的浮点查询结果对象。</returns>
    private static object ReadFloatingResult(Assembly assembly, System.Data.Common.DbDataReader reader, IReadOnlyDictionary<string, int> ordinals)
    {
        var read = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name.StartsWith("Read_", StringComparison.Ordinal));
        var materialize = (Delegate)read.Invoke(null, new object[] { ordinals })!;
        return materialize.DynamicInvoke(reader)!;
    }
}
