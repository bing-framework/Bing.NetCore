using Bing.Data.Enums;
using Bing.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 描述 SQL 数据源的 Provider、连接、映射和读取能力。
/// </summary>
public sealed class SqlDataSourceDescriptor
{
    /// <summary>
    /// 获取或设置数据源的唯一标识。
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// SQL Provider 唯一标识。未设置时仅允许使用官方数据库类型兼容映射。
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// 获取或设置官方 Provider 兼容映射使用的数据库类型。
    /// </summary>
    /// <remarks>
    /// <see cref="ProviderKey"/> 是优先的 Provider 身份；仅在未提供 Provider 键时使用该值进行官方兼容映射。
    /// </remarks>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 获取或设置连接字符串配置名称。
    /// </summary>
    public string ConnectionStringName { get; set; }

    /// <summary>
    /// 获取或设置数据源的直接连接字符串。
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// 是否为只读数据源。框架会拒绝结构化 Mutation、执行型存储过程和本地事务；原生 SQL 仍由调用方负责权限控制。
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// 获取或设置选择实体映射配置的名称；未设置时使用默认映射配置。
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 获取或设置强制读取主库时采用的策略。
    /// </summary>
    public PrimaryReadStrategy PrimaryReadStrategy { get; set; } = PrimaryReadStrategy.None;

    /// <summary>
    /// 获取或设置主库数据源标识。
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