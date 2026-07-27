using System.Data;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>SQL 事务诊断信息。</summary>
public sealed class SqlTransactionDiagnosticInfo
{
    public string TransactionId { get; set; }
    public bool HasTransaction { get; set; }
    public IsolationLevel? IsolationLevel { get; set; }
    public SqlResourceOwnership Ownership { get; set; }
    public bool IsPrimaryReadTransaction { get; set; }
}