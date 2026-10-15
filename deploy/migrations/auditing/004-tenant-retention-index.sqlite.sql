-- 为租户范围的审计保留清理增加匹配索引。
-- 在受控部署中执行；IF NOT EXISTS 允许重复执行。

CREATE INDEX IF NOT EXISTS IX_bing_audit_log_tenant_retention
    ON bing_audit_log (TenantId, IsCritical, ExecutionTimeUtc, AuditId);
