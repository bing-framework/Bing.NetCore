using System.Data;
using Bing.Data.Enums;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// Sql参数
/// </summary>
public class SqlParam
{
    /// <summary>
    /// 初始化<see cref="SqlParam"/>类型的实例
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="dbType">参数类型</param>
    /// <param name="direction">参数方向</param>
    /// <param name="size">字段长度</param>
    /// <param name="precision">数值有效位数</param>
    /// <param name="scale">数值小数位数</param>
    public SqlParam(string name, object value, DbType? dbType = null, ParameterDirection? direction = null, int? size = null, byte? precision = null, byte? scale = null)
    {
        Name = name;
        Value = value;
        Direction = direction;
        DbType = dbType;
        Size = size;
        Precision = precision;
        Scale = scale;
    }

    /// <summary>
    /// 参数名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 参数值
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// 参数方向
    /// </summary>
    public ParameterDirection? Direction { get; }

    /// <summary>
    /// 参数类型
    /// </summary>
    public DbType? DbType { get; }

    /// <summary>
    /// 字段长度
    /// </summary>
    public int? Size { get; }

    /// <summary>
    /// 数值有效位数
    /// </summary>
    public byte? Precision { get; }

    /// <summary>
    /// 数值小数位数
    /// </summary>
    public byte? Scale { get; }

    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; }

    /// <summary>
    /// 属性名
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType? DatabaseType { get; set; }

    /// <summary>
    /// 数据库角色
    /// </summary>
    public DatabaseRole? DatabaseRole { get; set; }

    /// <summary>
    /// Provider 数据类型名称
    /// </summary>
    public string ProviderTypeName { get; set; }

    /// <summary>
    /// 参数来源
    /// </summary>
    public SqlParameterSource Source { get; set; } = SqlParameterSource.Unknown;

    /// <summary>
    /// 参数元数据等级
    /// </summary>
    public SqlParameterMetadataLevel MetadataLevel { get; set; } = SqlParameterMetadataLevel.None;

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
