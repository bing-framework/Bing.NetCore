-- SQLite 本地或测试环境的 CAP 业务消费收据表迁移。
-- 生产环境仍应由部署流程执行迁移，不应在应用启动时隐式修改结构。

CREATE TABLE IF NOT EXISTS bing_event_consumption (
    Consumer TEXT COLLATE BINARY NOT NULL,
    EventId TEXT COLLATE BINARY NOT NULL,
    ProcessedAtUtc TEXT NOT NULL,
    PRIMARY KEY (Consumer, EventId)
);

CREATE INDEX IF NOT EXISTS IX_bing_event_consumption_retention
    ON bing_event_consumption (Consumer, ProcessedAtUtc, EventId);
