using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 验证聚合条件参数转换一致性的测试。
/// </summary>
/// <remarks>
/// 聚合条件业务值提升必须在普通解析和生成桥接间保持相同类型及失败边界。
/// </remarks>
public sealed class SqlFunctionParameterConversionTest
{
    /// <summary>
    /// 创建 SQL 函数参数转换测试查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery Create() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>(MockBehavior.Strict).Object, new TestSqlBuilder());
    /// <summary>
    /// 创建包含计数投影及分组的测试查询。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery Start() => Create().From<Row>().Select<Row>(r => new { Count = SqlFn.Count(r.Id) })
        .GroupBy<Row>(r => r.GroupId);
    /// <summary>
    /// 定义函数参数转换测试使用的基础 SQL。
    /// </summary>
    private const string BaseSql = "Select Count([r].[Id]) As [Count] \r\nFrom [Rows] As [r] \r\nGroup By [r].[GroupId]";
    /// <summary>
    /// 读取生成查询构建器的参数集合。
    /// </summary>
    /// <param name="q">需要读取生成参数的查询。</param>
    /// <returns>生成查询构建器持有的 SQL 参数集合。</returns>
    private static IReadOnlyDictionary<string, object> Parameters(SqlLambdaQuery q) =>
        ((ISqlCommonPartAccessor)q.GeneratedCore.ReadBuilder).ParameterManager.GetParams();

    /// <summary>
    /// 验证整型及可空整型聚合阈值提升后与生成桥接一致。
    /// </summary>
    /// <param name="syntax">用于选择聚合表达式写法的场景序号。</param>
    /// <param name="nullable">是否使用可空类型或可空阈值。</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void Having_WhenThresholdIsIntOrNullableInt_ShouldPromoteAndMatchGeneratedBridge(int syntax, bool nullable)
    {
        int minimum = 1;
        int? optional = 1;
        LambdaExpression expression = syntax switch
        {
            0 => nullable ? (Expression<Func<Row, bool>>)(r => SqlFn.Count(r.Id) > optional) : (Expression<Func<Row, bool>>)(r => SqlFn.Count(r.Id) > minimum),
            1 => nullable ? (Expression<Func<Row, SqlAggregateFunctions<Row>, bool>>)((r, f) => f.Count(r.Id) > optional) : (Expression<Func<Row, SqlAggregateFunctions<Row>, bool>>)((r, f) => f.Count(r.Id) > minimum),
            _ => nullable ? (Expression<Func<Row, SqlAggregateFunctions<Row>, bool>>)((r, f) => f.Count(x => x.Id) > optional) : (Expression<Func<Row, SqlAggregateFunctions<Row>, bool>>)((r, f) => f.Count(x => x.Id) > minimum)
        };
        var runtime = Start();
        if (syntax == 0) runtime.Having((Expression<Func<Row, bool>>)expression);
        else runtime.Having((Expression<Func<Row, SqlAggregateFunctions<Row>, bool>>)expression);
        var generated = Start();
        var value = SqlGeneratedQueryBridge.ComparisonValue(expression);
        Assert.Equal(1L, Assert.IsType<long>(value));
        SqlGeneratedQueryBridge.Compare(generated, true, new[] { typeof(Row) }, new[] { "r" }, null,
            new SqlGeneratedColumn(0, "Id", null, typeof(long), SqlAggregateFunction.Count), null, Operator.Greater, value);
        foreach (var query in new[] { runtime, generated })
        {
            Assert.Equal(BaseSql + " Having Count([r].[Id])>@_p_0", query.ToSql());
            Assert.Equal(query.ToSql(), query.Clone().ToSql());
            Assert.Equal(1L, Assert.IsType<long>(Assert.Single(Parameters(query)).Value));
        }
    }

    /// <summary>
    /// 验证空聚合阈值保留空值且不执行值转换。
    /// </summary>
    [Fact]
    public void Having_WhenNullableThresholdIsNull_ShouldPreserveNullWithoutExecutingConversion()
    {
        int? minimum = null;
        Expression<Func<Row, bool>> condition = r => SqlFn.Count(r.Id) == minimum;
        var query = Start().Having(condition);
        Assert.Null(SqlGeneratedQueryBridge.ComparisonValue(condition));
        Assert.Equal(BaseSql + " Having Count([r].[Id]) Is Null", query.ToSql());
        Assert.Empty(Parameters(query));
    }

    /// <summary>
    /// 验证阈值窄化或属性读取被拒绝且查询仍可修正。
    /// </summary>
    [Fact]
    public void Having_WhenThresholdNarrowsOrCallsGetter_ShouldRejectWithoutChangingState()
    {
        long minimum = 1;
        Expression<Func<Row, bool>> narrowing = r => SqlFn.Count(r.Id) > (int)minimum;
        var query = Start();
        Assert.Throws<NotSupportedException>(() => query.Having(narrowing));
        Assert.Throws<NotSupportedException>(() => SqlGeneratedQueryBridge.ComparisonValue(narrowing));
        Expression<Func<Row, bool>> getter = r => SqlFn.Count(r.Id) > ForbiddenThreshold;
        Assert.Throws<NotSupportedException>(() => query.Having(getter));
        Assert.Throws<NotSupportedException>(() => SqlGeneratedQueryBridge.ComparisonValue(getter));
        Assert.Equal(BaseSql, query.ToSql());
        Assert.Equal(BaseSql, query.Clone().ToSql());
        Assert.Empty(Parameters(query));
        query.Having<Row>(r => SqlFn.Count(r.Id) > minimum);
        Assert.Equal(BaseSql + " Having Count([r].[Id])>@_p_0", query.ToSql());
        Assert.Equal(1L, Assert.IsType<long>(Parameters(query)["@_p_0"]));
    }

    /// <summary>
    /// 验证整型阈值提升为十进制参数且不调用用户运算符。
    /// </summary>
    [Fact]
    public void Having_WhenDecimalThresholdWidensInt_ShouldUseDecimalParameterWithoutCallingOperator()
    {
        int minimum = 1;
        Expression<Func<Row, bool>> condition = r => SqlFn.Sum(r.Amount) > minimum;
        var query = Start().Having(condition);
        Assert.Equal(BaseSql + " Having Sum([r].[Amount])>@_p_0", query.ToSql());
        Assert.Equal(1m, Assert.IsType<decimal>(Parameters(query)["@_p_0"]));
        Assert.Equal(1m, Assert.IsType<decimal>(SqlGeneratedQueryBridge.ComparisonValue(condition)));
    }

    /// <summary>
    /// 验证字符阈值提升为双精度或十进制时保留数值类型。
    /// </summary>
    [Fact]
    public void Having_WhenCharThresholdWidensToDoubleOrDecimal_ShouldKeepNumericParameterType()
    {
        char minimum = '\u1234';
        Expression<Func<Row, bool>> doubleCondition = r => SqlFn.Sum(r.DoubleAmount) > minimum;
        Expression<Func<Row, bool>> decimalCondition = r => SqlFn.Sum(r.Amount) > minimum;
        var doubleQuery = Start().Having(doubleCondition);
        var decimalQuery = Start().Having(decimalCondition);

        Assert.Equal(BaseSql + " Having Sum([r].[DoubleAmount])>@_p_0", doubleQuery.ToSql());
        Assert.Equal((double)minimum, Assert.IsType<double>(Parameters(doubleQuery)["@_p_0"]));
        Assert.Equal((double)minimum, Assert.IsType<double>(SqlGeneratedQueryBridge.ComparisonValue(doubleCondition)));
        Assert.Equal(BaseSql + " Having Sum([r].[Amount])>@_p_0", decimalQuery.ToSql());
        Assert.Equal((decimal)minimum, Assert.IsType<decimal>(Parameters(decimalQuery)["@_p_0"]));
        Assert.Equal((decimal)minimum, Assert.IsType<decimal>(SqlGeneratedQueryBridge.ComparisonValue(decimalCondition)));
    }

    /// <summary>
    /// 验证字符阈值提升为单精度时运行时与桥接读取一致。
    /// </summary>
    [Fact]
    public void ReadValue_WhenCharThresholdWidensToFloat_ShouldKeepNumericValueAndBridgeType()
    {
        char minimum = '\u1234';
        Expression<Func<Row, bool>> condition = r => SqlFn.Count(r.Id) > (float)minimum;
        var right = ((BinaryExpression)condition.Body).Right;

        Assert.Equal((float)minimum, Assert.IsType<float>(SqlFunctionPredicate.ReadValue(right)));
        Assert.Equal((float)minimum, Assert.IsType<float>(SqlGeneratedQueryBridge.ComparisonValue(condition)));
    }

    /// <summary>
    /// 获取读取时用于验证异常传播的阈值。
    /// </summary>
    private static int ForbiddenThreshold => throw new InvalidOperationException("用户 getter 不应执行");

    /// <summary>
    /// 提供函数参数转换测试使用的查询数据模型。
    /// </summary>
    [Table("Rows")]
    private sealed class Row
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置分组标识。
        /// </summary>
        public int GroupId { get; set; }
        /// <summary>
        /// 获取或设置测试使用的数值。
        /// </summary>
        public decimal Amount { get; set; }
        /// <summary>
        /// 获取或设置参数转换测试使用的双精度值。
        /// </summary>
        public double DoubleAmount { get; set; }
    }
}
