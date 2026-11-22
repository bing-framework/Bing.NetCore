using System.ComponentModel.DataAnnotations;
using Bing.Dapper;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Dapper.MySql;
using Bing.Dapper.PostgreSql;
using Bing.Dapper.SqlServer;
using Bing.Dapper.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 跨 Provider 的结构化投影 SQL 契约测试。
/// </summary>
public sealed class SqlProjectionProviderTest
{
    /// <summary>
    /// 提供各数据库的标识符引用及别名语法用例。
    /// </summary>
    /// <returns>各数据库或请求场景对应的测试数据。</returns>
    public static IEnumerable<object[]> ProviderCases()
    {
        yield return new object[] { "Sqlite", "`", "`", true };
        yield return new object[] { "MySql", "`", "`", true };
        yield return new object[] { "PostgreSql", "\"", "\"", true };
        yield return new object[] { "SqlServer", "[", "]", true };
    }

    /// <summary>
    /// 验证各提供程序按映射展开来源列并保留排除及连接语义。
    /// </summary>
    /// <param name="providerName">目标提供程序名称。</param>
    /// <param name="openQuote">数据库标识符的起始引用符。</param>
    /// <param name="closeQuote">数据库标识符的结束引用符。</param>
    /// <param name="aliasWithAs">是否使用 AS 关键字分隔别名。</param>
    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void SelectAll_WhenJoinedColumnsAreExcluded_ShouldRenderCompleteProviderSql(
        string providerName, string openQuote, string closeQuote, bool aliasWithAs)
    {
        // Arrange
        using var provider = CreateProvider(providerName);
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("projection");

        // Act
        var query = rootQuery.From<ProjectionOrder>("o")
            .LeftJoin<ProjectionOrder, ProjectionProduct>(
                (order, product) => order.ProductId == product.Id, "p")
            .SelectAll()
            .ExcludeSelect<ProjectionOrder>(order => order.InternalNote, "o")
            .ExcludeSelect<ProjectionProduct>(product => product.InternalNote, "p");

        // Assert
        var expected = $"Select {Column("o", "Id")},{Column("o", "Name")},{Column("o", "ProductId")},{Column("o", "Quantity")},{Column("p", "Id")},{Column("p", "Name")} " +
                       $"\r\nFrom {Table("orders", "o")} \r\nLeft Join {Table("products", "p")} On {Column("o", "ProductId")}={Column("p", "Id")}";
        Assert.Equal(expected, query.ToSql());

        string Column(string alias, string name) => $"{Quote(alias)}.{Quote(name)}";
        string Table(string name, string alias) => $"{Quote(name)}{AliasSeparator(aliasWithAs)}{Quote(alias)}";
        string Quote(string name) => $"{openQuote}{name}{closeQuote}";
    }

    /// <summary>
    /// 验证累计聚合投影与分组、排序及查询克隆保持一致。
    /// </summary>
    /// <param name="providerName">目标提供程序名称。</param>
    /// <param name="openQuote">数据库标识符的起始引用符。</param>
    /// <param name="closeQuote">数据库标识符的结束引用符。</param>
    /// <param name="aliasWithAs">是否使用 AS 关键字分隔别名。</param>
    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void Select_WhenAggregatesAreComposed_ShouldRenderStableSqlAndCloneIndependently(
        string providerName, string openQuote, string closeQuote, bool aliasWithAs)
    {
        // Arrange
        using var provider = CreateProvider(providerName);
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("projection");
        var query = rootQuery.From<ProjectionOrder>("o")
            .Select<ProjectionOrder>((order, functions) => new
            {
                Category = order.Name,
                OrderCount = functions.CountAll()
            })
            .Select<ProjectionOrder>((order, functions) => new
            {
                TotalQuantity = functions.Sum(item => item.Quantity)
            })
            .GroupBy<ProjectionOrder>(order => order.Name, "o")
            .Having<ProjectionOrder>((order, functions) => functions.CountAll() > functions.Count(item => item.Id), "o")
            .OrderBy<ProjectionOrder>((order, functions) => functions.Sum(item => item.Quantity), true, "o");

        // Act
        var expected = $"Select {Aliased(Column("o", "Name"), "Category")},{Aliased("Count(*)", "OrderCount")},{Aliased($"Sum({Column("o", "Quantity")})", "TotalQuantity")} " +
                       $"\r\nFrom {Table("orders", "o")} \r\nGroup By {Column("o", "Name")} Having Count(*)>Count({Column("o", "Id")}) \r\nOrder By Sum({Column("o", "Quantity")}) Desc";
        var firstRender = query.ToSql();
        var secondRender = query.ToSql();
        var clone = query.Clone()
            .Select<ProjectionOrder>((order, functions) => new
            {
                MaxQuantity = functions.Max(item => item.Quantity)
            });
        var expectedClone = expected.Replace(" \r\nFrom", $",{Aliased($"Max({Column("o", "Quantity")})", "MaxQuantity")} \r\nFrom", StringComparison.Ordinal);

        // Assert
        Assert.Equal(expected, firstRender);
        Assert.Equal(expected, secondRender);
        Assert.Equal(expectedClone, clone.ToSql());
        Assert.Equal(expected, query.ToSql());

        string Column(string alias, string name) => $"{Quote(alias)}.{Quote(name)}";
        string Table(string name, string alias) => $"{Quote(name)}{AliasSeparator(aliasWithAs)}{Quote(alias)}";
        string Aliased(string expression, string alias) => $"{expression}{(aliasWithAs ? " As " : " ")}{Quote(alias)}";
        string Quote(string name) => $"{openQuote}{name}{closeQuote}";
    }

    /// <summary>
    /// 验证不支持的自动投影操作在渲染前被拒绝且不改变状态。
    /// </summary>
    /// <param name="providerName">目标提供程序名称。</param>
    /// <param name="openQuote">数据库标识符的起始引用符。</param>
    /// <param name="closeQuote">数据库标识符的结束引用符。</param>
    /// <param name="aliasWithAs">是否使用 AS 关键字分隔别名。</param>
    [Theory]
    [MemberData(nameof(ProviderCases))]
    public void SelectAll_WhenExcludedBeforeSelectionOrGrouped_ShouldRejectWithoutPartialSql(
        string providerName, string openQuote, string closeQuote, bool aliasWithAs)
    {
        // Arrange
        using var provider = CreateProvider(providerName);
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("projection");
        var query = rootQuery.From<ProjectionOrder>("o");
        var originalSql = query.ToSql();

        // Act
        var exclusionException = Assert.Throws<InvalidOperationException>(() =>
            query.ExcludeSelect<ProjectionOrder>(order => order.InternalNote, "o"));

        // Assert
        Assert.Equal("ExcludeSelect 需要先调用 SelectAll。", exclusionException.Message);
        Assert.Equal(originalSql, query.ToSql());

        var groupedAutoProjection = query.SelectAll().GroupBy<ProjectionOrder>(order => order.Name, "o");
        var groupingException = Assert.Throws<NotSupportedException>(() => groupedAutoProjection.ToSql());
        Assert.Equal("全来源自动投影不支持分组，请使用显式 Select。", groupingException.Message);
    }

    /// <summary>
    /// 创建指定数据库的投影测试容器。
    /// </summary>
    /// <param name="providerName">目标提供程序名称。</param>
    /// <returns>注册了当前测试依赖的服务容器。</returns>
    private static ServiceProvider CreateProvider(string providerName)
    {
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(ProjectionOrder),
                DbKey = "projection",
                TableName = "orders"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(ProjectionProduct),
                DbKey = "projection",
                TableName = "products"
            });
        });

        switch (providerName)
        {
            case "Sqlite":
                services.AddSqliteProvider();
                services.AddSqlDataSource("projection", DatabaseType.Sqlite, "Data Source=:memory:");
                break;
            case "MySql":
                services.AddMySqlProvider();
                services.AddSqlDataSource("projection", DatabaseType.MySql, "Server=localhost;Database=projection;");
                break;
            case "PostgreSql":
                services.AddPostgreSqlProvider();
                services.AddSqlDataSource("projection", DatabaseType.PgSql, "Host=localhost;Database=projection");
                break;
            case "SqlServer":
                services.AddSqlServerProvider();
                services.AddSqlDataSource("projection", DatabaseType.SqlServer, "Server=localhost;Database=projection;");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(providerName), providerName, "未知 Provider。");
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 选择表别名的分隔语法。
    /// </summary>
    /// <param name="aliasWithAs">是否使用 AS 关键字分隔别名。</param>
    /// <returns>包含 AS 关键字或仅含空格的别名分隔文本。</returns>
    private static string AliasSeparator(bool aliasWithAs) => aliasWithAs ? " As " : " ";

    /// <summary>
    /// 提供Provider 投影映射测试使用的查询数据模型。
    /// </summary>
    private sealed class ProjectionOrder
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置关联商品标识。
        /// </summary>
        public int ProductId { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 获取或设置投影过滤测试使用的内部备注。
        /// </summary>
        public string InternalNote { get; set; }
        /// <summary>
        /// 获取或设置订单数量。
        /// </summary>
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 提供Provider 投影映射测试使用的查询数据模型。
    /// </summary>
    private sealed class ProjectionProduct
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 获取或设置投影过滤测试使用的内部备注。
        /// </summary>
        public string InternalNote { get; set; }
    }
}
