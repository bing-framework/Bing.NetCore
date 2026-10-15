using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.FreeSQL.Auditing;
using FreeSql;
using Xunit;

namespace Bing.FreeSQL.Tests.Events;

/// <summary>
/// 验证真实 FreeSQL 事务内的关键审计。
/// </summary>
public sealed class FreeSqlTransactionalAuditingTest
{
    /// <summary>
    /// 业务和关键审计使用同一事务提交或回滚。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveTransactionalAuditAsync_ShouldShareBusinessTransaction(bool commit)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bing-audit-{Guid.NewGuid():N}.db");
        var orm = CreateOrm(path);
        try
        {
            foreach (var statement in AuditLogSchema.GetCreateStatements(AuditLogDatabase.Sqlite))
                orm.Ado.ExecuteNonQuery(statement);
            orm.Ado.ExecuteNonQuery("CREATE TABLE business (Id INTEGER PRIMARY KEY)");
            using (var unit = orm.CreateUnitOfWork())
            {
                var transaction = unit.GetOrBeginTransaction();
                orm.Ado.ExecuteNonQuery(transaction, "INSERT INTO business (Id) VALUES (1)");
                await unit.SaveTransactionalAuditAsync(new DbAuditingStore(AuditLogDatabase.Sqlite),
                    "request", new AuditLogInfo { TenantId = "tenant", ApplicationName = "test" });
                Assert.Same(transaction, unit.GetOrBeginTransaction(false));
                if (commit)
                    unit.Commit();
                else
                    unit.Rollback();
            }
            Assert.Equal(commit ? 1L : 0L, Convert.ToInt64(orm.Ado.ExecuteScalar("SELECT COUNT(*) FROM business")));
            Assert.Equal(commit ? 1L : 0L, Convert.ToInt64(orm.Ado.ExecuteScalar("SELECT COUNT(*) FROM bing_audit_log")));
        }
        finally
        {
            orm.Dispose();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// 没有活动事务时拒绝关键审计且不隐式开启事务。
    /// </summary>
    [Fact]
    public async Task SaveTransactionalAuditAsync_WhenNoTransaction_ShouldReject()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bing-audit-{Guid.NewGuid():N}.db");
        var orm = CreateOrm(path);
        try
        {
            using var unit = orm.CreateUnitOfWork();
            await Assert.ThrowsAsync<InvalidOperationException>(() => unit.SaveTransactionalAuditAsync(
                new DbAuditingStore(AuditLogDatabase.Sqlite), "request", new AuditLogInfo()));
            Assert.Null(unit.GetOrBeginTransaction(false));
        }
        finally
        {
            orm.Dispose();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// 创建测试用 SQLite ORM。
    /// </summary>
    private static IFreeSql CreateOrm(string path) => new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, $"Data Source={path}")
        .UseAutoSyncStructure(false).Build();

    /// <summary>
    /// 审计 SQL 失败继续传播并允许调用方回滚业务。
    /// </summary>
    [Fact]
    public async Task SaveTransactionalAuditAsync_WhenAuditWriteFails_ShouldAllowBusinessRollback()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bing-audit-{Guid.NewGuid():N}.db");
        var orm = CreateOrm(path);
        try
        {
            orm.Ado.ExecuteNonQuery("CREATE TABLE business (Id INTEGER PRIMARY KEY)");
            using (var unit = orm.CreateUnitOfWork())
            {
                var transaction = unit.GetOrBeginTransaction();
                orm.Ado.ExecuteNonQuery(transaction, "INSERT INTO business (Id) VALUES (1)");
                await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => unit.SaveTransactionalAuditAsync(
                    new DbAuditingStore(AuditLogDatabase.Sqlite), "request", new AuditLogInfo()));
                Assert.Same(transaction, unit.GetOrBeginTransaction(false));
                unit.Rollback();
            }
            Assert.Equal(0L, Convert.ToInt64(orm.Ado.ExecuteScalar("SELECT COUNT(*) FROM business")));
        }
        finally
        {
            orm.Dispose();
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
