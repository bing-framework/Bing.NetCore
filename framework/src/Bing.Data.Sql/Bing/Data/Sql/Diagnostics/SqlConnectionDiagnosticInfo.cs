using Bing.Data.Enums;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>SQL 连接诊断信息。</summary>
public sealed class SqlConnectionDiagnosticInfo
{
    public string Database { get; set; }
    public string DbKey { get; set; }
    public DatabaseType DatabaseType { get; set; }
    public SqlConnectionSource Source { get; set; }
    public SqlResourceOwnership Ownership { get; set; }
    public bool IsReadOnly { get; set; }
    public SqlReadPreference ReadPreference { get; set; }
}