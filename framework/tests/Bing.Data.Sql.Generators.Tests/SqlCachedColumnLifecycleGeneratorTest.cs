using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>静态只读列表达式在类型初始化递归期间尚为 null 时，应保留原 API 行为。</summary>
    [Theory]
    [InlineData("ReplaceSelect", "Select<Row>(row => row.Id, \"r\").ReplaceSelect<Row>(Selector, \"r\")",
        "Select `r`.`Name` \r\nFrom `Row` As `r`")]
    [InlineData("ExcludeSelect", "SelectAll().ExcludeSelect<Row>(Selector, \"r\")",
        "Select `r`.`Id` \r\nFrom `Row` As `r`")]
    [InlineData("GroupBy", "Select<Row>(row => row.Name, \"r\").GroupBy<Row>(Columns, \"r\")",
        "Select `r`.`Name` \r\nFrom `Row` As `r` \r\nGroup By `r`.`Name`")]
    [InlineData("OrderBy", "Select<Row>(row => row.Id, \"r\").OrderBy<Row>(Columns, \"r\", descending)",
        "Select `r`.`Id` \r\nFrom `Row` As `r` \r\nOrder By `r`.`Name` Desc")]
    public void CachedColumn_WhenFieldIsNullDuringInitialization_ShouldMatchUninterceptedBehavior(
        string operation, string chain, string expectedSql)
    {
        var source = LifecycleSource(chain, initializeBeforeSelector: true, reassignInStaticConstructor: false);
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        (string Initialization, string Sql) Run(bool interceptors)
        {
            var generated = RunGenerator(source, analyzerConfigOptionsProvider: interceptors ? strict : null,
                enableInterceptors: interceptors);
            AssertNoCompilationErrors(generated);
            if (interceptors)
            {
                Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
                Assert.Contains($"SqlGeneratedQueryBridge.Project(source, \"{operation}\"", generated.GeneratedSource,
                    StringComparison.Ordinal);
            }

            var assembly = LoadGeneratedAssembly(generated);
            var bootstrap = assembly.GetType("LifecycleBootstrap")!;
            bootstrap.GetField("Executor")!.SetValue(null, new Mock<ISqlQueryPlanExecutor>().Object);
            bootstrap.GetField("Builder")!.SetValue(null, new SqliteBuilder());

            var consumer = assembly.GetType("LifecycleConsumer")!;
            var initialization = (string)consumer.GetField("DuringInitialization")!.GetValue(null)!;
            var apply = consumer.GetMethod("Apply")!;
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
                new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder());
            var sql = (string)apply.Invoke(null, new object[] { query, true })!;
            return (initialization, sql);
        }

        var baseline = Run(interceptors: false);
        var intercepted = Run(interceptors: true);

        Assert.Equal(baseline.Initialization, intercepted.Initialization);
        Assert.NotEqual(expectedSql, intercepted.Initialization);
        Assert.Equal(expectedSql, baseline.Sql);
        Assert.Equal(expectedSql, intercepted.Sql);
    }

    /// <summary>显式静态构造器重赋表达式字段时，应保留运行时表达式和兼容诊断。</summary>
    [Theory]
    [InlineData("ReplaceSelect", "ReplaceSelect<Row>(Selector, \"r\")",
        "Select `r`.`Name` \r\nFrom `Row` As `r`")]
    [InlineData("ExcludeSelect", "SelectAll().ExcludeSelect<Row>(Selector, \"r\")",
        "Select `r`.`Id` \r\nFrom `Row` As `r`")]
    [InlineData("GroupBy", "Select<Row>(row => row.Name, \"r\").GroupBy<Row>(Columns, \"r\")",
        "Select `r`.`Name` \r\nFrom `Row` As `r` \r\nGroup By `r`.`Name`")]
    [InlineData("OrderBy", "Select<Row>(row => row.Id, \"r\").OrderBy<Row>(Columns, \"r\", descending)",
        "Select `r`.`Id` \r\nFrom `Row` As `r` \r\nOrder By `r`.`Name` Desc")]
    public void CachedColumn_WhenStaticConstructorReassignsField_ShouldUseRuntimeValue(
        string operation, string chain, string expectedSql)
    {
        var source = LifecycleSource(chain, initializeBeforeSelector: false, reassignInStaticConstructor: true);
        var generated = RunGenerator(source);

        AssertNoCompilationErrors(generated);
        Assert.Contains(generated.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain($"Project(source, \"{operation}\"", generated.GeneratedSource,
            StringComparison.Ordinal);

        var assembly = LoadGeneratedAssembly(generated);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder());
        var apply = assembly.GetType("LifecycleConsumer")!.GetMethod("Apply")!;

        Assert.Equal(expectedSql, (string)apply.Invoke(null, new object[] { query, true })!);
    }

    private static string LifecycleSource(string chain, bool initializeBeforeSelector,
        bool reassignInStaticConstructor)
    {
        var initialization = initializeBeforeSelector ? "public static readonly string DuringInitialization = Render();" : string.Empty;
        var selector = reassignInStaticConstructor ? "row => row.Id" : "row => row.Name";
        var columns = reassignInStaticConstructor ? "row => new object[] { row.Id }" : "row => new object[] { row.Name }";
        var staticConstructor = reassignInStaticConstructor
            ? "static LifecycleConsumer() { Selector = row => row.Name; Columns = row => new object[] { row.Name }; }"
            : string.Empty;

        return $$"""
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;

            public sealed class Row
            {
                public int Id { get; set; }
                public string Name { get; set; } = string.Empty;
            }

            public static class LifecycleBootstrap
            {
                public static ISqlQueryPlanExecutor Executor;
                public static ISqlBuilder Builder;
            }

            public static class LifecycleConsumer
            {
                {{initialization}}
                private static readonly Expression<Func<Row, object>> Selector = {{selector}};
                private static readonly Expression<Func<Row, object[]>> Columns = {{columns}};
                {{staticConstructor}}

                public static string Apply(SqlLambdaQuery query, bool descending) =>
                    query.From<Row>("r").{{chain}}.ToSql();

                private static string Render()
                {
                    try
                    {
                        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
                            LifecycleBootstrap.Executor, LifecycleBootstrap.Builder);
                        return Apply(query, true);
                    }
                    catch (Exception error)
                    {
                        return error.GetType().FullName + ":" + error.Message;
                    }
                }
            }
            """;
    }
}
