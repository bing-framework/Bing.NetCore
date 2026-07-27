namespace Bing.Data.Sql.Diagnostics;

/// <summary>SQL 参数诊断快照。</summary>
public sealed class SqlParameterDiagnosticSnapshot
{
    public string OriginalParameterType { get; set; }
    public bool IsMetadataBound { get; set; }
    public IReadOnlyList<SqlParameterDiagnosticInfo> Items { get; set; } = Array.Empty<SqlParameterDiagnosticInfo>();
}