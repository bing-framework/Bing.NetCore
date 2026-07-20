using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// Sql 参数诊断信息
/// </summary>
public sealed class SqlParameterDiagnosticInfo
{
    /// <summary>
    /// 参数名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 参数值
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// 原始参数值
    /// </summary>
    public object OriginalValue { get; set; }

    /// <summary>
    /// 是否敏感参数
    /// </summary>
    public bool IsSensitive { get; set; }

    /// <summary>
    /// 参数类型
    /// </summary>
    public DbType? DbType { get; set; }

    /// <summary>
    /// 参数方向
    /// </summary>
    public ParameterDirection? Direction { get; set; }

    /// <summary>
    /// 字段长度
    /// </summary>
    public int? Size { get; set; }

    /// <summary>
    /// 数值有效位数
    /// </summary>
    public byte? Precision { get; set; }

    /// <summary>
    /// 数值小数位数
    /// </summary>
    public byte? Scale { get; set; }

    /// <summary>
    /// 实体类型名称
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// 属性名
    /// </summary>
    public string PropertyName { get; set; }

    /// <summary>
    /// 列名
    /// </summary>
    public string ColumnName { get; set; }

    /// <summary>
    /// Provider 数据类型名称
    /// </summary>
    public string ProviderTypeName { get; set; }

    /// <summary>
    /// 参数来源
    /// </summary>
    public SqlParameterSource Source { get; set; }

    /// <summary>
    /// 参数元数据等级
    /// </summary>
    public SqlParameterMetadataLevel MetadataLevel { get; set; }

}

/// <summary>
/// SQL 参数诊断快照
/// </summary>
public sealed class SqlParameterDiagnosticSnapshot
{
    /// <summary>
    /// 原始参数类型名称
    /// </summary>
    public string OriginalParameterType { get; set; }

    /// <summary>
    /// 是否通过元数据绑定参数
    /// </summary>
    public bool IsMetadataBound { get; set; }

    /// <summary>
    /// 参数诊断项
    /// </summary>
    public IReadOnlyList<SqlParameterDiagnosticInfo> Items { get; set; } = Array.Empty<SqlParameterDiagnosticInfo>();
}

/// <summary>
/// SQL 连接诊断信息
/// </summary>
public sealed class SqlConnectionDiagnosticInfo
{
    /// <summary>
    /// 数据库
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// 数据源标识
    /// </summary>
    public string DbKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 连接来源
    /// </summary>
    public SqlConnectionSource Source { get; set; }

    /// <summary>
    /// 连接资源所有权
    /// </summary>
    public SqlResourceOwnership Ownership { get; set; }

    /// <summary>
    /// 是否只读数据源
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// 读取偏好
    /// </summary>
    public SqlReadPreference ReadPreference { get; set; }
}

/// <summary>
/// SQL 事务诊断信息
/// </summary>
public sealed class SqlTransactionDiagnosticInfo
{
    /// <summary>
    /// 事务标识
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// 是否存在事务
    /// </summary>
    public bool HasTransaction { get; set; }

    /// <summary>
    /// 事务隔离级别
    /// </summary>
    public IsolationLevel? IsolationLevel { get; set; }

    /// <summary>
    /// 事务资源所有权
    /// </summary>
    public SqlResourceOwnership Ownership { get; set; }

    /// <summary>
    /// 是否为主库读取短事务
    /// </summary>
    public bool IsPrimaryReadTransaction { get; set; }
}

/// <summary>
/// 诊断消息
/// </summary>
public sealed class DiagnosticsMessage
{
    /// <summary>
    /// 当前时间戳
    /// </summary>
    public long? Timestamp { get; set; }

    /// <summary>
    /// 操作
    /// </summary>
    public string Operation { get; set; }

    /// <summary>
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Sql语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// 映射配置名称。
    /// </summary>
    public string MappingProfile { get; set; }

    /// <summary>
    /// 租户标识，仅在调用方显式启用诊断租户输出时提供。
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// SQL 参数诊断快照
    /// </summary>
    public SqlParameterDiagnosticSnapshot Parameters { get; set; }

    /// <summary>
    /// SQL 连接诊断信息
    /// </summary>
    public SqlConnectionDiagnosticInfo Connection { get; set; }

    /// <summary>
    /// SQL 事务诊断信息
    /// </summary>
    public SqlTransactionDiagnosticInfo Transaction { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 异常
    /// </summary>
    public Exception Exception { get; set; }
}
