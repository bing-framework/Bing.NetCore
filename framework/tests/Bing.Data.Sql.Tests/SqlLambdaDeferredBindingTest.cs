using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 验证晚绑定别名与克隆后来源一致性的测试。
/// </summary>
/// <remarks>
/// 在晚绑定别名及克隆后，默认投影和条件必须引用同一来源。
/// </remarks>
public class SqlLambdaDeferredBindingTest
{
    /// <summary>
    /// 创建预设数据源及投影的延迟绑定测试查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery Create()
    {
        var builder = new TestSqlBuilder();
        builder.From<Item>();
        builder.Select<Item>();
        return SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
    }

    /// <summary>
    /// 验证延迟推断别名时同时重绑定默认投影和条件。
    /// </summary>
    [Fact]
    public void Where_WhenDefaultProjectionAndAliasInferred_ShouldRebindBoth()
    {
        var query = Create().Where<Item>(item => item.Id == 1);
        Assert.Equal("Select [item].[Id],[item].[ParentId] \r\nFrom [items] As [item] \r\nWhere [item].[Id]=@_p_0", query.ToSql());
        Assert.Equal(query.ToSql(), query.Clone().ToSql());
    }

    /// <summary>
    /// 验证首个参数为弃元时使用后续有效别名且参数值不变。
    /// </summary>
    [Fact]
    public void Where_WhenFirstParameterIsDiscard_ShouldFollowLaterValidNameWithoutChangingParameters()
    {
        var query = Create().Where<Item>(_ => _.Id == 1).Select<Item>(row => row.Id);
        Assert.Equal("Select [row].[Id] \r\nFrom [items] As [row] \r\nWhere [row].[Id]=@_p_0", query.ToSql());
        Assert.Equal(query.ToSql(), query.Clone().ToSql());
    }

    /// <summary>
    /// 验证值条件与集合条件使用最终推断的来源别名。
    /// </summary>
    [Fact]
    public void WhereValueAndIn_WhenFirstParameterIsDiscard_ShouldRenderUsingFinalAlias()
    {
        var query = Create().Where<Item, int>(_ => _.Id, 1).Where<Item, object>(_ => _.ParentId, new[] { 2, 3 }, Operator.In)
            .Select<Item>(row => row.Id);
        const string expected = "Select [row].[Id] \r\nFrom [items] As [row] \r\nWhere [row].[Id]=@_p_0 And [row].[ParentId] In (@_p_1,@_p_2)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>
    /// 验证条件分组两侧跟随最终推断的来源别名。
    /// </summary>
    [Fact]
    public void Group_WhenAliasInferredAfterEarlierPredicate_ShouldKeepBothBranchesBound()
    {
        var query = Create().WhereGroup(group =>
        {
            group.And<Item>(_ => _.Id == 1);
            group.Or<Item>(row => row.ParentId == 2);
        }).Select<Item>(renamed => renamed.Id);
        Assert.Equal("Select [row].[Id] \r\nFrom [items] As [row] \r\nWhere ([row].[Id]=@_p_0 Or [row].[ParentId]=@_p_1)", query.ToSql());
        Assert.Equal(query.ToSql(), query.Clone().ToSql());
    }

    /// <summary>
    /// 验证聚合筛选及其克隆跟随后续推断的别名。
    /// </summary>
    [Fact]
    public void Having_WhenAliasIsInferredLater_ShouldFollowSourceAndClone()
    {
        var query = Create().Having<Item>(_ => _.Id > 1).GroupBy<Item>(row => row.Id).Select<Item>(renamed => renamed.Id);
        const string expected = "Select [row].[Id] \r\nFrom [items] As [row] \r\nGroup By [row].[Id] Having [row].[Id]>@_p_0";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>
    /// 提供Lambda 延迟绑定测试使用的实体记录。
    /// </summary>
    [Table("items")]
    public class Item {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置父记录标识。
        /// </summary>
        public int ParentId { get; set; } }
}
