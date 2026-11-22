using Bing.Data.Sql;

namespace Bing.Dapper.Tests.SqlQuery;

public sealed partial class SqliteProjectionIntegrationTest
{
    /// <summary>
    /// 验证非法聚合标量目标在映射前失败且合法可空结果保留。
    /// </summary>
    [Fact]
    public async Task Aggregate_WhenScalarTerminalTargetIsInvalid_ShouldRejectBeforeMappingAndPreserveNullableResult()
    {
        using var root = _fixture.CreateQuery();
        var empty = root.From<Order>().Where<Order>(o => o.Id < 0).Select<Order>(o => SqlFn.Sum(o.Quantity));
        const string sql = "Select Sum(`o`.`Quantity`) \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`OrderId`<@_p_0";
        Assert.Equal(sql, empty.ToSql());
        Assert.Throws<InvalidOperationException>(() => empty.ToList<int>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => empty.ToListAsync<int>());
        Assert.Throws<InvalidOperationException>(() => empty.First<int>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => empty.FirstAsync<int>());
        Assert.Null(Assert.Single(empty.ToList<int?>()));
        Assert.Null(Assert.Single(await empty.ToListAsync<int?>()));
        Assert.Null(empty.First<int?>());
        Assert.Null(await empty.FirstAsync<int?>());
        Assert.Equal(sql, empty.ToSql());
        using var countRoot = _fixture.CreateQuery();
        var count = countRoot.From<Order>().Select<Order>(o => SqlFn.Count(o.Id));
        Assert.Equal("Select Count(`o`.`OrderId`) \r\nFrom `sqp_orders` As `o`", count.ToSql());
        Assert.Throws<InvalidOperationException>(() => count.ToList<int>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => count.ToListAsync<string>());
        Assert.Equal(4L, Assert.Single(await count.ToListAsync<long>()));
    }

    /// <summary>
    /// 验证对象 DTO 聚合与整型阈值返回实际数据库值。
    /// </summary>
    /// <param name="syntax">用于选择聚合表达式写法的场景序号。</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Aggregate_WhenObjectDtoAndIntThresholdAreUsed_ShouldReturnDatabaseValues(int syntax)
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>();
        if (syntax == 0) query.Select<Order>(o => new BoxedAggregate { Count = SqlFn.Count(o.Id), Total = SqlFn.Sum(o.Quantity) });
        if (syntax == 1) query.Select<Order>((o, f) => new BoxedAggregate { Count = f.Count(o.Id), Total = f.Sum(o.Quantity) });
        if (syntax == 2) query.Select<Order>((o, f) => new BoxedAggregate { Count = f.Count(x => x.Id), Total = f.Sum(x => x.Quantity) });
        int minimum = 1;
        query.GroupBy<Order>(o => o.ProductNo);
        if (syntax == 0) query.Having<Order>(o => SqlFn.Count(o.Id) > minimum);
        if (syntax == 1) query.Having<Order>((o, f) => f.Count(o.Id) > minimum);
        if (syntax == 2) query.Having<Order>((o, f) => f.Count(x => x.Id) > minimum);
        Assert.Equal("Select Count(`o`.`OrderId`) As `Count`,Sum(`o`.`Quantity`) As `Total` \r\nFrom `sqp_orders` As `o` \r\nGroup By `o`.`ProductNo` Having Count(`o`.`OrderId`)>@_p_0", query.ToSql());
        var row = Assert.Single(await query.ToListAsync<BoxedAggregate>());
        Assert.Equal(2L, Assert.IsType<long>(row.Count));
        Assert.Equal(12L, Assert.IsType<long>(row.Total));
    }

    /// <summary>
    /// 验证转义聚合 DTO 成员按 CLR 名称映射。
    /// </summary>
    [Fact]
    public async Task Aggregate_WhenDtoMembersAreEscaped_ShouldMapUsingClrNames()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>().Select<Order>(o => new EscapedAggregate { @Count = SqlFn.Count(o.Id, "Count"), @event = SqlFn.CountAll("event") });
        Assert.Equal("Select Count(`o`.`OrderId`) As `Count`,Count(*) As `event` \r\nFrom `sqp_orders` As `o`", query.ToSql());
        var row = Assert.Single(await query.ToListAsync<EscapedAggregate>());
        Assert.Equal(4L, row.Count);
        Assert.Equal(4L, row.@event);
    }

    /// <summary>
    /// 验证不支持的聚合引用目标在映射前失败。
    /// </summary>
    [Fact]
    public async Task Aggregate_WhenTargetIsUnsupportedReference_ShouldFailBeforeMapping()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>().Select<Order>(o => new { Count = SqlFn.Count(o.Id) });
        Assert.Equal("Select Count(`o`.`OrderId`) As `Count` \r\nFrom `sqp_orders` As `o`", query.ToSql());
        Assert.Throws<InvalidOperationException>(() => query.ToList<ComparableAggregate>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.ToListAsync<ValueAggregate>());
        Assert.Equal(4L, Assert.Single(query.ToList<BoxedAggregate>()).Count);
    }

    /// <summary>
    /// 验证双精度列聚合保留实际值与空值。
    /// </summary>
    /// <param name="mode">用于选择测试执行路径的模式标记。</param>
    /// <param name="count">预期记录数或待执行的操作数。</param>
    /// <param name="total">测试数据中的可空总额。</param>
    [Theory]
    [InlineData(0, 3L, 7.5d)]
    [InlineData(1, 0L, null)]
    [InlineData(2, 0L, null)]
    public async Task Aggregate_WhenDoubleColumnIsSelected_ShouldPreserveValuesAndNull(int mode, long count, double? total)
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Metric>().Select<Metric>(m => new FloatingAggregate { Count = SqlFn.Count(m.Value), Total = SqlFn.Sum(m.Value) });
        if (mode == 1) query.Where<Metric>(m => m.Id < 0);
        if (mode == 2) query.Where<Metric>(m => m.Id == 4);
        var suffix = mode == 0 ? "" : mode == 1 ? " \r\nWhere `m`.`MetricId`<@_p_0" : " \r\nWhere `m`.`MetricId`=@_p_0";
        Assert.Equal("Select Count(`m`.`Value`) As `Count`,Sum(`m`.`Value`) As `Total` \r\nFrom `sqp_metrics` As `m`" + suffix, query.ToSql());
        var row = Assert.Single(await query.ToListAsync<FloatingAggregate>());
        Assert.Equal(count, row.Count);
        Assert.Equal(total, row.Total);
    }

    /// <summary>
    /// 承载 SQLite 可比较数值聚合结果的计数。
    /// </summary>
    public class ComparableAggregate {
        /// <summary>
        /// 获取或设置聚合计数。
        /// </summary>
        public IComparable Count { get; set; } }
    /// <summary>
    /// 承载 SQLite 值类型聚合结果的计数。
    /// </summary>
    public class ValueAggregate {
        /// <summary>
        /// 获取或设置聚合计数。
        /// </summary>
        public ValueType Count { get; set; } }
    /// <summary>
    /// 承载 SQLite 浮点聚合结果的计数和总值。
    /// </summary>
    public class FloatingAggregate {
        /// <summary>
        /// 获取或设置聚合计数。
        /// </summary>
        public long Count { get; set; }
        /// <summary>
        /// 获取或设置聚合总值。
        /// </summary>
        public double? Total { get; set; } }

    /// <summary>
    /// 承载 SQLite 装箱聚合结果的计数和总值。
    /// </summary>
    public class BoxedAggregate {
        /// <summary>
        /// 获取或设置聚合计数。
        /// </summary>
        public object Count { get; set; }
        /// <summary>
        /// 获取或设置聚合总值。
        /// </summary>
        public object Total { get; set; } }
    /// <summary>
    /// 承载 SQLite 转义标识符聚合结果。
    /// </summary>
    public class EscapedAggregate {
        /// <summary>
        /// 获取或设置以转义标识符读取的聚合计数。
        /// </summary>
        public long @Count { get; set; }
        /// <summary>
        /// 获取或设置以转义标识符读取的事件字段值。
        /// </summary>
        public long @event { get; set; } }
}
