-- 有序事件已处理收件历史表迁移。
-- 执行前备份数据库，停止有序事件发布、消费者、留存调度和定时任务。
-- MySQL DDL 会隐式提交，以下语句不是一笔原子事务，也不承诺一键回滚。
-- 新版本清理会先把已处理收件写入该表，再删除 inbox；历史表用于重放期间的事件 ID 去重。
-- 历史表不会由应用自动清理。清理前必须根据允许重放期限、审计和业务幂等策略制定单独的归档方案。

CREATE TABLE IF NOT EXISTS `bing_event_inbox_archive` (
    `ConsumerKey` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `StreamKey` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `EventKey` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `EventId` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Sequence` bigint NOT NULL,
    `Payload` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ContractVersion` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `ArchivedAtUtc` datetime(6) NOT NULL,
    PRIMARY KEY (`ConsumerKey`, `StreamKey`, `Sequence`),
    UNIQUE KEY `UX_bing_event_inbox_archive_event` (`ConsumerKey`, `EventKey`),
    KEY `IX_bing_event_inbox_archive_time` (`ConsumerKey`, `ArchivedAtUtc`)
) ENGINE=InnoDB;
