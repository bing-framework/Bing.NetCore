-- Apply once to the existing FreeSql file-management schema on MySQL 8 / InnoDB.
-- 新表继承目标数据库的字符集和排序规则，避免新旧 GUID 文本列 JOIN 产生 collation 冲突。

-- FreeSql MySQL uses native ENUM (one-based storage), unlike its SQLite integer mapping.
ALTER TABLE `BingFileRecords` MODIFY COLUMN `State`
    ENUM('Pending','Uploading','Completing','Ready','DeletePending','Deleted','Aborted','Failed','Quarantined') NOT NULL;
ALTER TABLE `BingFileUploadSessions` MODIFY COLUMN `State`
    ENUM('Pending','Uploading','Completing','Ready','DeletePending','Deleted','Aborted','Failed','Quarantined') NOT NULL;
ALTER TABLE `BingFileOperations` MODIFY COLUMN `Kind`
    ENUM('Initialize','Complete','Abort','Delete','Migrate','DeleteReplica','Inspect') NOT NULL;

ALTER TABLE `BingFileRecords` ADD COLUMN `ContentPolicyJson` MEDIUMTEXT NULL;
UPDATE `BingFileRecords`
SET `ContentPolicyJson` = '{}'
WHERE `ContentPolicyJson` IS NULL OR `ContentPolicyJson` = '';
ALTER TABLE `BingFileRecords` MODIFY COLUMN `ContentPolicyJson` MEDIUMTEXT NOT NULL;

ALTER TABLE `BingFileOperations`
    ADD COLUMN `CreatedAt` BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN `FinishedAt` BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN `RecoveryOperationId` CHAR(36) NULL,
    ADD COLUMN `ParentOperationId` CHAR(36) NULL;
UPDATE `BingFileOperations` SET `CreatedAt` = 0 WHERE `CreatedAt` IS NULL;
UPDATE `BingFileOperations` SET `FinishedAt` = 0 WHERE `FinishedAt` IS NULL;

ALTER TABLE `BingFileRecords`
    DROP INDEX `IX_BingFileRecords_Scope_Creator_Created`,
    ADD INDEX `IX_BingFileRecords_Scope_Creator_State_Created`
        (`ScopeKey`, `CreatorId`, `State`, `CreatedAt`, `RequestKey`),
    ADD INDEX `IX_BingFileRecords_Scope_State_Created`
        (`ScopeKey`, `State`, `CreatedAt`, `RequestKey`),
    ADD INDEX `IX_BingFileRecords_Scope_Container_State`
        (`ScopeKey`, `Container`, `State`, `CreatedAt`);
ALTER TABLE `BingFileUploadSessions`
    ADD INDEX `IX_BingFileUploadSessions_Owner_Expires` (`OwnerId`, `ExpiresAt`, `FileId`);
ALTER TABLE `BingFileOperations`
    ADD INDEX `IX_BingFileOperations_Created` (`CreatedAt`, `Id`),
    ADD INDEX `IX_BingFileOperations_Recovery` (`RecoveryOperationId`);

CREATE TABLE `BingFileOperationAudits` (
    `Id` CHAR(36) NOT NULL,
    `RequestKey` VARCHAR(80) NOT NULL,
    `ScopeKey` VARCHAR(80) NOT NULL,
    `OperationId` CHAR(36) NOT NULL,
    `ReplacementId` CHAR(36) NOT NULL,
    `FileId` CHAR(36) NOT NULL,
    `ActorId` VARCHAR(200) NOT NULL,
    `Reason` VARCHAR(500) NOT NULL,
    `TraceId` VARCHAR(64) NOT NULL,
    `CreatedAt` BIGINT NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `UX_BingFileOperationAudits_Request` (`RequestKey`),
    UNIQUE INDEX `UX_BingFileOperationAudits_Operation` (`OperationId`),
    INDEX `IX_BingFileOperationAudits_Scope_Operation` (`ScopeKey`, `OperationId`, `CreatedAt`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileMigrationBatches` (
    `Id` CHAR(36) NOT NULL,
    `RequestKey` VARCHAR(80) NOT NULL,
    `ScopeKey` VARCHAR(80) NOT NULL,
    `ActorId` VARCHAR(200) NOT NULL,
    `SourceStore` VARCHAR(64) NOT NULL,
    `TargetStore` VARCHAR(64) NOT NULL,
    `Container` VARCHAR(64) NULL,
    `State` ENUM('Running','Paused','Completed') NOT NULL,
    `CreatedAt` BIGINT NOT NULL,
    `AfterCreatedAt` BIGINT NULL,
    `AfterKey` VARCHAR(80) NULL,
    `DiscoveryCompleted` TINYINT(1) NOT NULL,
    `Version` BIGINT NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `UX_BingFileMigrationBatches_Request` (`RequestKey`),
    INDEX `IX_BingFileMigrationBatches_Scope_Created` (`ScopeKey`, `CreatedAt`, `Id`),
    INDEX `IX_BingFileMigrationBatches_State_Created` (`State`, `CreatedAt`, `Id`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileMigrationBatchItems` (
    `BatchId` CHAR(36) NOT NULL,
    `FileId` CHAR(36) NOT NULL,
    `SourceVersion` BIGINT NOT NULL,
    `Length` BIGINT NOT NULL,
    `OperationId` CHAR(36) NULL,
    `State` ENUM('Pending','Queued','Succeeded','Failed','Conflict') NOT NULL,
    `ErrorCode` VARCHAR(80) NULL,
    `Version` BIGINT NOT NULL,
    PRIMARY KEY (`BatchId`, `FileId`),
    INDEX `IX_BingFileMigrationBatchItems_State` (`BatchId`, `State`, `FileId`),
    INDEX `IX_BingFileMigrationBatchItems_Operation` (`OperationId`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileControlLocks` (
    `Id` VARCHAR(80) NOT NULL,
    `Version` BIGINT NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileUploadReservations` (
    `FileId` CHAR(36) NOT NULL,
    `ScopeKey` VARCHAR(80) NOT NULL,
    `OwnerId` VARCHAR(200) NOT NULL,
    `Bytes` BIGINT NOT NULL,
    `CreatedAt` BIGINT NOT NULL,
    PRIMARY KEY (`FileId`),
    INDEX `IX_BingFileUploadReservations_Scope_Owner` (`ScopeKey`, `OwnerId`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileIoLeases` (
    `Id` CHAR(36) NOT NULL,
    `PoolKey` VARCHAR(128) NOT NULL,
    `Token` VARCHAR(128) NOT NULL,
    `ExpiresAt` BIGINT NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_BingFileIoLeases_Pool_Expires` (`PoolKey`, `ExpiresAt`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileOutboxEvents` (
    `Id` CHAR(36) NOT NULL,
    `FileId` CHAR(36) NOT NULL,
    `ScopeKey` VARCHAR(80) NOT NULL,
    `FileVersion` BIGINT NOT NULL,
    `Kind` VARCHAR(80) NOT NULL,
    `CreatedAt` BIGINT NOT NULL,
    `NextAttemptAt` BIGINT NOT NULL,
    `Attempts` INT NOT NULL,
    `LeaseToken` VARCHAR(128) NULL,
    `LeaseUntil` BIGINT NOT NULL,
    `Delivered` TINYINT(1) NOT NULL,
    `Version` BIGINT NOT NULL,
    `ErrorCode` VARCHAR(200) NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `UX_BingFileOutboxEvents_File_Version_Kind` (`FileId`, `FileVersion`, `Kind`),
    INDEX `IX_BingFileOutboxEvents_Due` (`Delivered`, `NextAttemptAt`, `LeaseUntil`)
) ENGINE=InnoDB;

INSERT INTO `BingFileUploadReservations` (`FileId`, `ScopeKey`, `OwnerId`, `Bytes`, `CreatedAt`)
SELECT file.`Id`, file.`ScopeKey`, file.`CreatorId`, file.`Length`, file.`CreatedAt`
FROM `BingFileRecords` AS file
INNER JOIN `BingFileUploadSessions` AS session ON session.`FileId` = file.`Id`
WHERE (file.`State` IN ('Pending','Uploading','Completing','Quarantined') AND session.`State` IN ('Pending','Uploading','Completing','Quarantined'))
   OR (file.`State` IN ('Aborted','Failed') AND session.`State` IN ('Aborted','Failed')
       AND NOT EXISTS (
           SELECT 1 FROM `BingFileOperations` AS abort
           WHERE abort.`FileId` = file.`Id` AND abort.`Kind` = 'Abort' AND abort.`State` = 'Succeeded'
       ));
