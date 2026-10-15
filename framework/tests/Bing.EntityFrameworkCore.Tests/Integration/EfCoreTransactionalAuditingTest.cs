using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.EntityFrameworkCore.Auditing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// 验证真实 EFCore 事务内的关键审计。
/// </summary>
public sealed class EfCoreTransactionalAuditingTest
{
    /// <summary>
    /// 业务和关键审计使用同一事务提交或回滚。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveTransactionalAuditAsync_ShouldShareBusinessTransaction(bool commit)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
        foreach (var statement in AuditLogSchema.GetCreateStatements(AuditLogDatabase.Sqlite))
            await context.Database.ExecuteSqlRawAsync(statement);
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE business (Id INTEGER PRIMARY KEY)");
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.Database.ExecuteSqlRawAsync("INSERT INTO business (Id) VALUES (1)");
            await context.SaveTransactionalAuditAsync(new DbAuditingStore(AuditLogDatabase.Sqlite),
                "request", new AuditLogInfo { TenantId = "tenant", ApplicationName = "test" });
            Assert.Same(transaction, context.Database.CurrentTransaction);
            if (commit)
                await transaction.CommitAsync();
            else
                await transaction.RollbackAsync();
        }
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT (SELECT COUNT(*) FROM business), (SELECT COUNT(*) FROM bing_audit_log)";
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(commit ? 1 : 0, reader.GetInt32(0));
        Assert.Equal(commit ? 1 : 0, reader.GetInt32(1));
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    /// <summary>
    /// 没有活动事务时拒绝关键审计且不隐式开启事务。
    /// </summary>
    [Fact]
    public async Task SaveTransactionalAuditAsync_WhenNoTransaction_ShouldReject()
    {
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:").Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveTransactionalAuditAsync(
            new DbAuditingStore(AuditLogDatabase.Sqlite), "request", new AuditLogInfo()));
        Assert.Null(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// 审计 SQL 失败继续传播原错并允许调用方回滚业务。
    /// </summary>
    [Fact]
    public async Task SaveTransactionalAuditAsync_WhenAuditWriteFails_ShouldAllowBusinessRollback()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new DbContext(new DbContextOptionsBuilder().UseSqlite(connection).Options);
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE business (Id INTEGER PRIMARY KEY)");
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            await context.Database.ExecuteSqlRawAsync("INSERT INTO business (Id) VALUES (1)");
            await Assert.ThrowsAsync<SqliteException>(() => context.SaveTransactionalAuditAsync(
                new DbAuditingStore(AuditLogDatabase.Sqlite), "request", new AuditLogInfo()));
            Assert.Same(transaction, context.Database.CurrentTransaction);
            await transaction.RollbackAsync();
        }
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM business";
        Assert.Equal(0L, (long)await command.ExecuteScalarAsync());
    }
}
