using System.ComponentModel.DataAnnotations;
using Bing.Dapper;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Dapper.Oracle;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle 结构化投影 SQL 契约测试。
/// </summary>
public sealed class OracleProjectionTest
{
    /// <summary>
    /// 测试目的：Oracle 自动投影应展开 Join 来源并排除指定列，别名使用 Oracle 语法。
    /// </summary>
    [Fact]
    public void SelectAll_WhenJoinedColumnsAreExcluded_ShouldRenderCompleteOracleSql()
    {
        // Arrange
        using var provider = CreateProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("projection");

        // Act
        var query = rootQuery.From<ProjectionOrder>("o")
            .LeftJoin<ProjectionOrder, ProjectionProduct>(
                (order, product) => order.ProductId == product.Id, "p")
            .SelectAll()
            .ExcludeSelect<ProjectionOrder>(order => order.InternalNote, "o")
            .ExcludeSelect<ProjectionProduct>(product => product.InternalNote, "p");

        // Assert
        Assert.Equal("Select \"o\".\"Id\",\"o\".\"Name\",\"o\".\"ProductId\",\"o\".\"Quantity\",\"p\".\"Id\",\"p\".\"Name\" " +
                     "\r\nFrom \"orders\" \"o\" \r\nLeft Join \"products\" \"p\" On \"o\".\"ProductId\"=\"p\".\"Id\"",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：连续聚合投影应与 GroupBy、Having、OrderBy 组合，并在重复渲染及 Clone 后保持隔离。
    /// </summary>
    [Fact]
    public void Select_WhenAggregatesAreComposed_ShouldRenderStableOracleSqlAndCloneIndependently()
    {
        // Arrange
        using var provider = CreateProvider();
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
        const string expected = "Select \"o\".\"Name\" \"Category\",Count(*) \"OrderCount\",Sum(\"o\".\"Quantity\") \"TotalQuantity\" " +
                                "\r\nFrom \"orders\" \"o\" \r\nGroup By \"o\".\"Name\" Having Count(*)>Count(\"o\".\"Id\") \r\nOrder By Sum(\"o\".\"Quantity\") Desc";

        // Act
        var firstRender = query.ToSql();
        var secondRender = query.ToSql();
        var clone = query.Clone()
            .Select<ProjectionOrder>((order, functions) => new
            {
                MaxQuantity = functions.Max(item => item.Quantity)
            });
        const string expectedClone = "Select \"o\".\"Name\" \"Category\",Count(*) \"OrderCount\",Sum(\"o\".\"Quantity\") \"TotalQuantity\",Max(\"o\".\"Quantity\") \"MaxQuantity\" " +
                                     "\r\nFrom \"orders\" \"o\" \r\nGroup By \"o\".\"Name\" Having Count(*)>Count(\"o\".\"Id\") \r\nOrder By Sum(\"o\".\"Quantity\") Desc";

        // Assert
        Assert.Equal(expected, firstRender);
        Assert.Equal(expected, secondRender);
        Assert.Equal(expectedClone, clone.ToSql());
        Assert.Equal(expected, query.ToSql());
    }

    /// <summary>
    /// 测试目的：排除列缺少自动投影前置条件及自动投影分组都应明确拒绝并保持查询状态。
    /// </summary>
    [Fact]
    public void SelectAll_WhenExcludedBeforeSelectionOrGrouped_ShouldRejectWithoutPartialSql()
    {
        // Arrange
        using var provider = CreateProvider();
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
    /// 创建 Oracle 投影映射测试容器。
    /// </summary>
    /// <returns>注册了当前测试依赖的服务容器。</returns>
    private static ServiceProvider CreateProvider()
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
        services.AddOracleProvider();
        services.AddSqlDataSource("projection", DatabaseType.Oracle, "Data Source=localhost;User Id=projection;");
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 提供 Provider 投影测试使用的记录模型。
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
    /// 提供 Provider 投影测试使用的记录模型。
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
