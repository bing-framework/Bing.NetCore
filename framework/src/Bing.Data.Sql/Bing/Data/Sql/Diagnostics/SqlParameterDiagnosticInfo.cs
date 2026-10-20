using System.Data;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// SQL 参数诊断信息。
/// </summary>
public sealed class SqlParameterDiagnosticInfo
{
    /// <summary>
    /// 获取或设置不含 Provider 参数前缀的标准参数名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置实际提交给数据库驱动的参数值。
    /// </summary>
    /// <remarks>
    /// 敏感参数可能已脱敏。
    /// </remarks>
    public object Value { get; set; }

    /// <summary>
    /// 获取或设置参数绑定或转换前保留的原始值。
    /// </summary>
    public object OriginalValue { get; set; }

    /// <summary>
    /// 获取或设置指示该参数值是否应在诊断输出中脱敏。
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// 获取或设置绑定时使用的 ADO.NET 数据类型。
    /// </summary>
    /// <remarks>
    /// 未指定时为 null。
    /// </remarks>
    public DbType? DbType { get; set; }

    /// <summary>
    /// 获取或设置参数方向。
    /// </summary>
    /// <remarks>
    /// 未显式指定时为 null。
    /// </remarks>
    public ParameterDirection? Direction { get; set; }

    /// <summary>
    /// 获取或设置参数长度。
    /// </summary>
    /// <remarks>
    /// 未指定时为 null。
    /// </remarks>
    public int? Size { get; set; }

    /// <summary>
    /// 获取或设置数值参数的有效位数。
    /// </summary>
    /// <remarks>
    /// 未指定时为 null。
    /// </remarks>
    public byte? Precision { get; set; }

    /// <summary>
    /// 获取或设置数值参数的小数位数。
    /// </summary>
    /// <remarks>
    /// 未指定时为 null。
    /// </remarks>
    public byte? Scale { get; set; }

    /// <summary>
    /// 获取或设置参数关联实体的完整类型名称。
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// 获取或设置参数关联的实体属性名称。
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// 获取或设置参数关联的数据库列名称。
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// 获取或设置数据库 Provider 专用类型名称。
    /// </summary>
    public string ProviderTypeName { get; set; }

    /// <summary>
    /// 获取或设置参数元数据的来源。
    /// </summary>
    public SqlParameterSource Source { get; set; }

    /// <summary>
    /// 获取或设置参数元数据的完整程度。
    /// </summary>
    public SqlParameterMetadataLevel MetadataLevel { get; set; }
}
