using System.Linq.Expressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>跨文件静态表达式应同时支持 Where 与 WhereIf，并保留完整 SQL 和参数。</summary>
    [Fact]
    public void CachedPredicate_WhenSharedFieldIsUsedByWhereAndWhereIf_ShouldGenerateAndBindCorrectly()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                public static SqlLambdaQuery Where(SqlLambdaQuery query) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .Where<CachedRow>(SharedCachedPredicate.Minimum, "r");

                public static SqlLambdaQuery WhereIf(SqlLambdaQuery query, bool enabled) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .WhereIf<CachedRow>(enabled, SharedCachedPredicate.Minimum, "r");
            }
            """;
        const string sharedSource = """
            using System;
            using System.Linq.Expressions;
            public static class SharedCachedPredicate
            {
                public static readonly Expression<Func<CachedRow, bool>> Minimum = row => row.Id >= 7;
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: strict, additionalSource: sharedSource);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");

        var assembly = LoadGeneratedAssembly(result);
        var consumer = assembly.GetType("CachedConsumer")!;
        var expectedSql = "Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>=@_p_0";
        foreach (var methodName in new[] { "Where", "WhereIf" })
        {
            var builder = new SqliteBuilder();
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
                new Mock<ISqlQueryPlanExecutor>().Object, builder);
            var arguments = methodName == "Where" ? new object[] { query } : new object[] { query, true };
            var built = (SqlLambdaQuery)consumer.GetMethod(methodName)!.Invoke(null, arguments)!;

            Assert.Equal(expectedSql, built.ToSql());
            Assert.Equal(7, Assert.Single(builder.GetParams()).Value);
        }
    }

    /// <summary>WhereIf 的关闭分支不追加条件，开启分支按 From、WhereIf、ToSql 链生成完整结果。</summary>
    [Theory]
    [InlineData(false, "Select `r`.`Id` \r\nFrom `CachedRow` As `r`", false)]
    [InlineData(true, "Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", true)]
    public void CachedPredicate_WhenWhereIfSwitchChanges_ShouldRenderExpectedSqlAndParameters(
        bool enabled, string expectedSql, bool hasParameter)
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                public static string Render(SqlLambdaQuery query, bool enabled) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .WhereIf<CachedRow>(enabled, SharedCachedPredicate.Minimum, "r")
                        .ToSql();
            }
            """;
        const string sharedSource = """
            using System;
            using System.Linq.Expressions;
            public static class SharedCachedPredicate
            {
                public static readonly Expression<Func<CachedRow, bool>> Minimum = row => row.Id >= 7;
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: strict, additionalSource: sharedSource);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        var assembly = LoadGeneratedAssembly(result);
        var builder = new SqliteBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, builder);

        var sql = (string)assembly.GetType("CachedConsumer")!.GetMethod("Render")!
            .Invoke(null, new object[] { query, enabled })!;

        Assert.Equal(expectedSql, sql);
        Assert.Equal(hasParameter ? 1 : 0, builder.GetParams().Count);
        if (hasParameter)
            Assert.Equal(7, Assert.Single(builder.GetParams()).Value);
    }

    /// <summary>静态双参数表达式可跨调用复用，Where<TEntity,TValue> 每次使用当前显式值。</summary>
    [Fact]
    public void CachedPredicate_WhenExplicitValueChangesAcrossCalls_ShouldBindCurrentValue()
    {
        const string source = """
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                public static SqlLambdaQuery Apply(SqlLambdaQuery query, int minimum) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .Where<CachedRow, int>(minimum, SharedCachedPredicate.Minimum, "r");
            }
            """;
        const string sharedSource = """
            using System;
            using System.Linq.Expressions;
            public static class SharedCachedPredicate
            {
                public static readonly Expression<Func<CachedRow, int, bool>> Minimum =
                    (row, value) => row.Id >= value;
            }
            """;
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });

        var result = RunGenerator(source, analyzerConfigOptionsProvider: strict, additionalSource: sharedSource);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        Assert.Contains("SqlGeneratedPredicate.CompareParameter(", result.GeneratedSource, StringComparison.Ordinal);
        var assembly = LoadGeneratedAssembly(result);
        var apply = assembly.GetType("CachedConsumer")!.GetMethod("Apply")!;
        const string expectedSql = "Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>=@_p_0";

        foreach (var minimum in new[] { 3, 11 })
        {
            var builder = new SqliteBuilder();
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
                new Mock<ISqlQueryPlanExecutor>().Object, builder);
            var built = (SqlLambdaQuery)apply.Invoke(null, new object[] { query, minimum })!;

            Assert.Equal(expectedSql, built.ToSql());
            Assert.Equal(minimum, Assert.Single(builder.GetParams()).Value);
        }
    }

    /// <summary>静态构造函数重赋只读字段时应回退到字段实际值，不能使用声明处表达式。</summary>
    [Fact]
    public void CachedPredicate_WhenStaticConstructorReassignsField_ShouldUseRuntimeValue()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                private static readonly Expression<Func<CachedRow, bool>> Predicate = row => row.Id < 2;
                static CachedConsumer() => Predicate = row => row.Id >= 8;
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .Where<CachedRow>(Predicate, "r");
            }
            """;
        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        var builder = new SqliteBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var built = (SqlLambdaQuery)LoadGeneratedAssembly(result).GetType("CachedConsumer")!
            .GetMethod("Apply")!.Invoke(null, new object[] { query })!;

        Assert.Equal("Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", built.ToSql());
        Assert.Equal(8, Assert.Single(builder.GetParams()).Value);
    }

    /// <summary>泛型宿主中的静态表达式保留运行时回退并可正常生成 SQL。</summary>
    [Fact]
    public void CachedPredicate_WhenFieldIsOnGenericHost_ShouldCompileAndUseRuntimeExpression()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class GenericCachedConsumer<TTag>
            {
                private static readonly Expression<Func<CachedRow, bool>> Predicate = row => row.Id >= 5;
                public static SqlLambdaQuery Apply(SqlLambdaQuery query) =>
                    query.From<CachedRow>("r").Select<CachedRow>(row => row.Id)
                        .Where<CachedRow>(Predicate, "r");
            }
            """;
        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        var consumer = LoadGeneratedAssembly(result).GetType("GenericCachedConsumer`1")!
            .MakeGenericType(typeof(string));
        var builder = new SqliteBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var built = (SqlLambdaQuery)consumer.GetMethod("Apply")!.Invoke(null, new object[] { query })!;

        Assert.Equal("Select `r`.`Id` \r\nFrom `CachedRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", built.ToSql());
        Assert.Equal(5, Assert.Single(builder.GetParams()).Value);
    }

    /// <summary>类型初始化期间字段仍为 null 时，null 实例调用 WhereIf 保持原有异常行为。</summary>
    [Fact]
    public void CachedPredicate_WhenFieldIsNullDuringInitializationAndQueryIsNull_ShouldPreserveOriginalBehavior()
    {
        const string source = """
            using System;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            public sealed class CachedRow { public int Id { get; set; } }
            public static class CachedConsumer
            {
                public static readonly string InitializationResult = Capture();
                private static readonly Expression<Func<CachedRow, bool>> Predicate = row => row.Id >= 5;

                private static string Capture()
                {
                    try
                    {
                        Apply(null, false);
                        return "no exception";
                    }
                    catch (NullReferenceException)
                    {
                        return "NullReferenceException";
                    }
                }

                public static SqlLambdaQuery Apply(SqlLambdaQuery query, bool enabled) =>
                    query.WhereIf<CachedRow>(enabled, Predicate);
            }
            """;
        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        var value = LoadGeneratedAssembly(result).GetType("CachedConsumer")!
            .GetField("InitializationResult")!.GetValue(null);

        Assert.Equal("NullReferenceException", value);
    }
}
