using Bing.Data.Enums;
using DotNetCore.CAP;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Events.Cap;

internal sealed class CapUnitOfWorkDataSourceRegistration
{
    public CapUnitOfWorkDataSourceRegistration(string dataSourceKey, DatabaseType databaseType,
        string storageConnectionString, Action<CapOptions, string> configureCap,
        Action<IServiceCollection> configureServices, bool publisherOnly)
    {
        if (string.IsNullOrWhiteSpace(dataSourceKey))
            throw new ArgumentException("CAP 数据源键不能为空。", nameof(dataSourceKey));
        if (string.IsNullOrWhiteSpace(storageConnectionString))
            throw new ArgumentException("CAP Outbox 连接字符串不能为空。", nameof(storageConnectionString));
        DataSourceKey = dataSourceKey.Trim();
        DatabaseType = databaseType;
        StorageConnectionString = storageConnectionString;
        ConfigureCap = configureCap ?? throw new ArgumentNullException(nameof(configureCap));
        ConfigureServices = configureServices;
        PublisherOnly = publisherOnly;
    }

    public string DataSourceKey { get; }
    public DatabaseType DatabaseType { get; }
    public string StorageConnectionString { get; }
    public Action<CapOptions, string> ConfigureCap { get; }
    public Action<IServiceCollection> ConfigureServices { get; }
    public bool PublisherOnly { get; }

    public bool EquivalentTo(CapUnitOfWorkDataSourceRegistration other) => other != null &&
        string.Equals(DataSourceKey, other.DataSourceKey, StringComparison.Ordinal) &&
        DatabaseType == other.DatabaseType &&
        string.Equals(StorageConnectionString, other.StorageConnectionString, StringComparison.Ordinal) &&
        Equals(ConfigureCap, other.ConfigureCap) &&
        Equals(ConfigureServices, other.ConfigureServices) && PublisherOnly == other.PublisherOnly;
}

internal sealed class CapUnitOfWorkRegistrationRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CapUnitOfWorkDataSourceRegistration> _registrations =
        new(StringComparer.Ordinal);

    public void Add(CapUnitOfWorkDataSourceRegistration registration)
    {
        lock (_gate)
        {
            if (_registrations.TryGetValue(registration.DataSourceKey, out var existing))
            {
                if (existing.EquivalentTo(registration))
                    return;
                throw new InvalidOperationException(
                    $"CAP 数据源 {registration.DataSourceKey} 已使用不同 runtime 配置注册。");
            }
            _registrations.Add(registration.DataSourceKey, registration);
        }
    }

    public IReadOnlyList<CapUnitOfWorkDataSourceRegistration> Snapshot()
    {
        lock (_gate)
            return _registrations.Values.ToArray();
    }
}
