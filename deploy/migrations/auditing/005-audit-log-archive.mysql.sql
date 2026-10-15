-- 创建普通审计日志归档表。
-- 执行前备份并确认目标环境已经完成 001-004 审计迁移。
-- 归档表只由显式归档服务写入；不要把该迁移当作自动删除策略。

CREATE TABLE IF NOT EXISTS `bing_audit_log_archive` (
    `TenantId` VARBINARY(512) NOT NULL,
    `AuditId` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `ApplicationName` varchar(256) NULL,
    `UserId` varchar(128) NULL,
    `ExecutionTimeUtc` datetime(6) NOT NULL,
    `ExecutionDuration` int NOT NULL,
    `HttpMethod` varchar(32) NULL,
    `HttpStatusCode` int NULL,
    `CorrelationId` varchar(128) NULL,
    `DetailsJson` longtext NOT NULL,
    `IsCritical` tinyint(1) NOT NULL DEFAULT 0,
    `ArchivedAtUtc` datetime(6) NOT NULL,
    PRIMARY KEY (`TenantId`, `AuditId`),
    KEY `IX_bing_audit_log_archive_time` (`ArchivedAtUtc`, `TenantId`, `AuditId`),
    KEY `IX_bing_audit_log_archive_tenant` (`TenantId`, `ArchivedAtUtc`, `AuditId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
