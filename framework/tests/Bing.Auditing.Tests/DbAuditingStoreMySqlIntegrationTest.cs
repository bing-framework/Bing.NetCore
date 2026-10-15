using System.Text;
using Bing.Auditing.Persistence;
using Bing.Test.Shared;
using MySqlConnector;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证数据库审计存储的真实 MySQL 归档事务。
/// </summary>
/// <remarks>测试只使用随机租户并在结束时删除自身记录，不重置共享测试数据库。</remarks>
public sealed class DbAuditingStoreMySqlIntegrationTest
{
    /// <summary>
    /// 真实 MySQL 归档应只移动有界批次中的过期普通审计。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task ArchiveBeforeAsync_WhenMySql_ShouldMoveExactBatch()
    {
        var connectionString = IntegrationTestConnectionStringResolver.Resolve("MySql");
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
        await using var setup = new MySqlConnection(connectionString);
        await setup.OpenAsync();
        foreach (var statement in AuditLogSchema.GetCreateStatements(AuditLogDatabase.MySql))
            await ExecuteAsync(setup, statement);

        var tenant = "audit-archive-" + Guid.NewGuid().ToString("N");
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var oldTime = cutoff.AddMinutes(-1);
        var currentTime = cutoff.AddMinutes(1);
        var oldFirst = "old-first-" + Guid.NewGuid().ToString("N");
        var oldSecond = "old-second-" + Guid.NewGuid().ToString("N");
        var current = "current-" + Guid.NewGuid().ToString("N");
        try
        {
            await InsertAsync(setup, tenant, oldFirst, oldTime);
            await InsertAsync(setup, tenant, oldSecond, oldTime.AddSeconds(1));
            await InsertAsync(setup, tenant, current, currentTime);

            var store = new DbAuditingStore(AuditLogDatabase.MySql,
                () => new MySqlConnection(connectionString));
            var archived = await ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 2);

            Assert.Equal(2, archived);
            Assert.Equal(1, await CountAsync(setup, "bing_audit_log", tenant));
            Assert.Equal(2, await CountAsync(setup, "bing_audit_log_archive", tenant));
            Assert.Equal(2, await CountByIdsAsync(setup, "bing_audit_log_archive", tenant,
                oldFirst, oldSecond));
            Assert.Equal(0, await CountByIdsAsync(setup, "bing_audit_log", tenant,
                oldFirst, oldSecond));
            Assert.Equal(1, await CountByIdsAsync(setup, "bing_audit_log", tenant, current));
        }
        finally
        {
            await DeleteTenantAsync(setup, "bing_audit_log_archive", tenant);
            await DeleteTenantAsync(setup, "bing_audit_log", tenant);
        }
    }

    /// <summary>
    /// 真实 MySQL 留存清理应只删除普通审计并遵守批量上限。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task DeleteBeforeAsync_WhenMySql_ShouldKeepCriticalAndRespectBatch()
    {
        var connectionString = IntegrationTestConnectionStringResolver.Resolve("MySql");
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
        await using var setup = new MySqlConnection(connectionString);
        await setup.OpenAsync();
        foreach (var statement in AuditLogSchema.GetCreateStatements(AuditLogDatabase.MySql))
            await ExecuteAsync(setup, statement);

        var tenant = "audit-retention-" + Guid.NewGuid().ToString("N");
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var oldFirst = "old-first-" + Guid.NewGuid().ToString("N");
        var oldSecond = "old-second-" + Guid.NewGuid().ToString("N");
        var critical = "critical-" + Guid.NewGuid().ToString("N");
        var current = "current-" + Guid.NewGuid().ToString("N");
        try
        {
            await InsertAsync(setup, tenant, oldFirst, cutoff.AddMinutes(-3));
            await InsertAsync(setup, tenant, oldSecond, cutoff.AddMinutes(-2));
            await InsertAsync(setup, tenant, critical, cutoff.AddMinutes(-1), true);
            await InsertAsync(setup, tenant, current, cutoff.AddMinutes(1));

            var store = new DbAuditingStore(AuditLogDatabase.MySql,
                () => new MySqlConnection(connectionString));
            var deleted = await ((IAuditLogRetentionStore)store).DeleteBeforeAsync(cutoff, 1);

            Assert.Equal(1, deleted);
            Assert.Equal(3, await CountAsync(setup, "bing_audit_log", tenant));
            Assert.Equal(0, await CountByIdsAsync(setup, "bing_audit_log", tenant, oldFirst));
            Assert.Equal(1, await CountByIdsAsync(setup, "bing_audit_log", tenant, oldSecond));
            Assert.Equal(1, await CountByIdsAsync(setup, "bing_audit_log", tenant, critical));
            Assert.Equal(1, await CountByIdsAsync(setup, "bing_audit_log", tenant, current));
        }
        finally
        {
            await DeleteTenantAsync(setup, "bing_audit_log_archive", tenant);
            await DeleteTenantAsync(setup, "bing_audit_log", tenant);
        }
    }

    /// <summary>
    /// 真实 MySQL 查询应按租户和关键标记返回安全元数据投影。
    /// </summary>
    [IntegrationFact("MySql")]
    public async Task QueryAsync_WhenMySql_ShouldReturnTenantScopedMetadata()
    {
        var connectionString = IntegrationTestConnectionStringResolver.Resolve("MySql");
        IntegrationDatabaseSafetyValidator.EnsureDatabaseOperationAllowed(connectionString, "MySql");
        await using var setup = new MySqlConnection(connectionString);
        await setup.OpenAsync();
        foreach (var statement in AuditLogSchema.GetCreateStatements(AuditLogDatabase.MySql))
            await ExecuteAsync(setup, statement);

        var tenant = "audit-query-" + Guid.NewGuid().ToString("N");
        var otherTenant = "audit-query-other-" + Guid.NewGuid().ToString("N");
        var first = "query-first-" + Guid.NewGuid().ToString("N");
        var second = "query-second-" + Guid.NewGuid().ToString("N");
        var other = "query-other-" + Guid.NewGuid().ToString("N");
        var firstTime = DateTime.UtcNow.AddMinutes(-3);
        try
        {
            await InsertAsync(setup, tenant, first, firstTime, false, "app-a", "user-a", "trace-a");
            await InsertAsync(setup, tenant, second, firstTime.AddSeconds(1), true, "app-a", "user-b", "trace-b");
            await InsertAsync(setup, otherTenant, other, firstTime, false, "app-a", "user-a", "trace-other");

            var store = new DbAuditingStore(AuditLogDatabase.MySql,
                () => new MySqlConnection(connectionString));
            var entries = await ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery
            {
                TenantId = tenant,
                ApplicationName = "app-a",
                Limit = 1,
                Offset = 1
            });

            var entry = Assert.Single(entries);
            Assert.Equal(tenant, entry.TenantId);
            Assert.Equal(second, entry.AuditId);
            Assert.True(entry.IsCritical);
            Assert.Equal("user-b", entry.UserId);
            Assert.Equal("trace-b", entry.CorrelationId);
        }
        finally
        {
            await DeleteTenantAsync(setup, "bing_audit_log_archive", tenant);
            await DeleteTenantAsync(setup, "bing_audit_log", tenant);
            await DeleteTenantAsync(setup, "bing_audit_log", otherTenant);
        }
    }

    /// <summary>
    /// 插入一条普通审计夹具记录。
    /// </summary>
    private static async Task InsertAsync(MySqlConnection connection, string tenant, string auditId,
        DateTime executionTimeUtc, bool critical = false, string applicationName = null,
        string userId = null, string correlationId = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO bing_audit_log " +
            "(TenantId, AuditId, ApplicationName, UserId, ExecutionTimeUtc, ExecutionDuration, " +
            "CorrelationId, DetailsJson, IsCritical) " +
            "VALUES (@tenant, @audit, @app, @user, @time, 0, @correlation, '{}', @critical)";
        command.Parameters.AddWithValue("@tenant", Encoding.UTF8.GetBytes(tenant));
        command.Parameters.AddWithValue("@audit", auditId);
        command.Parameters.AddWithValue("@app", (object)applicationName ?? DBNull.Value);
        command.Parameters.AddWithValue("@user", (object)userId ?? DBNull.Value);
        command.Parameters.AddWithValue("@time", executionTimeUtc);
        command.Parameters.AddWithValue("@correlation", (object)correlationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@critical", critical ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 统计指定租户的审计记录数量。
    /// </summary>
    private static async Task<long> CountAsync(MySqlConnection connection, string table, string tenant)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM `{table}` WHERE TenantId = @tenant";
        command.Parameters.AddWithValue("@tenant", Encoding.UTF8.GetBytes(tenant));
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 统计指定租户及审计标识集合的记录数量。
    /// </summary>
    private static async Task<long> CountByIdsAsync(MySqlConnection connection, string table, string tenant,
        params string[] auditIds)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM `{table}` WHERE TenantId = @tenant " +
            "AND AuditId IN (@first, @second)";
        command.Parameters.AddWithValue("@tenant", Encoding.UTF8.GetBytes(tenant));
        command.Parameters.AddWithValue("@first", auditIds[0]);
        command.Parameters.AddWithValue("@second", auditIds.Length > 1 ? auditIds[1] : auditIds[0]);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 删除指定租户的测试记录。
    /// </summary>
    private static async Task DeleteTenantAsync(MySqlConnection connection, string table, string tenant)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM `{table}` WHERE TenantId = @tenant";
        command.Parameters.AddWithValue("@tenant", Encoding.UTF8.GetBytes(tenant));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 执行测试所需的部署语句。
    /// </summary>
    private static async Task ExecuteAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
