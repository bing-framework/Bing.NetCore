-- 有序事件收件箱最老积压年龄指标的受控迁移。
-- 执行前备份数据库，停止有序事件发布、消费者和定时任务，并确认当前表中不存在同名字段或索引。
-- MySQL DDL 会隐式提交，以下语句不是一笔原子事务，也不承诺一键回滚。
-- 历史记录无法恢复真实入箱时间；回填值只作为明确的 UTC 基线，迁移报告必须记录该事实。
-- 全部实例升级并完成迁移后再恢复写入，不要让旧版本与新版本混跑。

ALTER TABLE `bing_event_inbox`
    ADD COLUMN `CreatedAtUtc` DATETIME(6) NULL,
    ADD INDEX `IX_bing_event_inbox_pending_age` (`ConsumerKey`, `Status`, `CreatedAtUtc`);

UPDATE `bing_event_inbox`
SET `CreatedAtUtc` = UTC_TIMESTAMP(6)
WHERE `CreatedAtUtc` IS NULL;

ALTER TABLE `bing_event_inbox`
    MODIFY COLUMN `CreatedAtUtc` DATETIME(6) NOT NULL;
