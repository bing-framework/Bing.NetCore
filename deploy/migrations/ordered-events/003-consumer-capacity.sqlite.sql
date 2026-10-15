-- 有序事件消费者全局容量计数表。
-- 先执行建表，再回填当前未完成收件记录；应用不会自动修改生产表结构。
CREATE TABLE IF NOT EXISTS bing_event_consumer_capacity (
    ConsumerKey TEXT NOT NULL PRIMARY KEY,
    PendingCount INTEGER NOT NULL
);

INSERT OR REPLACE INTO bing_event_consumer_capacity (ConsumerKey, PendingCount)
SELECT ConsumerKey, COUNT(*)
FROM bing_event_inbox
WHERE Status <> 2
GROUP BY ConsumerKey;
