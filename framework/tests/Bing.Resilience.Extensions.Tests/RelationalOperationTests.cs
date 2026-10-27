using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class RelationalOperationTests
{
    [Fact]
    public async Task IdempotencyCommitsBusinessAndResultInOneSqliteTransaction()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var coordinator = new IdempotencyCoordinator(database.Store, TimeProvider.System);
        var actionCalls = 0;
        var descriptor = IdempotentOperation("orders.write");

        var result = await coordinator.ExecuteAsync(
            descriptor, "tenant-a", "request-1", OperationFingerprint.Hash("payload-v1"), DateTimeOffset.UtcNow.AddMinutes(5),
            async (transaction, token) =>
            {
                Interlocked.Increment(ref actionCalls);
                await ExecuteBusinessInsertAsync(transaction, "committed");
                return new OperationResult("order-17");
            });

        Assert.True(result.IsCommitted);
        Assert.Equal(IdempotencyResultKind.Completed, result.Kind);
        Assert.Equal(new OperationResult("order-17"), result.Result);
        Assert.Equal(1, actionCalls);
        Assert.Equal(1, await database.CountBusinessRowsAsync());

        OperationRecord? persisted;
        await using (var journal = await database.Store.BeginAsync())
        {
            persisted = await journal.FindByKeyAsync(
                "idempotency",
                OperationFingerprint.Scope("orders.write", "v1", OperationFingerprint.Scope("orders.write", "v1", "tenant-a")),
                OperationFingerprint.Hash("request-1"));
        }
        Assert.NotNull(persisted);
        Assert.Equal("Succeeded", persisted!.State);
        Assert.Equal(OperationFingerprint.Hash("payload-v1"), persisted.Fingerprint);

        var replay = await coordinator.ExecuteAsync(
            descriptor, "tenant-a", "request-1", OperationFingerprint.Hash("payload-v1"), DateTimeOffset.UtcNow.AddMinutes(5),
            (_, _) =>
            {
                Interlocked.Increment(ref actionCalls);
                return ValueTask.FromResult(new OperationResult("unexpected"));
            });

        Assert.Equal(result.OperationId, replay.OperationId);
        Assert.Equal(new OperationResult("order-17"), replay.Result);
        Assert.Equal(1, actionCalls);
        Assert.Equal(1, await database.CountBusinessRowsAsync());
    }

    [Fact]
    public async Task IdempotencyRollsBackBusinessAndJournalWhenActionFails()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var coordinator = new IdempotencyCoordinator(database.Store, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<OperationResult>(
            IdempotentOperation("orders.rollback"), "tenant-a", "request-rollback", OperationFingerprint.Hash("payload"), DateTimeOffset.UtcNow.AddMinutes(5),
            async (transaction, _) =>
            {
                await ExecuteBusinessInsertAsync(transaction, "rolled-back");
                throw new InvalidOperationException("business action failed");
            }).AsTask());

        Assert.Equal(0, await database.CountBusinessRowsAsync());
        await using var journal = await database.Store.BeginAsync();
        Assert.Null(await journal.FindByKeyAsync(
            "idempotency",
            OperationFingerprint.Scope("orders.rollback", "v1", OperationFingerprint.Scope("orders.rollback", "v1", "tenant-a")),
            OperationFingerprint.Hash("request-rollback")));
    }

    [Fact]
    public async Task CorruptCompletedIdempotencyResultRequiresConfigurationIntervention()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var coordinator = new IdempotencyCoordinator(database.Store, TimeProvider.System);
        var operation = IdempotentOperation("orders.corrupt-result");
        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
        var first = await coordinator.ExecuteAsync(operation, "tenant-a", "same-key",
            OperationFingerprint.Hash("payload"), deadline, (_, _) => ValueTask.FromResult(new OperationResult("first")));

        await using (var journal = await database.Store.BeginAsync())
        {
            var saved = (await journal.FindAsync(first.OperationId))!;
            Assert.True(await journal.CompareExchangeAsync(saved with { Data = "{broken", Version = saved.Version + 1 }, saved.Version));
            await journal.CommitAsync();
        }

        await Assert.ThrowsAsync<ResilienceConfigurationException>(() => coordinator.ExecuteAsync(operation, "tenant-a", "same-key",
            OperationFingerprint.Hash("payload"), deadline, (_, _) => ValueTask.FromResult(new OperationResult("must-not-run"))).AsTask());
    }

    [Fact]
    public async Task IdempotencyRejectsSameKeyWithDifferentRequestFingerprint()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var coordinator = new IdempotencyCoordinator(database.Store, TimeProvider.System);
        var operation = IdempotentOperation("orders.conflict");
        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);

        var created = await coordinator.ExecuteAsync(operation, "tenant-a", "same-key", OperationFingerprint.Hash("body-a"), deadline,
            (_, _) => ValueTask.FromResult(new OperationResult("first")));

        var conflict = await Assert.ThrowsAsync<OperationIdempotencyConflictException>(() => coordinator.ExecuteAsync(
            operation, "tenant-a", "same-key", OperationFingerprint.Hash("body-b"), deadline,
            (_, _) => ValueTask.FromResult(new OperationResult("second"))).AsTask());

        Assert.Equal(created.OperationId, conflict.OperationId);
    }

    [Fact]
    public async Task ConcurrentSameKeyRequestsExecuteBusinessActionOnlyOnce()
    {
        await using var database = await SqliteDatabase.CreateAsync(defaultTimeoutSeconds: 15);
        var coordinator = new IdempotencyCoordinator(database.Store, TimeProvider.System);
        var actionCalls = 0;
        using var actionEntered = new ManualResetEventSlim();
        var releaseAction = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var descriptor = IdempotentOperation("orders.concurrent");
        var deadline = DateTimeOffset.UtcNow.AddMinutes(5);

        var first = Task.Run(async () => await coordinator.ExecuteAsync(
            descriptor, "tenant-a", "concurrent-key", OperationFingerprint.Hash("same-payload"), deadline,
            async (transaction, _) =>
            {
                Interlocked.Increment(ref actionCalls);
                actionEntered.Set();
                await releaseAction.Task;
                await ExecuteBusinessInsertAsync(transaction, "single-write");
                return new OperationResult("single-result");
            }));

        try
        {
            Assert.True(actionEntered.Wait(TimeSpan.FromSeconds(5)));
            var second = Task.Run(async () => await coordinator.ExecuteAsync(
                descriptor, "tenant-a", "concurrent-key", OperationFingerprint.Hash("same-payload"), deadline,
                (_, _) =>
                {
                    Interlocked.Increment(ref actionCalls);
                    return ValueTask.FromResult(new OperationResult("duplicate"));
                }));

            releaseAction.TrySetResult();
            var results = await Task.WhenAll(first, second);

            Assert.Equal(results[0].OperationId, results[1].OperationId);
            Assert.All(results, result => Assert.Equal(new OperationResult("single-result"), result.Result));
            Assert.All(results, result => Assert.True(result.IsCommitted));
            Assert.Equal(1, actionCalls);
            Assert.Equal(1, await database.CountBusinessRowsAsync());
        }
        finally
        {
            releaseAction.TrySetResult();
        }
    }

    [Fact]
    public async Task LeaseClaimRenewalAndVersionTokenFencingRejectStaleWriters()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var record = NewOperation("lease-fencing");
        await InsertOperationAsync(database.Store, record);

        await using var owner = await database.Store.BeginAsync();
        var claimed = await owner.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1));
        Assert.NotNull(claimed);
        Assert.Equal(1, claimed!.Version);
        Assert.False(string.IsNullOrWhiteSpace(claimed.LeaseToken));
        Assert.Null(await owner.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1)));

        Assert.True(await owner.RenewLeaseAsync(claimed.Id, claimed.Version, claimed.LeaseToken!, TimeSpan.FromMinutes(2)));
        Assert.False(await owner.RenewLeaseAsync(claimed.Id, claimed.Version - 1, claimed.LeaseToken!, TimeSpan.FromMinutes(2)));
        Assert.False(await owner.RenewLeaseAsync(claimed.Id, claimed.Version, "stale-token", TimeSpan.FromMinutes(2)));

        var staleVersionUpdate = claimed with { State = "StaleVersion" };
        Assert.False(await owner.CompareExchangeAsync(staleVersionUpdate, claimed.Version - 1));
        var staleTokenUpdate = claimed with { Version = claimed.Version + 1, State = "StaleToken" };
        Assert.False(await owner.CompareExchangeAsync(staleTokenUpdate, claimed.Version, expectedLeaseToken: "stale-token"));

        var validUpdate = claimed with { Version = claimed.Version + 1, State = "Processing", UpdatedAt = DateTimeOffset.UtcNow };
        Assert.True(await owner.CompareExchangeAsync(validUpdate, claimed.Version, expectedLeaseToken: claimed.LeaseToken));
        Assert.False(await owner.CompareExchangeAsync(validUpdate with { State = "StaleAfterCas" }, claimed.Version));
        await owner.CommitAsync();
    }

    [Fact]
    public async Task AttemptLedgerCompletionIsDeduplicatedAndFencedByCurrentLease()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        var record = NewOperation("attempt-fencing");
        await InsertOperationAsync(database.Store, record);

        await using var transaction = await database.Store.BeginAsync();
        var claimed = await transaction.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1));
        Assert.NotNull(claimed);
        var attempt = new OperationAttempt(claimed!.Id, 1, "http", claimed.LeaseToken!, DateTimeOffset.UtcNow,
            Generation: 4, Endpoint: "catalog-primary");
        await transaction.AppendAttemptAsync(attempt);
        await transaction.CompleteAttemptAsync(claimed.Id, 1, claimed.LeaseToken!, "Succeeded", DateTimeOffset.UtcNow);

        var completed = Assert.Single(await transaction.GetAttemptsAsync(claimed.Id));
        Assert.Equal("Succeeded", completed.Outcome);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal(4, completed.Generation);
        Assert.Equal("catalog-primary", completed.Endpoint);

        await Assert.ThrowsAsync<OperationConcurrencyException>(() => transaction.CompleteAttemptAsync(
            claimed.Id, 1, claimed.LeaseToken!, "Duplicate", DateTimeOffset.UtcNow).AsTask());

        var changedLease = claimed with
        {
            Version = claimed.Version + 1,
            LeaseToken = Guid.NewGuid().ToString("N"),
            LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(1),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Assert.True(await transaction.CompareExchangeAsync(changedLease, claimed.Version));
        await Assert.ThrowsAsync<OperationConcurrencyException>(() => transaction.CompleteAttemptAsync(
            claimed.Id, 1, claimed.LeaseToken!, "LateOldOwner", DateTimeOffset.UtcNow).AsTask());
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task EnlistedSqliteTransactionRemainsOwnedByCaller()
    {
        await using var database = await SqliteDatabase.CreateAsync();
        await using var connection = new SqliteConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var externalTransaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        var enlisted = Assert.IsAssignableFrom<IRelationalOperationTransaction>(database.Store.Enlist(connection, externalTransaction));
        Assert.False(enlisted.OwnsTransaction);

        var coordinator = new IdempotencyCoordinator(database.Store, TimeProvider.System);
        var result = await coordinator.ExecuteInTransactionAsync(
            enlisted, IdempotentOperation("orders.external"), "tenant-a", "external-key", OperationFingerprint.Hash("external-fingerprint"),
            DateTimeOffset.UtcNow.AddMinutes(5), async (transaction, _) =>
            {
                await ExecuteBusinessInsertAsync(transaction, "external-transaction");
                return new OperationResult("external-result");
            });
        Assert.False(result.IsCommitted);

        await enlisted.DisposeAsync();
        Assert.Equal(ConnectionState.Open, connection.State);
        await ExecuteSqliteCommandAsync(connection, externalTransaction, "INSERT INTO business_rows (value) VALUES ('caller-still-owns-it')");
        Assert.Equal(2, await CountRowsInTransactionAsync(connection, externalTransaction));
        await externalTransaction.CommitAsync();

        Assert.Equal(2, await database.CountBusinessRowsAsync());
        await using var journal = await database.Store.BeginAsync();
        var saved = await journal.FindByKeyAsync(
            "idempotency",
            OperationFingerprint.Scope("orders.external", "v1", OperationFingerprint.Scope("orders.external", "v1", "tenant-a")),
            OperationFingerprint.Hash("external-key"));
        Assert.NotNull(saved);
        Assert.Equal("Succeeded", saved!.State);
    }

    [MySqlFact]
    public async Task MySqlSkipLockedAllowsTwoWorkersToClaimDifferentDueOperations()
    {
        await using var database = await MySqlDatabase.CreateAsync();
        var firstRecord = NewOperation("mysql-claim-a");
        var secondRecord = NewOperation("mysql-claim-b");
        await InsertOperationAsync(database.Store, firstRecord);
        await InsertOperationAsync(database.Store, secondRecord);

        await using var workerA = await database.Store.BeginAsync();
        var claimA = await workerA.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1));
        Assert.NotNull(claimA);

        await using var workerB = await database.Store.BeginAsync();
        var claimB = await workerB.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1));
        Assert.NotNull(claimB);
        Assert.NotEqual(claimA!.Id, claimB!.Id);
        Assert.Contains(claimA.Id, new[] { firstRecord.Id, secondRecord.Id });
        Assert.Contains(claimB.Id, new[] { firstRecord.Id, secondRecord.Id });

        await workerA.CommitAsync();
        await workerB.CommitAsync();
    }

    private static ResilienceOperationDescriptor IdempotentOperation(string name) =>
        new(name, "v1", ResilienceOperationKind.IdempotentWrite, name);

    private static OperationRecord NewOperation(string id)
    {
        var now = DateTimeOffset.UtcNow;
        return new OperationRecord(
            id, "replay", OperationFingerprint.Hash("scope:" + id), OperationFingerprint.Hash("key:" + id),
            OperationFingerprint.Hash("fingerprint:" + id), "Pending", 0, "{}", 0,
            now, now, now.AddMinutes(-1), now.AddHours(1));
    }

    private static async Task InsertOperationAsync(IResilienceOperationStore store, OperationRecord record)
    {
        await using var transaction = await store.BeginAsync();
        Assert.True(await transaction.TryInsertAsync(record));
        await transaction.CommitAsync();
    }

    private static async ValueTask ExecuteBusinessInsertAsync(IOperationTransaction transaction, string value)
    {
        var relational = Assert.IsAssignableFrom<IRelationalOperationTransaction>(transaction);
        await ExecuteSqliteCommandAsync(relational.Connection, relational.Transaction,
            "INSERT INTO business_rows (value) VALUES ($value)", ("$value", value));
    }

    private static async Task ExecuteSqliteCommandAsync(DbConnection connection, DbTransaction transaction,
        string commandText, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountRowsInTransactionAsync(DbConnection connection, DbTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM business_rows";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed record OperationResult(string OrderId);

    private sealed class SqliteDatabase : IAsyncDisposable
    {
        private SqliteDatabase(string path, string connectionString, RelationalOperationStore store)
        {
            Path = path;
            ConnectionString = connectionString;
            Store = store;
        }

        public string Path { get; }
        public string ConnectionString { get; }
        public RelationalOperationStore Store { get; }

        public static async Task<SqliteDatabase> CreateAsync(int defaultTimeoutSeconds = 5)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bing-relational-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = defaultTimeoutSeconds
            }.ToString();
            var store = new RelationalOperationStore(
                _ => ValueTask.FromResult<DbConnection>(new SqliteConnection(connectionString)),
                ResilienceRelationalDialect.Sqlite,
                tablePrefix: "journal");
            var database = new SqliteDatabase(path, connectionString, store);
            await store.InitializeAsync();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE business_rows (id INTEGER PRIMARY KEY AUTOINCREMENT, value TEXT NOT NULL)";
            await command.ExecuteNonQueryAsync();
            return database;
        }

        public async Task<int> CountBusinessRowsAsync()
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM business_rows";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(Path)) File.Delete(Path);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MySqlDatabase : IAsyncDisposable
    {
        private MySqlDatabase(string connectionString, string prefix, RelationalOperationStore store)
        {
            ConnectionString = connectionString;
            Prefix = prefix;
            Store = store;
        }

        public string ConnectionString { get; }
        public string Prefix { get; }
        public RelationalOperationStore Store { get; }

        public static async Task<MySqlDatabase> CreateAsync()
        {
            var connectionString = MySqlTestSettings.ConnectionString;
            var prefix = "op_" + Guid.NewGuid().ToString("N")[..20];
            var store = new RelationalOperationStore(
                _ => ValueTask.FromResult<DbConnection>(new MySqlConnection(connectionString)),
                ResilienceRelationalDialect.MySql,
                tablePrefix: prefix);
            var database = new MySqlDatabase(connectionString, prefix, store);
            await store.InitializeAsync();
            return database;
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {Prefix}_attempts, {Prefix}_audit, {Prefix}_operations";
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(MySqlTestSettings.ConnectionFileVariable)))
        {
            Skip = $"Set {MySqlTestSettings.ConnectionFileVariable} to enable the local MySQL integration test.";
            return;
        }

        _ = MySqlTestSettings.ConnectionString;
    }
}

internal static class MySqlTestSettings
{
    public const string ConnectionFileVariable = "BING_RESILIENCE_TEST_CONNECTION_FILE";

    public static string ConnectionString
    {
        get
        {
            var path = Environment.GetEnvironmentVariable(ConnectionFileVariable);
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException($"{ConnectionFileVariable} is not set.");
            if (!File.Exists(path))
                throw new FileNotFoundException("The relational integration connection file was not found.", path);

            using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (!document.RootElement.TryGetProperty("mysql", out var mysql) ||
                !mysql.TryGetProperty("connectionString", out var connectionString) ||
                string.IsNullOrWhiteSpace(connectionString.GetString()))
                throw new InvalidDataException("The relational integration file has no mysql.connectionString.");

            return connectionString.GetString()!;
        }
    }
}
