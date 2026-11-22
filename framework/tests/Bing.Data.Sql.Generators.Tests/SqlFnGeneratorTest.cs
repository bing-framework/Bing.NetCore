using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>
    /// 验证不同聚合表达形式生成相同逻辑函数。
    /// </summary>
    /// <param name="projection">预期 SQL 的投影片段。</param>
    [Theory]
    [InlineData("r => new { Count = SqlFn.Count(r.Quantity), Total = SqlFn.Sum(r.Quantity) }")]
    [InlineData("(r, f) => new { Count = f.Count(r.Quantity), Total = f.Sum(r.Quantity) }")]
    [InlineData("(r, f) => new { Count = f.Count((x) => x.Quantity), Total = f.Sum(x => x.Quantity) }")]
    [InlineData("r => new { Count = A.Count(r.Quantity), Total = A.Sum(r.Quantity) }")]
    [InlineData("r => new { Count = Count(r.Quantity), Total = Sum(r.Quantity) }")]
    public void Generate_WhenAggregateFormsVary_ShouldCompileSameLogicalFunctions(string projection)
    {
        var source = """
            using Bing.Data.Sql;
            using A = Bing.Data.Sql.SqlFn;
            using static Bing.Data.Sql.SqlFn;
            public class Row { public int Id { get; set; } public int GroupId { get; set; } public int? Quantity { get; set; } }
            public class Summary { public int GroupId { get; set; } public long Count { get; set; } public int? Total { get; set; } }
            public static class Consumer {
                public static object Load(SqlLambdaQuery q, long minimum) =>
                    q.From<Row>().Select<Row>(r => new { r.GroupId })
                     .Select<Row>(PROJECTION)
                     .GroupBy<Row>(other => other.GroupId)
                     .Having<Row>(r => A.Count(r.Quantity) > minimum)
                     .OrderBy<Row>(r => Sum(r.Quantity), true)
                     .ToList<Summary>();
            }
            """.Replace("PROJECTION", projection);
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("Builders.SqlAggregateFunction.Count", result.GeneratedSource);
        Assert.Contains("Builders.SqlAggregateFunction.Sum", result.GeneratedSource);
        Assert.Contains("SqlGeneratedQueryBridge.Compare", result.GeneratedSource);
    }

    /// <summary>
    /// 验证自连接生成代码保留参数角色与列来源索引。
    /// </summary>
    [Fact]
    public void Generate_WhenSelfJoinUsesParameterRoles_ShouldPreserveNamesAndColumnSourceIndexes()
    {
        const string source = """
            using Bing.Data.Sql;
            public class Employee { public int Id { get; set; } public int ParentId { get; set; } public int? Quantity { get; set; } }
            public class Summary { public int? First { get; set; } public int? Second { get; set; } }
            public static class Consumer {
                public static object Load(SqlLambdaQuery q) =>
                    q.From<Employee>()
                     .LeftJoin<Employee, Employee>((employee, manager) => employee.ParentId == manager.Id)
                     .LeftJoin<Employee, Employee>((manager, director) => manager.ParentId == director.Id)
                     .Select<Employee, Employee, Summary>((employee, director) => new Summary {
                         First = SqlFn.Sum(employee.Quantity), Second = SqlFn.Sum(director.Quantity)
                     }).ToList<Summary>();
            }
            """;
        var result = RunGenerator(source);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("\"employee\", \"manager\", \"ParentId\", \"Id\", null, null", result.GeneratedSource);
        Assert.Contains("\"manager\", \"director\", \"ParentId\", \"Id\", null, null", result.GeneratedSource);
        Assert.Contains("SqlGeneratedColumn(0, \"Quantity\", \"First\"", result.GeneratedSource);
        Assert.Contains("SqlGeneratedColumn(1, \"Quantity\", \"Second\"", result.GeneratedSource);
    }

    /// <summary>
    /// 验证非法聚合表达在调用位置产生诊断。
    /// </summary>
    /// <param name="operation">用于选择被测操作的名称。</param>
    [Theory]
    [InlineData("q.Where<Row>(r => SqlFn.Count(r.Id) > 0L)")]
    [InlineData("q.GroupBy<Row>(r => SqlFn.Count(r.Id))")]
    [InlineData("q.ExcludeSelect<Row>(r => SqlFn.Count(r.Id))")]
    [InlineData("q.Select<Row>(r => SqlFn.Count(42))")]
    [InlineData("q.Select<Row>(r => SqlFn.Sum(r.Quantity + 1))")]
    [InlineData("q.Select<Row>(r => SqlFn.Count(SqlFn.Sum(r.Quantity)))")]
    [InlineData("q.Select<Row>(r => Fake.SqlFn.Count(r.Id))")]
    [InlineData("q.Having<Row>(r => SqlFn.Count(r.Id, \"Count\") > 0L)")]
    [InlineData("q.OrderBy<Row>(r => SqlFn.Sum(r.Quantity, \"Total\"))")]
    public void Generate_WhenAggregateIsInvalid_ShouldDiagnoseAtCallSite(string operation)
    {
        var source = """
            using Bing.Data.Sql;
            public class Row { public int Id { get; set; } public int? Quantity { get; set; } }
            namespace Fake { public static class SqlFn { public static long Count(int value) => value; } }
            public class Consumer { public SqlLambdaQuery Build(SqlLambdaQuery q) => OPERATION; }
            """.Replace("OPERATION", operation);
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string> { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001" && diagnostic.Location.IsInSource);
        var fallback = RunGenerator(source);
        AssertNoCompilationErrors(fallback);
        Assert.Contains(fallback.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
    }
}
