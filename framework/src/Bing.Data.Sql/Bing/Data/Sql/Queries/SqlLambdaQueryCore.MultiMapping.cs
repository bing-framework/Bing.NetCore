using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Data.Sql;

/// <summary>
/// Lambda 查询多类型映射终结器的内部转发。
/// </summary>
internal sealed partial class SqlLambdaQueryCore
{
    /// <summary>
    /// 同步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并转发两个类型映射委托。
    /// </remarks>
    internal List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, int? timeout)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond) });
        return _query.ToList(map, timeout);
    }

    /// <summary>
    /// 异步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含映射后查询结果的异步任务。</returns>
    /// <remarks>
    /// 异步执行当前查询，并转发两个类型映射委托。
    /// </remarks>
    internal Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, int? timeout,
        CancellationToken cancellationToken)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond) });
        return _query.ToListAsync(map, timeout, cancellationToken);
    }

    /// <summary>
    /// 同步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并转发三个类型映射委托。
    /// </remarks>
    internal List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map, int? timeout)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) });
        return _query.ToList(map, timeout);
    }

    /// <summary>
    /// 异步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含映射后查询结果的异步任务。</returns>
    /// <remarks>
    /// 异步执行当前查询，并转发三个类型映射委托。
    /// </remarks>
    internal Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map, int? timeout,
        CancellationToken cancellationToken)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird) });
        return _query.ToListAsync(map, timeout, cancellationToken);
    }

    /// <summary>
    /// 同步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并转发四个类型映射委托。
    /// </remarks>
    internal List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth) });
        return _query.ToList(map, timeout);
    }

    /// <summary>
    /// 异步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含映射后查询结果的异步任务。</returns>
    /// <remarks>
    /// 异步执行当前查询，并转发四个类型映射委托。
    /// </remarks>
    internal Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout,
        CancellationToken cancellationToken)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth) });
        return _query.ToListAsync(map, timeout, cancellationToken);
    }

    /// <summary>
    /// 同步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并转发五个类型映射委托。
    /// </remarks>
    internal List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth) });
        return _query.ToList(map, timeout);
    }

    /// <summary>
    /// 异步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含映射后查询结果的异步任务。</returns>
    /// <remarks>
    /// 异步执行当前查询，并转发五个类型映射委托。
    /// </remarks>
    internal Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout,
        CancellationToken cancellationToken)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth) });
        return _query.ToListAsync(map, timeout, cancellationToken);
    }

    /// <summary>
    /// 同步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并转发六个类型映射委托。
    /// </remarks>
    internal List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth) });
        return _query.ToList(map, timeout);
    }

    /// <summary>
    /// 异步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含映射后查询结果的异步任务。</returns>
    /// <remarks>
    /// 异步执行当前查询，并转发六个类型映射委托。
    /// </remarks>
    internal Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout,
        CancellationToken cancellationToken)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth) });
        return _query.ToListAsync(map, timeout, cancellationToken);
    }

    /// <summary>
    /// 同步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并转发七个类型映射委托。
    /// </remarks>
    internal List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh) });
        return _query.ToList(map, timeout);
    }

    /// <summary>
    /// 异步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">映射委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含映射后查询结果的异步任务。</returns>
    /// <remarks>
    /// 异步执行当前查询，并转发七个类型映射委托。
    /// </remarks>
    internal Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout,
        CancellationToken cancellationToken)
    {
        ValidateProjectionResult<TResult>(new[] { typeof(TFirst), typeof(TSecond), typeof(TThird), typeof(TFourth), typeof(TFifth), typeof(TSixth), typeof(TSeventh) });
        return _query.ToListAsync(map, timeout, cancellationToken);
    }

    /// <summary>
    /// 设置 Dapper 多映射使用的分段列名称。
    /// </summary>
    /// <param name="splitOn">分段列名称，多个分段列使用逗号分隔。</param>
    internal void SplitOn(string splitOn) => _query.SplitOn(splitOn);
}
