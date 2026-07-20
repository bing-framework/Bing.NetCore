using System.Data;
using System.ComponentModel;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 查询外部上下文
/// </summary>
[Obsolete("外部资源和元数据绑定已内部化，请使用框架集成 API。")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlQueryExternalContext
{
    /// <summary>
    /// 设置实体元数据
    /// </summary>
    /// <param name="metadata">实体元数据</param>
    void SetEntityMetadata(IEntityMetadata metadata);

    /// <summary>
    /// 设置实体映射解析器
    /// </summary>
    /// <param name="resolver">实体映射解析器</param>
    void SetEntityMappingResolver(IEntityMappingResolver resolver);

    /// <summary>
    /// 设置由查询对象释放的连接
    /// </summary>
    /// <param name="connection">数据库连接</param>
    [Obsolete("连接绑定已内部化，请使用 ISqlTransactionScope 或框架内部资源绑定器。")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    void SetOwnedConnection(IDbConnection connection);

    /// <summary>
    /// 设置外部事务解析器
    /// </summary>
    /// <param name="resolver">外部事务解析器</param>
    [Obsolete("事务绑定已内部化，请使用 ISqlTransactionScope 或框架内部资源绑定器。")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    void SetExternalTransactionResolver(Func<IDbTransaction> resolver);

    /// <summary>
    /// 设置连接来源
    /// </summary>
    /// <param name="source">连接来源</param>
    [Obsolete("连接绑定已内部化，请使用 ISqlTransactionScope 或框架内部资源绑定器。")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    void SetConnectionSource(SqlConnectionSource source);
}