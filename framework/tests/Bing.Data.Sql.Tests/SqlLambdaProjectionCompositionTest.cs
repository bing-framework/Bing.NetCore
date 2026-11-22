using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// Lambda 查询投影组合与排除规则测试。
/// </summary>
public class SqlLambdaProjectionCompositionTest
{
    /// <summary>
    /// 测试目的：From 的默认实体投影在首次显式 Select 时应被接管。
    /// </summary>
    [Fact]
    public void From_WhenFirstSelectIsApplied_ShouldReplaceDefaultProjection()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code);

        Assert.Equal("Select [o].[OrderCode] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：连续选择不同属性时应按调用顺序累计投影列。
    /// </summary>
    [Fact]
    public void Select_WhenDifferentMembersAreSelectedRepeatedly_ShouldAccumulateColumns()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code)
            .Select<Order>(order => order.Quantity);

        Assert.Equal("Select [o].[OrderCode],[o].[Quantity] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：重复选择同一属性时应幂等去重。
    /// </summary>
    [Fact]
    public void Select_WhenSameMemberIsSelectedRepeatedly_ShouldRenderOneColumn()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code)
            .Select<Order>(order => order.Code);

        Assert.Equal("Select [o].[OrderCode] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：ReplaceSelect 应原子替换已有显式投影。
    /// </summary>
    [Fact]
    public void ReplaceSelect_WhenProjectionExists_ShouldKeepOnlyReplacementColumns()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code)
            .Select<Order>(order => order.Quantity)
            .ReplaceSelect<Order>(order => order.Status);

        Assert.Equal("Select [o].[OrderStatus] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：Clone 后修改投影不得改变原查询的投影状态。
    /// </summary>
    [Fact]
    public void Clone_WhenProjectionChangesOnClone_ShouldKeepSourceAndCloneIndependent()
    {
        var source = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code);
        var clone = source.Clone()
            .Select<Order>(order => order.Quantity);

        Assert.Equal("Select [o].[OrderCode] \r\nFrom [Orders] As [o]", source.ToSql());
        Assert.Equal("Select [o].[OrderCode],[o].[Quantity] \r\nFrom [Orders] As [o]", clone.ToSql());
    }

    /// <summary>
    /// 测试目的：显式 From 别名应优先于不同名称的 Lambda 参数。
    /// </summary>
    [Fact]
    public void Select_WhenSourceHasExplicitAlias_ShouldPreserveThatAlias()
    {
        var query = CreateQuery()
            .From<Order>("sales")
            .Select<Order>(order => order.Code);

        Assert.Equal("Select [sales].[OrderCode] \r\nFrom [Orders] As [sales]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：未显式命名的 From 来源应采用首次绑定的 Lambda 参数名。
    /// </summary>
    [Fact]
    public void Select_WhenSourceHasNoExplicitAlias_ShouldInferAliasFromParameterName()
    {
        var query = CreateQuery()
            .From<Order>()
            .Select<Order>(order => order.Code);

        Assert.Equal("Select [order].[OrderCode] \r\nFrom [Orders] As [order]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：自连接后应能通过 manager 参数名选择对应的 Employee 来源。
    /// </summary>
    [Fact]
    public void Select_WhenEmployeeSourcesAreSelfJoined_ShouldBindManagerByParameterName()
    {
        var query = CreateQuery()
            .From<Employee>()
            .Join<Employee, Employee>((employee, manager) => employee.ManagerId == manager.Id)
            .Select<Employee>(manager => manager.Name);

        Assert.Equal(
            "Select [manager].[EmployeeName] \r\nFrom [Employees] As [employee] \r\nJoin [Employees] As [manager] On [employee].[ManagerId]=[manager].[EmployeeId]",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：非法属性表达式失败后应保留原投影，查询仍可完整渲染。
    /// </summary>
    [Fact]
    public void Select_WhenProjectionIsInvalid_ShouldKeepPreviousSqlUnchanged()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code);

        Assert.Throws<NotSupportedException>(() => query.Select<Order>(order => order.Code.ToLower()));

        Assert.Equal("Select [o].[OrderCode] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：Join 别名冲突失败后应保留查询结构和完整 SQL。
    /// </summary>
    [Fact]
    public void Join_WhenInferredAliasConflictsWithExistingAlias_ShouldKeepSqlUnchanged()
    {
        var query = CreateQuery()
            .From<Order>("product")
            .Select<Order>(order => order.Code);

        Assert.Throws<InvalidOperationException>(() =>
            query.Join<Order, Product>((order, product) => order.ProductId == product.Id));

        Assert.Equal("Select [product].[OrderCode] \r\nFrom [Orders] As [product]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：SelectAll 后可通过实体表达式排除映射属性。
    /// </summary>
    [Fact]
    public void ExcludeSelect_WhenExpressionIsUsedAfterSelectAll_ShouldOmitMappedColumn()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Order>(order => order.Status);

        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code],[o].[ProductId],[o].[Quantity] \r\nFrom [Orders] As [o]",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：SelectAll 后可通过 alias.物理列名字符串排除映射属性。
    /// </summary>
    [Fact]
    public void ExcludeSelect_WhenAliasQualifiedColumnIsUsedAfterSelectAll_ShouldOmitMappedColumn()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .SelectAll()
            .ExcludeSelect("o.OrderCode");

        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[ProductId],[o].[Quantity],[o].[OrderStatus] As [Status] \r\nFrom [Orders] As [o]",
            query.ToSql());
    }

    /// <summary>
    /// 创建投影组合测试查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateQuery() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder());

    /// <summary>
    /// 提供Lambda 投影组合测试使用的实体记录。
    /// </summary>
    [Table("Orders")]
    private sealed class Order
    {
        /// <summary>
        /// 获取或设置订单记录标识。
        /// </summary>
        [Key]
        [Column("OrderId")]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置订单编码。
        /// </summary>
        [Column("OrderCode")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置订单关联的商品标识。
        /// </summary>
        [Column("ProductId")]
        public int ProductId { get; set; }

        /// <summary>
        /// 获取或设置订单状态值。
        /// </summary>
        [Column("OrderStatus")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置订单商品数量。
        /// </summary>
        [Column("Quantity")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// 提供Lambda 投影组合测试使用的实体记录。
    /// </summary>
    [Table("Products")]
    private sealed class Product
    {
        /// <summary>
        /// 获取或设置商品记录标识。
        /// </summary>
        [Key]
        [Column("ProductId")]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置商品名称。
        /// </summary>
        [Column("ProductName")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 提供Lambda 投影组合测试使用的实体记录。
    /// </summary>
    [Table("Employees")]
    private sealed class Employee
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        [Column("EmployeeId")]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置直属经理的员工标识。
        /// </summary>
        [Column("ManagerId")]
        public int ManagerId { get; set; }

        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        [Column("EmployeeName")]
        public string Name { get; set; } = string.Empty;
    }
}
