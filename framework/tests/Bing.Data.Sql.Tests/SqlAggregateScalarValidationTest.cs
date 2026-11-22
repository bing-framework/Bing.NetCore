using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 聚合标量结果的类型与可空性验证测试。
/// </summary>
public sealed class SqlAggregateScalarValidationTest
{
    /// <summary>
    /// 验证可空聚合不能映射到不可空标量且不会调用执行器。
    /// </summary>
    [Fact]
    public void Scalar_WhenNullableAggregateTargetsNonNullableType_ShouldRejectBeforeExecution()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        var query = CreateSumQuery(executor.Object);

        var exception = Assert.Throws<InvalidOperationException>(() => query.Scalar<int>());

        Assert.Contains("必须可空", exception.Message, StringComparison.Ordinal);
        executor.Verify(item => item.Scalar<int>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()), Times.Never);
    }

    /// <summary>
    /// 验证可空聚合不能异步映射到不可空标量且不会调用执行器。
    /// </summary>
    [Fact]
    public void ScalarAsync_WhenNullableAggregateTargetsNonNullableType_ShouldRejectBeforeExecution()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        var query = CreateSumQuery(executor.Object);

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = query.ScalarAsync<int>();
        });

        Assert.Contains("必须可空", exception.Message, StringComparison.Ordinal);
        executor.Verify(item => item.ScalarAsync<int>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 验证匹配的可空聚合标量结果保留空值。
    /// </summary>
    [Fact]
    public void Scalar_WhenNullableAggregateTypeMatches_ShouldKeepNull()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        executor.Setup(item => item.ScalarAsync<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        executor.Setup(item => item.Scalar<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((int?)null);
        var query = CreateSumQuery(executor.Object);

        Assert.Null(query.Scalar<int?>());
        Assert.Null(query.ScalarAsync<int?>().GetAwaiter().GetResult());

        executor.Verify(item => item.Scalar<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()), Times.Once);
        executor.Verify(item => item.ScalarAsync<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证需要窄化转换的聚合标量在执行前被拒绝。
    /// </summary>
    [Fact]
    public void Scalar_WhenAggregateTypeWouldRequireNarrowing_ShouldRejectBeforeExecution()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        var query = CreateLongSumQuery(executor.Object);

        Assert.Throws<InvalidOperationException>(() => query.Scalar<int?>());

        executor.Verify(item => item.Scalar<int?>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()), Times.Never);
    }

    /// <summary>
    /// 验证全行计数可以读取为不可空长整型。
    /// </summary>
    [Fact]
    public void Scalar_WhenCountAllIsSelected_ShouldAllowNonNullableLong()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        executor.Setup(item => item.Scalar<long>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>())).Returns(7L);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<AggregateRow>("r")
            .Select<AggregateRow>((row, functions) => functions.CountAll());

        Assert.Equal(7L, query.Scalar<long>());

        executor.Verify(item => item.Scalar<long>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()), Times.Once);
    }

    /// <summary>
    /// 创建数值求和投影查询。
    /// </summary>
    /// <param name="executor">执行测试查询或操作的执行器。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateSumQuery(ISqlQueryPlanExecutor executor) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, new TestSqlBuilder())
            .From<AggregateRow>("r")
            .Select<AggregateRow>((row, functions) => functions.Sum(value => value.IntValue));

    /// <summary>
    /// 创建长整型求和投影查询。
    /// </summary>
    /// <param name="executor">执行测试查询或操作的执行器。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery CreateLongSumQuery(ISqlQueryPlanExecutor executor) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor, new TestSqlBuilder())
            .From<AggregateRow>("r")
            .Select<AggregateRow>((row, functions) => functions.Sum(value => value.LongValue));

    /// <summary>
    /// 提供标量聚合输入测试使用的实体记录。
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

        /// <summary>
        /// 获取或设置聚合函数使用的长整数值。
        /// </summary>
        public long? LongValue { get; set; }
    }
}
