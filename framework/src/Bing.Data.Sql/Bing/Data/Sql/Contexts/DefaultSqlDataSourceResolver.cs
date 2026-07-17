using Bing.Data.Enums;
using Bing.Data.Sql.Configs;

namespace Bing.Data.Sql;

/// <summary>
/// 默认 SQL 数据源解析器
/// </summary>
public sealed class DefaultSqlDataSourceResolver : ISqlDataSourceResolver
{
    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlDataSourceResolver"/>类型的实例
    /// </summary>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultSqlDataSourceResolver(SqlMetadataOptions options = null) =>
        _options = options ?? new SqlMetadataOptions();

    /// <inheritdoc />
    public SqlDataSourceDescriptor Resolve(string dbKey = null, DatabaseScopeOptions options = null)
    {
        var requestedKey = string.IsNullOrWhiteSpace(dbKey) ? options?.DbKey : dbKey;
        var isExplicit = string.IsNullOrWhiteSpace(requestedKey) == false;
        if (isExplicit)
        {
            if (_options.DataSources.DataSources.TryGetValue(requestedKey, out var dataSource))
                return ApplyReadPreference(CreateDescriptor(requestedKey, dataSource), options);
            throw CreateDataSourceMissingException(requestedKey);
        }
        return ApplyReadPreference(ResolveDefaultDataSourceOrThrow(), options);
    }

    /// <summary>
    /// 解析默认数据源，无法确定时抛出异常
    /// </summary>
    /// <returns>数据源描述</returns>
    private SqlDataSourceDescriptor ResolveDefaultDataSourceOrThrow()
    {
        var defaultKey = _options.DataSources.DefaultDataSourceKey;
        if (string.IsNullOrWhiteSpace(defaultKey) == false &&
            _options.DataSources.DataSources.TryGetValue(defaultKey, out var defaultDataSource))
            return CreateDescriptor(defaultKey, defaultDataSource);
        if (_options.DataSources.DataSources.Count == 1)
        {
            var item = _options.DataSources.DataSources.First();
            return CreateDescriptor(item.Key, item.Value);
        }
        var configuredKeys = GetConfiguredKeys();
        if (string.IsNullOrWhiteSpace(defaultKey))
            throw new InvalidOperationException(
                $"未能解析 SQL 数据源。请求的 dbKey: <未指定>。当前已配置的数据源 key: {configuredKeys}。缺失配置字段: {nameof(SqlDataSourceOptions.DefaultDataSourceKey)}。");
        throw new InvalidOperationException(
            $"未能解析 SQL 数据源。请求的 dbKey: <未指定>。当前已配置的数据源 key: {configuredKeys}。缺失配置字段: {nameof(SqlDataSourceOptions.DataSources)}[{defaultKey}]。");
    }

    /// <summary>
    /// 创建数据源描述副本
    /// </summary>
    /// <param name="key">数据源标识</param>
    /// <param name="descriptor">数据源描述</param>
    /// <returns>数据源描述副本</returns>
    private SqlDataSourceDescriptor CreateDescriptor(string key, SqlDataSourceDescriptor descriptor)
    {
        if (descriptor == null)
            throw CreateDataSourceMissingException(key);
        var result = new SqlDataSourceDescriptor
        {
            Key = string.IsNullOrWhiteSpace(descriptor.Key) ? key : descriptor.Key,
            DatabaseType = descriptor.DatabaseType,
            ConnectionStringName = descriptor.ConnectionStringName,
            ConnectionString = descriptor.ConnectionString,
            IsReadOnly = descriptor.IsReadOnly,
            MappingProfile = descriptor.MappingProfile,
            PrimaryReadStrategy = descriptor.PrimaryReadStrategy,
            PrimaryDataSourceKey = descriptor.PrimaryDataSourceKey,
            SupportsTransactions = descriptor.SupportsTransactions
        };
        return result;
    }

    /// <summary>
    /// 应用读取偏好
    /// </summary>
    /// <param name="descriptor">数据源描述信息</param>
    /// <param name="options">作用域选项</param>
    /// <returns>数据源描述信息</returns>
    private SqlDataSourceDescriptor ApplyReadPreference(SqlDataSourceDescriptor descriptor, DatabaseScopeOptions options)
    {
        if (descriptor == null || options?.ReadPreference != SqlReadPreference.Primary)
            return descriptor;
        if (descriptor.PrimaryReadStrategy == PrimaryReadStrategy.None)
            return descriptor;
        if (descriptor.PrimaryReadStrategy == PrimaryReadStrategy.Transaction)
            return descriptor;
        if (string.IsNullOrWhiteSpace(descriptor.PrimaryDataSourceKey))
            throw new InvalidOperationException(
                $"数据源 {descriptor.Key} 未配置主库数据源键。请求的 dbKey: {descriptor.Key}。当前已配置的数据源 key: {GetConfiguredKeys()}。缺失配置字段: {nameof(SqlDataSourceDescriptor.PrimaryDataSourceKey)}。");
        if (!_options.DataSources.DataSources.TryGetValue(descriptor.PrimaryDataSourceKey, out var primary))
            throw CreateDataSourceMissingException(descriptor.PrimaryDataSourceKey);
        var result = CreateDescriptor(descriptor.PrimaryDataSourceKey, primary);
        if (result.DatabaseType != descriptor.DatabaseType)
            throw new InvalidOperationException(
                $"数据源 {descriptor.Key} 的 Provider {descriptor.DatabaseType} 与主库数据源 {result.Key} 的 Provider {result.DatabaseType} 不一致，无法建立主库读取关系。");
        if (string.IsNullOrWhiteSpace(result.MappingProfile))
            result.MappingProfile = descriptor.MappingProfile;
        return result;
    }

    /// <summary>
    /// 创建数据源缺失异常
    /// </summary>
    /// <param name="key">数据源标识</param>
    /// <returns>异常</returns>
    private InvalidOperationException CreateDataSourceMissingException(string key)
    {
        var keyText = string.IsNullOrWhiteSpace(key) ? "<未指定>" : key;
        return new InvalidOperationException(
            $"未找到 SQL 数据源。请求的 dbKey: {keyText}。当前已配置的数据源 key: {GetConfiguredKeys()}。缺失配置字段: {nameof(SqlDataSourceOptions.DataSources)}[{keyText}]。");
    }

    /// <summary>
    /// 获取已配置的数据源标识列表
    /// </summary>
    /// <returns>已配置的数据源标识列表</returns>
    private string GetConfiguredKeys() =>
        _options.DataSources.DataSources.Count == 0
            ? "<空>"
            : string.Join(",", _options.DataSources.DataSources.Keys);
}