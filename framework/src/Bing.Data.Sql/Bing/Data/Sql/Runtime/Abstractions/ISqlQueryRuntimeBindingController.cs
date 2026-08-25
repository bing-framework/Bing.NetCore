using System.ComponentModel;
using System.Data;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 查询运行时绑定控制器。
/// </summary>
/// <remarks>
/// 由 SQL 查询实现提供，仅供 <see cref="SqlQueryRuntimeBinding"/> 调用。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ISqlQueryRuntimeBindingController
{
    /// <summary>
    /// 绑定由查询对象负责释放的连接。
    /// </summary>
    void BindOwnedConnection(IDbConnection connection, SqlConnectionSource source);

    /// <summary>
    /// 绑定由外部调用方负责释放的连接。
    /// </summary>
    void BindExternalConnection(IDbConnection connection, SqlConnectionSource source);

    /// <summary>
    /// 绑定外部事务的延迟解析器。
    /// </summary>
    void BindExternalTransactionResolver(Func<IDbTransaction> resolver);

    /// <summary>
    /// 绑定查询执行的固定数据库上下文。
    /// </summary>
    void BindDatabaseContext(DatabaseContext context);

    /// <summary>
    /// 绑定实体映射解析器。
    /// </summary>
    void BindEntityMappingResolver(IEntityMappingResolver resolver);
}