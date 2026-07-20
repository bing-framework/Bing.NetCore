using System.Data;

namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 列映射元数据
/// </summary>
public sealed class ColumnMappingMetadata
{
    /// <summary>
    /// 属性名
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// 结构化列标识符
    /// </summary>
    public ColumnIdentifier Column { get; set; }

    /// <summary>
    /// CLR 类型
    /// </summary>
    public Type ClrType { get; set; }

    /// <summary>
    /// 数据库参数类型
    /// </summary>
    public DbType? DbType { get; set; }

    /// <summary>
    /// 长度
    /// </summary>
    public int? Size { get; set; }

    /// <summary>
    /// 精度
    /// </summary>
    public byte? Precision { get; set; }

    /// <summary>
    /// 小数位
    /// </summary>
    public byte? Scale { get; set; }

    /// <summary>
    /// Provider 数据类型名称
    /// </summary>
    public string ProviderTypeName { get; set; }

    /// <summary>
    /// 是否可空
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// 字段存储方式
    /// </summary>
    public ColumnStorageKind StorageKind { get; set; } = ColumnStorageKind.Default;

    /// <summary>
    /// 字段值转换器类型
    /// </summary>
    public FieldValueConverterKind ConverterKind { get; set; } = FieldValueConverterKind.None;

    /// <summary>
    /// 自定义转换器名称
    /// </summary>
    public string CustomConverterName { get; set; }
}
