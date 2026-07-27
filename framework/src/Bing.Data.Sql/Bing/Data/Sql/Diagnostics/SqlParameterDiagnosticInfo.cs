using System.Data;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>Sql 参数诊断信息。</summary>
public sealed class SqlParameterDiagnosticInfo
{
    public string Name { get; set; }
    public object Value { get; set; }
    public object OriginalValue { get; set; }
    public bool IsSensitive { get; set; }
    public DbType? DbType { get; set; }
    public ParameterDirection? Direction { get; set; }
    public int? Size { get; set; }
    public byte? Precision { get; set; }
    public byte? Scale { get; set; }
    public string EntityType { get; set; }
    public string PropertyName { get; set; }
    public string ColumnName { get; set; }
    public string ProviderTypeName { get; set; }
    public SqlParameterSource Source { get; set; }
    public SqlParameterMetadataLevel MetadataLevel { get; set; }
}