-- 创建普通审计日志归档表。
-- 执行前备份并确认目标环境已经包含 IsCritical 字段和租户清理索引。
-- 归档表只由显式归档服务写入；不要把该迁移当作自动删除策略。

CREATE TABLE IF NOT EXISTS bing_audit_log_archive (
    TenantId TEXT COLLATE BINARY NOT NULL,
    AuditId TEXT COLLATE BINARY NOT NULL,
    ApplicationName TEXT NULL,
    UserId TEXT NULL,
    ExecutionTimeUtc TEXT NOT NULL,
    ExecutionDuration INTEGER NOT NULL,
    HttpMethod TEXT NULL,
    HttpStatusCode INTEGER NULL,
    CorrelationId TEXT NULL,
    DetailsJson TEXT NOT NULL,
    IsCritical INTEGER NOT NULL DEFAULT 0,
    ArchivedAtUtc TEXT NOT NULL,
    PRIMARY KEY (TenantId, AuditId)
);

CREATE INDEX IF NOT EXISTS IX_bing_audit_log_archive_time
    ON bing_audit_log_archive (ArchivedAtUtc, TenantId, AuditId);

CREATE INDEX IF NOT EXISTS IX_bing_audit_log_archive_tenant
    ON bing_audit_log_archive (TenantId, ArchivedAtUtc, AuditId);
