using System.ComponentModel;
using System.Data;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 查询运行时资源绑定入口。
/// </summary>
/// <remarks>
/// 仅供 ORM 集成在创建查询后绑定连接、事务解析器、数据库上下文或实体映射。
/// 不暴露查询执行、Builder 或事务作用域内部状态。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlQueryRuntimeBinding
{
    /// <summary>
    /// 绑定由查询对象负责释放的连接。
    /// </summary>
    /// <param name="query">目标查询对象。</param>
    /// <param name="connection">由查询对象拥有的数据库连接。</param>
    /// <param name="source">连接来源。</param>
    public static void BindOwnedConnection(ISqlQuery query, IDbConnection connection, SqlConnectionSource source) =>
        GetController(query).BindOwnedConnection(connection, source);

    /// <summary>
    /// 绑定由外部 ORM 或调用方负责释放的连接。
    /// </summary>
    /// <param name="query">目标查询对象。</param>
    /// <param name="connection">外部拥有的数据库连接。</param>
    /// <param name="source">连接来源。</param>
    public static void BindExternalConnection(ISqlQuery query, IDbConnection connection, SqlConnectionSource source) =>
        GetController(query).BindExternalConnection(connection, source);

    /// <summary>
    /// 绑定外部事务的延迟解析器。
    /// </summary>
    /// <param name="query">目标查询对象。</param>
    /// <param name="resolver">获取当前外部事务的解析器。</param>
    public static void BindExternalTransactionResolver(ISqlQuery query, Func<IDbTransaction> resolver) =>
        GetController(query).BindExternalTransactionResolver(resolver);

    /// <summary>
    /// 绑定查询执行的固定数据库上下文。
    /// </summary>
    /// <param name="query">目标查询对象。</param>
    /// <param name="context">要冻结的数据库上下文。</param>
    public static void BindDatabaseContext(ISqlQuery query, DatabaseContext context) =>
        GetController(query).BindDatabaseContext(context);

    /// <summary>
    /// 绑定实体映射解析器。
    /// </summary>
    /// <param name="query">目标查询对象。</param>
    /// <param name="resolver">实体映射解析器。</param>
    public static void BindEntityMappingResolver(ISqlQuery query, IEntityMappingResolver resolver) =>
        GetController(query).BindEntityMappingResolver(resolver);

    /// <summary>
    /// 获取查询实现提供的运行时绑定控制器。
    /// </summary>
    /// <param name="query">目标查询对象。</param>
    /// <returns>运行时绑定控制器。</returns>
    private static ISqlQueryRuntimeBindingController GetController(ISqlQuery query)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return query as ISqlQueryRuntimeBindingController ??
               throw new InvalidOperationException("当前 SQL 查询对象不支持框架运行时资源绑定。");
    }
}