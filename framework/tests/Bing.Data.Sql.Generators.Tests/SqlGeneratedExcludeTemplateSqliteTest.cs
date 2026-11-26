using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;
using System.Reflection;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>生成模板在真实 SQLite 中复用 SQL，同时绑定当前值并保留排除列。</summary>
    [Fact]
    public void GeneratedExcludeTemplate_WhenExecutedAgainstSqlite_ShouldUseCurrentValuesAndColumns()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders;
            public sealed class ExcludeSqliteRow
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public string Secret { get; set; }
            }
            public static class ExcludeSqliteConsumer
            {
                private static readonly Expression<Func<ExcludeSqliteRow, object>> Excluded = row => row.Secret;
                public static List<ExcludeSqliteRow> Render(ISqlQueryPlanExecutor executor, ISqlBuilder builder, bool enabled, int minimum) =>
                    SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<ExcludeSqliteRow>("r")
                        .WhereIf<ExcludeSqliteRow, int>(enabled, minimum, (row, value) => row.Id >= value, "r")
                        .SelectAll().ExcludeSelect<ExcludeSqliteRow>(Excluded, "r").ToList<ExcludeSqliteRow>();
            }
            """;
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var generated = RunGenerator(source, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        var render = LoadGeneratedAssembly(generated).GetType("ExcludeSqliteConsumer")!.GetMethod("Render")!;
        var services = SqlBuilderServices.CreateDefault();

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE ExcludeSqliteRow (Id INTEGER, Name TEXT, Secret TEXT); " +
            "INSERT INTO ExcludeSqliteRow VALUES (1, 'one', 'x'), (2, 'two', 'y'), (3, 'three', 'z');";
        command.ExecuteNonQuery();

        var first = Execute(true, 2);
        var sameShape = Execute(true, 3);
        var disabled = Execute(false, 3);
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `ExcludeSqliteRow` As `r` \r\nWhere `r`.`Id`>=@_p_0", first.Sql);
        Assert.Same(first.Sql, sameShape.Sql);
        Assert.Equal(new[] { "2:two", "3:three" }, first.Rows);
        Assert.Equal(new[] { "3:three" }, sameShape.Rows);
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `ExcludeSqliteRow` As `r`", disabled.Sql);
        Assert.Equal(new[] { "1:one", "2:two", "3:three" }, disabled.Rows);

        (string Sql, string[] Rows) Execute(bool enabled, int minimum)
        {
            var builder = new SqliteBuilder(services);
            var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
            render.Invoke(null, new object[] { executor, builder, enabled, minimum });
            var plan = Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
            var sql = plan.CommandText;
            command.Parameters.Clear();
            command.CommandText = sql;
            foreach (var parameter in Assert.IsType<SqlParam[]>(plan.Parameters))
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
            using var reader = command.ExecuteReader();
            Assert.Equal(2, reader.FieldCount);
            var rows = new List<string>();
            while (reader.Read()) rows.Add($"{reader.GetInt32(0)}:{reader.GetString(1)}");
            return (sql, rows.ToArray());
        }
    }
}
