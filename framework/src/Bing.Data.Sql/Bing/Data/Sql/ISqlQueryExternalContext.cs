using System.Data;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 查询外部上下文
/// </summary>
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
    void SetOwnedConnection(IDbConnection connection);

    /// <summary>
    /// 设置外部事务解析器
    /// </summary>
    /// <param name="resolver">外部事务解析器</param>
    void SetExternalTransactionResolver(Func<IDbTransaction> resolver);

    /// <summary>
    /// 设置连接来源
    /// </summary>
    /// <param name="source">连接来源</param>
    void SetConnectionSource(SqlConnectionSource source);
}