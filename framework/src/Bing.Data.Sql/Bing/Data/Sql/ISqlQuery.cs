using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// Sql查询对象
/// </summary>
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
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<Task<List<TResult>>> func, IPager parameter, int? timeout = null);

    /// <summary>
    /// 临时禁用调试日志
    /// </summary>
    ISqlQuery DisableDebugLog();
}
