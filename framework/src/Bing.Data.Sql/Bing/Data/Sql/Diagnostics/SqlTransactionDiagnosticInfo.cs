using System.Data;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>SQL 事务诊断信息。</summary>
public sealed class SqlTransactionDiagnosticInfo
{
    /// <summary>
    /// 当前事务的诊断标识；不存在事务时为 null。
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// 指示当前操作是否绑定到事务。
    /// </summary>
    public bool HasTransaction { get; set; }

    /// <summary>
    /// 当前事务的隔离级别；不存在事务或驱动未提供时为 null。
    /// </summary>
    public IsolationLevel? IsolationLevel { get; set; }

    /// <summary>
    /// 当前操作对事务资源承担的所有权。
    /// </summary>
    public SqlResourceOwnership Ownership { get; set; }

    /// <summary>
    /// 指示该事务是否为保证主库读取而创建的短事务。
    /// </summary>
    public bool IsPrimaryReadTransaction { get; set; }
}