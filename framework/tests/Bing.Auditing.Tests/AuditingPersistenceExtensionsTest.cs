using System.Data.Common;
using System.Text.Json;
using Bing.Auditing.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证数据库审计存储的服务注册与配置预检。
/// </summary>
public sealed class AuditingPersistenceExtensionsTest
{
    /// <summary>
    /// 显式注册应替换默认日志存储，并让两个契约共享单例。
    /// </summary>
    [Fact]
    public void AddBingDatabaseAuditing_ShouldReplaceDefaultStoreAndShareInstance()
    {
        var services = new ServiceCollection();
        services.AddBingAuditing();
        var factoryCalls = 0;

        var result = services.AddBingDatabaseAuditing(AuditLogDatabase.Sqlite, () =>
        {
            factoryCalls++;
            return new SqliteConnection("Data Source=:memory:");
        });
        using var provider = services.BuildServiceProvider();

        Assert.Same(services, result);
        Assert.Single(services, item => item.ServiceType == typeof(IAuditingStore));
        Assert.Single(services, item => item.ServiceType == typeof(ITransactionalAuditingStore));
        Assert.Single(services, item => item.ServiceType == typeof(IAuditLogRetentionStore));
        Assert.Single(services, item => item.ServiceType == typeof(IAuditLogArchiveStore));
        Assert.Single(services, item => item.ServiceType == typeof(IAuditLogArchiveRetentionStore));
        Assert.Single(services, item => item.ServiceType == typeof(IAuditLogTenantRetentionStore));
        Assert.Single(services, item => item.ServiceType == typeof(IAuditLogQueryStore));
        Assert.Single(services, item => item.ServiceType == typeof(DbAuditingStore));
        var store = provider.GetRequiredService<DbAuditingStore>();
        Assert.Same(store, provider.GetRequiredService<IAuditingStore>());
        Assert.Same(store, provider.GetRequiredService<ITransactionalAuditingStore>());
        Assert.Same(store, provider.GetRequiredService<IAuditLogRetentionStore>());
        Assert.Same(store, provider.GetRequiredService<IAuditLogArchiveStore>());
        Assert.Same(store, provider.GetRequiredService<IAuditLogArchiveRetentionStore>());
        Assert.Same(store, provider.GetRequiredService<IAuditLogTenantRetentionStore>());
        Assert.Same(store, provider.GetRequiredService<IAuditLogQueryStore>());
        Assert.Equal(0, factoryCalls);
    }

    /// <summary>
    /// 重复注册应保留每个契约一条注册并使用最后一次配置。
    /// </summary>
    [Fact]
    public async Task AddBingDatabaseAuditing_WhenRepeated_ShouldKeepSingleLatestStore()
    {
        var path = CreateDatabasePath();
        try
        {
            await CreateAuditTableAsync(path);
            var services = new ServiceCollection();
            services.AddBingDatabaseAuditing(AuditLogDatabase.Sqlite, () => CreateConnection(path),
                options => options.MaxFieldLength = 4);
            var previous = services.Single(item => item.ServiceType == typeof(DbAuditingStore)).ImplementationInstance;
            services.AddBingDatabaseAuditing(AuditLogDatabase.Sqlite, () => CreateConnection(path),
                options => options.MaxFieldLength = 12);
            using var provider = services.BuildServiceProvider();

            Assert.Single(services, item => item.ServiceType == typeof(IAuditingStore));
            Assert.Single(services, item => item.ServiceType == typeof(ITransactionalAuditingStore));
            Assert.Single(services, item => item.ServiceType == typeof(IAuditLogRetentionStore));
            Assert.Single(services, item => item.ServiceType == typeof(IAuditLogArchiveStore));
            Assert.Single(services, item => item.ServiceType == typeof(IAuditLogArchiveRetentionStore));
            Assert.Single(services, item => item.ServiceType == typeof(IAuditLogTenantRetentionStore));
            Assert.Single(services, item => item.ServiceType == typeof(IAuditLogQueryStore));
            Assert.Single(services, item => item.ServiceType == typeof(DbAuditingStore));
            var store = provider.GetRequiredService<DbAuditingStore>();
            Assert.NotSame(previous, store);
            Assert.Same(store, provider.GetRequiredService<IAuditingStore>());
            Assert.Same(store, provider.GetRequiredService<ITransactionalAuditingStore>());
            Assert.Same(store, provider.GetRequiredService<IAuditLogRetentionStore>());
            Assert.Same(store, provider.GetRequiredService<IAuditLogArchiveStore>());
            Assert.Same(store, provider.GetRequiredService<IAuditLogArchiveRetentionStore>());
            Assert.Same(store, provider.GetRequiredService<IAuditLogTenantRetentionStore>());
            Assert.Same(store, provider.GetRequiredService<IAuditLogQueryStore>());
            await provider.GetRequiredService<IAuditingStore>().SaveAsync(new AuditLogInfo
            {
                Actions = new List<AuditLogActionInfo>
                {
                    new() { ServiceName = "abcdefghijklmnop" }
                }
            });
            using var details = JsonDocument.Parse(await ReadDetailsAsync(path));
            Assert.Equal("abcdefghijkl", details.RootElement.GetProperty("Actions")[0]
                .GetProperty("ServiceName").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 回调选项在注册后变化不应改变存储快照。
    /// </summary>
    [Fact]
    public async Task AddBingDatabaseAuditing_WhenConfiguredOptionsMutate_ShouldKeepSnapshot()
    {
        var path = CreateDatabasePath();
        try
        {
            await CreateAuditTableAsync(path);
            var services = new ServiceCollection();
            AuditLogPersistenceOptions configured = null;
            services.AddBingDatabaseAuditing(AuditLogDatabase.Sqlite, () => CreateConnection(path), options =>
            {
                configured = options;
                options.MaxFieldLength = 8;
                options.AllowedPropertyNames.Add("Name");
            });
            configured.MaxFieldLength = 80;
            configured.AllowedPropertyNames.Add("Password");
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAuditingStore>().SaveAsync(new AuditLogInfo
            {
                EntityChanges = new List<EntityChangeInfo>
                {
                    new()
                    {
                        ChangeType = EntityChangeType.Updated,
                        PropertyChanges = new List<EntityPropertyChangeInfo>
                        {
                            new() { PropertyName = "Name", NewValue = "long-name-value" },
                            new() { PropertyName = "Password", NewValue = "secret-password" }
                        }
                    }
                }
            });
            using var details = JsonDocument.Parse(await ReadDetailsAsync(path));
            var change = details.RootElement.GetProperty("EntityChanges")[0];
            var property = Assert.Single(change.GetProperty("PropertyChanges").EnumerateArray());
            Assert.Equal("Name", property.GetProperty("PropertyName").GetString());
            Assert.Equal("long-nam", property.GetProperty("NewValue").GetString());
            Assert.DoesNotContain("secret-password", details.RootElement.GetRawText());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 无效输入应在修改服务集合前失败。
    /// </summary>
    [Fact]
    public void AddBingDatabaseAuditing_WhenInvalid_ShouldNotMutateServices()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            AuditingPersistenceExtensions.AddBingDatabaseAuditing(null, AuditLogDatabase.Sqlite, CreateConnection));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddBingDatabaseAuditing(AuditLogDatabase.Sqlite, null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddBingDatabaseAuditing((AuditLogDatabase)int.MaxValue, CreateConnection));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddBingDatabaseAuditing(AuditLogDatabase.Sqlite, CreateConnection,
                options => options.MaxPayloadBytes = 0));
        Assert.Empty(services);
    }

    /// <summary>
    /// 创建未打开的测试连接，注册阶段不应调用该工厂。
    /// </summary>
    private static DbConnection CreateConnection() => new SqliteConnection("Data Source=:memory:");

    /// <summary>
    /// 创建临时 SQLite 文件路径。
    /// </summary>
    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"bing-audit-{Guid.NewGuid():N}.db");

    /// <summary>
    /// 创建临时数据库审计表。
    /// </summary>
    private static async Task CreateAuditTableAsync(string path)
    {
        await using var connection = CreateConnection(path);
        await connection.OpenAsync();
        foreach (var statement in AuditLogSchema.GetCreateStatements(AuditLogDatabase.Sqlite))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 读取数据库保存的审计明细。
    /// </summary>
    private static async Task<string> ReadDetailsAsync(string path)
    {
        await using var connection = CreateConnection(path);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DetailsJson FROM bing_audit_log";
        return (string)await command.ExecuteScalarAsync();
    }

    /// <summary>
    /// 创建未打开的临时数据库连接。
    /// </summary>
    private static SqliteConnection CreateConnection(string path) => new(new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Pooling = false
    }.ToString());
}
