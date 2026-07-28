using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数映射项。
/// </summary>
public class SqlParameterMapItem
{
    /// <summary>
    /// 参数名。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 实体类型。
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 属性名。
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// 参数值。
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// 是否已显式提供参数值。
    /// </summary>
    public bool HasExplicitValue { get; set; }

    /// <summary>
    /// 参数值是否已成功解析。
    /// </summary>
    public bool ValueResolved { get; set; }

    /// <summary>
    /// 参数方向。
    /// </summary>
    public ParameterDirection? Direction { get; set; }

    /// <summary>
    /// 参数类型。
    /// </summary>
    public DbType? DbType { get; set; }

    /// <summary>
    /// 参数长度。
    /// </summary>
    public int? Size { get; set; }

    /// <summary>
    /// 数值有效位数。
    /// </summary>
    public byte? Precision { get; set; }

    /// <summary>
    /// 数值小数位数。
    /// </summary>
    public byte? Scale { get; set; }
}