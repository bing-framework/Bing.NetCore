using System.Data;
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
    /// 查询
    /// </summary>
    /// <typeparam name="TResult">实体类型</typeparam>
    /// <param name="func">查询操作</param>
    /// <param name="connection">数据库连接</param>
    TResult Query<TResult>(Func<IDbConnection, string, IReadOnlyDictionary<string, object>, TResult> func,
        IDbConnection connection = null);

    /// <summary>
    /// 查询
    /// </summary>
    /// <typeparam name="TResult">实体类型</typeparam>
    /// <param name="func">查询操作</param>
    /// <param name="connection">数据库连接</param>
    Task<TResult> QueryAsync<TResult>(Func<IDbConnection, string, IReadOnlyDictionary<string, object>, Task<TResult>> func,
        IDbConnection connection = null);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    PagerList<TResult> PagerQuery<TResult>(Func<List<TResult>> func, IPager parameter,
        IDbConnection connection = null);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<Task<List<TResult>>> func, IPager parameter,
        IDbConnection connection = null);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    PagerList<TResult> ToPagerList<TResult>(IPager parameter = null, IDbConnection connection = null);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="parameter">分页参数</param>
    /// <param name="connection">数据库连接</param>
    Task<PagerList<TResult>> ToPagerListAsync<TResult>(IPager parameter = null, IDbConnection connection = null);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    PagerList<TResult> ToPagerList<TResult>(int page, int pageSize, IDbConnection connection = null);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    Task<PagerList<TResult>> ToPagerListAsync<TResult>(int page, int pageSize, IDbConnection connection = null);

    /// <summary>
    /// 获取分页列表
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="sql">Sql语句</param>
    /// <param name="page">页数</param>
    /// <param name="pageSize">每页显示行数</param>
    /// <param name="connection">数据库连接</param>
    Task<PagerList<TResult>> ToPagerListAsync<TResult>(string sql, int page, int pageSize,
        IDbConnection connection = null);
}
