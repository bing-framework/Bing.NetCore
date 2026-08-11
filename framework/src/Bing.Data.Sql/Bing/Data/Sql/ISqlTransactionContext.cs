using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 事务只读上下文。
/// </summary>
public interface ISqlTransactionContext
{
    /// <summary>
    /// 获取事务标识。
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// 获取数据库标识。
    /// </summary>
    string DbKey { get; }

    /// <summary>
    /// 获取数据库类型。
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 获取映射配置标识。
    /// </summary>
    string MappingProfile { get; }

    /// <summary>
    /// 获取读取偏好。
    /// </summary>
    SqlReadPreference ReadPreference { get; }

    /// <summary>
    /// 获取事务隔离级别。
    /// </summary>
    System.Data.IsolationLevel IsolationLevel { get; }
}