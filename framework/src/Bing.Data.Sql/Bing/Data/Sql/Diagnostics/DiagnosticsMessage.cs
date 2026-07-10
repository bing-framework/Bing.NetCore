using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;

namespace Bing.Data.Sql.Diagnostics;

/// <summary>
/// Sql 参数诊断信息
/// </summary>
public class SqlParameterDiagnosticInfo
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
    public SqlParameterSource Source { get; set; }

    /// <summary>
    /// 参数元数据等级
    /// </summary>
    public SqlParameterMetadataLevel MetadataLevel { get; set; }

    /// <summary>
    /// 字段存储方式
    /// </summary>
    public ColumnStorageKind StorageKind { get; set; }

    /// <summary>
    /// 字段值转换器类型
    /// </summary>
    public FieldValueConverterKind ConverterKind { get; set; }

    /// <summary>
    /// 自定义转换器名称
    /// </summary>
    public string CustomConverterName { get; set; }
}

/// <summary>
/// SQL 参数诊断快照
/// </summary>
public sealed class SqlParameterDiagnosticSnapshot
{
    /// <summary>
    /// 原始参数对象
    /// </summary>
    public object RawParameters { get; set; }

    /// <summary>
    /// 绑定后的参数对象
    /// </summary>
    public object BoundParameters { get; set; }

    /// <summary>
    /// 参数诊断项
    /// </summary>
    public IReadOnlyCollection<SqlParameterDiagnosticInfo> Items { get; set; } = new List<SqlParameterDiagnosticInfo>();
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
    /// 数据源
    /// </summary>
    public string DataSource { get; set; }

    /// <summary>
    /// 数据源键
    /// </summary>
    public string DataSourceKey { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 连接状态
    /// </summary>
    public ConnectionState State { get; set; }

    /// <summary>
    /// 连接类型
    /// </summary>
    public string ConnectionType { get; set; }
}

/// <summary>
/// SQL 事务诊断信息
/// </summary>
public sealed class SqlTransactionDiagnosticInfo
{
    /// <summary>
    /// 是否存在事务
    /// </summary>
    public bool HasTransaction { get; set; }

    /// <summary>
    /// 事务隔离级别
    /// </summary>
    public IsolationLevel? IsolationLevel { get; set; }

    /// <summary>
    /// 事务类型
    /// </summary>
    public string TransactionType { get; set; }
}

/// <summary>
/// 诊断消息
/// </summary>
public class DiagnosticsMessage
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
    /// Sql参数
    /// </summary>
    public object Parameters { get; set; }

    /// <summary>
    /// 原始 Sql 参数
    /// </summary>
    public object RawParameters { get; set; }

    /// <summary>
    /// 绑定后的 Sql 参数
    /// </summary>
    public object BoundParameters { get; set; }

    /// <summary>
    /// Sql 增强参数元数据
    /// </summary>
    public IReadOnlyCollection<SqlParameterDiagnosticInfo> SqlParametersMetadata { get; set; }

    /// <summary>
    /// SQL 参数诊断快照
    /// </summary>
    public SqlParameterDiagnosticSnapshot ParameterSnapshot { get; set; }

    /// <summary>
    /// SQL 连接诊断信息
    /// </summary>
    public SqlConnectionDiagnosticInfo Connection { get; set; }

    /// <summary>
    /// SQL 事务诊断信息
    /// </summary>
    public SqlTransactionDiagnosticInfo Transaction { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据库
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 异常
    /// </summary>
    public Exception Exception { get; set; }
}

/// <summary>
/// SqlQuery日志诊断 - 执行前消息
/// </summary>
public class SqlQueryDiagnosticBeforeMessage
{
    /// <summary>
    /// Sql语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// Json参数
    /// </summary>
    public string ParameterJson { get; set; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 数据源
    /// </summary>
    public string DataSource { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public long? Timestamp { get; set; }

    /// <summary>
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecuteBefore { get; set; }
}

/// <summary>
/// SqlQuery日志诊断 - 执行后消息
/// </summary>
public class SqlQueryDiagnosticAfterMessage
{
    /// <summary>
    /// Sql语句
    /// </summary>
    public string Sql { get; set; }

    /// <summary>
    /// Json参数
    /// </summary>
    public string ParameterJson { get; set; }

    /// <summary>
    /// 数据源
    /// </summary>
    public string DataSource { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecuteAfter { get; set; }
}

/// <summary>
/// SqlQuery日志诊断 - 异常消息
/// </summary>
public class SqlQueryDiagnosticErrorMessage
{
    /// <summary>
    /// 异常
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// 耗时(ms)
    /// </summary>
    public long? ElapsedMilliseconds { get; set; }

    /// <summary>
    /// 操作标识
    /// </summary>
    public string OperationId { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecuteError { get; set; }
}
