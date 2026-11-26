using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>同一个缓存条件在 SQLite 中按每次开关和参数独立生成并执行。</summary>
    [Theory]
    [InlineData(false, 2, 3)]
    [InlineData(true, 2, 2)]
    [InlineData(true, 3, 1)]
    public void CachedPredicate_WhenExecutedAgainstSqlite_ShouldUseCurrentConditionAndValue(
        bool enabled, int minimum, int expectedCount)
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                private static readonly Expression<Func<CachedRow, int, bool>> Filter = (row, value) => row.Id >= value;
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled, int minimum) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .WhereIf<CachedRow, int>(enabled, minimum, Filter, "r");
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");

        var builder = new SqliteBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var apply = LoadGeneratedAssembly(generated).GetType("CachedConsumer")!.GetMethod("Apply")!;
        var built = (SqlLambdaQuery)apply.Invoke(null, new object[] { query, enabled, minimum })!;
        var expectedSql = "Select `r`.`Id` \r\nFrom `CachedRow` As `r`" +
            (enabled ? " \r\nWhere `r`.`Id`>=@_p_0" : "");
        Assert.Equal(expectedSql, built.ToSql());
        if (enabled)
            Assert.Equal(minimum, Assert.Single(builder.GetParams()).Value);
        else
            Assert.Empty(builder.GetParams());

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE CachedRow (Id INTEGER); INSERT INTO CachedRow VALUES (1), (2), (3);";
        command.ExecuteNonQuery();
        command.CommandText = built.ToSql();
        foreach (var parameter in builder.GetParams())
            command.Parameters.AddWithValue(parameter.Key, parameter.Value);
        using var reader = command.ExecuteReader();
        var count = 0;
        while (reader.Read()) count++;
        Assert.Equal(expectedCount, count);
    }

    /// <summary>并发复用同一字段时，每个查询仍绑定自己的参数。</summary>
    [Fact]
    public async Task CachedPredicate_WhenUsedConcurrently_ShouldKeepParametersIndependent()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                private static readonly Expression<Func<CachedRow, int, bool>> Filter = (row, value) => row.Id >= value;
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int minimum) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .Where<CachedRow, int>(minimum, Filter, "r");
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        var apply = LoadGeneratedAssembly(generated).GetType("CachedConsumer")!.GetMethod("Apply")!;

        await Task.WhenAll(Enumerable.Range(0, 64).Select(minimum => Task.Run(() =>
        {
            var builder = new SqliteBuilder();
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
            var built = (SqlLambdaQuery)apply.Invoke(null, new object[] { query, minimum })!;
            Assert.Equal("Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", built.ToSql());
            Assert.Equal(minimum, Assert.Single(builder.GetParams()).Value);
        })));
    }

    /// <summary>字段尚未初始化时，关闭的条件应返回原查询。</summary>
    [Fact]
    public void CachedPredicate_WhenFieldIsNullDuringInitialization_ShouldPreserveDisabledQuery()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class Bootstrap { public static SqlLambdaQuery Query; }
            public static class CachedConsumer
            {
                public static readonly SqlLambdaQuery DuringInitialization = Apply();
                private static readonly Expression<Func<CachedRow, bool>> Predicate = row => row.Id >= 5;
                private static SqlLambdaQuery Apply() => Bootstrap.Query.WhereIf<CachedRow>(false, Predicate);
            }
            """;
        var generated = RunGenerator(source);
        AssertNoCompilationErrors(generated);
        var assembly = LoadGeneratedAssembly(generated);
        var builder = new SqliteBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        assembly.GetType("Bootstrap")!.GetField("Query")!.SetValue(null, query);

        var result = assembly.GetType("CachedConsumer")!.GetField("DuringInitialization")!.GetValue(null);

        Assert.Same(query, result);
        Assert.Empty(builder.GetParams());
    }

    /// <summary>完整查询链中的缓存条件继续使用现有查询模板。</summary>
    [Theory]
    [InlineData(false, "Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>@_p_0")]
    [InlineData(true, "Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>@_p_0 And `r`.`Id`>=@_p_1")]
    public void CachedPredicate_WhenCompleteChainIsUsed_ShouldReuseQueryTemplate(bool enabled, string expectedSql)
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                private static readonly Expression<Func<CachedRow, bool>> Predicate = row => row.Id >= 7;
                public static string Render(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<CachedRow>("r")
                        .Where<CachedRow>(row => row.Id > 1, "r")
                        .WhereIf<CachedRow>(enabled, Predicate, "r")
                        .Select<CachedRow>(row => row.Id, "r").ToSql();
            }
            """;
        var generated = RunGenerator(source);
        AssertNoCompilationErrors(generated);
        Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", generated.GeneratedSource, StringComparison.Ordinal);
        var render = LoadGeneratedAssembly(generated).GetType("CachedConsumer")!.GetMethod("Render")!;
        var builder = new SqliteBuilder();

        var sql = (string)render.Invoke(null, new object[] { new Mock<ISqlQueryPlanExecutor>().Object, builder, enabled })!;

        Assert.Equal(expectedSql, sql);
    }

    /// <summary>模板执行遇到尚未初始化的缓存字段时，仍保留已追加的条件。</summary>
    [Fact]
    public void CachedPredicate_WhenUninitializedInTemplate_ShouldMaterializePreviousOperations()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class Bootstrap
            {
                public static ISqlQueryPlanExecutor Executor;
                public static ISqlBuilder Builder;
            }
            public static class CachedConsumer
            {
                public static readonly string DuringInitialization = Render();
                private static readonly Expression<Func<CachedRow, bool>> Predicate = row => row.Id >= 7;
                private static string Render() =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(Bootstrap.Executor, Bootstrap.Builder).From<CachedRow>("r")
                        .Where<CachedRow>(row => row.Id > 1, "r")
                        .WhereIf<CachedRow>(false, Predicate, "r")
                        .Select<CachedRow>(row => row.Id, "r").ToSql();
            }
            """;
        var generated = RunGenerator(source);
        AssertNoCompilationErrors(generated);
        Assert.Contains("SqlGeneratedQueryTemplate.Apply(source, Template_", generated.GeneratedSource, StringComparison.Ordinal);
        var assembly = LoadGeneratedAssembly(generated);
        var bootstrap = assembly.GetType("Bootstrap")!;
        bootstrap.GetField("Executor")!.SetValue(null, new Mock<ISqlQueryPlanExecutor>().Object);
        bootstrap.GetField("Builder")!.SetValue(null, new SqliteBuilder());

        var sql = (string)assembly.GetType("CachedConsumer")!.GetField("DuringInitialization")!.GetValue(null)!;

        Assert.Equal("Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>@_p_0", sql);
    }
}
