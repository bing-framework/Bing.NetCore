using System.Reflection;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string CollectionTemplateSource = """
        using System.Collections.Generic;
        using System.Linq;
        using Bing.Data.Sql;
        public sealed class CollectionTemplateRow { public int Id { get; set; } }
        public static class CollectionTemplateConsumer
        {
            public static List<CollectionTemplateRow> Load(ISqlQueryPlanExecutor executor, ISqlBuilder builder,
                string alias, bool enabled, int[] ids) =>
                SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder).From<CollectionTemplateRow>(alias)
                    .WhereIf<CollectionTemplateRow, int[]>(enabled, ids, (row, values) => values.Contains(row.Id), alias)
                    .Select<CollectionTemplateRow>(row => row.Id, alias).ToList<CollectionTemplateRow>();
        }
        """;

    private static readonly SqlBuilderServices CollectionTemplateServices = SqlBuilderServices.CreateDefault();
    private static readonly Lazy<(Assembly Assembly, string Source)> CollectionTemplateFixture = new(() =>
    {
        var options = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
            { ["build_property.BingSqlAotStrict"] = "true" });
        var result = RunGenerator(CollectionTemplateSource, analyzerConfigOptionsProvider: options);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        return (LoadGeneratedAssembly(result), result.GeneratedSource);
    });

    /// <summary>集合显式值应进入完整模板，并保留严格生成诊断合同。</summary>
    [Fact]
    public void CollectionTemplate_WhenGenerated_ShouldUseTemplateWithoutDiagnostics()
    {
        Assert.Equal(2, CountOccurrences(CollectionTemplateFixture.Value.Source, "SqlGeneratedQueryTemplate.Apply(source, Template_"));
    }

    /// <summary>开关、集合长度及别名隔离 SQL 形状，相同形状独立绑定本次集合。</summary>
    [Fact]
    public void CollectionTemplate_WhenReused_ShouldBindCurrentValuesAndSeparateShapes()
    {
        var first = ExecuteCollectionTemplate("r", true, new[] { 1, 2 });
        var second = ExecuteCollectionTemplate("r", true, new[] { 2, 3 });
        var empty = ExecuteCollectionTemplate("r", true, Array.Empty<int>());
        var single = ExecuteCollectionTemplate("r", true, new[] { 2 });
        var disabled = ExecuteCollectionTemplate("r", false, new[] { 99 });
        var otherAlias = ExecuteCollectionTemplate("s", true, new[] { 1, 2 });
        Assert.Equal("Select `r`.`Id` \r\nFrom `CollectionTemplateRow` As `r` \r\nWhere `r`.`Id` In (@_p_0,@_p_1)", first.CommandText);
        Assert.Same(first.CommandText, second.CommandText);
        Assert.Equal(new object[] { 1, 2 }, Assert.IsType<SqlParam[]>(first.Parameters).Select(p => p.Value));
        Assert.Equal(new object[] { 2, 3 }, Assert.IsType<SqlParam[]>(second.Parameters).Select(p => p.Value));
        Assert.Equal("Select `r`.`Id` \r\nFrom `CollectionTemplateRow` As `r` \r\nWhere 1 = 0", empty.CommandText);
        Assert.Empty(Assert.IsType<SqlParam[]>(empty.Parameters));
        Assert.Equal("Select `r`.`Id` \r\nFrom `CollectionTemplateRow` As `r` \r\nWhere `r`.`Id` In (@_p_0)", single.CommandText);
        Assert.Equal("Select `r`.`Id` \r\nFrom `CollectionTemplateRow` As `r`", disabled.CommandText);
        Assert.Empty(Assert.IsType<SqlParam[]>(disabled.Parameters));
        Assert.Equal("Select `s`.`Id` \r\nFrom `CollectionTemplateRow` As `s` \r\nWhere `s`.`Id` In (@_p_0,@_p_1)", otherAlias.CommandText);
    }

    /// <summary>真实 SQLite 执行使用生成计划与当前集合，不串用首个请求的参数。</summary>
    [Fact]
    public void CollectionTemplate_WhenExecutedAgainstSqlite_ShouldUseCurrentValues()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE CollectionTemplateRow (Id INTEGER); INSERT INTO CollectionTemplateRow VALUES (1), (2), (3);";
        command.ExecuteNonQuery();
        Assert.Equal(new[] { 1, 2 }, Read(new[] { 1, 2 }));
        Assert.Equal(new[] { 2, 3 }, Read(new[] { 2, 3 }));

        int[] Read(int[] ids)
        {
            var plan = ExecuteCollectionTemplate("r", true, ids);
            command.Parameters.Clear();
            command.CommandText = plan.CommandText;
            foreach (var parameter in Assert.IsType<SqlParam[]>(plan.Parameters))
                command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
            using var reader = command.ExecuteReader();
            var rows = new List<int>();
            while (reader.Read()) rows.Add(reader.GetInt32(0));
            return rows.ToArray();
        }
    }

    private static SqlQueryPlan ExecuteCollectionTemplate(string alias, bool enabled, int[] ids)
    {
        var executor = DispatchProxy.Create<ISqlQueryPlanExecutor, CapturingPlanExecutor>();
        CollectionTemplateFixture.Value.Assembly.GetType("CollectionTemplateConsumer")!.GetMethod("Load")!
            .Invoke(null, new object[] { executor, new SqliteBuilder(CollectionTemplateServices), alias, enabled, ids });
        return Assert.IsType<SqlQueryPlan>(((CapturingPlanExecutor)executor).Plan);
    }
}
