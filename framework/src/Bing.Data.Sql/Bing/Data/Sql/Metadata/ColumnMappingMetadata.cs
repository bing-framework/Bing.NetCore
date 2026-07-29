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
    public string PropertyName { get; init; }

    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; init; }

    /// <summary>
    /// 结构化列标识符
    /// </summary>
    public ColumnIdentifier Column { get; init; }

    /// <summary>
    /// CLR 类型
    /// </summary>
    public Type ClrType { get; init; }

    /// <summary>
    /// 数据库参数类型
    /// </summary>
    public DbType? DbType { get; init; }

    /// <summary>
    /// 长度
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// 精度
    /// </summary>
    public byte? Precision { get; init; }

    /// <summary>
    /// 小数位
    /// </summary>
    public byte? Scale { get; init; }

    /// <summary>
    /// Provider 数据类型名称
    /// </summary>
    public string ProviderTypeName { get; init; }

    /// <summary>
    /// 是否可空
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// 是否为主键。
    /// </summary>
    public bool IsKey { get; init; }

    /// <summary>
    /// 是否由数据库生成。
    /// </summary>
    public bool IsDatabaseGenerated { get; init; }

    /// <summary>
    /// 是否为并发令牌。
    /// </summary>
    public bool IsConcurrencyToken { get; init; }

    /// <summary>
    /// 是否可插入。
    /// </summary>
    public bool CanInsert { get; init; }

    /// <summary>
    /// 是否可更新。
    /// </summary>
    public bool CanUpdate { get; init; }

    /// <summary>
    /// 字段存储方式
    /// </summary>
    public ColumnStorageKind StorageKind { get; init; } = ColumnStorageKind.Default;

    /// <summary>
    /// 字段值转换器类型
    /// </summary>
    public FieldValueConverterKind ConverterKind { get; init; } = FieldValueConverterKind.None;

    /// <summary>
    /// 自定义转换器名称
    /// </summary>
    public string CustomConverterName { get; init; }
}
