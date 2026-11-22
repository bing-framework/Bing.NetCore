using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 验证聚合目标类型校验与物化一致性的测试。
/// </summary>
/// <remarks>
/// 聚合目标的类型必须同时满足校验与物化合同，不能作为导航静默跳过。
/// </remarks>
public sealed class SqlAggregateReferenceTargetTest
{
    /// <summary>
    /// 验证接口聚合目标在两条执行路径中均于执行前被拒绝。
    /// </summary>
    [Fact]
    public void Count_WhenTargetIsInterface_ShouldRejectBothPathsBeforeExecutor() => Reject<ComparableResult>();

    /// <summary>
    /// 验证值类型基类聚合目标在两条执行路径中均于执行前被拒绝。
    /// </summary>
    [Fact]
    public void Count_WhenTargetIsValueTypeBase_ShouldRejectBothPathsBeforeExecutor() => Reject<ValueResult>();

    /// <summary>
    /// 验证不支持的引用聚合目标被拒绝。
    /// </summary>
    /// <typeparam name="TResult">表达式或异步操作的结果类型。</typeparam>
    private static void Reject<TResult>() where TResult : new()
    {
        var executor = new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict);
        executor.As<ISqlGeneratedPlanExecutor>();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Row>().Select<Row>(r => new { Count = SqlFn.Count(r.Id) });
        const string sql = "Select Count([r].[Id]) As [Count] \r\nFrom [ReferenceRows] As [r]";
        Assert.Equal(sql, query.ToSql());
        Assert.Throws<InvalidOperationException>(() => query.ToList<TResult>());
        Assert.Throws<InvalidOperationException>(() => { _ = query.ToListAsync<TResult>(); });
        Assert.Throws<InvalidOperationException>(() => query.First<TResult>());
        Assert.Throws<InvalidOperationException>(() => SqlGeneratedExecution.ToList(query, _ => new TResult(), (_, _) => { }, null));
        Assert.Throws<InvalidOperationException>(() => { _ = SqlGeneratedExecution.ToListAsync(query, _ => new TResult(), (_, _) => { }, null, default); });
        Assert.Equal(sql, query.ToSql());
        executor.VerifyNoOtherCalls();
    }

    /// <summary>
    /// 提供聚合引用目标测试使用的查询数据模型。
    /// </summary>
    [Table("ReferenceRows")]
    public class Row {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; } }
    /// <summary>
    /// 承载聚合引用目标查询结果的投影数据模型。
    /// </summary>
    public class ComparableResult {
        /// <summary>
        /// 获取或设置可比较类型聚合结果。
        /// </summary>
        public IComparable Count { get; set; } }
    /// <summary>
    /// 承载聚合引用目标查询结果的投影数据模型。
    /// </summary>
    public class ValueResult {
        /// <summary>
        /// 获取或设置值类型聚合结果。
        /// </summary>
        public ValueType Count { get; set; } }
}
