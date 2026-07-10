using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据源描述信息
/// </summary>
public sealed class SqlDataSourceDescriptor
{
    /// <summary>
    /// 数据源键
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// 业务数据库标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 连接字符串名称
    /// </summary>
    public string ConnectionStringName { get; set; }

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// 映射配置标识
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 主库读取策略
    /// </summary>
    public PrimaryReadStrategy PrimaryReadStrategy { get; set; } = PrimaryReadStrategy.None;

    /// <summary>
    /// 主库数据源键
    /// </summary>
    public string PrimaryDataSourceKey { get; set; }
}