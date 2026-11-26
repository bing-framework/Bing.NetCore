using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string CachedColumnClauseSource = """
        using System;
        using System.Linq.Expressions;
        using Bing.Data.Sql;

        public sealed class CachedColumnRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string GroupName { get; set; }
            public string Secret { get; set; }
            public int Score { get; set; }
        }

        public static class SharedColumns
        {
            public static readonly Expression<Func<CachedColumnRow, object>> Replacement = row => row.Name;
            public static readonly Expression<Func<CachedColumnRow, object>> Excluded = row => row.Secret;
            public static readonly Expression<Func<CachedColumnRow, object>> Group = row => row.GroupName;
            public static readonly Expression<Func<CachedColumnRow, object>> Sort = row => row.Id;
        }

        public static class CachedColumnConsumer
        {
            public static SqlLambdaQuery Replace(SqlLambdaQuery query, bool enabled, int minimum) =>
                query.From<CachedColumnRow>("r")
                    .WhereIf<CachedColumnRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                    .Select<CachedColumnRow>(row => row.Id, "r")
                    .ReplaceSelect<CachedColumnRow>(SharedColumns.Replacement, "r");

            public static SqlLambdaQuery Exclude(SqlLambdaQuery query, bool enabled, int minimum) =>
                query.From<CachedColumnRow>("r")
                    .WhereIf<CachedColumnRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                    .SelectAll()
                    .ExcludeSelect<CachedColumnRow>(SharedColumns.Excluded, "r");

            public static SqlLambdaQuery Group(SqlLambdaQuery query, bool enabled, int minimum) =>
                query.From<CachedColumnRow>("r")
                    .WhereIf<CachedColumnRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                    .Select<CachedColumnRow>(row => row.GroupName, "r")
                    .GroupBy<CachedColumnRow>(SharedColumns.Group, "r");

            public static SqlLambdaQuery Order(SqlLambdaQuery query, bool enabled, int minimum, bool descending) =>
                query.From<CachedColumnRow>("r")
                    .WhereIf<CachedColumnRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                    .Select<CachedColumnRow>(row => row.Name, "r")
                    .OrderBy<CachedColumnRow>(SharedColumns.Sort, descending, "r");
        }
        """;

    /// <summary>同一编译内静态只读列表达式应生成 Replace、Exclude、GroupBy 和 OrderBy SQL。</summary>
    [Fact]
    public void CachedColumns_WhenUsedByColumnClauses_ShouldGenerateCompleteSqlAndBindWhereIfValues()
    {
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(CachedColumnClauseSource, analyzerConfigOptionsProvider: strict);

        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");

        var consumer = LoadGeneratedAssembly(generated).GetType("CachedColumnConsumer")!;
        const int minimum = 2;

        var replaceBuilder = new SqliteBuilder();
        var replace = Apply("Replace", replaceBuilder, true, minimum);
        Assert.Equal(
            "Select `r`.`Name` \r\nFrom `CachedColumnRow` As `r` \r\nWhere `r`.`Id`>=@_p_0",
            replace.ToSql());
        Assert.Equal(minimum, Assert.Single(replaceBuilder.GetParams()).Value);

        var excludeBuilder = new SqliteBuilder();
        var exclude = Apply("Exclude", excludeBuilder, true, minimum);
        Assert.Equal(
            "Select `r`.`Id`,`r`.`GroupName`,`r`.`Name`,`r`.`Score` \r\nFrom `CachedColumnRow` As `r` \r\nWhere `r`.`Id`>=@_p_0",
            exclude.ToSql());
        Assert.Equal(minimum, Assert.Single(excludeBuilder.GetParams()).Value);

        var groupBuilder = new SqliteBuilder();
        var group = Apply("Group", groupBuilder, true, minimum);
        Assert.Equal(
            "Select `r`.`GroupName` \r\nFrom `CachedColumnRow` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nGroup By `r`.`GroupName`",
            group.ToSql());
        Assert.Equal(minimum, Assert.Single(groupBuilder.GetParams()).Value);

        var ascendingBuilder = new SqliteBuilder();
        var ascending = Apply("Order", ascendingBuilder, true, minimum, false);
        Assert.Equal(
            "Select `r`.`Name` \r\nFrom `CachedColumnRow` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nOrder By `r`.`Id`",
            ascending.ToSql());
        Assert.Equal(minimum, Assert.Single(ascendingBuilder.GetParams()).Value);

        var descendingBuilder = new SqliteBuilder();
        var descending = Apply("Order", descendingBuilder, true, minimum, true);
        Assert.Equal(
            "Select `r`.`Name` \r\nFrom `CachedColumnRow` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nOrder By `r`.`Id` Desc",
            descending.ToSql());
        Assert.Equal(minimum, Assert.Single(descendingBuilder.GetParams()).Value);

        SqlLambdaQuery Apply(string methodName, SqliteBuilder builder, bool enabled, int value, bool? descending = null)
        {
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
            var arguments = descending.HasValue
                ? new object[] { query, enabled, value, descending.Value }
                : new object[] { query, enabled, value };
            return (SqlLambdaQuery)consumer.GetMethod(methodName)!.Invoke(null, arguments)!;
        }
    }

    /// <summary>静态只读排序列生成的 SQL 应在 SQLite 中按升降序返回正确行。</summary>
    [Theory]
    [InlineData(false, "two,three")]
    [InlineData(true, "three,two")]
    public void CachedColumns_WhenOrderedAgainstSqlite_ShouldReturnExpectedRows(bool descending, string expectedNames)
    {
        var strict = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(CachedColumnClauseSource, analyzerConfigOptionsProvider: strict);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");

        var consumer = LoadGeneratedAssembly(generated).GetType("CachedColumnConsumer")!;
        var builder = new SqliteBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var built = (SqlLambdaQuery)consumer.GetMethod("Order")!.Invoke(null,
            new object[] { query, true, 2, descending })!;
        var expectedSql = "Select `r`.`Name` \r\nFrom `CachedColumnRow` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nOrder By `r`.`Id`" +
            (descending ? " Desc" : "");
        Assert.Equal(expectedSql, built.ToSql());

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE CachedColumnRow (Id INTEGER, Name TEXT, GroupName TEXT, Secret TEXT, Score INTEGER); " +
            "INSERT INTO CachedColumnRow VALUES (1, 'one', 'a', 'x', 10), (2, 'two', 'a', 'y', 20), (3, 'three', 'b', 'z', 30);";
        command.ExecuteNonQuery();
        command.CommandText = built.ToSql();
        foreach (var parameter in builder.GetParams())
            command.Parameters.AddWithValue(parameter.Key, parameter.Value);

        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        Assert.Equal(expectedNames.Split(','), names);
        Assert.Equal(2, Assert.Single(builder.GetParams()).Value);
    }
}
