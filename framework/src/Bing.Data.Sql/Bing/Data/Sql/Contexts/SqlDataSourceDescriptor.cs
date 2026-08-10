using Bing.Data.Enums;
using Bing.Data;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 数据源描述信息
/// </summary>
public sealed class SqlDataSourceDescriptor
{
    /// <summary>
    /// 数据源标识
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// SQL Provider 唯一标识。未设置时仅允许使用官方数据库类型兼容映射。
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// 数据库类型，仅供框架内部使用
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 连接字符串配置名称
    /// </summary>
    public string ConnectionStringName { get; set; }

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// 是否为只读数据源。框架会拒绝结构化 Mutation、执行型存储过程和本地事务；原生 SQL 仍由调用方负责权限控制。
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// 实体映射配置名称
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 强制读取主库时采用的策略
    /// </summary>
    public PrimaryReadStrategy PrimaryReadStrategy { get; set; } = PrimaryReadStrategy.None;

    /// <summary>
    /// 主库数据源标识
    /// </summary>
    public string PrimaryDataSourceKey { get; set; }

    /// <summary>
    /// 是否支持本地事务。Doris 等仅通过 MySQL 协议提供分析查询的数据源应设为 false。
    /// </summary>
    public bool SupportsTransactions { get; set; } = true;

    /// <summary>
    /// 已验证的数据源查询语法能力配置。
    /// </summary>
    /// <remarks>
    /// 用于覆盖受服务器版本影响的 Provider 默认能力；未设置的属性继承 Provider 声明。
    /// </remarks>
    public SqlQueryCapabilities QueryCapabilities { get; set; }
}