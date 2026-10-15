-- 为租户范围的审计保留清理增加匹配索引。
-- 执行前备份并确认表结构已包含 TenantId、IsCritical 和 ExecutionTimeUtc。
-- MySQL DDL 可能隐式提交；该迁移不承诺一键回滚。

ALTER TABLE `bing_audit_log`
    ADD KEY `IX_bing_audit_log_tenant_retention`
        (`TenantId`, `IsCritical`, `ExecutionTimeUtc`, `AuditId`);
