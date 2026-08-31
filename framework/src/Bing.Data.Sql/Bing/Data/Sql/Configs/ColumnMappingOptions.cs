using System.Data;

namespace Bing.Data.Sql.Configs;

/// <summary>
/// 配置实体属性到数据库列及参数元数据的映射。
/// </summary>
public sealed class ColumnMappingOptions
{
    /// <summary>
    /// 获取或设置实体属性名称。
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// 获取或设置对应的物理列名称。
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// 获取或设置数据库参数类型。
    /// </summary>
    public DbType? DbType { get; set; }

    /// <summary>
    /// 获取或设置字符、二进制或 Provider 支持类型的长度约束；未设置时使用 Provider 或 ADO 默认行为。
    /// </summary>
    public int? Size { get; set; }

    /// <summary>
    /// 获取或设置数值列或参数的总有效位数；通常与 <see cref="Scale"/> 配合使用。
    /// </summary>
    public byte? Precision { get; set; }

    /// <summary>
    /// 获取或设置数值列或参数的小数位数；通常与 <see cref="Precision"/> 配合使用。
    /// </summary>
    public byte? Scale { get; set; }

    /// <summary>
    /// 获取或设置 Provider 特定的数据类型名称。
    /// </summary>
    public string ProviderTypeName { get; set; }

    /// <summary>
    /// 获取或设置字段值的存储方式。
    /// </summary>
    public ColumnStorageKind StorageKind { get; set; } = ColumnStorageKind.Default;

    /// <summary>
    /// 获取或设置参数写入和结果读取时采用的字段值转换器类型。
    /// </summary>
    public FieldValueConverterKind ConverterKind { get; set; } = FieldValueConverterKind.None;

    /// <summary>
    /// 获取或设置自定义字段值转换器的注册名称。
    /// </summary>
    public string CustomConverterName { get; set; }
}