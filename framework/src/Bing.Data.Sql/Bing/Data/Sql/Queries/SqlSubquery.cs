using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// 由严格 DTO 投影冻结的类型化派生表。
/// </summary>
/// <typeparam name="TProjection">派生表公开的投影类型。</typeparam>
public sealed class SqlSubquery<TProjection> where TProjection : class
{
    /// <summary>
    /// 初始化类型化派生表。
    /// </summary>
    /// <param name="builder">已冻结投影和参数的独立 SQL Builder。</param>
    /// <param name="alias">派生表别名。</param>
    /// <param name="projectedMembers">允许由外层 Lambda 引用的 DTO 成员名称。</param>
    /// <param name="providerKey">创建派生表时的 Provider 标识。</param>
    /// <param name="dataSourceKey">创建派生表时的数据源标识。</param>
    /// <param name="mappingProfile">创建派生表时的映射配置标识。</param>
    /// <param name="tenantId">创建派生表时的租户标识。</param>
    /// <param name="databaseIdentity">创建派生表时的物理数据库身份。</param>
    /// <param name="executionScope">创建派生表的根查询执行作用域。</param>
    /// <param name="parentQueryContextId">创建派生表的父查询上下文标识。</param>
    internal SqlSubquery(ISqlBuilder builder, string alias, IReadOnlyCollection<string> projectedMembers,
        string providerKey, string dataSourceKey, string mappingProfile, string tenantId,
        SqlDatabaseIdentity databaseIdentity, object executionScope, string parentQueryContextId = null)
    {
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("派生表别名不能为空。", nameof(alias));
        Alias = alias;
        ProjectedMembers = projectedMembers ?? throw new ArgumentNullException(nameof(projectedMembers));
        ProviderKey = providerKey;
        DataSourceKey = dataSourceKey;
        MappingProfile = mappingProfile;
        TenantId = tenantId;
        DatabaseIdentity = CloneDatabaseIdentity(databaseIdentity);
        ExecutionScope = executionScope;
        ParentQueryContextId = parentQueryContextId;
    }

    /// <summary>
    /// 派生表在外层查询中的别名。
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// 已冻结的派生查询 Builder。
    /// </summary>
    internal ISqlBuilder Builder { get; }

    /// <summary>
    /// 允许由外层 Lambda 引用的 DTO 成员名称。
    /// </summary>
    internal IReadOnlyCollection<string> ProjectedMembers { get; }

    /// <summary>
    /// 创建派生表时冻结的 Provider 标识。
    /// </summary>
    internal string ProviderKey { get; }

    /// <summary>
    /// 创建派生表时冻结的数据源标识。
    /// </summary>
    internal string DataSourceKey { get; }

    /// <summary>
    /// 创建派生表时冻结的映射配置标识。
    /// </summary>
    internal string MappingProfile { get; }

    /// <summary>
    /// 创建派生表时冻结的租户标识。
    /// </summary>
    internal string TenantId { get; }

    /// <summary>
    /// 创建派生表时冻结的物理数据库身份。
    /// </summary>
    internal SqlDatabaseIdentity DatabaseIdentity { get; }

    /// <summary>
    /// 创建派生表的根查询执行作用域令牌。
    /// </summary>
    internal object ExecutionScope { get; }

    /// <summary>
    /// 创建派生表的父查询上下文标识。
    /// </summary>
    internal string ParentQueryContextId { get; }

    /// <summary>
    /// 验证派生表可由指定外层 Builder 安全使用。
    /// </summary>
    /// <param name="builder">外层查询 Builder。</param>
    internal void ValidateCompatible(ISqlBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var providerKey = builder.Provider?.Key;
        if (string.IsNullOrWhiteSpace(providerKey) == false &&
            string.IsNullOrWhiteSpace(ProviderKey) == false &&
            string.Equals(providerKey, ProviderKey, StringComparison.OrdinalIgnoreCase) == false)
            throw new NotSupportedException($"类型化派生表 Provider {ProviderKey} 与当前 Provider {providerKey} 不兼容。");

        var sqlBuilder = builder as SqlBuilderBase;
        var context = sqlBuilder?.GetDatabaseContext();
        var dataSourceKey = context?.DataSource?.Key ?? context?.DbKey;
        var mappingProfile = context?.MappingProfile;
        if (IsSameContextValue(TenantId, context?.TenantId) == false)
            throw new NotSupportedException("类型化派生表租户上下文与当前查询不兼容。");
        if (IsSameContextValue(MappingProfile, mappingProfile) == false)
            throw new NotSupportedException($"类型化派生表映射配置 {GetContextValue(MappingProfile)} 与当前映射配置 {GetContextValue(mappingProfile)} 不兼容。");

        var databaseIdentity = sqlBuilder?.GetDatabaseIdentity();
        if (CanUseSamePhysicalDatabase(sqlBuilder, databaseIdentity))
            return;
        if (string.IsNullOrWhiteSpace(dataSourceKey) == false &&
            string.IsNullOrWhiteSpace(DataSourceKey) == false &&
            string.Equals(dataSourceKey, DataSourceKey, StringComparison.OrdinalIgnoreCase) == false)
            throw new NotSupportedException($"类型化派生表数据源 {DataSourceKey} 与当前数据源 {dataSourceKey} 不兼容。");
        if (DatabaseIdentity != null && databaseIdentity != null)
            throw new NotSupportedException("类型化派生表物理数据库身份与当前查询不兼容。");
    }

    /// <summary>
    /// 判断派生表与外层查询是否可安全使用同一物理数据库。
    /// </summary>
    private bool CanUseSamePhysicalDatabase(SqlBuilderBase builder, SqlDatabaseIdentity databaseIdentity)
    {
        if (DatabaseIdentity == null || databaseIdentity == null)
            return false;
        if (DatabaseIdentity.IsComparable && databaseIdentity.IsComparable)
            return DatabaseIdentity.Equals(databaseIdentity);
        return ReferenceEquals(ExecutionScope, builder?.GetExecutionScope());
    }

    /// <summary>
    /// 比较可能为空的隔离上下文值。
    /// </summary>
    private static bool IsSameContextValue(string left, string right) => string.Equals(
        string.IsNullOrWhiteSpace(left) ? string.Empty : left,
        string.IsNullOrWhiteSpace(right) ? string.Empty : right,
        StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 格式化隔离上下文值用于诊断。
    /// </summary>
    private static string GetContextValue(string value) => string.IsNullOrWhiteSpace(value) ? "<默认>" : value;

    /// <summary>
    /// 创建物理数据库身份的不可变快照。
    /// </summary>
    private static SqlDatabaseIdentity CloneDatabaseIdentity(SqlDatabaseIdentity source)
    {
        if (source == null)
            return null;
        return new SqlDatabaseIdentity
        {
            DatabaseType = source.DatabaseType,
            Server = source.Server,
            Port = source.Port,
            Database = source.Database,
            Instance = source.Instance,
            FilePath = source.FilePath,
            ServiceName = source.ServiceName,
            Sid = source.Sid,
            OracleAlias = source.OracleAlias,
            SharedMemoryName = source.SharedMemoryName,
            IsComparable = source.IsComparable,
            IsExclusiveMemory = source.IsExclusiveMemory
        };
    }
}
