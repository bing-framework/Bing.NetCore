-- Bing.Resilience.Operations.Relational SQLite schema v1
-- Replace {{prefix}} with the table prefix configured in RelationalOperationStore.
-- Execute once through the host migration runner; do not call InitializeAsync in production startup.

CREATE TABLE IF NOT EXISTS {{prefix}}_operations (
    id VARCHAR(64) PRIMARY KEY,
    kind VARCHAR(64) NOT NULL,
    scope_hash CHAR(64) NOT NULL,
    key_hash CHAR(64) NOT NULL,
    fingerprint CHAR(64) NOT NULL,
    state VARCHAR(64) NOT NULL,
    version BIGINT NOT NULL,
    data TEXT NOT NULL,
    attempts INT NOT NULL,
    created_at BIGINT NOT NULL,
    updated_at BIGINT NOT NULL,
    next_at BIGINT NOT NULL,
    deadline BIGINT NOT NULL,
    lease_token VARCHAR(64) NULL,
    lease_until BIGINT NULL,
    UNIQUE(kind, scope_hash, key_hash)
);

CREATE TABLE IF NOT EXISTS {{prefix}}_attempts (
    operation_id VARCHAR(64) NOT NULL,
    attempt_no INT NOT NULL,
    kind VARCHAR(64) NOT NULL,
    lease_token VARCHAR(64) NOT NULL,
    started_at BIGINT NOT NULL,
    completed_at BIGINT NULL,
    outcome VARCHAR(64) NULL,
    generation BIGINT NULL,
    endpoint VARCHAR(256) NULL,
    PRIMARY KEY(operation_id, attempt_no)
);

CREATE TABLE IF NOT EXISTS {{prefix}}_audit (
    operation_id VARCHAR(64) NOT NULL,
    version BIGINT NOT NULL,
    event_type VARCHAR(64) NOT NULL,
    actor VARCHAR(128) NOT NULL,
    reason TEXT NOT NULL,
    at_utc BIGINT NOT NULL,
    PRIMARY KEY(operation_id, version, event_type)
);

CREATE INDEX IF NOT EXISTS {{prefix}}_due ON {{prefix}}_operations(kind, next_at, id);
