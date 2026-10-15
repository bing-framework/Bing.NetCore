-- CAP 业务消费收据表迁移。
-- 执行前备份数据库，并停止相关消费者、清理任务和业务写入。
-- MySQL DDL 会隐式提交，以下语句不是一笔原子事务，也不承诺一键回滚。
-- Consumer 和 EventId 使用二进制排序，避免大小写折叠造成幂等身份冲突。
-- retention 索引服务于受控的按消费者、时间和批次清理；不代表可以绕过业务重放策略删除。

CREATE TABLE IF NOT EXISTS `bing_event_consumption` (
    `Consumer` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `EventId` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `ProcessedAtUtc` datetime(6) NOT NULL,
    PRIMARY KEY (`Consumer`, `EventId`),
    KEY `IX_bing_event_consumption_retention` (`Consumer`, `ProcessedAtUtc`, `EventId`)
) ENGINE=InnoDB;
