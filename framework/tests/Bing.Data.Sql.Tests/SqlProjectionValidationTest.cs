using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// Lambda SQL 聚合投影及自动排除规则测试。
/// </summary>
public class SqlProjectionValidationTest
{
    /// <summary>
    /// 验证可空求和阈值比较将提升后的常量参数化。
    /// </summary>
    [Fact]
    public void Having_WhenNullableSumComparesToValue_ShouldParameterizeLiftedConstant()
    {
        var query = CreateQuery().From<Order>("o")
            .Select<Order>((o, f) => new { Total = f.Sum(x => x.Quantity) })
            .Having<Order>((o, f) => f.Sum(x => x.Quantity) > 2);
        Assert.Equal("Select Sum([o].[Quantity]) As [Total] \r\nFrom [Orders] As [o] \r\nHaving Sum([o].[Quantity])>@_p_0", query.ToSql());
    }

    /// <summary>
    /// 测试目的：匿名对象聚合投影应支持重命名的外层和选择器参数，并按显式来源别名渲染。
    /// </summary>
    [Fact]
    public void Select_WhenAnonymousCountAndSumUseRenamedParameters_ShouldRenderAgainstBoundSource()
    {
        var query = CreateQuery()
            .From<Order>("sales")
            .Select<Order>((source, aggregate) => new
            {
                RowCount = aggregate.Count(item => item.Id),
                TotalQuantity = aggregate.Sum(value => value.Quantity)
            });

        Assert.Equal(
            "Select Count([sales].[OrderId]) As [RowCount],Sum([sales].[Quantity]) As [TotalQuantity] \r\nFrom [Orders] As [sales]",
            query.ToSql());
    }

    /// <summary>
    /// 验证静态聚合标记入口与 SqlFn 使用相同的完整 SQL 渲染语义。
    /// </summary>
    [Fact]
    public void Select_WhenSqlAggregatesMarkerIsUsed_ShouldRenderCompleteAggregateSql()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => new
            {
                RowCount = SqlAggregates.Count(order.Id),
                TotalQuantity = SqlAggregates.Sum(order.Quantity)
            });

        Assert.Equal(
            "Select Count([o].[OrderId]) As [RowCount],Sum([o].[Quantity]) As [TotalQuantity] \r\nFrom [Orders] As [o]",
            query.ToSql());
    }

    /// <summary>
    /// 验证结果原型只选择可写标量 DTO 成员，并沿用实体列映射和结果别名。
    /// </summary>
    [Fact]
    public void Prototype_WhenDtoHasMappedMembers_ShouldRenderMappedProjection()
    {
        var query = CreateQuery().From<Order>("o");

        SqlGeneratedQueryBridge.Prototype(query, typeof(OrderSummary), new OrderSummary());

        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code] \r\nFrom [Orders] As [o]",
            query.ToSql());
    }

    /// <summary>兼容模式传入 null 成员列表时，继续使用 DTO 公开可写属性。</summary>
    [Fact]
    public void Prototype_WhenMembersAreNullInCompatibleMode_ShouldUseReflection()
    {
        var query = CreateQuery().From<Order>("o");

        SqlGeneratedQueryBridge.Prototype(query, typeof(OrderSummary), new OrderSummary(), null);

        Assert.Equal("Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>严格生成模式缺少成员描述时必须拒绝反射回退，并允许修正后继续使用。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prototype_WhenGeneratedOnlyHasNoMembers_ShouldRejectAndAllowGeneratedMembers(bool explicitNull)
    {
        var sourceMetadata = new DataAnnotationsEntityModelMetadataProvider().GetMetadata(typeof(Order));
        var builder = new TestSqlBuilder(entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(sourceMetadata),
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Order>("o");

        var error = Assert.Throws<NotSupportedException>(() =>
        {
            if (explicitNull)
                SqlGeneratedQueryBridge.Prototype(query, typeof(OrderSummary), new OrderSummary(), null);
            else
                SqlGeneratedQueryBridge.Prototype(query, typeof(OrderSummary), new OrderSummary());
        });
        Assert.Contains(typeof(OrderSummary).FullName, error.Message);
        Assert.Equal("Select * \r\nFrom [Orders] As [o]", query.ToSql());

        SqlGeneratedQueryBridge.Prototype(query, typeof(OrderSummary), new OrderSummary(), new[] { "Id", "Code" });
        Assert.Equal("Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>已有显式投影无需枚举 DTO 成员，严格模式仍应保留它。</summary>
    [Fact]
    public void Prototype_WhenGeneratedOnlyHasExplicitProjection_ShouldPreserveItWithoutMembers()
    {
        var sourceMetadata = new DataAnnotationsEntityModelMetadataProvider().GetMetadata(typeof(Order));
        var builder = new TestSqlBuilder(entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(sourceMetadata),
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Order>("o").Select<Order>(order => order.Code);

        SqlGeneratedQueryBridge.Prototype(query, typeof(OrderSummary), new OrderSummary());

        Assert.Equal("Select [o].[OrderCode] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>结果类型与实体相同时，严格模式可以直接使用已登记的实体映射。</summary>
    [Fact]
    public void Prototype_WhenGeneratedOnlyResultIsSourceEntity_ShouldUseRegisteredMapping()
    {
        var sourceMetadata = new DataAnnotationsEntityModelMetadataProvider().GetMetadata(typeof(Order));
        var builder = new TestSqlBuilder(entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(sourceMetadata),
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Order>("o");

        SqlGeneratedQueryBridge.Prototype(query, typeof(Order), new Order());

        Assert.Equal("Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code],[o].[ProductId],[o].[OrderStatus] As [Status],[o].[Quantity] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：未知函数、嵌套属性和数值转换失败后应保留已有投影。
    /// </summary>
    [Fact]
    public void Select_WhenUnsupportedExpressionIsUsed_ShouldRejectAndRollback()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code);
        const string expectedSql = "Select [o].[OrderCode] \r\nFrom [Orders] As [o]";

        Assert.Throws<NotSupportedException>(() => query.Select<Order>(order => order.Code.ToUpper()));
        Assert.Equal(expectedSql, query.ToSql());

        Assert.Throws<NotSupportedException>(() => query.Select<Order>(order => order.Product.Name));
        Assert.Equal(expectedSql, query.ToSql());

        Assert.Throws<NotSupportedException>(() => query.Select<Order>(order => (long)order.Quantity));
        Assert.Equal(expectedSql, query.ToSql());
    }

    /// <summary>
    /// 测试目的：连续聚合投影发生结果别名冲突时应失败并保留原 SQL。
    /// </summary>
    [Fact]
    public void Select_WhenAggregateAliasConflicts_ShouldKeepPreviousProjection()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>((order, functions) => new { Total = functions.CountAll() });
        const string expectedSql = "Select Count(*) As [Total] \r\nFrom [Orders] As [o]";

        Assert.Throws<ArgumentException>(() => query.Select<Order>((order, functions) =>
            new { Total = functions.Sum(value => value.Quantity) }));

        Assert.Equal(expectedSql, query.ToSql());
    }

    /// <summary>
    /// 测试目的：聚合结果 DTO 的属性类型不兼容时应在执行前拒绝。
    /// </summary>
    [Fact]
    public void ToList_WhenAggregateDtoPropertyHasIncompatibleType_ShouldRejectBeforeExecution()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>((order, functions) => new { RowCount = functions.CountAll() });

        Assert.Equal("Select Count(*) As [RowCount] \r\nFrom [Orders] As [o]", query.ToSql());
        Assert.Throws<InvalidOperationException>(() => query.ToList<WrongCountProjection>());
    }

    /// <summary>
    /// 严格生成模式下缺少聚合结果元数据时，应在所有执行路径中给出可定位的错误。
    /// </summary>
    [Fact]
    public void Aggregate_WhenResultMetadataIsMissing_ShouldRejectAllPathsBeforeExecution()
    {
        var sourceMetadata = new DataAnnotationsEntityModelMetadataProvider().GetMetadata(typeof(Order));
        var provider = new RegisteredEntityModelMetadataProvider(sourceMetadata);
        var builder = new TestSqlBuilder(entityModelMetadataProvider: provider,
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly });
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        executor.As<ISqlGeneratedPlanExecutor>();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, builder)
            .From<Order>("o")
            .Select<Order>((order, functions) => new { RowCount = functions.CountAll() });
        const string sql = "Select Count(*) As [RowCount] \r\nFrom [Orders] As [o]";

        Assert.Equal(sql, query.ToSql());
        var error = Assert.Throws<NotSupportedException>(() => query.ToList<MissingAggregateProjection>());
        Assert.Contains(typeof(MissingAggregateProjection).FullName, error.Message);
        Assert.Contains("CreateModels", error.Message);
        Assert.Throws<NotSupportedException>(() => { _ = query.ToListAsync<MissingAggregateProjection>(); });
        Assert.Throws<NotSupportedException>(() => query.First<MissingAggregateProjection>());
        Assert.Throws<NotSupportedException>(() => SqlGeneratedExecution.ToList(query,
            _ => new MissingAggregateProjection(), (_, _) => { }, null));
        Assert.Throws<NotSupportedException>(() => { _ = SqlGeneratedExecution.ToListAsync(query,
            _ => new MissingAggregateProjection(), (_, _) => { }, null, default); });
        Assert.Equal(sql, query.ToSql());
        executor.VerifyNoOtherCalls();
    }

    /// <summary>
    /// 测试目的：聚合投影中的普通列没有加入 GroupBy 时应拒绝渲染，修正投影后可恢复。
    /// </summary>
    [Fact]
    public void ToSql_WhenOrdinaryAggregateColumnIsNotGrouped_ShouldRejectUntilProjectionIsCorrected()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>(order => order.Code)
            .Select<Order>((order, functions) => new { RowCount = functions.CountAll() })
            .GroupBy<Order>(order => order.Status);

        Assert.Throws<InvalidOperationException>(() => query.ToSql());

        query.ReplaceSelect<Order>(order => order.Status);

        Assert.Equal(
            "Select [o].[OrderStatus] \r\nFrom [Orders] As [o] \r\nGroup By [o].[OrderStatus]",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：ReplaceSelect 替换聚合投影后应只渲染新列，不保留旧聚合状态。
    /// </summary>
    [Fact]
    public void ReplaceSelect_WhenAggregateProjectionExists_ShouldRenderOnlyReplacementColumn()
    {
        var query = CreateQuery()
            .From<Order>("o")
            .Select<Order>((order, functions) => new { RowCount = functions.CountAll() })
            .ReplaceSelect<Order>(order => order.Code);

        Assert.Equal("Select [o].[OrderCode] \r\nFrom [Orders] As [o]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：SelectAll 应支持排除单属性、匿名对象属性、数组属性和带别名的字符串列。
    /// </summary>
    [Fact]
    public void ExcludeSelect_WhenSingleAnonymousArrayOrStringColumnsAreUsed_ShouldRenderRemainingColumns()
    {
        var single = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Order>(order => order.Status);
        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code],[o].[ProductId],[o].[Quantity] \r\nFrom [Orders] As [o]",
            single.ToSql());

        var anonymous = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Order>(order => new { order.Code, order.Status });
        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[ProductId],[o].[Quantity] \r\nFrom [Orders] As [o]",
            anonymous.ToSql());

        var array = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Order>(order => new object[] { order.Code, order.Status });
        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[ProductId],[o].[Quantity] \r\nFrom [Orders] As [o]",
            array.ToSql());

        var text = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect("o.OrderCode");
        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[ProductId],[o].[Quantity],[o].[OrderStatus] As [Status] \r\nFrom [Orders] As [o]",
            text.ToSql());
    }

    /// <summary>
    /// 测试目的：相同实体类型存在多个来源时，排除表达式必须显式指定来源别名。
    /// </summary>
    [Fact]
    public void ExcludeSelect_WhenEntitySourceIsAmbiguous_ShouldRequireAliasAndPreserveState()
    {
        var query = CreateQuery()
            .From<Order>("left")
            .Join<Order, Order>((first, second) => first.Id == second.Id, "right")
            .SelectAll();
        const string originalSql =
            "Select [left].[OrderId] As [Id],[left].[OrderCode] As [Code],[left].[ProductId],[left].[Quantity],[left].[OrderStatus] As [Status],[right].[OrderId] As [Id],[right].[OrderCode] As [Code],[right].[ProductId],[right].[Quantity],[right].[OrderStatus] As [Status] \r\nFrom [Orders] As [left] \r\nJoin [Orders] As [right] On [left].[OrderId]=[right].[OrderId]";

        Assert.Throws<InvalidOperationException>(() => query.ExcludeSelect<Order>(order => order.Code));
        Assert.Equal(originalSql, query.ToSql());

        query.ExcludeSelect<Order>(order => order.Code, "right");

        Assert.Equal(
            "Select [left].[OrderId] As [Id],[left].[OrderCode] As [Code],[left].[ProductId],[left].[Quantity],[left].[OrderStatus] As [Status],[right].[OrderId] As [Id],[right].[ProductId],[right].[Quantity],[right].[OrderStatus] As [Status] \r\nFrom [Orders] As [left] \r\nJoin [Orders] As [right] On [left].[OrderId]=[right].[OrderId]",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：排除全部自动投影列时应报告无可读取列。
    /// </summary>
    [Fact]
    public void ExcludeSelect_WhenAllEntityColumnsAreExcluded_ShouldRejectRendering()
    {
        var query = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Order>(order => order.Id)
            .ExcludeSelect<Order>(order => order.Code)
            .ExcludeSelect<Order>(order => order.ProductId)
            .ExcludeSelect<Order>(order => order.Quantity)
            .ExcludeSelect<Order>(order => order.Status);

        Assert.Throws<InvalidOperationException>(() => query.ToSql());
    }

    /// <summary>
    /// 测试目的：SelectAll 记录的待绑定排除列应能在之后追加 Join 时绑定到目标来源。
    /// </summary>
    [Fact]
    public void ExcludeSelect_WhenJoinIsAddedAfterExclusion_ShouldBindDeferredSource()
    {
        var query = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Product>(product => product.Name, "p")
            .Join<Order, Product>((order, product) => order.ProductId == product.Id, "p");

        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code],[o].[ProductId],[o].[Quantity],[o].[OrderStatus] As [Status],[p].[ProductId] As [Id] \r\nFrom [Orders] As [o] \r\nJoin [Products] As [p] On [o].[ProductId]=[p].[ProductId]",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：Clone 后追加排除列不得改变来源查询的自动投影。
    /// </summary>
    [Fact]
    public void Clone_WhenExclusionsChangeOnClone_ShouldKeepSourceProjectionIndependent()
    {
        var source = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Order>(order => order.Status);
        var clone = source.Clone().ExcludeSelect<Order>(order => order.Code);

        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[OrderCode] As [Code],[o].[ProductId],[o].[Quantity] \r\nFrom [Orders] As [o]",
            source.ToSql());
        Assert.Equal(
            "Select [o].[OrderId] As [Id],[o].[ProductId],[o].[Quantity] \r\nFrom [Orders] As [o]",
            clone.ToSql());
    }

    /// <summary>
    /// 测试目的：自动投影排除列时应保留 SelectAll 已设置的 Distinct。
    /// </summary>
    [Fact]
    public void Distinct_WhenSelectAllProjectionExcludesColumn_ShouldRemainEnabled()
    {
        var query = CreateQuery().From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Order>(order => order.Status)
            .Distinct();

        Assert.Equal(
            "Select Distinct [o].[OrderId] As [Id],[o].[OrderCode] As [Code],[o].[ProductId],[o].[Quantity] \r\nFrom [Orders] As [o]",
            query.ToSql());
    }

    /// <summary>
    /// 创建投影校验测试查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateQuery() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder());

    /// <summary>
    /// 提供SQL 投影结构校验测试使用的实体记录。
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

        /// <summary>
        /// 获取或设置未映射的商品导航对象。
        /// </summary>
        [NotMapped]
        public Product Product { get; set; }
    }

    /// <summary>
    /// 提供SQL 投影结构校验测试使用的实体记录。
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
    /// 承载SQL 投影结构校验查询结果的投影数据模型。
    /// </summary>
    private sealed class WrongCountProjection
    {
        /// <summary>
        /// 获取或设置投影返回的记录数。
        /// </summary>
        public int RowCount { get; set; }
    }

    /// <summary>
    /// 用于验证缺失结果元数据时的错误提示。
    /// </summary>
    private sealed class MissingAggregateProjection
    {
        /// <summary>获取或设置聚合记录数。</summary>
        public long RowCount { get; set; }
    }

    /// <summary>
    /// 承载结果原型投影测试的 DTO。
    /// </summary>
    private sealed class OrderSummary
    {
        /// <summary>
        /// 获取或设置订单标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置订单编码。
        /// </summary>
        public string Code { get; set; } = string.Empty;
    }
}
