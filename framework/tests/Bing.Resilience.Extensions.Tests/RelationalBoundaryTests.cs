using System.Data.Common;
using Bing.Resilience.Operations;
using Bing.Resilience.Operations.Relational;
using Bing.Resilience.Operations.EntityFrameworkCore;
using Bing.Resilience.Operations.FreeSql;
using Dapper;
using FreeSql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace Bing.Resilience.Extensions.Tests;

public sealed class RelationalBoundaryTests
{
    [MySqlFact]
    public async Task MySqlMigrationAndReadOnlyValidationPreserveAtomicJournalRollback()
    {
        var prefix = "boundary_" + Guid.NewGuid().ToString("N")[..16];
        var store = new RelationalOperationStore(_ => ValueTask.FromResult<DbConnection>(new MySqlConnection(MySqlTestSettings.ConnectionString)), ResilienceRelationalDialect.MySql, prefix);
        await using var connection = new MySqlConnection(MySqlTestSettings.ConnectionString); await connection.OpenAsync();
        try
        {
            await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
            await connection.ExecuteAsync(store.GetMigrationScript());
            await store.ValidateSchemaAsync(); await store.InitializeAsync(); await store.ValidateSchemaAsync();
            await using (var journal = await store.BeginAsync()) Assert.True(await journal.TryInsertAsync(Record("rollback")));
            await using (var journal = await store.BeginAsync()) Assert.Null(await journal.FindAsync("rollback"));
            await connection.ExecuteAsync($"ALTER TABLE {prefix}_attempts MODIFY started_at INT NOT NULL");
            var width = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
            Assert.Contains("started_at 类型不兼容", width.Message);
            await connection.ExecuteAsync($"ALTER TABLE {prefix}_attempts MODIFY started_at BIGINT NOT NULL");
            await connection.ExecuteAsync($"ALTER TABLE {prefix}_operations DROP INDEX {prefix}_operation_key");
            var missing = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
            Assert.Contains("幂等唯一键", missing.Message);
            await connection.ExecuteAsync($"ALTER TABLE {prefix}_attempts ENGINE=MyISAM");
            var engine = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
            Assert.Contains("InnoDB", engine.Message);
        }
        finally { await connection.ExecuteAsync($"DROP TABLE IF EXISTS {prefix}_operations,{prefix}_attempts,{prefix}_audit"); }
    }

    private static OperationRecord Record(string id)
    {
        var now = DateTimeOffset.UtcNow;
        return new(id, "test", new('a', 64), new('b', 64), new('c', 64), "Pending", 0, "{}", 0, now, now, now, now.AddHours(1));
    }

    [Fact]
    public async Task EmbeddedMigrationCreatesExpectedSchemaAndValidationIsReadOnly()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var store = new RelationalOperationStore(_ => ValueTask.FromResult<DbConnection>(new SqliteConnection(database.ConnectionString)), ResilienceRelationalDialect.Sqlite, "custom");
        await using var connection = new SqliteConnection(database.ConnectionString); await connection.OpenAsync();
        await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'custom_%'"));
        await connection.ExecuteAsync(store.GetMigrationScript().Replace("state VARCHAR(64) NOT NULL",
            "state VARCHAR(64) COLLATE 'BINARY' NOT NULL DEFAULT 'COLLATE NOCASE'", StringComparison.Ordinal));
        await store.ValidateSchemaAsync();
        await using (var session = await store.BeginAsync()) { Assert.True(await session.TryInsertAsync(Record("first"))); await session.CommitAsync(); }
        await database.CreateStore().ValidateSchemaAsync(); // 原 InitializeAsync 生成的 v1 可直接被校验。
        await connection.ExecuteAsync("DROP INDEX custom_due");
        var error = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
        Assert.Contains("认领索引", error.Message);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sqlite_master WHERE name='custom_due'"));
    }

    [Fact]
    public async Task SchemaValidationRejectsMissingUniqueKeyAndWrongColumnType()
    {
        await using var database = await DurableSqliteDatabase.CreateAsync(); var store = database.CreateStore();
        await using var connection = new SqliteConnection(database.ConnectionString); await connection.OpenAsync();
        await connection.ExecuteAsync($"DROP TABLE {database.TablePrefix}_operations");
        var sql = store.GetMigrationScript().Replace("UNIQUE(kind, scope_hash, key_hash)", "CHECK(version >= 0)", StringComparison.Ordinal)
            .Replace("version BIGINT", "version TEXT", StringComparison.Ordinal);
        await connection.ExecuteAsync(sql);
        var error = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
        Assert.Contains("类型不兼容", error.Message); Assert.Contains("幂等唯一键", error.Message);
    }

    [Theory]
    [InlineData("id VARCHAR(64) PRIMARY KEY", "id VARCHAR(64) COLLATE NOCASE PRIMARY KEY")]
    [InlineData("state VARCHAR(64) NOT NULL", "state VARCHAR(64) COLLATE /* review */ [NOCASE] NOT NULL")]
    [InlineData("state VARCHAR(64) NOT NULL", "state VARCHAR(64) COLLATE 'NOCASE' NOT NULL")]
    public async Task SqliteSchemaRejectsCaseInsensitiveIdentityAndState(string original, string replacement)
    {
        await using var database = await DurableSqliteDatabase.CreateAsync();
        var store = new RelationalOperationStore(_ => ValueTask.FromResult<DbConnection>(new SqliteConnection(database.ConnectionString)), ResilienceRelationalDialect.Sqlite, "case_probe");
        await using var connection = new SqliteConnection(database.ConnectionString); await connection.OpenAsync();
        await connection.ExecuteAsync(store.GetMigrationScript().Replace(original, replacement, StringComparison.Ordinal));
        var error = await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await store.ValidateSchemaAsync());
        Assert.Contains("BINARY", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EfBridgeSharesTransactionAndNeverCommitsOwner(bool commit)
    {
        await using var database = await DurableSqliteDatabase.CreateAsync(); var store = database.CreateStore();
        await using var context = new Microsoft.EntityFrameworkCore.DbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder().UseSqlite(database.ConnectionString).Options);
        Assert.Throws<ResilienceConfigurationException>(() => store.EnlistEntityFramework(context));
        await using (var owner = await context.Database.BeginTransactionAsync())
        {
            await context.Database.ExecuteSqlRawAsync("INSERT INTO business_rows(value) VALUES ('ef')");
            await using (var journal = store.EnlistEntityFramework(context))
            {
                Assert.False(journal.OwnsTransaction); Assert.True(await journal.TryInsertAsync(Record("ef")));
                await Assert.ThrowsAsync<ResilienceConfigurationException>(async () => await journal.CommitAsync());
            }
            // 释放桥接会话不关闭宿主连接或事务。
            await context.Database.ExecuteSqlRawAsync("UPDATE business_rows SET value='ef-alive'");
            if (commit) await owner.CommitAsync(); else await owner.RollbackAsync();
        }
        Assert.Equal(commit ? 1 : 0, await database.CountBusinessRowsAsync());
        await using var read = await store.BeginAsync(); Assert.Equal(commit, await read.FindAsync("ef") is not null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FreeSqlBridgeUsesExistingWorkUnitAndPreservesOwner(bool commit)
    {
        await using var database = await DurableSqliteDatabase.CreateAsync(); var store = database.CreateStore();
        using (var orm = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, $"Data Source={database.Path}").UseAutoSyncStructure(false).Build())
        {
            using var owner = orm.CreateUnitOfWork();
            Assert.Throws<ResilienceConfigurationException>(() => store.EnlistFreeSql(owner));
            var transaction = owner.GetOrBeginTransaction();
            orm.Ado.ExecuteNonQuery(transaction, "INSERT INTO business_rows(value) VALUES ('free-sql')");
            await using (var journal = store.EnlistFreeSql(owner))
            {
                Assert.Same(transaction, ((IRelationalOperationTransaction)journal).Transaction);
                Assert.True(await journal.TryInsertAsync(Record("free-sql")));
            }
            orm.Ado.ExecuteNonQuery(transaction, "UPDATE business_rows SET value='free-alive'");
            if (commit) owner.Commit(); else owner.Rollback();
        }
        Assert.Equal(commit ? 1 : 0, await database.CountBusinessRowsAsync());
        await using var read = await store.BeginAsync(); Assert.Equal(commit, await read.FindAsync("free-sql") is not null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DapperReusesSameAdoTransactionForBusinessAndJournal(bool commit)
    {
        await using var database = await DurableSqliteDatabase.CreateAsync(); var store = database.CreateStore();
        await using var connection = new SqliteConnection(database.ConnectionString); await connection.OpenAsync();
        await using (var owner = await connection.BeginTransactionAsync())
        {
            await connection.ExecuteAsync("INSERT INTO business_rows(value) VALUES (@value)", new { value = "dapper" }, owner);
            await using (var journal = store.Enlist(connection, owner)) Assert.True(await journal.TryInsertAsync(Record("dapper")));
            if (commit) await owner.CommitAsync(); else await owner.RollbackAsync();
        }
        Assert.Equal(commit ? 1 : 0, await database.CountBusinessRowsAsync());
        await using var read = await store.BeginAsync(); Assert.Equal(commit, await read.FindAsync("dapper") is not null);
    }
}
