using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Dapper.Tests.Infrastructure;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQLite 结构化投影、聚合和多映射真执行测试。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed partial class SqliteProjectionIntegrationTest : IAsyncLifetime
{
    /// <summary>
    /// 保存 SQLite 投影集成测试数据库夹具。
    /// </summary>
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个 <see cref="SqliteProjectionIntegrationTest" /> 类型的实例。
    /// </summary>
    /// <param name="fixture">共享 SQLite 集成数据库固定装置。</param>
    /// <remarks>
    /// 初始化 SQLite 投影集成测试。
    /// </remarks>
    public SqliteProjectionIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 测试目的：连续 Select 可组合普通列和聚合函数，并按分组执行真实汇总。
    /// </summary>
    [Fact]
    public async Task Select_WhenComposedWithAggregateAndGroupBy_ShouldReturnGroupedSummaries()
    {
        using var root = _fixture.CreateQuery();
        var description = root.From<Order>("o")
            .Select<Order>(order => order.Name)
            .Select<Order>((order, functions) => new
            {
                OrderCount = functions.Count(item => item.Id),
                TotalQuantity = functions.Sum(item => item.Quantity)
            })
            .GroupBy<Order>(order => order.Name);

        Assert.Equal(
            "Select `o`.`Name`,Count(`o`.`OrderId`) As `OrderCount`,Sum(`o`.`Quantity`) As `TotalQuantity` \r\nFrom `sqp_orders` As `o` \r\nGroup By `o`.`Name`",
            description.ToSql());

        var results = await description.ToListAsync<OrderSummary>();

        Assert.Equal(3, results.Count);
        var bulk = Assert.Single(results, item => item.Name == "bulk");
        Assert.Equal(2, bulk.OrderCount);
        Assert.Equal(12, bulk.TotalQuantity);
        var nullQuantity = Assert.Single(results, item => item.Name == "null-quantity");
        Assert.Equal(1, nullQuantity.OrderCount);
        Assert.Null(nullQuantity.TotalQuantity);
        var orphan = Assert.Single(results, item => item.Name == "orphan");
        Assert.Equal(1, orphan.OrderCount);
        Assert.Equal(3, orphan.TotalQuantity);
    }

    /// <summary>
    /// 验证 ToListAsync/ToAsync 直接接收结果原型时，会按 DTO 成员生成投影并完成真实 SQLite 物化。
    /// </summary>
    [Fact]
    public async Task PrototypeTerminals_WhenDtoIsPassedDirectly_ShouldProjectAndMaterializeRows()
    {
        using var root = _fixture.CreateQuery();
        var listQuery = root.From<Order>("o");
        var rows = await listQuery.ToListAsync(new OrderIdentity());

        Assert.Equal(
            "Select `o`.`OrderId` As `Id`,`o`.`Name` As `Name` \r\nFrom `sqp_orders` As `o`",
            listQuery.ToSql());
        Assert.Equal(4, rows.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(item => item.Id));
        Assert.Equal(new[] { "bulk", "bulk", "null-quantity", "orphan" }, rows.Select(item => item.Name));

        using var singleRoot = _fixture.CreateQuery();
        var singleQuery = singleRoot.From<Order>("o")
            .Where<Order>(order => order.Id == 2);
        var single = await singleQuery.ToAsync(new OrderIdentity());

        Assert.Equal(
            "Select `o`.`OrderId` As `Id`,`o`.`Name` As `Name` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`OrderId`=@_p_0",
            singleQuery.ToSql());
        Assert.Equal(2, single.Id);
        Assert.Equal("bulk", single.Name);
    }

    /// <summary>
    /// 测试目的：聚合 Having 条件和聚合排序应共同作用于分组结果。
    /// </summary>
    [Fact]
    public async Task Having_WhenCountIsFilteredAndSumOrdersGroups_ShouldReturnMatchingSummary()
    {
        using var root = _fixture.CreateQuery();
        var description = root.From<Order>("o")
            .Select<Order>(order => order.Name)
            .Select<Order>((order, functions) => new
            {
                OrderCount = functions.Count(item => item.Id),
                TotalQuantity = functions.Sum(item => item.Quantity)
            })
            .GroupBy<Order>(order => order.Name)
            .Having<Order>((order, functions) => functions.Count(item => item.Id) > 1)
            .OrderBy<Order>((order, functions) => functions.Sum(item => item.Quantity), true);

        Assert.Equal(
            "Select `o`.`Name`,Count(`o`.`OrderId`) As `OrderCount`,Sum(`o`.`Quantity`) As `TotalQuantity` \r\nFrom `sqp_orders` As `o` \r\nGroup By `o`.`Name` Having Count(`o`.`OrderId`)>@_p_0 \r\nOrder By Sum(`o`.`Quantity`) Desc",
            description.ToSql());

        var result = await description.ToListAsync<OrderSummary>();

        var summary = Assert.Single(result);
        Assert.Equal("bulk", summary.Name);
        Assert.Equal(2, summary.OrderCount);
        Assert.Equal(12, summary.TotalQuantity);
    }

    /// <summary>
    /// 测试目的：空输入的整体聚合保留 Count 和 Sum 的 SQL 空集语义，分组聚合返回零行。
    /// </summary>
    [Fact]
    public async Task Aggregate_WhenInputIsEmpty_ShouldReturnZeroAndNullWithoutGroups()
    {
        using var root = _fixture.CreateQuery();
        var aggregate = root.From<Order>("o")
            .Where<Order>(order => order.ProductNo == 123456)
            .Select<Order>((order, functions) => new
            {
                OrderCount = functions.CountAll(),
                TotalQuantity = functions.Sum(item => item.Quantity)
            });

        Assert.Equal(
            "Select Count(*) As `OrderCount`,Sum(`o`.`Quantity`) As `TotalQuantity` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`ProductNo`=@_p_0",
            aggregate.ToSql());

        var summary = Assert.Single(await aggregate.ToListAsync<AggregateTotal>());
        Assert.Equal(0, summary.OrderCount);
        Assert.Null(summary.TotalQuantity);

        var grouped = root.From<Order>("o")
            .Where<Order>(order => order.ProductNo == 123456)
            .Select<Order>((order, functions) => new { OrderCount = functions.CountAll() })
            .GroupBy<Order>(order => order.Name);

        Assert.Equal(
            "Select Count(*) As `OrderCount` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`ProductNo`=@_p_0 \r\nGroup By `o`.`Name`",
            grouped.ToSql());
        Assert.Empty(await grouped.ToListAsync<CountSummary>());
    }

    /// <summary>
    /// 测试目的：结构化 Count(Distinct)、Avg、Min、Max 忽略空值，且空集结果符合 SQLite 聚合语义。
    /// </summary>
    [Fact]
    public async Task Aggregate_WhenValuesContainNullsOrNoRows_ShouldReturnSqliteAggregateSemantics()
    {
        using var root = _fixture.CreateQuery();
        var populated = root.From<Metric>("m")
            .Select<Metric>((metric, functions) => new
            {
                DistinctValueCount = functions.Count(item => item.Value, true),
                AverageValue = functions.Avg(item => item.Value),
                MinimumValue = functions.Min(item => item.Value),
                MaximumValue = functions.Max(item => item.Value)
            });

        Assert.Equal(
            "Select Count(Distinct `m`.`Value`) As `DistinctValueCount`,Avg(`m`.`Value`) As `AverageValue`,Min(`m`.`Value`) As `MinimumValue`,Max(`m`.`Value`) As `MaximumValue` \r\nFrom `sqp_metrics` As `m`",
            populated.ToSql());

        var populatedSummary = Assert.Single(await populated.ToListAsync<MetricAggregateSummary>());
        Assert.Equal(2, populatedSummary.DistinctValueCount);
        Assert.Equal(2.5d, populatedSummary.AverageValue);
        Assert.Equal(1.5d, populatedSummary.MinimumValue);
        Assert.Equal(4.5d, populatedSummary.MaximumValue);

        var empty = root.From<Metric>("m")
            .Where<Metric>(metric => metric.Id == 999)
            .Select<Metric>((metric, functions) => new
            {
                DistinctValueCount = functions.Count(item => item.Value, true),
                AverageValue = functions.Avg(item => item.Value),
                MinimumValue = functions.Min(item => item.Value),
                MaximumValue = functions.Max(item => item.Value)
            });

        Assert.Equal(
            "Select Count(Distinct `m`.`Value`) As `DistinctValueCount`,Avg(`m`.`Value`) As `AverageValue`,Min(`m`.`Value`) As `MinimumValue`,Max(`m`.`Value`) As `MaximumValue` \r\nFrom `sqp_metrics` As `m` \r\nWhere `m`.`MetricId`=@_p_0",
            empty.ToSql());

        var emptySummary = Assert.Single(await empty.ToListAsync<MetricAggregateSummary>());
        Assert.Equal(0, emptySummary.DistinctValueCount);
        Assert.Null(emptySummary.AverageValue);
        Assert.Null(emptySummary.MinimumValue);
        Assert.Null(emptySummary.MaximumValue);
    }

    /// <summary>
    /// 测试目的：分组分页的总数应统计分组数量，而不是分组中的底层行数。
    /// </summary>
    [Fact]
    public async Task ToPageAsync_WhenQueryIsGrouped_ShouldCountGroupsAsTotal()
    {
        using var root = _fixture.CreateQuery();
        var description = root.From<Order>("o")
            .Select<Order>(order => order.Name)
            .Select<Order>((order, functions) => new { OrderCount = functions.CountAll() })
            .GroupBy<Order>(order => order.Name);

        Assert.Equal(
            "Select `o`.`Name`,Count(*) As `OrderCount` \r\nFrom `sqp_orders` As `o` \r\nGroup By `o`.`Name`",
            description.ToSql());

        var page = await description.ToPageAsync<OrderSummary>(new Pager(1, 2) { Order = "Name" });

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "bulk", "null-quantity" }, page.Data.Select(item => item.Name));
        Assert.Equal(new long[] { 2, 1 }, page.Data.Select(item => item.OrderCount));
    }

    /// <summary>
    /// 测试目的：三段 Lambda 多映射应按 SplitOn 同步和异步映射订单、产品及匹配订单。
    /// </summary>
    [Fact]
    public async Task MultiMapping_WhenThreeTypesAreMapped_ShouldExecuteSynchronouslyAndAsynchronously()
    {
        using var root = _fixture.CreateQuery();
        var description = root.From<Order>("o")
            .Join<Order, Product>((order, product) => order.ProductNo == product.Id, "p")
            .Join<Product, Order>((product, matchingOrder) => product.Id == matchingOrder.ProductNo, "o2")
            .SelectAll()
            .SplitOn("Id,Id");

        const string expectedSql =
            "Select `o`.`OrderId` As `Id`,`o`.`Name`,`o`.`ProductNo`,`o`.`Quantity`," +
            "`p`.`ProductNo` As `Id`,`p`.`Code`,`p`.`CreateTime`,`p`.`Name`," +
            "`o2`.`OrderId` As `Id`,`o2`.`Name`,`o2`.`ProductNo`,`o2`.`Quantity` \r\n" +
            "From `sqp_orders` As `o` \r\n" +
            "Join `sqp_products` As `p` On `o`.`ProductNo`=`p`.`ProductNo` \r\n" +
            "Join `sqp_orders` As `o2` On `p`.`ProductNo`=`o2`.`ProductNo`";
        Assert.Equal(expectedSql, description.ToSql());

        var expected = new[] { "1:101:1", "1:101:2", "2:101:1", "2:101:2", "3:102:3" };
        var synchronous = description.ToList<Order, Product, Order, string>(MapOrderProductOrder);
        var asynchronous = await description.ToListAsync<Order, Product, Order, string>(MapOrderProductOrder);

        Assert.Equal(expected, synchronous.OrderBy(item => item));
        Assert.Equal(expected, asynchronous.OrderBy(item => item));
    }

    /// <summary>
    /// 测试目的：七段 Lambda 多映射应能同步和异步处理连续自连接的全部结果段。
    /// </summary>
    [Fact]
    public async Task MultiMapping_WhenSevenTypesAreMapped_ShouldExecuteSynchronouslyAndAsynchronously()
    {
        using var root = _fixture.CreateQuery();
        var description = root.From<Order>("o")
            .Join<Order, Order>((o, o2) => o.Id == o2.Id, "o2")
            .Join<Order, Order>((o2, o3) => o2.Id == o3.Id, "o3")
            .Join<Order, Order>((o3, o4) => o3.Id == o4.Id, "o4")
            .Join<Order, Order>((o4, o5) => o4.Id == o5.Id, "o5")
            .Join<Order, Order>((o5, o6) => o5.Id == o6.Id, "o6")
            .Join<Order, Order>((o6, o7) => o6.Id == o7.Id, "o7")
            .SelectAll()
            .SplitOn("Id,Id,Id,Id,Id,Id");

        const string expectedSql =
            "Select `o`.`OrderId` As `Id`,`o`.`Name`,`o`.`ProductNo`,`o`.`Quantity`," +
            "`o2`.`OrderId` As `Id`,`o2`.`Name`,`o2`.`ProductNo`,`o2`.`Quantity`," +
            "`o3`.`OrderId` As `Id`,`o3`.`Name`,`o3`.`ProductNo`,`o3`.`Quantity`," +
            "`o4`.`OrderId` As `Id`,`o4`.`Name`,`o4`.`ProductNo`,`o4`.`Quantity`," +
            "`o5`.`OrderId` As `Id`,`o5`.`Name`,`o5`.`ProductNo`,`o5`.`Quantity`," +
            "`o6`.`OrderId` As `Id`,`o6`.`Name`,`o6`.`ProductNo`,`o6`.`Quantity`," +
            "`o7`.`OrderId` As `Id`,`o7`.`Name`,`o7`.`ProductNo`,`o7`.`Quantity` \r\n" +
            "From `sqp_orders` As `o` \r\n" +
            "Join `sqp_orders` As `o2` On `o`.`OrderId`=`o2`.`OrderId` \r\n" +
            "Join `sqp_orders` As `o3` On `o2`.`OrderId`=`o3`.`OrderId` \r\n" +
            "Join `sqp_orders` As `o4` On `o3`.`OrderId`=`o4`.`OrderId` \r\n" +
            "Join `sqp_orders` As `o5` On `o4`.`OrderId`=`o5`.`OrderId` \r\n" +
            "Join `sqp_orders` As `o6` On `o5`.`OrderId`=`o6`.`OrderId` \r\n" +
            "Join `sqp_orders` As `o7` On `o6`.`OrderId`=`o7`.`OrderId`";
        Assert.Equal(expectedSql, description.ToSql());

        var expected = new[] { "1:1:1:1:1:1:1", "2:2:2:2:2:2:2", "3:3:3:3:3:3:3", "4:4:4:4:4:4:4" };
        var synchronous = description.ToList<Order, Order, Order, Order, Order, Order, Order, string>(MapSevenOrders);
        var asynchronous = await description.ToListAsync<Order, Order, Order, Order, Order, Order, Order, string>(MapSevenOrders);

        Assert.Equal(expected, synchronous.OrderBy(item => item));
        Assert.Equal(expected, asynchronous.OrderBy(item => item));
    }

    /// <summary>
    /// 测试目的：SelectAll 排除 Join 目标列时不受 Join 调用顺序影响，且左连接多映射保留缺失目标。
    /// </summary>
    [Fact]
    public async Task SelectAll_WhenExcludedBeforeOrAfterLeftJoin_ShouldMapBothQueriesEqually()
    {
        using var beforeRoot = _fixture.CreateQuery();
        var beforeJoin = beforeRoot.From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Product>(product => product.Code, "p")
            .ExcludeSelect<Product>(product => product.CreateTime, "p")
            .LeftJoin<Order, Product>((order, product) => order.ProductNo == product.Id, "p")
            .OrderBy<Order>(order => new object[] { order.Id });

        using var afterRoot = _fixture.CreateQuery();
        var afterJoin = afterRoot.From<Order>("o")
            .SelectAll()
            .LeftJoin<Order, Product>((order, product) => order.ProductNo == product.Id, "p")
            .ExcludeSelect<Product>(product => product.Code, "p")
            .ExcludeSelect<Product>(product => product.CreateTime, "p")
            .OrderBy<Order>(order => new object[] { order.Id });

        const string expectedSql =
            "Select `o`.`OrderId` As `Id`,`o`.`Name`,`o`.`ProductNo`,`o`.`Quantity`,`p`.`ProductNo` As `Id`,`p`.`Name` \r\nFrom `sqp_orders` As `o` \r\nLeft Join `sqp_products` As `p` On `o`.`ProductNo`=`p`.`ProductNo` \r\nOrder By `o`.`OrderId`";
        Assert.Equal(expectedSql, beforeJoin.ToSql());
        Assert.Equal(expectedSql, afterJoin.ToSql());

        var beforeResults = await beforeJoin.ToListAsync<Order, Product, JoinedOrder>(MapJoinedOrder);
        var afterResults = await afterJoin.ToListAsync<Order, Product, JoinedOrder>(MapJoinedOrder);

        AssertJoinedRows(beforeResults);
        AssertJoinedRows(afterResults);
        Assert.Equal(beforeResults.Select(JoinedOrderKey), afterResults.Select(JoinedOrderKey));
    }

    /// <summary>
    /// 测试目的：ReplaceSelect 应在真实执行时丢弃先前选择的列。
    /// </summary>
    [Fact]
    public async Task ReplaceSelect_WhenEarlierColumnsExist_ShouldExecuteOnlyReplacementColumn()
    {
        using var root = _fixture.CreateQuery();
        var description = root.From<Order>("o")
            .Select<Order>(order => order.Name)
            .Select<Order>(order => order.Quantity)
            .ReplaceSelect<Order>(order => order.Id);

        Assert.Equal("Select `o`.`OrderId` \r\nFrom `sqp_orders` As `o`", description.ToSql());
        Assert.Equal(new[] { 1, 2, 3, 4 }, await description.ToListAsync<int>());
    }

    /// <summary>
    /// 测试目的：从同一投影克隆出的筛选查询应保留各自的参数值。
    /// </summary>
    [Fact]
    public async Task Clone_WhenFiltersDiffer_ShouldExecuteWithIndependentParameters()
    {
        using var root = _fixture.CreateQuery();
        var template = root.From<Order>("o").Select<Order>(order => order.Id);
        var first = template.Clone().Where<Order>(order => order.ProductNo == 101);
        var second = template.Clone().Where<Order>(order => order.ProductNo == 999);

        const string expectedSql =
            "Select `o`.`OrderId` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`ProductNo`=@_p_0";
        Assert.Equal(expectedSql, first.ToSql());
        Assert.Equal(expectedSql, second.ToSql());

        Assert.Equal(new[] { 1, 2 }, await first.ToListAsync<int>());
        Assert.Equal(new[] { 4 }, await second.ToListAsync<int>());
    }

    /// <summary>
    /// 测试目的：自动投影的重复 Id 不适合单对象映射，分段主键被排除时应报错且未修改的克隆仍可执行。
    /// </summary>
    [Fact]
    public async Task AutoAll_WhenSingleObjectHasDuplicateIdsOrSplitKeyIsExcluded_ShouldAllowCorrectedMapping()
    {
        using var root = _fixture.CreateQuery();
        var baseline = root.From<Order>("o")
            .SelectAll()
            .ExcludeSelect<Product>(product => product.Code, "p")
            .ExcludeSelect<Product>(product => product.CreateTime, "p")
            .LeftJoin<Order, Product>((order, product) => order.ProductNo == product.Id, "p")
            .SplitOn("Id");

        const string expectedSql =
            "Select `o`.`OrderId` As `Id`,`o`.`Name`,`o`.`ProductNo`,`o`.`Quantity`,`p`.`ProductNo` As `Id`,`p`.`Name` \r\nFrom `sqp_orders` As `o` \r\nLeft Join `sqp_products` As `p` On `o`.`ProductNo`=`p`.`ProductNo`";
        Assert.Equal(expectedSql, baseline.ToSql());
        await Assert.ThrowsAsync<InvalidOperationException>(() => baseline.ToListAsync<Order>());

        var mapped = await baseline.ToListAsync<Order, Product, JoinedOrder>(MapJoinedOrder);
        AssertJoinedRows(mapped);

        var invalidSplit = baseline.Clone().ExcludeSelect<Product>(product => product.Id, "p");
        Assert.Equal(
            "Select `o`.`OrderId` As `Id`,`o`.`Name`,`o`.`ProductNo`,`o`.`Quantity`,`p`.`Name` \r\nFrom `sqp_orders` As `o` \r\nLeft Join `sqp_products` As `p` On `o`.`ProductNo`=`p`.`ProductNo`",
            invalidSplit.ToSql());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invalidSplit.ToListAsync<Order, Product, JoinedOrder>(MapJoinedOrder));

        var corrected = baseline.Clone().SplitOn("Id");
        AssertJoinedRows(await corrected.ToListAsync<Order, Product, JoinedOrder>(MapJoinedOrder));
    }

    /// <summary>
    /// 测试目的：执行前已取消的令牌应沿异步投影终结器传递至 SQLite 执行路径。
    /// </summary>
    [Fact]
    public async Task ToListAsync_WhenTokenIsCancelledBeforeExecution_ShouldThrowCancellation()
    {
        using var root = _fixture.CreateQuery();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var description = root.From<Order>("o").Select<Order>(order => order.Id);

        Assert.Equal("Select `o`.`OrderId` \r\nFrom `sqp_orders` As `o`", description.ToSql());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            description.ToListAsync<int>(cancellationToken: cancellation.Token));
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        await using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
Create Table If Not Exists sqp_orders(
    OrderId Integer Not Null Primary Key,
    ProductNo Integer Not Null,
    Name Text Not Null,
    Quantity Integer Null
);
Create Table If Not Exists sqp_products(
    ProductNo Integer Not Null Primary Key,
    Code Text Not Null,
    Name Text Not Null,
    CreateTime Text Null
);
Create Table If Not Exists sqp_metrics(
    MetricId Integer Not Null Primary Key,
    Value Real Null
);
Delete From sqp_orders;
Delete From sqp_products;
Delete From sqp_metrics;
Insert Into sqp_products(ProductNo, Code, Name, CreateTime) Values
    (101, 'C101', 'Widget 101', '2026-10-29T08:00:00'),
    (102, 'C102', 'Widget 102', '2026-10-29T09:00:00');
Insert Into sqp_orders(OrderId, ProductNo, Name, Quantity) Values
    (1, 101, 'bulk', 5),
    (2, 101, 'bulk', 7),
    (3, 102, 'null-quantity', NULL),
    (4, 999, 'orphan', 3);";
        command.CommandText += @"
Insert Into sqp_metrics(MetricId, Value) Values
    (1, 1.5),
    (2, 1.5),
    (3, 4.5),
    (4, NULL);";
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 将订单及可选商品映射为连接结果。
    /// </summary>
    /// <param name="order">订单映射来源对象。</param>
    /// <param name="product">商品映射来源对象。</param>
    /// <returns>包含订单及可选商品字段的连接结果。</returns>
    private static JoinedOrder MapJoinedOrder(Order order, Product product) => new()
    {
        OrderId = order.Id,
        Quantity = order.Quantity,
        ProductId = product?.Id,
        ProductName = product?.Name,
        ProductCode = product?.Code,
        ProductCreateTime = product?.CreateTime
    };

    /// <summary>
    /// 组合订单、商品及匹配订单的标识。
    /// </summary>
    /// <param name="order">订单映射来源对象。</param>
    /// <param name="product">商品映射来源对象。</param>
    /// <param name="matchingOrder">第三个来源的匹配订单。</param>
    /// <returns>三个来源标识按顺序组成的文本。</returns>
    private static string MapOrderProductOrder(Order order, Product product, Order matchingOrder) =>
        $"{order.Id}:{product.Id}:{matchingOrder.Id}";

    /// <summary>
    /// 组合七个订单的标识。
    /// </summary>
    /// <param name="first">第一个映射或批次来源对象。</param>
    /// <param name="second">第二个映射或批次来源对象。</param>
    /// <param name="third">第三个映射来源的订单。</param>
    /// <param name="fourth">第四个映射来源的订单。</param>
    /// <param name="fifth">第五个映射来源的订单。</param>
    /// <param name="sixth">第六个映射来源的订单。</param>
    /// <param name="seventh">第七个映射来源的订单。</param>
    /// <returns>七个订单标识按顺序组成的文本。</returns>
    private static string MapSevenOrders(Order first, Order second, Order third, Order fourth, Order fifth,
        Order sixth, Order seventh) =>
        $"{first.Id}:{second.Id}:{third.Id}:{fourth.Id}:{fifth.Id}:{sixth.Id}:{seventh.Id}";

    /// <summary>
    /// 验证订单商品连接结果的内容。
    /// </summary>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    private static void AssertJoinedRows(IReadOnlyCollection<JoinedOrder> rows)
    {
        Assert.Equal(4, rows.Count);
        var first = Assert.Single(rows, item => item.OrderId == 1);
        Assert.Equal(101, first.ProductId);
        Assert.Equal("Widget 101", first.ProductName);
        Assert.Equal(string.Empty, first.ProductCode);
        Assert.Null(first.ProductCreateTime);

        var second = Assert.Single(rows, item => item.OrderId == 2);
        Assert.Equal(101, second.ProductId);
        Assert.Equal("Widget 101", second.ProductName);
        Assert.Equal(string.Empty, second.ProductCode);
        Assert.Null(second.ProductCreateTime);

        var third = Assert.Single(rows, item => item.OrderId == 3);
        Assert.Equal(102, third.ProductId);
        Assert.Equal("Widget 102", third.ProductName);
        Assert.Equal(string.Empty, third.ProductCode);
        Assert.Null(third.ProductCreateTime);

        var unmatched = Assert.Single(rows, item => item.OrderId == 4);
        Assert.Null(unmatched.ProductId);
        Assert.Null(unmatched.ProductName);
        Assert.Null(unmatched.ProductCode);
        Assert.Null(unmatched.ProductCreateTime);
    }

    /// <summary>
    /// 组合连接结果的可比较文本键。
    /// </summary>
    /// <param name="item">需要生成比较键的连接结果。</param>
    /// <returns>连接结果字段组成的稳定比较文本。</returns>
    private static string JoinedOrderKey(JoinedOrder item) =>
        $"{item.OrderId}:{item.ProductId}:{item.ProductName}:{item.ProductCode}:{item.ProductCreateTime:O}";

    /// <summary>
    /// 订单实体映射。
    /// </summary>
    [Table("sqp_orders")]
    public sealed class Order
    {
        /// <summary>
        /// 获取或设置订单记录标识。
        /// </summary>
        [Key]
        [Column("OrderId")]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置商品编号。
        /// </summary>
        [Column("ProductNo")]
        public int ProductNo { get; set; }

        /// <summary>
        /// 获取或设置订单名称。
        /// </summary>
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置订单商品数量。
        /// </summary>
        [Column("Quantity")]
        public int? Quantity { get; set; }

        /// <summary>
        /// 获取或设置未映射的商品导航对象。
        /// </summary>
        [NotMapped]
        public Product Product { get; set; }
    }

    /// <summary>
    /// 产品实体映射。
    /// </summary>
    [Table("sqp_products")]
    public sealed class Product
    {
        /// <summary>
        /// 获取或设置商品记录标识。
        /// </summary>
        [Key]
        [Column("ProductNo")]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置商品编码。
        /// </summary>
        [Column("Code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置商品名称。
        /// </summary>
        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置商品创建时间。
        /// </summary>
        [Column("CreateTime")]
        public DateTime? CreateTime { get; set; }
    }

    /// <summary>
    /// 聚合测试度量实体。
    /// </summary>
    [Table("sqp_metrics")]
    public sealed class Metric
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        [Column("MetricId")]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置参与查询或聚合的测试值。
        /// </summary>
        [Column("Value")]
        public double? Value { get; set; }
    }

    /// <summary>
    /// 聚合空值语义验证结果。
    /// </summary>
    public sealed class MetricAggregateSummary
    {
        /// <summary>
        /// 获取或设置去重后的数值数量。
        /// </summary>
        public long DistinctValueCount { get; set; }

        /// <summary>
        /// 获取或设置数值平均值。
        /// </summary>
        public double? AverageValue { get; set; }

        /// <summary>
        /// 获取或设置数值最小值。
        /// </summary>
        public double? MinimumValue { get; set; }

        /// <summary>
        /// 获取或设置数值最大值。
        /// </summary>
        public double? MaximumValue { get; set; }
    }

    /// <summary>
    /// 订单分组聚合结果。
    /// </summary>
    public sealed class OrderSummary
    {
        /// <summary>
        /// 获取或设置分组对应的商品名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置分组内订单数量。
        /// </summary>
        public long OrderCount { get; set; }

        /// <summary>
        /// 获取或设置分组内订单数量合计。
        /// </summary>
        public int? TotalQuantity { get; set; }
    }

    /// <summary>
    /// 直接结果原型终结测试的订单 DTO。
    /// </summary>
    public sealed class OrderIdentity
    {
        /// <summary>
        /// 获取或设置订单记录标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置订单名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 整体聚合结果。
    /// </summary>
    public sealed class AggregateTotal
    {
        /// <summary>
        /// 获取或设置汇总范围内的订单数量。
        /// </summary>
        public long OrderCount { get; set; }

        /// <summary>
        /// 获取或设置订单总数量。
        /// </summary>
        public int? TotalQuantity { get; set; }
    }

    /// <summary>
    /// 分组计数结果。
    /// </summary>
    public sealed class CountSummary
    {
        /// <summary>
        /// 获取或设置汇总范围内的订单数量。
        /// </summary>
        public long OrderCount { get; set; }
    }

    /// <summary>
    /// 左连接多映射结果。
    /// </summary>
    public sealed class JoinedOrder
    {
        /// <summary>
        /// 获取或设置关联订单标识。
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// 获取或设置订单数量。
        /// </summary>
        public int? Quantity { get; set; }

        /// <summary>
        /// 获取或设置关联商品标识。
        /// </summary>
        public int? ProductId { get; set; }

        /// <summary>
        /// 获取或设置关联商品名称。
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// 获取或设置关联商品编码。
        /// </summary>
        public string ProductCode { get; set; }

        /// <summary>
        /// 获取或设置关联商品创建时间。
        /// </summary>
        public DateTime? ProductCreateTime { get; set; }
    }
}
