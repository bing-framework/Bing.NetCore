using System.Data.Common;
using System.Security.Cryptography;
using Bing.Resilience.Compensation;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Bing.Resilience.Replay;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Resilience.Extensions.Tests;

internal sealed class DurableSqliteDatabase : IAsyncDisposable
{
    private readonly byte[] _keyV1 = RandomNumberGenerator.GetBytes(32);
    private readonly byte[] _keyV2 = RandomNumberGenerator.GetBytes(32);

    private DurableSqliteDatabase(string path, string connectionString, string tablePrefix)
    {
        Path = path;
        ConnectionString = connectionString;
        TablePrefix = tablePrefix;
    }

    public string Path { get; }
    public string ConnectionString { get; }
    public string TablePrefix { get; }

    public static async Task<DurableSqliteDatabase> CreateAsync(TimeProvider? clock = null)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bing-durable-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 10
        }.ToString();
        var database = new DurableSqliteDatabase(path, connectionString, "dr_" + Guid.NewGuid().ToString("N")[..20]);
        await database.CreateStore(clock).InitializeAsync();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE business_rows (id INTEGER PRIMARY KEY AUTOINCREMENT, value TEXT NOT NULL)";
        await command.ExecuteNonQueryAsync();
        return database;
    }

    public RelationalOperationStore CreateStore(TimeProvider? clock = null) => new(
        _ => ValueTask.FromResult<DbConnection>(new SqliteConnection(ConnectionString)),
        ResilienceRelationalDialect.Sqlite,
        TablePrefix,
        clock: clock);

    public AesGcmOperationDataProtector CreateProtector(bool rotatedKeyRing = false) => new(
        rotatedKeyRing ? "durable-tests-v2" : "durable-tests-v1", new Dictionary<string, byte[]>
    {
        ["durable-tests-v1"] = _keyV1,
        ["durable-tests-v2"] = _keyV2
    });

    public async Task<int> CountBusinessRowsAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM business_rows";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var file = Path + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
        CryptographicOperations.ZeroMemory(_keyV1);
        CryptographicOperations.ZeroMemory(_keyV2);
    }
}

internal sealed class DurableWorkflowHost(ServiceProvider provider, AesGcmOperationDataProtector protector) : IDisposable
{
    public ServiceProvider Services { get; } = provider;

    public void Dispose()
    {
        Services.Dispose();
        protector.Dispose();
    }
}

internal static class DurableWorkflowTestSupport
{
    public static ResilienceOperationDescriptor WriteOperation(string name) =>
        new(name, "v1", ResilienceOperationKind.IdempotentWrite, "orders-shared");

    public static ServiceCollection CreateServices(DurableSqliteDatabase database, ResilienceOperationDescriptor operation,
        TimeProvider? clock = null, bool rotatedKeyRing = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(clock ?? TimeProvider.System);
        services.AddSingleton<IResilienceOperationStore>(database.CreateStore(clock));
        services.AddSingleton<IOperationDataProtector>(database.CreateProtector(rotatedKeyRing));
        services.AddBingResilienceOperations(operation);
        return services;
    }

    public static DurableWorkflowHost Build(ServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        return new DurableWorkflowHost(provider, provider.GetRequiredService<IOperationDataProtector>() as AesGcmOperationDataProtector
            ?? throw new InvalidOperationException("The durable tests require the registered AES protector."));
    }

    public static ReplayHandlerRegistration ReplayHandler(string name, string schema, string operationName,
        IReplayCommandHandler handler, bool allowSameKeyRecovery = false) =>
        new(name, schema, operationName, _ => handler, allowSameKeyRecovery);

    public static SagaHandlerRegistration SagaHandler(string name, string version, ISagaStepHandler handler,
        bool usesLocalTransaction = false) => new(name, version, _ => handler, usesLocalTransaction);
}
