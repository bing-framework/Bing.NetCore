using System.Data;
using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>
    /// 验证对象聚合目标生成的读取器处理实际值与数据库空值。
    /// </summary>
    /// <param name="projection">预期 SQL 的投影片段。</param>
    [Theory]
    [InlineData("r => new Result { Count = SqlFn.Count(r.Id), Total = SqlFn.Sum(r.Quantity) }")]
    [InlineData("(r, f) => new Result { Count = f.Count(r.Id), Total = f.Sum(r.Quantity) }")]
    [InlineData("(r, f) => new Result { Count = f.Count(x => x.Id), Total = f.Sum(x => x.Quantity) }")]
    public void Generate_WhenAggregateTargetIsObject_ShouldReadValuesAndDbNull(string projection)
    {
        var source = """
            using Bing.Data.Sql;
            public class Row { public int Id { get; set; } public int? Quantity { get; set; } }
            public class Result { public object Count { get; set; } public object Total { get; set; } public Row Navigation { get; set; } }
            public static class Consumer { public static object Load(SqlLambdaQuery q) =>
                q.From<Row>().Select<Row>(PROJECTION).ToList<Result>(); }
            """.Replace("PROJECTION", projection);
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        Assert.DoesNotContain(result.Diagnostics, item => item.Id is "BINGSQLG001" or "BINGSQLG002");
        var assembly = LoadGeneratedAssembly(result);
        var readerMethod = assembly.GetType("Bing.Data.Sql.Generated.QueryCalls")!
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method => method.Name.StartsWith("Read_", StringComparison.Ordinal));
        using var table = new DataTable();
        table.Columns.Add("Count", typeof(long));
        table.Columns.Add("Total", typeof(long));
        table.Rows.Add(3L, 12L);
        table.Rows.Add(0L, DBNull.Value);
        using var reader = table.CreateDataReader();
        var materialize = (Delegate)readerMethod.Invoke(null, new object[] {
            new Dictionary<string,int> { ["Count"] = 0, ["Total"] = 1 }
        })!;
        object Read() => materialize.DynamicInvoke(reader)!;
        Assert.True(reader.Read());
        var first = Read();
        Assert.Equal(3L, Assert.IsType<long>(first.GetType().GetProperty("Count")!.GetValue(first)));
        Assert.Equal(12L, Assert.IsType<long>(first.GetType().GetProperty("Total")!.GetValue(first)));
        Assert.Null(first.GetType().GetProperty("Navigation")!.GetValue(first));
        Assert.True(reader.Read());
        var empty = Read();
        Assert.Equal(0L, empty.GetType().GetProperty("Count")!.GetValue(empty));
        Assert.Null(empty.GetType().GetProperty("Total")!.GetValue(empty));
    }

    /// <summary>
    /// 验证转义 DTO 成员使用实际 CLR 名称生成读取代码。
    /// </summary>
    /// <param name="name">注册或构造的测试服务、操作或场景名称。</param>
    [Theory]
    [InlineData("Count")]
    [InlineData("event")]
    public void Generate_WhenDtoMemberIsEscaped_ShouldUseClrName(string name)
    {
        var source = """
            using Bing.Data.Sql;
            public class Row { public int Id { get; set; } }
            public class Result { public long @MEMBER { get; set; } }
            public static class Consumer { public static object Load(SqlLambdaQuery q) =>
                q.From<Row>().Select<Row>(r => new Result { @MEMBER = SqlFn.Count(r.Id, "MEMBER") }).ToList<Result>(); }
            """.Replace("MEMBER", name);
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, item => item.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedColumn(0, \"Id\", \"" + name + "\"", result.GeneratedSource);
        Assert.DoesNotContain("\"@" + name + "\"", result.GeneratedSource);
    }

    /// <summary>
    /// 验证提升后的聚合阈值生成可编译的共享值读取代码。
    /// </summary>
    /// <param name="type">用于生成测试代码的类型名称。</param>
    /// <param name="value">测试对象、记录或输入文本的值。</param>
    [Theory]
    [InlineData("int", "minimum")]
    [InlineData("int?", "minimum")]
    [InlineData("int", "(long)minimum")]
    public void Generate_WhenHavingThresholdPromotes_ShouldCompileSharedValueReader(string type, string value)
    {
        var source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } } " +
            "public class Consumer { public SqlLambdaQuery Load(SqlLambdaQuery q, " + type + " minimum) => " +
            "q.Having<Row>(r => SqlFn.Count(r.Id) > " + value + "); }";
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, item => item.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryBridge.ComparisonValue(", result.GeneratedSource);
    }

    /// <summary>
    /// 验证聚合阈值窄化转换产生调用点诊断。
    /// </summary>
    /// <param name="strict">是否启用严格的生成查询诊断。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Generate_WhenHavingThresholdNarrows_ShouldDiagnose(bool strict)
    {
        const string source = "using Bing.Data.Sql; public class Row { public int Id { get; set; } } public class Consumer { public SqlLambdaQuery Load(SqlLambdaQuery q, long minimum) => q.Having<Row>(r => SqlFn.Count(r.Id) > (int)minimum); }";
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = strict ? "true" : "false" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: options);
        Assert.Contains(result.Diagnostics, item => item.Id == (strict ? "BINGSQLG001" : "BINGSQLG002"));
        if (!strict) AssertNoCompilationErrors(result);
    }
}
