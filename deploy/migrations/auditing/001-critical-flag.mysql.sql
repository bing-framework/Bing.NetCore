-- Bing audit log retention migration.
-- Run during a controlled deployment after taking a backup and stopping audit cleanup.
-- Existing rows are classified as ordinary because the old schema did not record
-- whether a row was written in a business transaction. Keep the historical rows
-- outside the retention cutoff until they have been reviewed.

ALTER TABLE `bing_audit_log`
    ADD COLUMN `IsCritical` tinyint(1) NOT NULL DEFAULT 0 AFTER `DetailsJson`;

ALTER TABLE `bing_audit_log`
    ADD KEY `IX_bing_audit_log_retention` (`IsCritical`, `ExecutionTimeUtc`);
