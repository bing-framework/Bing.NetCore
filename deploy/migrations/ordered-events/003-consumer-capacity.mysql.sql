-- 有序事件消费者全局容量计数表。
-- 先执行建表，再回填当前未完成收件记录；应用不会自动修改生产表结构。
CREATE TABLE IF NOT EXISTS bing_event_consumer_capacity (
    ConsumerKey varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    PendingCount bigint NOT NULL,
    PRIMARY KEY (ConsumerKey)
) ENGINE=InnoDB;

INSERT INTO bing_event_consumer_capacity (ConsumerKey, PendingCount)
SELECT ConsumerKey, COUNT(*)
FROM bing_event_inbox
WHERE Status <> 2
GROUP BY ConsumerKey
ON DUPLICATE KEY UPDATE PendingCount = VALUES(PendingCount);
