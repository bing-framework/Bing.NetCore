using System.Data;
using System.Data.Common;
using System.Text.Json;
using Bing.Auditing.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证 SQLite 审计存储的持久化和事务边界。
/// </summary>
public sealed class DbAuditingStoreTest
{
    /// <summary>
    /// 不同租户的精确身份保存同一审计标识时互不合并。
    /// </summary>
    [Fact]
    public async Task TransactionalSave_WhenTenantIdentitiesDiffer_ShouldPreserveExactValues()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var tenants = new[] { "café", "CAFÉ", "café ", " café", "cafe\u0301", "\ud83e\udde9" };
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite);
        foreach (var tenant in tenants)
        {
            await using var transaction = await database.Connection.BeginTransactionAsync();
            await store.SaveAsync(transaction, "shared-audit", CreateInfo(tenant));
            await transaction.CommitAsync();
        }
        await using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT TenantId FROM bing_audit_log";
        await using var reader = await command.ExecuteReaderAsync();
        var stored = new List<string>();
        while (await reader.ReadAsync())
            stored.Add(reader.GetString(0));
        Assert.Equal(tenants.OrderBy(value => value, StringComparer.Ordinal),
            stored.OrderBy(value => value, StringComparer.Ordinal));
    }

    /// <summary>
    /// 不能无损编码的租户标识在插入前被拒绝。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task TransactionalSave_WhenTenantHasUnpairedSurrogate_ShouldReject(int kind)
    {
        await using var database = await AuditDatabase.CreateAsync();
        await using var transaction = await database.Connection.BeginTransactionAsync();
        var tenant = "tenant-" + (kind == 0 ? '\ud800' : '\udc00');
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite);
        await Assert.ThrowsAsync<System.Text.EncoderFallbackException>(() =>
            store.SaveAsync(transaction, "invalid-tenant", CreateInfo(tenant)));
        await transaction.RollbackAsync();
        Assert.Equal(0, await database.CountAuditAsync());
    }

    /// <summary>
    /// 租户允许保留空白不扩大审计标识和用户标识的允许范围。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TransactionalSave_WhenAuditOrUserHasSurroundingWhitespace_ShouldReject(bool auditIdentity)
    {
        await using var database = await AuditDatabase.CreateAsync();
        await using var transaction = await database.Connection.BeginTransactionAsync();
        var info = CreateInfo("tenant");
        if (!auditIdentity)
            info.UserId = "user ";
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveAsync(transaction, auditIdentity ? "audit " : "audit", info));
        await transaction.RollbackAsync();
        Assert.Equal(0, await database.CountAuditAsync());
    }

    /// <summary>
    /// 普通保存应自行管理连接并持久化基础字段。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenUsingOwnConnection_ShouldPersistAndDisposeConnection()
    {
        await using var database = await AuditDatabase.CreateAsync();
        SqliteConnection ownedConnection = null;
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite, () =>
        {
            ownedConnection = new SqliteConnection(database.ConnectionString);
            return ownedConnection;
        });
        var info = CreateInfo("tenant-a");

        await store.SaveAsync(info);

        Assert.Equal(ConnectionState.Closed, ownedConnection.State);
        using var row = await database.ReadSingleAuditAsync();
        Assert.Equal("tenant-a", row.RootElement.GetProperty("TenantId").GetString());
        Assert.Equal("Admin", row.RootElement.GetProperty("ApplicationName").GetString());
        Assert.Equal("user-1", row.RootElement.GetProperty("UserId").GetString());
        Assert.Equal("POST", row.RootElement.GetProperty("HttpMethod").GetString());
    }

    /// <summary>
    /// 普通保存失败应传播原始数据库异常并释放自有连接。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenTableIsMissing_ShouldPropagateOriginalErrorAndDisposeConnection()
    {
        await using var database = await AuditDatabase.CreateAsync();
        await database.ExecuteAsync("DROP TABLE bing_audit_log");
        SqliteConnection ownedConnection = null;
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite, () =>
        {
            ownedConnection = new SqliteConnection(database.ConnectionString);
            return ownedConnection;
        });

        var exception = await Assert.ThrowsAsync<SqliteException>(() => store.SaveAsync(CreateInfo()));

        Assert.Equal(1, exception.SqliteErrorCode);
        Assert.Equal(ConnectionState.Closed, ownedConnection.State);
    }

    /// <summary>
    /// 连接工厂异常应以原始对象传播。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenConnectionFactoryThrows_ShouldPreserveOriginalException()
    {
        var original = new InvalidOperationException("connection unavailable");
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite, () => throw original);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(CreateInfo()));

        Assert.Same(original, actual);
    }

    /// <summary>
    /// 连接工厂返回已打开连接时拒绝接管调用方资源。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenFactoryReturnsOpenConnection_ShouldNotDisposeBorrowedConnection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite, () => connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(CreateInfo()));

        Assert.Equal(ConnectionState.Open, connection.State);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 查询应严格限定租户并只返回稳定排序的元数据。
    /// </summary>
    [Fact]
    public async Task QueryAsync_ShouldFilterByTenantAndReturnStableMetadata()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var first = CreateInfo("tenant-a");
        first.ExecutionTime = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        await store.SaveAsync(first);
        var second = CreateInfo("tenant-a");
        second.ApplicationName = "Worker";
        second.UserId = "user-2";
        second.ExecutionTime = new DateTime(2026, 10, 5, 8, 1, 0, DateTimeKind.Utc);
        await store.SaveAsync(second);
        var otherTenant = CreateInfo("tenant-b");
        await store.SaveAsync(otherTenant);
        await using (var transaction = await database.Connection.BeginTransactionAsync())
        {
            var critical = CreateInfo("tenant-a");
            critical.ExecutionTime = new DateTime(2026, 10, 5, 8, 2, 0, DateTimeKind.Utc);
            await store.SaveAsync(transaction, "critical-a", critical);
            await transaction.CommitAsync();
        }

        var queryStore = (IAuditLogQueryStore)store;
        var entries = await queryStore.QueryAsync(new AuditLogQuery { TenantId = "tenant-a" });
        Assert.Equal(3, entries.Count);
        Assert.All(entries, entry => Assert.Equal("tenant-a", entry.TenantId));
        Assert.Equal(entries.OrderBy(entry => entry.ExecutionTimeUtc)
            .ThenBy(entry => entry.AuditId, StringComparer.Ordinal), entries);
        Assert.DoesNotContain(entries, entry => entry.ApplicationName == "Other");
        Assert.All(entries, entry => Assert.NotNull(entry.AuditId));

        var page = await queryStore.QueryAsync(new AuditLogQuery
        {
            TenantId = "tenant-a",
            Offset = 1,
            Limit = 1
        });
        Assert.Equal("Worker", Assert.Single(page).ApplicationName);

        var filtered = await queryStore.QueryAsync(new AuditLogQuery
        {
            TenantId = "tenant-a",
            ApplicationName = "Worker",
            UserId = "user-2",
            FromUtc = new DateTime(2026, 10, 5, 8, 1, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 10, 5, 8, 2, 0, DateTimeKind.Utc),
            Limit = 1
        });
        var worker = Assert.Single(filtered);
        Assert.Equal("Worker", worker.ApplicationName);
        Assert.False(worker.IsCritical);

        var criticalEntries = await queryStore.QueryAsync(new AuditLogQuery
        {
            TenantId = "tenant-a",
            IsCritical = true
        });
        var criticalEntry = Assert.Single(criticalEntries);
        Assert.Equal("critical-a", criticalEntry.AuditId);
        Assert.True(criticalEntry.IsCritical);
    }

    /// <summary>
    /// 查询应支持有界分页并在参数无效或取消时不打开连接。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenArgumentsAreInvalid_ShouldRejectBeforeOpeningConnection()
    {
        var opened = false;
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite, () =>
        {
            opened = true;
            return new SqliteConnection("Data Source=:memory:");
        });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery { TenantId = "tenant", Limit = 1001 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery { TenantId = "tenant", Offset = 1000001 }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery
            {
                TenantId = "tenant",
                FromUtc = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Local)
            }));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery { TenantId = "tenant" }, cancellation.Token));
        Assert.False(opened);
    }

    /// <summary>
    /// 查询工厂返回已打开连接时不应接管调用方资源。
    /// </summary>
    [Fact]
    public async Task QueryAsync_WhenFactoryReturnsOpenConnection_ShouldNotDisposeBorrowedConnection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite, () => connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery { TenantId = "tenant" }));

        Assert.Equal(ConnectionState.Open, connection.State);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// 关键审计和业务写入应由调用方同一事务提交。
    /// </summary>
    [Fact]
    public async Task TransactionalSave_WhenCommitted_ShouldPersistBusinessAndAudit()
    {
        await using var database = await AuditDatabase.CreateAsync();
        await using var transaction = await database.Connection.BeginTransactionAsync();
        await database.InsertBusinessAsync(transaction, "committed");
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite);

        await ((ITransactionalAuditingStore)store).SaveAsync(transaction, "audit-1", CreateInfo("tenant-a"));
        Assert.Same(database.Connection, transaction.Connection);
        await transaction.CommitAsync();

        Assert.Equal(1, await database.CountBusinessAsync());
        Assert.Equal(1, await database.CountAuditAsync());
    }

    /// <summary>
    /// 关键审计和业务写入应由调用方同一事务回滚。
    /// </summary>
    [Fact]
    public async Task TransactionalSave_WhenRolledBack_ShouldLeaveNoRecords()
    {
        await using var database = await AuditDatabase.CreateAsync();
        await using var transaction = await database.Connection.BeginTransactionAsync();
        await database.InsertBusinessAsync(transaction, "rolled-back");
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite);

        await ((ITransactionalAuditingStore)store).SaveAsync(transaction, "audit-2", CreateInfo());
        await transaction.RollbackAsync();

        Assert.Equal(0, await database.CountBusinessAsync());
        Assert.Equal(0, await database.CountAuditAsync());
    }

    /// <summary>
    /// 审计唯一约束失败后调用方可回滚整笔业务事务。
    /// </summary>
    [Fact]
    public async Task TransactionalSave_WhenAuditInsertFails_ShouldPreserveErrorAndAllowBusinessRollback()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = (ITransactionalAuditingStore)new DbAuditingStore(AuditLogDatabase.Sqlite);
        await using (var initial = await database.Connection.BeginTransactionAsync())
        {
            await store.SaveAsync(initial, "duplicate", CreateInfo("tenant-a"));
            await initial.CommitAsync();
        }
        await using var transaction = await database.Connection.BeginTransactionAsync();
        await database.InsertBusinessAsync(transaction, "must-rollback");

        var exception = await Assert.ThrowsAsync<SqliteException>(() =>
            store.SaveAsync(transaction, "duplicate", CreateInfo("tenant-a")));
        await transaction.RollbackAsync();

        Assert.Equal(19, exception.SqliteErrorCode);
        Assert.Equal(0, await database.CountBusinessAsync());
        Assert.Equal(1, await database.CountAuditAsync());
    }

    /// <summary>
    /// 取消令牌在插入前取消时不写入审计记录。
    /// </summary>
    [Fact]
    public async Task TransactionalSave_WhenCancelledBeforeInsert_ShouldNotWrite()
    {
        await using var database = await AuditDatabase.CreateAsync();
        await using var transaction = await database.Connection.BeginTransactionAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = (ITransactionalAuditingStore)new DbAuditingStore(AuditLogDatabase.Sqlite);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SaveAsync(transaction, "cancelled", CreateInfo(), cancellation.Token));
        await transaction.RollbackAsync();

        Assert.Equal(0, await database.CountAuditAsync());
    }

    /// <summary>
    /// 明细仅保存白名单属性，且不持久化请求参数或异常正文。
    /// </summary>
    [Fact]
    public async Task SaveAsync_ShouldApplyAllowlistAndExcludeSensitiveDetails()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var options = new AuditLogPersistenceOptions { MaxFieldLength = 8 };
        options.AllowedPropertyNames.Add("Name");
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString), options);
        var info = CreateInfo();
        info.Url = "/secret-url";
        info.Comments.Add("secret-comment");
        info.Actions.Add(new AuditLogActionInfo
        {
            ServiceName = "LongServiceName",
            MethodName = "Save",
            Parameters = "secret-parameter"
        });
        info.Exceptions.Add(new InvalidOperationException("secret-exception"));
        info.EntityChanges.Add(new EntityChangeInfo
        {
            EntityId = "entity-1",
            EntityTypeFullName = "Sample.Entity",
            ChangeType = EntityChangeType.Created,
            PropertyChanges = new List<EntityPropertyChangeInfo>
            {
                new() { PropertyName = "Name", OriginalValue = "old-value-long", NewValue = "new-value-long" },
                new() { PropertyName = "Password", OriginalValue = "secret-old", NewValue = "secret-new" }
            }
        });

        await store.SaveAsync(info);

        var details = await database.ReadDetailsAsync();
        Assert.DoesNotContain("secret-", details);
        Assert.DoesNotContain("/secret-url", details);
        using var json = JsonDocument.Parse(details);
        var action = Assert.Single(json.RootElement.GetProperty("Actions").EnumerateArray());
        Assert.True(action.GetProperty("ServiceName").GetString().Length <= 8);
        Assert.False(action.TryGetProperty("Parameters", out _));
        var change = Assert.Single(json.RootElement.GetProperty("EntityChanges").EnumerateArray());
        Assert.Equal(nameof(EntityChangeType.Created), change.GetProperty("ChangeType").GetString());
        var property = Assert.Single(change.GetProperty("PropertyChanges").EnumerateArray());
        Assert.Equal("Name", property.GetProperty("PropertyName").GetString());
        Assert.True(property.GetProperty("NewValue").GetString().Length <= 8);
        Assert.Equal("System.I", json.RootElement.GetProperty("ExceptionTypes")[0].GetString());
    }

    /// <summary>
    /// 存储构造后选项变化不应扩大字段白名单或长度上限。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenOptionsMutate_ShouldUseConstructionSnapshot()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var options = new AuditLogPersistenceOptions { MaxFieldLength = 4 };
        options.AllowedPropertyNames.Add("Name");
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString), options);
        options.MaxFieldLength = 100;
        options.AllowedPropertyNames.Add("Password");
        var info = CreateInfo();
        info.EntityChanges.Add(new EntityChangeInfo
        {
            EntityId = "entity-1",
            ChangeType = EntityChangeType.Updated,
            PropertyChanges = new List<EntityPropertyChangeInfo>
            {
                new() { PropertyName = "Name", NewValue = "long-name" },
                new() { PropertyName = "Password", NewValue = "secret-password" }
            }
        });

        await store.SaveAsync(info);

        var details = await database.ReadDetailsAsync();
        Assert.DoesNotContain("secret-password", details);
        using var json = JsonDocument.Parse(details);
        var change = Assert.Single(json.RootElement.GetProperty("EntityChanges").EnumerateArray());
        var property = Assert.Single(change.GetProperty("PropertyChanges").EnumerateArray());
        Assert.Equal("Name", property.GetProperty("PropertyName").GetString());
        Assert.True(property.GetProperty("NewValue").GetString().Length <= 4);
    }

    /// <summary>
    /// 超过总负载字节上限时应拒绝保存，不能写入超限正文。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenPayloadExceedsLimit_ShouldNotPersistOversizedDetails()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var options = new AuditLogPersistenceOptions { MaxPayloadBytes = 128 };
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString), options);
        var info = CreateInfo();
        for (var index = 0; index < 20; index++)
            info.Actions.Add(new AuditLogActionInfo { ServiceName = "Service", MethodName = $"Method{index}" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(info));

        Assert.Equal(0, await database.CountAuditAsync());
    }

    /// <summary>
    /// 超过明细数量上限时应拒绝保存且不写入记录。
    /// </summary>
    [Fact]
    public async Task SaveAsync_WhenDetailCountExceedsLimit_ShouldNotPersist()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var options = new AuditLogPersistenceOptions { MaxDetailCount = 1 };
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString), options);
        var info = CreateInfo();
        info.Actions.Add(new AuditLogActionInfo { ServiceName = "First" });
        info.Actions.Add(new AuditLogActionInfo { ServiceName = "Second" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(info));

        Assert.Equal(0, await database.CountAuditAsync());
    }

    /// <summary>
    /// 保留清理只删除过期的普通审计，不删除关键审计或新记录。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_ShouldDeleteOnlyExpiredOrdinaryAudits()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var ordinaryOld = CreateInfo("ordinary-old");
        ordinaryOld.ExecutionTime = cutoff.AddMinutes(-2);
        await store.SaveAsync(ordinaryOld);
        var ordinaryNew = CreateInfo("ordinary-new");
        ordinaryNew.ExecutionTime = cutoff.AddMinutes(2);
        await store.SaveAsync(ordinaryNew);
        await using (var transaction = await database.Connection.BeginTransactionAsync())
        {
            var critical = CreateInfo("critical-old");
            critical.ExecutionTime = cutoff.AddMinutes(-3);
            await store.SaveAsync(transaction, "critical-old", critical);
            await transaction.CommitAsync();
        }

        var deleted = await ((IAuditLogRetentionStore)store).DeleteBeforeAsync(cutoff, 10);

        Assert.Equal(1, deleted);
        Assert.Equal(2, await database.CountAuditAsync());
        Assert.Equal(1, await database.CountByCriticalAsync(0));
        Assert.Equal(1, await database.CountByCriticalAsync(1));
    }

    /// <summary>
    /// 租户范围保留清理只能删除目标租户的过期普通审计。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenTenantIsSpecified_ShouldNotDeleteOtherTenants()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var tenantAOld = CreateInfo("tenant-a");
        tenantAOld.ExecutionTime = cutoff.AddMinutes(-2);
        await store.SaveAsync(tenantAOld);
        var tenantANew = CreateInfo("tenant-a");
        tenantANew.ExecutionTime = cutoff.AddMinutes(2);
        await store.SaveAsync(tenantANew);
        var tenantBOld = CreateInfo("tenant-b");
        tenantBOld.ExecutionTime = cutoff.AddMinutes(-3);
        await store.SaveAsync(tenantBOld);
        await using (var transaction = await database.Connection.BeginTransactionAsync())
        {
            var critical = CreateInfo("tenant-a");
            critical.ExecutionTime = cutoff.AddMinutes(-4);
            await store.SaveAsync(transaction, "critical-tenant-a", critical);
            await transaction.CommitAsync();
        }

        var deleted = await ((IAuditLogTenantRetentionStore)store)
            .DeleteBeforeAsync("tenant-a", cutoff, 10);

        Assert.Equal(1, deleted);
        var tenantA = await ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery
        {
            TenantId = "tenant-a",
            Limit = 100
        });
        var tenantB = await ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery
        {
            TenantId = "tenant-b",
            Limit = 100
        });
        Assert.Equal(2, tenantA.Count);
        Assert.Single(tenantB);
        Assert.Contains(tenantA, item => item.IsCritical);
        Assert.DoesNotContain(tenantB, item => item.IsCritical);
    }

    /// <summary>
    /// 低层租户清理入口应按原值匹配带首尾空白的身份。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenTenantHasBoundaryWhitespace_ShouldDeleteExactTenantOnly()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var exact = CreateInfo(" tenant-a ");
        exact.ExecutionTime = cutoff.AddMinutes(-1);
        await store.SaveAsync(exact);
        var trimmed = CreateInfo("tenant-a");
        trimmed.ExecutionTime = cutoff.AddMinutes(-1);
        await store.SaveAsync(trimmed);

        var deleted = await ((IAuditLogTenantRetentionStore)store)
            .DeleteBeforeAsync(" tenant-a ", cutoff, 1);

        Assert.Equal(1, deleted);
        var exactEntries = await ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery
        {
            TenantId = " tenant-a ",
            Limit = 100
        });
        var trimmedEntries = await ((IAuditLogQueryStore)store).QueryAsync(new AuditLogQuery
        {
            TenantId = "tenant-a",
            Limit = 100
        });
        Assert.Empty(exactEntries);
        Assert.Single(trimmedEntries);
    }

    /// <summary>
    /// 清理入口拒绝非 UTC 截止时间、无界批量和已取消调用。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenArgumentsAreInvalid_ShouldRejectBeforeOpeningConnection()
    {
        var opened = false;
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite, () =>
        {
            opened = true;
            return new SqliteConnection("Data Source=:memory:");
        });
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IAuditLogRetentionStore)store).DeleteBeforeAsync(cutoff.ToLocalTime(), 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ((IAuditLogRetentionStore)store).DeleteBeforeAsync(cutoff, 10001));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff.ToLocalTime(), 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 10001));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IAuditLogArchiveRetentionStore)store).DeleteBeforeAsync(cutoff.ToLocalTime(), 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ((IAuditLogArchiveRetentionStore)store).DeleteBeforeAsync(cutoff, 10001));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((IAuditLogRetentionStore)store).DeleteBeforeAsync(cutoff, 1, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 1, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ((IAuditLogArchiveRetentionStore)store).DeleteBeforeAsync(cutoff, 1, cancellation.Token));

        Assert.False(opened);
    }

    /// <summary>
    /// 清理批量限制应在单次事务内生效。
    /// </summary>
    [Fact]
    public async Task DeleteBeforeAsync_WhenBatchIsLimited_ShouldDeleteAtMostBatchSize()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 3; index++)
        {
            var info = CreateInfo($"ordinary-{index}");
            info.ExecutionTime = cutoff.AddMinutes(-index - 1);
            await store.SaveAsync(info);
        }

        var deleted = await ((IAuditLogRetentionStore)store).DeleteBeforeAsync(cutoff, 2);

        Assert.Equal(2, deleted);
        Assert.Equal(1, await database.CountAuditAsync());
        Assert.Equal(1, await database.CountByCriticalAsync(0));
    }

    /// <summary>
    /// 归档只处理过期普通审计，并保留关键审计和新记录。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_ShouldMoveOnlyExpiredOrdinaryAudits()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        var old = CreateInfo("ordinary-old");
        old.ExecutionTime = cutoff.AddMinutes(-2);
        await store.SaveAsync(old);
        var current = CreateInfo("ordinary-new");
        current.ExecutionTime = cutoff.AddMinutes(2);
        await store.SaveAsync(current);
        await using (var transaction = await database.Connection.BeginTransactionAsync())
        {
            var critical = CreateInfo("critical-old");
            critical.ExecutionTime = cutoff.AddMinutes(-3);
            await store.SaveAsync(transaction, "critical-old", critical);
            await transaction.CommitAsync();
        }

        var archived = await ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 10);

        Assert.Equal(1, archived);
        Assert.Equal(2, await database.CountAuditAsync());
        Assert.Equal(1, await database.CountArchiveAsync());
        Assert.Equal(1, await database.CountByCriticalAsync(1));
        Assert.Equal(0, await database.CountArchiveByCriticalAsync(1));
    }

    /// <summary>
    /// 归档重复遇到同一历史标识时应拒绝身份复用并保留当前主表副本。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenArchiveAlreadyContainsIdentity_ShouldRejectIdentityReuse()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var first = CreateInfo("tenant-a");
        first.ExecutionTime = cutoff.AddMinutes(-1);
        await database.InsertOrdinaryAsync("tenant-a", "same-audit", first.ExecutionTime);

        Assert.Equal(1, await ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 10));
        await database.InsertOrdinaryAsync("tenant-a", "same-audit", cutoff.AddMinutes(-2));

        await Assert.ThrowsAsync<SqliteException>(() =>
            ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 10));
        Assert.Equal(1, await database.CountAuditAsync());
        Assert.Equal(1, await database.CountArchiveAsync());
    }

    /// <summary>
    /// 归档删除失败时应回滚已写入的归档记录和主表删除。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenDeleteFails_ShouldRollbackArchiveInsert()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var old = CreateInfo("tenant-a");
        old.ExecutionTime = cutoff.AddMinutes(-1);
        await store.SaveAsync(old);
        await database.ExecuteAsync("CREATE TRIGGER fail_audit_archive_delete BEFORE DELETE ON bing_audit_log " +
            "BEGIN SELECT RAISE(ABORT, 'archive delete failure'); END");
        try
        {
            await Assert.ThrowsAsync<SqliteException>(() =>
                ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 10));
            Assert.Equal(1, await database.CountAuditAsync());
            Assert.Equal(0, await database.CountArchiveAsync());
        }
        finally
        {
            await database.ExecuteAsync("DROP TRIGGER fail_audit_archive_delete");
        }
    }

    /// <summary>
    /// 归档插入后源记录失去资格时应回滚整个批次，不能删除其他记录补足数量。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenSourceChangesAfterCopy_ShouldRollbackBatch()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        await database.InsertOrdinaryAsync("tenant-a", "first", cutoff.AddMinutes(-2));
        await database.InsertOrdinaryAsync("tenant-a", "second", cutoff.AddMinutes(-1));
        await database.ExecuteAsync("CREATE TRIGGER mutate_audit_after_archive " +
            "AFTER INSERT ON bing_audit_log_archive BEGIN " +
            "UPDATE bing_audit_log SET ExecutionTimeUtc = '9999-12-31 23:59:59.9999999' " +
            "WHERE TenantId = NEW.TenantId AND AuditId = NEW.AuditId; END");
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 2));

            Assert.Contains("归档批次不一致", exception.Message, StringComparison.Ordinal);
            Assert.Equal(2, await database.CountAuditAsync());
            Assert.Equal(0, await database.CountArchiveAsync());
        }
        finally
        {
            await database.ExecuteAsync("DROP TRIGGER mutate_audit_after_archive");
        }
    }

    /// <summary>
    /// 归档批量限制应在单次事务内生效。
    /// </summary>
    [Fact]
    public async Task ArchiveBeforeAsync_WhenBatchIsLimited_ShouldArchiveAtMostBatchSize()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 3; index++)
        {
            var info = CreateInfo($"ordinary-{index}");
            info.ExecutionTime = cutoff.AddMinutes(-index - 1);
            await store.SaveAsync(info);
        }

        var archived = await ((IAuditLogArchiveStore)store).ArchiveBeforeAsync(cutoff, 2);

        Assert.Equal(2, archived);
        Assert.Equal(1, await database.CountAuditAsync());
        Assert.Equal(2, await database.CountArchiveAsync());
    }

    /// <summary>
    /// 归档历史清理只删除过期普通归档，不删除新记录或关键标记记录。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_ShouldDeleteOnlyExpiredOrdinaryArchives()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        await database.InsertArchiveAsync("tenant-a", "old", cutoff.AddDays(-2), cutoff.AddDays(-1), false);
        await database.InsertArchiveAsync("tenant-a", "new", cutoff.AddDays(-1), cutoff.AddDays(1), false);
        await database.InsertArchiveAsync("tenant-a", "critical", cutoff.AddDays(-3), cutoff.AddDays(-2), true);

        var deleted = await ((IAuditLogArchiveRetentionStore)store)
            .DeleteBeforeAsync(cutoff, 10);

        Assert.Equal(1, deleted);
        Assert.Equal(2, await database.CountArchiveAsync());
        Assert.Equal(1, await database.CountArchiveByCriticalAsync(1));
    }

    /// <summary>
    /// 归档历史清理失败时应回滚删除。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_WhenDeleteFails_ShouldKeepHistory()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        await database.InsertArchiveAsync("tenant-a", "old", cutoff.AddDays(-2), cutoff.AddDays(-1), false);
        await database.ExecuteAsync("CREATE TRIGGER fail_audit_archive_retention_delete " +
            "BEFORE DELETE ON bing_audit_log_archive BEGIN SELECT RAISE(ABORT, 'archive retention failure'); END");
        try
        {
            await Assert.ThrowsAsync<SqliteException>(() =>
                ((IAuditLogArchiveRetentionStore)store).DeleteBeforeAsync(cutoff, 10));
            Assert.Equal(1, await database.CountArchiveAsync());
        }
        finally
        {
            await database.ExecuteAsync("DROP TRIGGER fail_audit_archive_retention_delete");
        }
    }

    /// <summary>
    /// 归档历史清理应遵守单批上限。
    /// </summary>
    [Fact]
    public async Task DeleteArchivedBeforeAsync_WhenBatchIsLimited_ShouldDeleteAtMostBatchSize()
    {
        await using var database = await AuditDatabase.CreateAsync();
        var store = new DbAuditingStore(AuditLogDatabase.Sqlite,
            () => new SqliteConnection(database.ConnectionString));
        var cutoff = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 3; index++)
            await database.InsertArchiveAsync("tenant-a", $"old-{index}", cutoff.AddDays(-index - 1),
                cutoff.AddDays(-index - 1), false);

        var deleted = await ((IAuditLogArchiveRetentionStore)store).DeleteBeforeAsync(cutoff, 2);

        Assert.Equal(2, deleted);
        Assert.Equal(1, await database.CountArchiveAsync());
    }

    /// <summary>
    /// 创建含基础审计字段的测试记录。
    /// </summary>
    private static AuditLogInfo CreateInfo(string tenant = null) => new()
    {
        TenantId = tenant,
        ApplicationName = "Admin",
        UserId = "user-1",
        HttpMethod = "POST",
        HttpStatusCode = 201,
        ExecutionTime = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc),
        ExecutionDuration = 12,
        CorrelationId = "correlation-1"
    };

    /// <summary>
    /// 为每个测试提供独立的共享内存 SQLite 数据库。
    /// </summary>
    private sealed class AuditDatabase : IAsyncDisposable
    {
        /// <summary>
        /// 初始化一个 <see cref="AuditDatabase" /> 类型的实例。
        /// </summary>
        private AuditDatabase(SqliteConnection connection, string connectionString)
        {
            Connection = connection;
            ConnectionString = connectionString;
        }

        /// <summary>
        /// 获取保持内存数据库存活的连接。
        /// </summary>
        public SqliteConnection Connection { get; }

        /// <summary>
        /// 获取当前独立数据库的连接字符串。
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// 创建数据库及业务、审计表。
        /// </summary>
        public static async Task<AuditDatabase> CreateAsync()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"audit-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared
            }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            var database = new AuditDatabase(connection, connectionString);
            foreach (var statement in AuditLogSchema.GetCreateStatements(AuditLogDatabase.Sqlite))
                await database.ExecuteAsync(statement);
            await database.ExecuteAsync("CREATE TABLE audit_business (Name TEXT NOT NULL)");
            return database;
        }

        /// <summary>
        /// 执行建表或夹具命令。
        /// </summary>
        public async Task ExecuteAsync(string sql)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 在指定事务内写入业务记录。
        /// </summary>
        public async Task InsertBusinessAsync(DbTransaction transaction, string name)
        {
            await using var command = Connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "INSERT INTO audit_business (Name) VALUES (@name)";
            command.Parameters.AddWithValue("@name", name);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 插入指定标识的普通审计夹具记录。
        /// </summary>
        public async Task InsertOrdinaryAsync(string tenantId, string auditId, DateTime executionTime)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = "INSERT INTO bing_audit_log " +
                "(TenantId, AuditId, ExecutionTimeUtc, ExecutionDuration, DetailsJson, IsCritical) " +
                "VALUES (@tenant, @audit, @time, 0, '{}', 0)";
            command.Parameters.AddWithValue("@tenant", tenantId);
            command.Parameters.AddWithValue("@audit", auditId);
            command.Parameters.AddWithValue("@time", executionTime);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 插入指定时间的归档夹具记录。
        /// </summary>
        public async Task InsertArchiveAsync(string tenantId, string auditId, DateTime executionTime,
            DateTime archivedAtUtc, bool critical)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = "INSERT INTO bing_audit_log_archive " +
                "(TenantId, AuditId, ExecutionTimeUtc, ExecutionDuration, DetailsJson, IsCritical, ArchivedAtUtc) " +
                "VALUES (@tenant, @audit, @time, 0, '{}', @critical, @archived)";
            command.Parameters.AddWithValue("@tenant", tenantId);
            command.Parameters.AddWithValue("@audit", auditId);
            command.Parameters.AddWithValue("@time", executionTime);
            command.Parameters.AddWithValue("@critical", critical ? 1 : 0);
            command.Parameters.AddWithValue("@archived", archivedAtUtc);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 查询业务记录数量。
        /// </summary>
        public Task<long> CountBusinessAsync() => CountAsync("audit_business");

        /// <summary>
        /// 查询审计记录数量。
        /// </summary>
        public Task<long> CountAuditAsync() => CountAsync("bing_audit_log");

        /// <summary>
        /// 查询审计归档记录数量。
        /// </summary>
        public Task<long> CountArchiveAsync() => CountAsync("bing_audit_log_archive");

        /// <summary>
        /// 按关键审计标记统计记录数量。
        /// </summary>
        public async Task<long> CountByCriticalAsync(int critical)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM bing_audit_log WHERE IsCritical = @critical";
            command.Parameters.AddWithValue("@critical", critical);
            return (long)await command.ExecuteScalarAsync();
        }

        /// <summary>
        /// 按关键标记统计审计归档记录数量。
        /// </summary>
        public async Task<long> CountArchiveByCriticalAsync(int critical)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM bing_audit_log_archive WHERE IsCritical = @critical";
            command.Parameters.AddWithValue("@critical", critical);
            return (long)await command.ExecuteScalarAsync();
        }

        /// <summary>
        /// 查询指定测试表中的记录数量。
        /// </summary>
        private async Task<long> CountAsync(string table)
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table}";
            return (long)await command.ExecuteScalarAsync();
        }

        /// <summary>
        /// 读取唯一审计行的基础字段。
        /// </summary>
        public async Task<JsonDocument> ReadSingleAuditAsync()
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = "SELECT TenantId, ApplicationName, UserId, HttpMethod FROM bing_audit_log";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var json = JsonSerializer.Serialize(new
            {
                TenantId = reader.GetString(0),
                ApplicationName = reader.GetString(1),
                UserId = reader.GetString(2),
                HttpMethod = reader.GetString(3)
            });
            Assert.False(await reader.ReadAsync());
            return JsonDocument.Parse(json);
        }

        /// <summary>
        /// 读取唯一审计行的明细 JSON。
        /// </summary>
        public async Task<string> ReadDetailsAsync()
        {
            await using var command = Connection.CreateCommand();
            command.CommandText = "SELECT DetailsJson FROM bing_audit_log";
            var value = await command.ExecuteScalarAsync();
            return Assert.IsType<string>(value);
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
