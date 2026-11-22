using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 聚合标量列表及单行终结器的类型与可空性验证测试。
/// </summary>
public sealed class SqlAggregateTerminalValidationTest
{
    /// <summary>
    /// 验证计数查询的标量终结方法在执行前拒绝不兼容类型。
    /// </summary>
    [Fact]
    public void Count_WhenScalarTerminalsUseIncompatibleTypes_ShouldRejectBeforeExecution()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        var query = CreateCountQuery(executor.Object);

        Assert.Equal("Select Count(*) \r\nFrom [AggregateRows] As [r]", query.ToSql());
        AssertRejected(() => query.ToList<int>(), "不兼容");
        AssertRejected(() => query.ToEntity<int>(), "不兼容");
        AssertRejected(() => query.First<int>(), "不兼容");
        AssertRejected(() => query.FirstOrDefault<string>(), "不兼容");
        AssertRejected(() => query.Single<string>(), "不兼容");
        AssertRejected(() => query.ToPage<int>(), "不兼容");
        AssertRejected(() => query.AsEnumerable<string>(), "不兼容");

        AssertRejected(() => { _ = query.ToListAsync<int>(); }, "不兼容");
        AssertRejected(() => { _ = query.ToEntityAsync<int>(); }, "不兼容");
        AssertRejected(() => { _ = query.FirstAsync<string>(); }, "不兼容");
        AssertRejected(() => { _ = query.FirstOrDefaultAsync<int>(); }, "不兼容");
        AssertRejected(() => { _ = query.SingleAsync<string>(); }, "不兼容");
        AssertRejected(() => { _ = query.ToPageAsync<int>(); }, "不兼容");
        AssertRejected(() => { _ = query.AsAsyncEnumerable<string>(); }, "不兼容");

        executor.VerifyNoOtherCalls();
    }

    /// <summary>
    /// 验证求和查询的标量终结方法在执行前拒绝不可空类型。
    /// </summary>
    [Fact]
    public void Sum_WhenScalarTerminalsUseNonNullableTypes_ShouldRejectBeforeExecution()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        var query = CreateSumQuery(executor.Object);

        Assert.Equal("Select Sum([r].[IntValue]) \r\nFrom [AggregateRows] As [r]", query.ToSql());
        AssertRejected(() => query.ToList<int>(), "必须可空");
        AssertRejected(() => query.First<int>(), "必须可空");
        AssertRejected(() => { _ = query.ToListAsync<int>(); }, "必须可空");
        AssertRejected(() => { _ = query.FirstAsync<int>(); }, "必须可空");

        executor.VerifyNoOtherCalls();
    }

    /// <summary>
    /// 验证计数和求和查询以匹配的列表类型保留结果。
    /// </summary>
    [Fact]
    public async Task CountAndSum_WhenScalarListTargetsMatch_ShouldPreserveResults()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        executor.Setup(item => item.ToList<long>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns(new List<long> { 7L });
        executor.Setup(item => item.ToListAsync<long>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<long> { 8L });
        executor.Setup(item => item.ToList<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns(new List<int?> { null });
        executor.Setup(item => item.ToListAsync<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int?> { null });

        var count = CreateCountQuery(executor.Object);
        Assert.Equal("Select Count(*) \r\nFrom [AggregateRows] As [r]", count.ToSql());
        Assert.Equal(new long[] { 7L }, count.ToList<long>());
        Assert.Equal(new long[] { 8L }, await count.ToListAsync<long>());

        var sum = CreateSumQuery(executor.Object);
        Assert.Equal("Select Sum([r].[IntValue]) \r\nFrom [AggregateRows] As [r]", sum.ToSql());
        Assert.Equal(new int?[] { null }, sum.ToList<int?>());
        Assert.Equal(new int?[] { null }, await sum.ToListAsync<int?>());

        executor.Verify(item => item.ToList<long>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()), Times.Once);
        executor.Verify(item => item.ToListAsync<long>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        executor.Verify(item => item.ToList<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()), Times.Once);
        executor.Verify(item => item.ToListAsync<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证手写 SQL 保留既有宽松标量映射行为。
    /// </summary>
    [Fact]
    public void ManualSql_WhenScalarTargetUsesLooseMapping_ShouldKeepExistingBehavior()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        executor.Setup(item => item.ToList<string>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns(new List<string> { "3" });
        var query = SqlQueryRuntimeFactory.CreateQuery(executor.Object, new TestSqlBuilder())
            .AppendSelect("Count(*)")
            .From("AggregateRows");

        Assert.Equal("Select Count(*) \r\nFrom [AggregateRows]", query.ToSql());
        Assert.Equal(new[] { "3" }, query.ToList<string>());

        executor.Verify(item => item.ToList<string>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()), Times.Once);
    }

    /// <summary>
    /// 创建计数聚合投影查询。
    /// </summary>
    /// <param name="executor">执行测试查询或操作的执行器。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateCountQuery(ISqlQueryPlanExecutor executor) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, new TestSqlBuilder())
            .From<AggregateRow>("r")
            .Select<AggregateRow>(row => SqlFn.CountAll());

    /// <summary>
    /// 创建数值求和投影查询。
    /// </summary>
    /// <param name="executor">执行测试查询或操作的执行器。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateSumQuery(ISqlQueryPlanExecutor executor) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, new TestSqlBuilder())
            .From<AggregateRow>("r")
            .Select<AggregateRow>(row => SqlFn.Sum(row.IntValue));

    /// <summary>
    /// 验证操作被拒绝并包含预期错误信息。
    /// </summary>
    /// <param name="action">在测试上下文或事务内执行的操作。</param>
    /// <param name="messageFragment">预期异常消息包含的文本。</param>
    private static void AssertRejected(Action action, string messageFragment)
    {
        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains(messageFragment, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 提供聚合终端操作测试使用的实体记录。
    /// </summary>
    [Table("AggregateRows")]
    private sealed class AggregateRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置聚合函数使用的整数值。
        /// </summary>
        public int? IntValue { get; set; }
    }
}
