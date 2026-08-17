using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 为独立查询描述创建 SQL Builder 的运行时来源。
/// </summary>
/// <remarks>
/// 该契约仅用于将公开查询描述与具体执行实现解耦，创建结果不得复用根查询的可变 Builder 状态。
/// </remarks>
public interface ISqlQueryBuilderSource
{
    /// <summary>
    /// 创建绑定当前数据源、Provider 和查询选项的独立 SQL Builder。
    /// </summary>
    /// <returns>不与根查询共享子句和参数状态的 SQL Builder。</returns>
    ISqlBuilder CreateIndependentSqlBuilder();
}

/// <summary>
/// 访问独立查询描述专属 SQL Builder 的内部契约。
/// </summary>
/// <remarks>
/// 仅供框架内部参数和子查询扩展使用，避免将 Builder 作为公开查询 API 的逃逸入口。
/// </remarks>
internal interface ISqlQueryBuilderAccessor
{
    /// <summary>
    /// 获取当前查询描述专属的 SQL Builder。
    /// </summary>
    /// <returns>当前查询描述的独立 SQL Builder。</returns>
    ISqlBuilder GetSqlBuilder();
}

/// <summary>
/// 执行独立 SQL 查询计划的运行时契约。
/// </summary>
public partial interface ISqlQueryPlanExecutor : ISqlQueryBuilderSource
{

    /// <summary>
    /// 同步执行查询计划并完整物化结果集。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终结果列表。</returns>
    List<TResult> ToList<TResult>(SqlQueryPlan plan, int? timeout);

    /// <summary>
    /// 同步执行查询计划并将每行映射为两个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    List<TResult> ToList<TFirst, TSecond, TResult>(SqlQueryPlan plan, Func<TFirst, TSecond, TResult> map,
        int? timeout);

    /// <summary>
    /// 同步执行查询计划并将每行映射为三个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    List<TResult> ToList<TFirst, TSecond, TThird, TResult>(SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout);

    /// <summary>
    /// 同步执行查询计划并将每行映射为四个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout);

    /// <summary>
    /// 同步执行查询计划并将每行映射为五个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout);

    /// <summary>
    /// 同步执行查询计划并将每行映射为六个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout);

    /// <summary>
    /// 同步执行查询计划并将每行映射为七个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout);

    /// <summary>
    /// 同步执行查询计划并获取第一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果。</returns>
    TResult First<TResult>(SqlQueryPlan plan, int? timeout);

    /// <summary>
    /// 同步执行查询计划并获取第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行或默认值。</returns>
    TResult FirstOrDefault<TResult>(SqlQueryPlan plan, int? timeout);

    /// <summary>
    /// 同步执行查询计划并获取唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行。</returns>
    TResult Single<TResult>(SqlQueryPlan plan, int? timeout);

    /// <summary>
    /// 同步执行查询计划并获取唯一一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行或默认值。</returns>
    TResult SingleOrDefault<TResult>(SqlQueryPlan plan, int? timeout);

    /// <summary>
    /// 同步执行查询计划并获取首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>首行首列值；无结果时返回默认值。</returns>
    TResult Scalar<TResult>(SqlQueryPlan plan, int? timeout);

    /// <summary>
    /// 异步执行查询计划并完整物化结果集。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终结果列表的异步操作。</returns>
    Task<List<TResult>> ToListAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并将每行映射为两个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(SqlQueryPlan plan, Func<TFirst, TSecond, TResult> map,
        int? timeout, CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并将每行映射为三个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout, CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并将每行映射为四个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout, CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并将每行映射为五个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并将每行映射为六个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(SqlQueryPlan plan,
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并将每行映射为七个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        SqlQueryPlan plan, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map,
        int? timeout, CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并获取第一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行结果的异步操作。</returns>
    Task<TResult> FirstAsync<TResult>(SqlQueryPlan plan, int? timeout, CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并获取第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行或默认值的异步操作。</returns>
    Task<TResult> FirstOrDefaultAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并获取唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行的异步操作。</returns>
    Task<TResult> SingleAsync<TResult>(SqlQueryPlan plan, int? timeout, CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并获取唯一一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行或默认值的异步操作。</returns>
    Task<TResult> SingleOrDefaultAsync<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// 异步执行查询计划并获取首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示首行首列值的异步操作；无结果时返回默认值。</returns>
    Task<TResult> ScalarAsync<TResult>(SqlQueryPlan plan, int? timeout, CancellationToken cancellationToken);

}