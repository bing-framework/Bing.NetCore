using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>跨文件只读聚合投影应生成列描述，并使用调用处的来源别名。</summary>
    [Fact]
    public void CachedProjection_WhenDeclaredInAnotherFile_ShouldGenerateAggregateSql()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public static class Consumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) =>
                    query.From<ProjectionRow>("r").Select<ProjectionRow>(SharedProjection.Aggregate, "r");
            }
            """;
        const string shared = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using static Bing.Data.Sql.SqlFn;
            public static class SharedProjection
            {
                public static readonly Expression<Func<ProjectionRow, object>> Aggregate =
                    row => new { Total = Count(row.Id) };
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict, additionalSource: shared);

        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryBridge.Project", generated.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("SqlAggregateFunction.Count", generated.GeneratedSource, StringComparison.Ordinal);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder());
        var apply = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Apply")!;
        var result = (SqlLambdaQuery)apply.Invoke(null, new object[] { query })!;
        Assert.Equal("Select Count(`r`.`Id`) As `Total` \r\nFrom `ProjectionRow` As `r`", result.ToSql());
    }

    /// <summary>可选条件改变时，缓存投影使用正确的模板形状并复用 SQL。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedProjection_WhenUsedInCompleteChain_ShouldReuseSqlForSameShape(bool enabled)
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public static class Consumer
            {
                private static readonly Expression<Func<ProjectionRow, object>> Projection = row => row.Id;
                public static string Render(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ProjectionRow>("r")
                        .WhereIf<ProjectionRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                        .Select<ProjectionRow>(Projection, "r").ToSql();
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", generated.GeneratedSource, StringComparison.Ordinal);
        var render = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Render")!;
        var services = SqlBuilderServices.CreateDefault();
        var executor = new Mock<ISqlQueryPlanExecutor>().Object;

        var firstBuilder = new SqliteBuilder(services);
        var secondBuilder = new SqliteBuilder(services);
        string Build(SqliteBuilder builder, int minimum) => (string)render.Invoke(null, new object[]
            { executor, builder, enabled, minimum })!;
        var first = Build(firstBuilder, 2);
        var second = Build(secondBuilder, 3);
        var expected = "Select `r`.`Id` \r\nFrom `ProjectionRow` As `r`" +
            (enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        Assert.Equal(expected, first);
        Assert.Same(first, second);
    }

    /// <summary>常用数组列投影和聚合函数对象投影均使用生成路径。</summary>
    [Theory]
    [InlineData("Expression<Func<ProjectionRow, object[]>>", "row => new object[] { row.Id }", "Select `r`.`Id` \r\nFrom `ProjectionRow` As `r`")]
    [InlineData("Expression<Func<ProjectionRow, SqlAggregateFunctions<ProjectionRow>, object>>", "(row, fn) => new { Total = fn.Count(x => x.Id) }", "Select Count(`r`.`Id`) As `Total` \r\nFrom `ProjectionRow` As `r`")]
    public void CachedProjection_WhenUsingSupportedSelectOverloads_ShouldGenerateSql(string type, string expression, string expected)
    {
        var source = $$"""
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public static class Consumer
            {
                private static readonly {{type}} Projection = {{expression}};
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) =>
                    query.From<ProjectionRow>("r").Select<ProjectionRow>(Projection, "r");
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedQueryBridge.Project", generated.GeneratedSource, StringComparison.Ordinal);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder());
        var apply = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Apply")!;
        Assert.Equal(expected, ((SqlLambdaQuery)apply.Invoke(null, new object[] { query })!).ToSql());
    }

    /// <summary>同形状模板的执行计划应分别绑定每次调用的显式值。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedProjection_WhenExecutingTemplate_ShouldBindCurrentValues(bool enabled)
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public static class Consumer
            {
                private static readonly Expression<Func<ProjectionRow, object>> Projection = row => row.Id;
                public static int Execute(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ProjectionRow>("r")
                        .WhereIf<ProjectionRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                        .Select<ProjectionRow>(Projection, "r").Scalar<int>();
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        AssertNoCompilationErrors(generated);
        Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", generated.GeneratedSource, StringComparison.Ordinal);
        var execute = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Execute")!;
        var plans = new List<SqlQueryPlan>();
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(value => value.Scalar<int>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Callback<SqlQueryPlan, int?>((plan, _) => plans.Add(plan)).Returns(0);
        var services = SqlBuilderServices.CreateDefault();
        foreach (var minimum in new[] { 2, 3 })
            execute.Invoke(null, new object[] { executor.Object, new SqliteBuilder(services), enabled, minimum });

        Assert.Equal(2, plans.Count);
        var sql = "Select `r`.`Id` \r\nFrom `ProjectionRow` As `r`" +
            (enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        Assert.All(plans, plan => Assert.Equal(sql, plan.CommandText));
        for (var index = 0; index < plans.Count; index++)
        {
            var parameters = Assert.IsAssignableFrom<IEnumerable<SqlParam>>(plans[index].Parameters);
            if (enabled) Assert.Equal(index + 2, Assert.Single(parameters).Value);
            else Assert.Empty(parameters);
        }
    }

    /// <summary>类型初始化时投影字段尚未赋值，应保持与原方法一致的结果。</summary>
    [Fact]
    public void CachedProjection_WhenFieldIsNullDuringInitialization_ShouldPreserveRuntimeBehavior()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public static class Bootstrap { public static ISqlQueryPlanExecutor Executor; public static ISqlBuilder Builder; }
            public static class Consumer
            {
                public static readonly string DuringInitialization = Render();
                private static readonly Expression<Func<ProjectionRow, object>> Projection = row => row.Id;
                private static string Render()
                {
                    try
                    {
                        return SqlQueryRuntimeFactory.CreateLambdaQuery(Bootstrap.Executor, Bootstrap.Builder)
                            .From<ProjectionRow>("r").WhereIf<ProjectionRow>(true, row => row.Id, 1, "r", Bing.Operator.Greater)
                            .Select<ProjectionRow>(Projection, "r").ToSql();
                    }
                    catch (Exception error) { return error.GetType().Name; }
                }
            }
            """;
        string Run(bool interceptors)
        {
            var generated = RunGenerator(source, enableInterceptors: interceptors);
            AssertNoCompilationErrors(generated);
            if (interceptors)
                Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", generated.GeneratedSource, StringComparison.Ordinal);
            var assembly = LoadGeneratedAssembly(generated);
            var bootstrap = assembly.GetType("Bootstrap")!;
            bootstrap.GetField("Executor")!.SetValue(null, new Mock<ISqlQueryPlanExecutor>().Object);
            bootstrap.GetField("Builder")!.SetValue(null, new SqliteBuilder());
            return (string)assembly.GetType("Consumer")!.GetField("DuringInitialization")!.GetValue(null)!;
        }

        Assert.Equal(Run(false), Run(true));
    }

    /// <summary>显式静态构造器可重赋字段，必须保留字段实际值及运行时路径。</summary>
    [Fact]
    public void CachedProjection_WhenStaticConstructorReassignsField_ShouldUseRuntimeValue()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class ProjectionRow { public int Id { get; set; } public string Name { get; set; } }
            public static class Consumer
            {
                private static readonly Expression<Func<ProjectionRow, object>> Projection = row => row.Id;
                static Consumer() { Projection = row => row.Name; }
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) =>
                    query.From<ProjectionRow>("r").Select<ProjectionRow>(Projection, "r");
            }
            """;
        var generated = RunGenerator(source);
        AssertNoCompilationErrors(generated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder());
        var apply = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Apply")!;
        var result = (SqlLambdaQuery)apply.Invoke(null, new object[] { query })!;
        Assert.Equal("Select `r`.`Name` \r\nFrom `ProjectionRow` As `r`", result.ToSql());
    }

    /// <summary>并发复用同一投影时，每次查询的条件参数仍相互隔离。</summary>
    [Fact]
    public async Task CachedProjection_WhenUsedConcurrently_ShouldIsolateParameters()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public static class Consumer
            {
                private static readonly Expression<Func<ProjectionRow, object>> Projection = row => row.Id;
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int minimum) =>
                    query.From<ProjectionRow>("r")
                        .WhereIf<ProjectionRow, int>(true, minimum, (row, value) => row.Id >= value, "r")
                        .Select<ProjectionRow>(Projection, "r");
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        AssertNoCompilationErrors(generated);
        var apply = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Apply")!;
        var executor = new Mock<ISqlQueryPlanExecutor>().Object;

        var results = await Task.WhenAll(Enumerable.Range(1, 64).Select(minimum => Task.Run(() =>
        {
            var builder = new SqliteBuilder();
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder);
            var built = (SqlLambdaQuery)apply.Invoke(null, new object[] { query, minimum })!;
            return (minimum, sql: built.ToSql(), value: Assert.Single(builder.GetParams()).Value);
        })));

        foreach (var result in results)
        {
            Assert.Equal("Select `r`.`Id` \r\nFrom `ProjectionRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", result.sql);
            Assert.Equal(result.minimum, result.value);
        }
    }

    /// <summary>生成的缓存聚合投影应在 SQLite 中使用实际参数执行。</summary>
    [Theory]
    [InlineData(false, 2, 3)]
    [InlineData(true, 2, 2)]
    [InlineData(true, 3, 1)]
    public void CachedProjection_WhenExecutedAgainstSqlite_ShouldUseCurrentCondition(bool enabled, int minimum, long expected)
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class ProjectionRow { public int Id { get; set; } }
            public static class Consumer
            {
                private static readonly Expression<Func<ProjectionRow, object>> Projection =
                    row => new { Total = SqlFn.Count(row.Id) };
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled, int minimum) =>
                    query.From<ProjectionRow>("r")
                        .WhereIf<ProjectionRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                        .Select<ProjectionRow>(Projection, "r");
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: strict);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        var builder = new SqliteBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var apply = LoadGeneratedAssembly(generated).GetType("Consumer")!.GetMethod("Apply")!;
        var built = (SqlLambdaQuery)apply.Invoke(null, new object[] { query, enabled, minimum })!;
        var sql = "Select Count(`r`.`Id`) As `Total` \r\nFrom `ProjectionRow` As `r`" +
            (enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        Assert.Equal(sql, built.ToSql());

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE ProjectionRow (Id INTEGER); INSERT INTO ProjectionRow VALUES (1), (2), (3);";
        command.ExecuteNonQuery();
        command.CommandText = sql;
        foreach (var parameter in builder.GetParams())
            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        Assert.Equal(expected, (long)command.ExecuteScalar()!);
    }
}
