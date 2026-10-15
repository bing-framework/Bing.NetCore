namespace Bing.Auditing.Persistence;
/// <summary>
/// 提供审计日志表的首次部署语句。
/// </summary>
public static class AuditLogSchema
{
    /// <summary>
    /// 取得审计日志建表语句。
    /// </summary>
    /// <remarks>部署时显式执行；不替代现有表的版本迁移或数据保留策略。</remarks>
    public static IReadOnlyList<string> GetCreateStatements(AuditLogDatabase database)
    {
        switch (database)
        {
            case AuditLogDatabase.MySql:
                return new[]
                {
                    "CREATE TABLE IF NOT EXISTS `bing_audit_log` (" +
                    "TenantId VARBINARY(512) NOT NULL, " +
                    "AuditId varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
                    "ApplicationName varchar(256) NULL, UserId varchar(128) NULL, " +
                    "ExecutionTimeUtc datetime(6) NOT NULL, ExecutionDuration int NOT NULL, " +
                    "HttpMethod varchar(32) NULL, HttpStatusCode int NULL, CorrelationId varchar(128) NULL, " +
                    "DetailsJson longtext NOT NULL, IsCritical tinyint(1) NOT NULL DEFAULT 0, " +
                    "PRIMARY KEY (TenantId, AuditId), KEY IX_bing_audit_log_retention (IsCritical, ExecutionTimeUtc), " +
                    "KEY IX_bing_audit_log_tenant_retention (TenantId, IsCritical, ExecutionTimeUtc, AuditId)) " +
                    "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                    "CREATE TABLE IF NOT EXISTS `bing_audit_log_archive` (" +
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
                    "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
                };
            case AuditLogDatabase.Sqlite:
                return new[]
                {
                    "CREATE TABLE IF NOT EXISTS bing_audit_log (" +
                    "TenantId TEXT COLLATE BINARY NOT NULL, AuditId TEXT COLLATE BINARY NOT NULL, " +
                    "ApplicationName TEXT NULL, UserId TEXT NULL, ExecutionTimeUtc TEXT NOT NULL, " +
                    "ExecutionDuration INTEGER NOT NULL, HttpMethod TEXT NULL, HttpStatusCode INTEGER NULL, " +
                    "CorrelationId TEXT NULL, DetailsJson TEXT NOT NULL, IsCritical INTEGER NOT NULL DEFAULT 0, " +
                    "PRIMARY KEY (TenantId, AuditId))",
                    "CREATE INDEX IF NOT EXISTS IX_bing_audit_log_retention ON bing_audit_log (IsCritical, ExecutionTimeUtc)",
                    "CREATE INDEX IF NOT EXISTS IX_bing_audit_log_tenant_retention ON bing_audit_log (TenantId, IsCritical, ExecutionTimeUtc, AuditId)",
                    "CREATE TABLE IF NOT EXISTS bing_audit_log_archive (" +
                    "TenantId TEXT COLLATE BINARY NOT NULL, AuditId TEXT COLLATE BINARY NOT NULL, " +
                    "ApplicationName TEXT NULL, UserId TEXT NULL, ExecutionTimeUtc TEXT NOT NULL, " +
                    "ExecutionDuration INTEGER NOT NULL, HttpMethod TEXT NULL, HttpStatusCode INTEGER NULL, " +
                    "CorrelationId TEXT NULL, DetailsJson TEXT NOT NULL, IsCritical INTEGER NOT NULL DEFAULT 0, " +
                    "ArchivedAtUtc TEXT NOT NULL, PRIMARY KEY (TenantId, AuditId))",
                    "CREATE INDEX IF NOT EXISTS IX_bing_audit_log_archive_time ON bing_audit_log_archive (ArchivedAtUtc, TenantId, AuditId)",
                    "CREATE INDEX IF NOT EXISTS IX_bing_audit_log_archive_tenant ON bing_audit_log_archive (TenantId, ArchivedAtUtc, AuditId)"
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(database));
        }
    }
}
