using Bing.Auditing.Persistence;
using Xunit;

namespace Bing.Auditing.Tests;

/// <summary>
/// 验证审计持久化表的数据库建表语句。
/// </summary>
public sealed class AuditLogSchemaTest
{
    /// <summary>
    /// SQLite 建表语句应包含租户范围内唯一的审计标识。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenSqlite_ShouldMatchCompleteStatement()
    {
        const string expected = "CREATE TABLE IF NOT EXISTS bing_audit_log (" +
            "TenantId TEXT COLLATE BINARY NOT NULL, AuditId TEXT COLLATE BINARY NOT NULL, " +
            "ApplicationName TEXT NULL, UserId TEXT NULL, ExecutionTimeUtc TEXT NOT NULL, " +
            "ExecutionDuration INTEGER NOT NULL, HttpMethod TEXT NULL, HttpStatusCode INTEGER NULL, " +
            "CorrelationId TEXT NULL, DetailsJson TEXT NOT NULL, IsCritical INTEGER NOT NULL DEFAULT 0, " +
            "PRIMARY KEY (TenantId, AuditId))";

        var statements = AuditLogSchema.GetCreateStatements(AuditLogDatabase.Sqlite);
        Assert.Equal(6, statements.Count);
        Assert.Equal(expected, statements[0]);
        Assert.Equal("CREATE INDEX IF NOT EXISTS IX_bing_audit_log_retention ON bing_audit_log (IsCritical, ExecutionTimeUtc)",
            statements[1]);
        Assert.Equal("CREATE INDEX IF NOT EXISTS IX_bing_audit_log_tenant_retention ON bing_audit_log (TenantId, IsCritical, ExecutionTimeUtc, AuditId)",
            statements[2]);
        Assert.Equal("CREATE TABLE IF NOT EXISTS bing_audit_log_archive (" +
            "TenantId TEXT COLLATE BINARY NOT NULL, AuditId TEXT COLLATE BINARY NOT NULL, " +
            "ApplicationName TEXT NULL, UserId TEXT NULL, ExecutionTimeUtc TEXT NOT NULL, " +
            "ExecutionDuration INTEGER NOT NULL, HttpMethod TEXT NULL, HttpStatusCode INTEGER NULL, " +
            "CorrelationId TEXT NULL, DetailsJson TEXT NOT NULL, IsCritical INTEGER NOT NULL DEFAULT 0, " +
            "ArchivedAtUtc TEXT NOT NULL, PRIMARY KEY (TenantId, AuditId))", statements[3]);
        Assert.Equal("CREATE INDEX IF NOT EXISTS IX_bing_audit_log_archive_time ON bing_audit_log_archive (ArchivedAtUtc, TenantId, AuditId)",
            statements[4]);
        Assert.Equal("CREATE INDEX IF NOT EXISTS IX_bing_audit_log_archive_tenant ON bing_audit_log_archive (TenantId, ArchivedAtUtc, AuditId)",
            statements[5]);
    }

    /// <summary>
    /// MySQL 建表语句应使用二进制排序规则与复合主键。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenMySql_ShouldMatchCompleteStatement()
    {
        const string expected = "CREATE TABLE IF NOT EXISTS `bing_audit_log` (" +
            "TenantId VARBINARY(512) NOT NULL, " +
            "AuditId varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
            "ApplicationName varchar(256) NULL, UserId varchar(128) NULL, " +
            "ExecutionTimeUtc datetime(6) NOT NULL, ExecutionDuration int NOT NULL, " +
            "HttpMethod varchar(32) NULL, HttpStatusCode int NULL, CorrelationId varchar(128) NULL, " +
            "DetailsJson longtext NOT NULL, IsCritical tinyint(1) NOT NULL DEFAULT 0, " +
            "PRIMARY KEY (TenantId, AuditId), KEY IX_bing_audit_log_retention (IsCritical, ExecutionTimeUtc), " +
            "KEY IX_bing_audit_log_tenant_retention (TenantId, IsCritical, ExecutionTimeUtc, AuditId)) " +
            "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

        var statements = AuditLogSchema.GetCreateStatements(AuditLogDatabase.MySql);
        Assert.Equal(2, statements.Count);
        Assert.Equal(expected, statements[0]);
        Assert.Equal("CREATE TABLE IF NOT EXISTS `bing_audit_log_archive` (" +
            "TenantId VARBINARY(512) NOT NULL, " +
            "AuditId varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
            "ApplicationName varchar(256) NULL, UserId varchar(128) NULL, " +
            "ExecutionTimeUtc datetime(6) NOT NULL, ExecutionDuration int NOT NULL, " +
            "HttpMethod varchar(32) NULL, HttpStatusCode int NULL, CorrelationId varchar(128) NULL, " +
            "DetailsJson longtext NOT NULL, IsCritical tinyint(1) NOT NULL DEFAULT 0, " +
            "ArchivedAtUtc datetime(6) NOT NULL, " +
            "PRIMARY KEY (TenantId, AuditId), " +
            "KEY IX_bing_audit_log_archive_time (ArchivedAtUtc, TenantId, AuditId), " +
            "KEY IX_bing_audit_log_archive_tenant (TenantId, ArchivedAtUtc, AuditId)) " +
            "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", statements[1]);
    }

    /// <summary>
    /// 未知数据库枚举值应被拒绝。
    /// </summary>
    [Fact]
    public void GetCreateStatements_WhenDatabaseIsUnknown_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuditLogSchema.GetCreateStatements((AuditLogDatabase)int.MaxValue));
    }
}
