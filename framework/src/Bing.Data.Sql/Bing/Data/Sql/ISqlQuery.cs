using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象
/// </summary>
/// <remarks>
/// 实例包含可变的 Sql 生成器、连接和事务状态，不能被多个并发操作共享。每个独立操作应使用独立实例。
/// </remarks>
public partial interface ISqlQuery : ISqlQueryOperation, ISqlOptions, IDisposable
{
    /// <summary>
    /// 上下文标识
    /// </summary>
    string ContextId { get; }

    /// <summary>
    /// Sql生成器
    /// </summary>
    ISqlBuilder SqlBuilder { get; }

    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="configAction">配置操作</param>
    void Config(Action<SqlOptions> configAction);

    /// <summary>
    /// 获取Sql生成器
    /// </summary>
    ISqlBuilder GetBuilder();

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    PagerList<TResult> PagerQuery<TResult>(Func<List<TResult>> func, IPager parameter, int? timeout = null);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">不接收取消令牌的获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <remarks>该重载无法将取消令牌传递给列表操作，请改用接收 <see cref="CancellationToken"/> 的重载。</remarks>
    [Obsolete("请使用接收 CancellationToken 的 PagerQueryAsync 重载")]
    Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<Task<List<TResult>>> func, IPager parameter, int? timeout = null);

    /// <summary>
    /// 分页查询。
    /// </summary>
    /// <typeparam name="TResult">返回结果类型。</typeparam>
    /// <param name="func">使用取消令牌获取列表的操作。</param>
    /// <param name="parameter">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终分页结果的异步操作。</returns>
    Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<CancellationToken, Task<List<TResult>>> func,
        IPager parameter, int? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 临时禁用调试日志
    /// </summary>
    ISqlQuery DisableDebugLog();
}
