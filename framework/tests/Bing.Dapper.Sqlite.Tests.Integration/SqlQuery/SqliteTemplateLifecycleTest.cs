using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>冻结模板可在真实 SQLite 查询中复用，且与来源草稿、消费分支隔离。</summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteTemplateLifecycleTest : IAsyncLifetime
{
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>使用专用 SQLite 本地测试库。</summary>
    public SqliteTemplateLifecycleTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>派生表模板在来源和消费 Builder 派生修改后仍保持独立。</summary>
    [Fact]
    public async Task FrozenTemplate_WhenUsedAsFromFragment_ShouldIsolateSourceAndConsumerClone()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        var source = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 2, Operator.GreaterEqual);
        var template = source.ToTemplate();
        const string templateSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0";
        source.Where("Name", "missing");

        Assert.Equal(templateSql, template.Sql);
        Assert.Equal(new object[] { 2 }, template.Parameters.Select(parameter => parameter.Value).ToArray());

        var originalBuilder = new SqliteBuilder().Select("Name")
            .From(template.CreateFragment(), "items").OrderBy("Name");
        var branchBuilder = (SqliteBuilder)originalBuilder.Clone();
        branchBuilder.Where("Name", "two");
        var secondBuilder = new SqliteBuilder().Select("Name")
            .From(template.CreateFragment(), "other").Where("Name", "three").OrderBy("Name");

        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var original = new Bing.Data.Sql.SqlQuery(executor, originalBuilder);
        var branch = new Bing.Data.Sql.SqlQuery(executor, branchBuilder);
        var second = new Bing.Data.Sql.SqlQuery(executor, secondBuilder);

        const string originalSql = "Select `Name` \r\nFrom (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0) As `items` \r\nOrder By `Name`";
        const string branchSql = "Select `Name` \r\nFrom (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0) As `items` \r\nWhere `Name`=@_p_1 \r\nOrder By `Name`";
        const string secondSql = "Select `Name` \r\nFrom (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0) As `other` \r\nWhere `Name`=@_p_1 \r\nOrder By `Name`";
        var originalPlan = original.GetPlan();
        var branchPlan = branch.GetPlan();
        var secondPlan = second.GetPlan();

        Assert.Equal(originalSql, originalPlan.CommandText);
        Assert.Equal(new object[] { 2 }, Values(originalPlan));
        Assert.Equal(branchSql, branchPlan.CommandText);
        Assert.Equal(new object[] { 2, "two" }, Values(branchPlan));
        Assert.Equal(secondSql, secondPlan.CommandText);
        Assert.Equal(new object[] { 2, "three" }, Values(secondPlan));
        Assert.Equal(new[] { "three", "two" }, original.ToList<string>());
        Assert.Equal(new[] { "two" }, branch.ToList<string>());
        Assert.Equal(new[] { "three" }, second.ToList<string>());
        Assert.Equal(originalSql, original.GetPlan().CommandText);
        Assert.Equal(templateSql, template.Sql);
    }

    /// <summary>同一 CTE 模板可由独立查询消费，并正确派生 Count 与数据页。</summary>
    [Fact]
    public async Task FrozenTemplate_AsCte_ShouldSupportIndependentCountAndPageQueries()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        var source = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 2, Operator.GreaterEqual);
        var template = source.ToTemplate();
        var cte = template.AsCte("selected");
        source.Where("Name", "missing");

        var pageBuilder = new SqliteBuilder().Select("Name").From("selected")
            .With(cte.Name, cte.Fragment).OrderBy("Name");
        var filteredBuilder = new SqliteBuilder().Select("Name").From("selected")
            .With(cte.Name, cte.Fragment).Where("Name", "two").OrderBy("Name");
        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var pageQuery = new Bing.Data.Sql.SqlQuery(executor, pageBuilder);
        var filteredQuery = new Bing.Data.Sql.SqlQuery(executor, filteredBuilder);

        const string ctePrefix = "With Recursive `selected` \r\nAs (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_0)\r\n";
        const string pageSql = ctePrefix + "Select `Name` \r\nFrom `selected` \r\nOrder By `Name`";
        // 外层先分配条件参数，CTE 合并时为同名的不同值分配新名称。
        const string filteredSql = "With Recursive `selected` \r\nAs (Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_1)\r\n" +
            "Select `Name` \r\nFrom `selected` \r\nWhere `Name`=@_p_0 \r\nOrder By `Name`";
        var plan = pageQuery.GetPlan();
        Assert.Equal(pageSql, plan.CommandText);
        Assert.Equal(new object[] { 2 }, Values(plan));

        var pagePlan = SqlBuilderRuntimeBridge.CreatePagePlan(plan, new Pager(1, 1));
        Assert.Equal(pageSql + " \r\nLimit @_p_2 OFFSET @_p_1", pagePlan.CommandText);
        Assert.Equal(new object[] { 2, 0, 1 }, Values(pagePlan));
        var countPlan = SqlBuilderRuntimeBridge.CreateCountPlan(pagePlan);
        Assert.Equal(ctePrefix + "Select Count(*) \r\nFrom `selected`", countPlan.CommandText);
        Assert.Equal(new object[] { 2 }, Values(countPlan));

        var page = pageQuery.ToPage<string>(new Pager(1, 1));
        var filteredPlan = filteredQuery.GetPlan();
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "three" }, page.Data);
        Assert.Equal(filteredSql, filteredPlan.CommandText);
        Assert.Equal(new object[] { "two", 2 }, Values(filteredPlan));
        Assert.Equal(new[] { "two" }, filteredQuery.ToList<string>());
        Assert.Equal(pageSql, plan.CommandText);
        Assert.Equal(new object[] { 2 }, Values(plan));
    }

    /// <summary>多个 UnionAll 消费者共享冻结分支但保留各自主分支参数。</summary>
    [Fact]
    public async Task FrozenTemplate_WhenUsedInUnionAll_ShouldKeepConsumerBindingsIndependent()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        var source = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 2, Operator.GreaterEqual);
        var template = source.ToTemplate();
        source.Where("Name", "missing");

        var firstBuilder = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 1).UnionAll(template.CreateFragment()).OrderBy("Name");
        var secondBuilder = new SqliteBuilder().Select("Name").From("samples")
            .Where("Amount", 3).UnionAll(template.CreateFragment()).OrderBy("Name");
        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var first = new Bing.Data.Sql.SqlQuery(executor, firstBuilder);
        var second = new Bing.Data.Sql.SqlQuery(executor, secondBuilder);

        const string expectedSql = "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`=@_p_0 \r\nUnion All \r\n" +
            "Select `Name` \r\nFrom `samples` \r\nWhere `Amount`>=@_p_1 \r\nOrder By `Name`";
        var firstPlan = first.GetPlan();
        var secondPlan = second.GetPlan();

        Assert.Equal(expectedSql, firstPlan.CommandText);
        Assert.Equal(new object[] { 1, 2 }, Values(firstPlan));
        Assert.Equal(expectedSql, secondPlan.CommandText);
        Assert.Equal(new object[] { 3, 2 }, Values(secondPlan));
        Assert.Equal(new[] { "one", "three", "two" }, first.ToList<string>());
        Assert.Equal(new[] { "three", "three", "two" }, second.ToList<string>());
        Assert.Equal(expectedSql, first.GetPlan().CommandText);
        Assert.Equal(new object[] { 2 }, template.Parameters.Select(parameter => parameter.Value).ToArray());
    }

    /// <summary>动态过滤条件在冻结时读取，后续冻结可捕获过滤器的新值。</summary>
    [Fact]
    public async Task DynamicFilterTemplate_ShouldCaptureValueWhenFrozen()
    {
        using (var writer = _fixture.CreateExecutor())
            await writer.ExecuteSqlAsync(
                "Insert Into samples(Name, Amount) Values ('one', 1), ('two', 2), ('three', 3)");

        var filter = new AlternatingFilter();
        var source = new SqliteBuilder(new SqlBuilderServices(filters: new[] { filter }))
            .Select("Name").From<FilterRow>("s");
        var firstTemplate = source.ToTemplate();
        var secondTemplate = source.ToTemplate();
        Assert.Equal(2, filter.Calls);

        using var root = _fixture.CreateQuery();
        var executor = (ISqlQueryPlanExecutor)root;
        var first = new Bing.Data.Sql.SqlQuery(executor,
            new SqliteBuilder().Select("Name").From(firstTemplate.CreateFragment(), "items").OrderBy("Name"));
        var second = new Bing.Data.Sql.SqlQuery(executor,
            new SqliteBuilder().Select("Name").From(secondTemplate.CreateFragment(), "items").OrderBy("Name"));
        const string expectedSql = "Select `Name` \r\nFrom (Select `Name` \r\nFrom `samples` As `s` \r\n" +
            "Where `s`.`Name`=@_p_0) As `items` \r\nOrder By `Name`";
        var firstPlan = first.GetPlan();
        var secondPlan = second.GetPlan();

        Assert.Equal(expectedSql, firstPlan.CommandText);
        Assert.Equal(new object[] { "one" }, Values(firstPlan));
        Assert.Equal(expectedSql, secondPlan.CommandText);
        Assert.Equal(new object[] { "two" }, Values(secondPlan));
        Assert.Equal(new[] { "one" }, first.ToList<string>());
        Assert.Equal(new[] { "two" }, second.ToList<string>());
        Assert.Equal(2, filter.Calls);
    }

    /// <summary>每个测试前重置本地数据。</summary>
    public Task InitializeAsync() => _fixture.ResetAsync();
    /// <summary>共享连接由测试 fixture 负责释放。</summary>
    public Task DisposeAsync() => Task.CompletedTask;

    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<SqlParam[]>(plan.Parameters).Select(parameter => parameter.Value).ToArray();

    [Table("samples")]
    private sealed class FilterRow { public string Name { get; set; } }

    private sealed class AlternatingFilter : ISqlFilter
    {
        internal int Calls { get; private set; }

        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(FilterRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(FilterRow.Name)),
                ++Calls % 2 == 1 ? "one" : "two");
        }
    }
}
