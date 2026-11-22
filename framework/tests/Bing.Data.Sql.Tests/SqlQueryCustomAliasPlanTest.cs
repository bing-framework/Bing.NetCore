using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>验证 Lambda 别名冻结后可与 CTE 的分页计划组合。</summary>
public sealed class SqlQueryCustomAliasPlanTest
{
    /// <summary>克隆注册器保留 Lambda 别名，CTE 内部分页和参数在重复派生时保持不变。</summary>
    [Fact]
    public void FrozenLambdaAliasWithCte_WhenPagedRepeatedly_ShouldKeepAliasesInnerPageAndSource()
    {
        var aliases = new TestAliasRegister(typeof(AliasRow), "frozen_alias");
        var frozenAliases = aliases.Clone();
        aliases.Replace(typeof(AliasRow), "changed_alias");

        var projection = new SelectClause(TestSqlBuilder.CreateTestClauseContext(aliasRegister: frozenAliases));
        projection.Select<AliasRow>(row => row.Id);
        const string expectedProjection = "Select [frozen_alias].[Id]";
        Assert.Equal(expectedProjection, projection.ToSql());
        Assert.Equal("frozen_alias", frozenAliases.GetAlias(typeof(AliasRow)));
        Assert.Equal("changed_alias", aliases.GetAlias(typeof(AliasRow)));

        var cte = new TestSqlBuilder().Select("Id").From("archive", "history")
            .Where("Status", 7).OrderBy("Id").Skip(1).Take(3);
        var source = SqlQueryPlan.Create(new TestSqlBuilder()
            .Select(projection.ToSql().Substring("Select ".Length))
            .From("recent", "frozen_alias").Where("TenantId", 9)
            .OrderBy("frozen_alias.Id").Skip(5).Take(2)
            .With("recent", cte)).Freeze();
        const string prefix = "With [recent] \r\nAs (Select [Id] \r\nFrom [archive] As [history] \r\nWhere [Status]=@_p_3 \r\nOrder By [Id] \r\nOffset @_p_4 Rows Fetch Next @_p_5 Rows Only)\r\n";
        const string body = "Select [frozen_alias].[Id] \r\nFrom [recent] As [frozen_alias] \r\nWhere [TenantId]=@_p_0";
        const string sourceSql = prefix + body + " \r\nOrder By [frozen_alias].[Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only";
        Assert.Equal(sourceSql, source.CommandText);

        var firstPage = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(3, 4, "TenantId Desc"));
        var secondPage = SqlBuilderRuntimeBridge.CreatePagePlan(firstPage, new Pager(2, 2, "Name Desc"));
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(secondPage);

        Assert.Equal(prefix + body + " \r\nOrder By [frozen_alias].[Id],[TenantId] Desc \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only",
            firstPage.CommandText);
        Assert.Equal(prefix + body + " \r\nOrder By [frozen_alias].[Id],[TenantId] Desc,[Name] Desc \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only",
            secondPage.CommandText);
        Assert.Equal(prefix + "Select Count(*) \r\nFrom [recent] As [frozen_alias] \r\nWhere [TenantId]=@_p_0",
            count.CommandText);
        Assert.Equal(new object[] { 9, 8, 4, 7, 1, 3 }, Values(firstPage));
        Assert.Equal(new object[] { 9, 2, 2, 7, 1, 3 }, Values(secondPage));
        Assert.Equal(new object[] { 9, 7, 1, 3 }, Values(count));
        Assert.Equal(sourceSql, source.CommandText);
        Assert.Equal(new object[] { 9, 5, 2, 7, 1, 3 }, Values(source));
    }

    private static object[] Values(SqlQueryPlan plan) =>
        Assert.IsType<Bing.Data.Sql.Builders.Params.SqlParam[]>(plan.Parameters).Select(item => item.Value).ToArray();

    [Table("alias_rows")]
    private sealed class AliasRow
    {
        public int Id { get; set; }
    }

    private sealed class TestAliasRegister : IEntityAliasRegister
    {
        private readonly Dictionary<Type, string> _data;

        internal TestAliasRegister(Type entity, string alias) => _data = new Dictionary<Type, string> { [entity] = alias };

        private TestAliasRegister(Dictionary<Type, string> data) => _data = data;

        public Type FromType { get; set; }
        public IReadOnlyDictionary<Type, string> Data => _data;
        public void Register(Type entity, string alias) => _data.Add(entity, alias);
        public void Replace(Type entity, string alias) => _data[entity] = alias;
        public void RegisterAlias(string alias) { }
        public bool Contains(Type entity) => _data.ContainsKey(entity);
        public string GetAlias(Type entity) => _data.TryGetValue(entity, out var alias) ? alias : null;
        public string GetSelfJoinAlias(Type entity, bool right) => GetAlias(entity);
        public IEntityAliasRegister Clone() => new TestAliasRegister(new Dictionary<Type, string>(_data))
        {
            FromType = FromType
        };
    }
}
