using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Moq;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle Lambda Join 与聚合别名策略测试。
/// </summary>
public sealed class SqlAliasFunctionTest
{
    /// <summary>
    /// 验证别名策略变化时连接与聚合生成完整方言 SQL。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void JoinAndAggregate_WhenAliasPolicyChanges_ShouldRenderCompleteSql(bool automatic)
    {
        var query = CreateQuery(automatic).From<AliasOrder>()
            .Join<AliasOrder, AliasProduct>((o, p) => o.ProductId == p.Id)
            .Select<AliasProduct>(p => new { Category = p.Name })
            .Select<AliasOrder>((o, f) => new
            {
                OrderCount = SqlFn.Count(o.Id),
                TotalQuantity = SqlFn.Sum(o.Quantity)
            })
            .GroupBy<AliasProduct>(p => p.Name);

        var expected = automatic
            ? "Select \"p\".\"Name\" \"Category\",Count(\"o\".\"Id\") \"OrderCount\",Sum(\"o\".\"Quantity\") \"TotalQuantity\" " +
              "\r\nFrom \"orders\" \"o\" \r\nJoin \"products\" \"p\" On \"o\".\"ProductId\"=\"p\".\"Id\" \r\nGroup By \"p\".\"Name\""
            : "Select \"products\".\"Name\" \"Category\",Count(\"orders\".\"Id\") \"OrderCount\",Sum(\"orders\".\"Quantity\") \"TotalQuantity\" " +
              "\r\nFrom \"orders\" \r\nJoin \"products\" On \"orders\".\"ProductId\"=\"products\".\"Id\" \r\nGroup By \"products\".\"Name\"";

        Assert.Equal(expected, query.ToSql());
    }

    /// <summary>
    /// 验证禁用别名生成时自连接拒绝隐式别名且接受显式别名。
    /// </summary>
    [Fact]
    public void SelfJoin_WhenAliasGenerationIsDisabled_ShouldRejectImplicitAndAllowExplicitAliases()
    {
        var implicitQuery = CreateQuery(false).From<AliasOrder>()
            .Select<AliasOrder>(o => new { o.Id });
        const string beforeJoin = "Select \"orders\".\"Id\" \"Id\" \r\nFrom \"orders\"";

        Assert.Throws<InvalidOperationException>(() =>
            implicitQuery.Join<AliasOrder, AliasOrder>((o, manager) => o.ParentId == manager.Id));
        Assert.Equal(beforeJoin, implicitQuery.ToSql());

        var explicitQuery = CreateQuery(false).From<AliasOrder>("o")
            .Select<AliasOrder>(o => new { o.Id })
            .Join<AliasOrder, AliasOrder>((o, manager) => o.ParentId == manager.Id, "manager");
        const string expected = "Select \"o\".\"Id\" \"Id\" \r\nFrom \"orders\" \"o\" " +
                                "\r\nJoin \"orders\" \"manager\" On \"o\".\"ParentId\"=\"manager\".\"Id\"";

        Assert.Equal(expected, explicitQuery.ToSql());
    }

    /// <summary>
    /// 创建指定自动别名模式的 SQL 测试查询。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateQuery(bool automatic) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new OracleBuilder(new SqlBuilderServices(options: new SqlOptions
            {
                AutoGenerateTableAlias = automatic
            })));

    /// <summary>
    /// 表示 Provider 别名解析测试使用的订单记录。
    /// </summary>
    [Table("orders")]
    private sealed class AliasOrder
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置关联商品标识。
        /// </summary>
        public int ProductId { get; set; }
        /// <summary>
        /// 获取或设置父记录标识。
        /// </summary>
        public int ParentId { get; set; }
        /// <summary>
        /// 获取或设置订单数量。
        /// </summary>
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 表示 Provider 订单关联测试使用的商品记录。
    /// </summary>
    [Table("products")]
    private sealed class AliasProduct
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }
}
