-- Apply once to the existing EF Core file-management schema on MySQL 8 / InnoDB.
-- 新表继承目标数据库的字符集和排序规则，不使用服务器的 utf8mb4 默认排序规则。

ALTER TABLE `BingFileRecords` ADD COLUMN `ContentPolicyJson` LONGTEXT NULL;
UPDATE `BingFileRecords`
SET `ContentPolicyJson` = '{}'
WHERE `ContentPolicyJson` IS NULL OR `ContentPolicyJson` = '';
ALTER TABLE `BingFileRecords` MODIFY COLUMN `ContentPolicyJson` LONGTEXT NOT NULL;

ALTER TABLE `BingFileOperations`
    ADD COLUMN `CreatedAt` BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN `FinishedAt` BIGINT NOT NULL DEFAULT 0,
    ADD COLUMN `RecoveryOperationId` CHAR(36) NULL,
    ADD COLUMN `ParentOperationId` CHAR(36) NULL;
UPDATE `BingFileOperations` SET `CreatedAt` = 0 WHERE `CreatedAt` IS NULL;
UPDATE `BingFileOperations` SET `FinishedAt` = 0 WHERE `FinishedAt` IS NULL;

ALTER TABLE `BingFileRecords`
    DROP INDEX `IX_BingFileRecords_ScopeKey_CreatorId_CreatedAt`,
    ADD INDEX `IX_BingFileRecords_ScopeKey_CreatorId_State_CreatedAt_RequestKey`
        (`ScopeKey`, `CreatorId`, `State`, `CreatedAt`, `RequestKey`),
    ADD INDEX `IX_BingFileRecords_ScopeKey_State_CreatedAt_RequestKey`
        (`ScopeKey`, `State`, `CreatedAt`, `RequestKey`),
    ADD INDEX `IX_BingFileRecords_ScopeKey_Container_State_CreatedAt`
        (`ScopeKey`, `Container`, `State`, `CreatedAt`);
ALTER TABLE `BingFileUploadSessions`
    ADD INDEX `IX_BingFileUploadSessions_OwnerId_ExpiresAt_FileId` (`OwnerId`, `ExpiresAt`, `FileId`);
ALTER TABLE `BingFileOperations`
    ADD INDEX `IX_BingFileOperations_CreatedAt_Id` (`CreatedAt`, `Id`),
    ADD INDEX `IX_BingFileOperations_RecoveryOperationId` (`RecoveryOperationId`);

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
    UNIQUE INDEX `IX_BingFileOperationAudits_RequestKey` (`RequestKey`),
    UNIQUE INDEX `IX_BingFileOperationAudits_OperationId` (`OperationId`),
    INDEX `IX_BingFileOperationAudits_ScopeKey_OperationId_CreatedAt` (`ScopeKey`, `OperationId`, `CreatedAt`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileMigrationBatches` (
    `Id` CHAR(36) NOT NULL,
    `RequestKey` VARCHAR(80) NOT NULL,
    `ScopeKey` VARCHAR(80) NOT NULL,
    `ActorId` VARCHAR(200) NOT NULL,
    `SourceStore` VARCHAR(64) NOT NULL,
    `TargetStore` VARCHAR(64) NOT NULL,
    `Container` VARCHAR(64) NULL,
    `State` INT NOT NULL,
    `CreatedAt` BIGINT NOT NULL,
    `AfterCreatedAt` BIGINT NULL,
    `AfterKey` VARCHAR(80) NULL,
    `DiscoveryCompleted` TINYINT(1) NOT NULL,
    `Version` BIGINT NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_BingFileMigrationBatches_RequestKey` (`RequestKey`),
    INDEX `IX_BingFileMigrationBatches_ScopeKey_CreatedAt_Id` (`ScopeKey`, `CreatedAt`, `Id`),
    INDEX `IX_BingFileMigrationBatches_State_CreatedAt_Id` (`State`, `CreatedAt`, `Id`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileMigrationBatchItems` (
    `BatchId` CHAR(36) NOT NULL,
    `FileId` CHAR(36) NOT NULL,
    `SourceVersion` BIGINT NOT NULL,
    `Length` BIGINT NOT NULL,
    `OperationId` CHAR(36) NULL,
    `State` INT NOT NULL,
    `ErrorCode` VARCHAR(80) NULL,
    `Version` BIGINT NOT NULL,
    PRIMARY KEY (`BatchId`, `FileId`),
    INDEX `IX_BingFileMigrationBatchItems_BatchId_State_FileId` (`BatchId`, `State`, `FileId`),
    INDEX `IX_BingFileMigrationBatchItems_OperationId` (`OperationId`)
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
    INDEX `IX_BingFileUploadReservations_ScopeKey_OwnerId` (`ScopeKey`, `OwnerId`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileIoLeases` (
    `Id` CHAR(36) NOT NULL,
    `PoolKey` VARCHAR(128) NOT NULL,
    `Token` VARCHAR(128) NOT NULL,
    `ExpiresAt` BIGINT NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_BingFileIoLeases_PoolKey_ExpiresAt` (`PoolKey`, `ExpiresAt`)
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
    UNIQUE INDEX `IX_BingFileOutboxEvents_FileId_FileVersion_Kind` (`FileId`, `FileVersion`, `Kind`),
    INDEX `IX_BingFileOutboxEvents_Delivered_NextAttemptAt_LeaseUntil` (`Delivered`, `NextAttemptAt`, `LeaseUntil`)
) ENGINE=InnoDB;

INSERT INTO `BingFileUploadReservations` (`FileId`, `ScopeKey`, `OwnerId`, `Bytes`, `CreatedAt`)
SELECT file.`Id`, file.`ScopeKey`, file.`CreatorId`, file.`Length`, file.`CreatedAt`
FROM `BingFileRecords` AS file
INNER JOIN `BingFileUploadSessions` AS session ON session.`FileId` = file.`Id`
WHERE (file.`State` IN (0, 1, 2, 8) AND session.`State` IN (0, 1, 2, 8))
   OR (file.`State` IN (6, 7) AND session.`State` IN (6, 7)
       AND NOT EXISTS (
           SELECT 1 FROM `BingFileOperations` AS abort
           WHERE abort.`FileId` = file.`Id` AND abort.`Kind` = 2 AND abort.`State` = 2
       ));
