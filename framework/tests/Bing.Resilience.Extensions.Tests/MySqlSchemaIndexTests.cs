using System.Data.Common;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Dapper;
using MySqlConnector;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class MySqlSchemaIndexTests
{
    [MySqlFact]
    public async Task SchemaValidationAcceptsExpectedDueIndexAndClaimUsesIt()
    {
        await using var database = await MySqlJournal.CreateAsync();
        await database.Store.ValidateSchemaAsync();

        var record = NewOperation("default-index-claim");
        await using (var transaction = await database.Store.BeginAsync())
        {
            Assert.True(await transaction.TryInsertAsync(record));
            await transaction.CommitAsync();
        }

        await using var claim = await database.Store.BeginAsync();
        var claimed = await claim.ClaimDueAsync("replay", new[] { "Pending" }, TimeSpan.FromMinutes(1));
        Assert.Equal(record.Id, claimed?.Id);
        await claim.CommitAsync();
    }

    [MySqlFact]
    public async Task SchemaValidationRejectsEquivalentDueIndexUnderDifferentName()
    {
        await using var database = await MySqlJournal.CreateAsync();
        await database.Connection.ExecuteAsync(
            $"ALTER TABLE {database.Prefix}_operations RENAME INDEX {database.Prefix}_due TO {database.Prefix}_equivalent_due");

        var error = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () =>
            await database.Store.ValidateSchemaAsync());

        Assert.Contains("认领索引", error.Message);
    }

    [MySqlFact]
    public async Task SchemaValidationRejectsMissingDueIndex()
    {
        await using var database = await MySqlJournal.CreateAsync();
        await database.Connection.ExecuteAsync(
            $"ALTER TABLE {database.Prefix}_operations DROP INDEX {database.Prefix}_due");

        var error = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () =>
            await database.Store.ValidateSchemaAsync());

        Assert.Contains("认领索引", error.Message);
    }

    [MySqlFact]
    public async Task SchemaValidationRejectsDueIndexWithWrongColumnOrder()
    {
        await using var database = await MySqlJournal.CreateAsync();
        await database.Connection.ExecuteAsync(
            $"ALTER TABLE {database.Prefix}_operations DROP INDEX {database.Prefix}_due");
        await database.Connection.ExecuteAsync(
            $"CREATE INDEX {database.Prefix}_due ON {database.Prefix}_operations(kind,id,next_at)");

        var error = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () =>
            await database.Store.ValidateSchemaAsync());

        Assert.Contains("认领索引", error.Message);
    }

    private static OperationRecord NewOperation(string id)
    {
        var now = DateTimeOffset.UtcNow;
        return new(id, "replay", new('a', 64), new('b', 64), new('c', 64), "Pending", 0, "{}", 0,
            now, now, now.AddMinutes(-1), now.AddMinutes(5));
    }

    private sealed class MySqlJournal : IAsyncDisposable
    {
        private MySqlJournal(MySqlConnection connection, string prefix, RelationalOperationStore store)
        {
            Connection = connection;
            Prefix = prefix;
            Store = store;
        }

        public MySqlConnection Connection { get; }
        public string Prefix { get; }
        public RelationalOperationStore Store { get; }

        public static async Task<MySqlJournal> CreateAsync()
        {
            var connectionString = MySqlTestSettings.ConnectionString;
            var prefix = "schema_" + Guid.NewGuid().ToString("N")[..16];
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            var store = new RelationalOperationStore(
                _ => ValueTask.FromResult<DbConnection>(new MySqlConnection(connectionString)),
                ResilienceRelationalDialect.MySql,
                prefix);
            var journal = new MySqlJournal(connection, prefix, store);
            try
            {
                await connection.ExecuteAsync(store.GetMigrationScript());
                return journal;
            }
            catch
            {
                await journal.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Connection.ExecuteAsync(
                    $"DROP TABLE IF EXISTS {Prefix}_operations,{Prefix}_attempts,{Prefix}_audit");
            }
            finally
            {
                await Connection.DisposeAsync();
            }
        }
    }
}
