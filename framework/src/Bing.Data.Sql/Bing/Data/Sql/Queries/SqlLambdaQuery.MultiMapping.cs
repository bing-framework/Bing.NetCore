using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bing.Data.Sql;

/// <summary>
/// 为 Lambda 查询描述提供显式多类型映射终结器。
/// </summary>
public partial class SqlLambdaQuery
{
    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将两个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将两个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, int? timeout) =>
        _core.ToList(map, timeout);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将两个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map) =>
        _core.ToList(map, null);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将三个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将三个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map, int? timeout) =>
        _core.ToList(map, timeout);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将三个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map) =>
        _core.ToList(map, null);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将四个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将四个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout) =>
        _core.ToList(map, timeout);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将四个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map) =>
        _core.ToList(map, null);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将五个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将五个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout) =>
        _core.ToList(map, timeout);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将五个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map) =>
        _core.ToList(map, null);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将六个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将六个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout) =>
        _core.ToList(map, timeout);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将六个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map) =>
        _core.ToList(map, null);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将七个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将七个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout) =>
        _core.ToList(map, timeout);

    /// <summary>
    /// 同步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前查询，并使用委托将七个结果段映射为单个结果。
    /// </remarks>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map) =>
        _core.ToList(map, null);

    /// <summary>
    /// 设置 Dapper 多映射使用的分段列名称。
    /// </summary>
    /// <param name="splitOn">分段列名称，多个分段列使用逗号分隔。</param>
    /// <returns>当前查询描述。</returns>
    public SqlLambdaQuery SplitOn(string splitOn)
    {
        _core.SplitOn(splitOn);
        return this;
    }

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将两个结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将两个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map) =>
        _core.ToListAsync(map, null, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将两个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将两个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, int? timeout) =>
        _core.ToListAsync(map, timeout, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将两个结果段映射为结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将两个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, CancellationToken cancellationToken) =>
        _core.ToListAsync(map, null, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将两个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将两个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, int? timeout,
        CancellationToken cancellationToken) => _core.ToListAsync(map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将三个结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将三个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map) =>
        _core.ToListAsync(map, null, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将三个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将三个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map, int? timeout) =>
        _core.ToListAsync(map, timeout, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将三个结果段映射为结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将三个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map, CancellationToken cancellationToken) =>
        _core.ToListAsync(map, null, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将三个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将三个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map, int? timeout,
        CancellationToken cancellationToken) => _core.ToListAsync(map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将四个结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将四个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map) =>
        _core.ToListAsync(map, null, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将四个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将四个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout) =>
        _core.ToListAsync(map, timeout, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将四个结果段映射为结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将四个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map, CancellationToken cancellationToken) =>
        _core.ToListAsync(map, null, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将四个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将四个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout,
        CancellationToken cancellationToken) => _core.ToListAsync(map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将五个结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将五个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map) =>
        _core.ToListAsync(map, null, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将五个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将五个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout) =>
        _core.ToListAsync(map, timeout, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将五个结果段映射为结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将五个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, CancellationToken cancellationToken) =>
        _core.ToListAsync(map, null, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将五个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将五个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout,
        CancellationToken cancellationToken) => _core.ToListAsync(map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将六个结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将六个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map) =>
        _core.ToListAsync(map, null, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将六个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将六个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout) =>
        _core.ToListAsync(map, timeout, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将六个结果段映射为结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将六个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, CancellationToken cancellationToken) =>
        _core.ToListAsync(map, null, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将六个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将六个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout,
        CancellationToken cancellationToken) => _core.ToListAsync(map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将七个结果段映射为结果的委托。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将七个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map) =>
        _core.ToListAsync(map, null, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将七个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将七个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout) =>
        _core.ToListAsync(map, timeout, default);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将七个结果段映射为结果的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将七个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, CancellationToken cancellationToken) =>
        _core.ToListAsync(map, null, cancellationToken);

    /// <summary>
    /// 异步执行查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TFirst">第一个映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个映射类型。</typeparam>
    /// <typeparam name="TThird">第三个映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将七个结果段映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>映射后的查询结果列表。</returns>
    /// <remarks>
    /// 异步执行当前查询，并使用委托将七个结果段映射为单个结果。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout,
        CancellationToken cancellationToken) => _core.ToListAsync(map, timeout, cancellationToken);

}
