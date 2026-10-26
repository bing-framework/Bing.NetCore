-- Apply after the FreeSql file-management enhancement schema on MySQL 8 / InnoDB.
-- New tables inherit the target database's character set and collation.

CREATE TABLE `BingFileEventDeliveries` (
    `EventId` CHAR(36) NOT NULL,
    `Consumer` VARCHAR(100) NOT NULL,
    `Attempts` INT NOT NULL,
    `Delivered` TINYINT(1) NOT NULL,
    `NextAttemptAt` BIGINT NOT NULL,
    `CompletedAt` BIGINT NOT NULL,
    `ErrorCode` VARCHAR(200) NULL,
    PRIMARY KEY (`EventId`, `Consumer`),
    INDEX `IX_BingFileEventDeliveries_Delivered_NextAttemptAt` (`Delivered`, `NextAttemptAt`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileGovernanceAudits` (
    `Id` CHAR(36) NOT NULL,
    `ScopeKey` VARCHAR(80) NOT NULL,
    `ActorId` VARCHAR(200) NOT NULL,
    `Action` VARCHAR(80) NOT NULL,
    `TargetId` CHAR(36) NOT NULL,
    `Reason` VARCHAR(500) NOT NULL,
    `CreatedAt` BIGINT NOT NULL,
    `RequestJson` MEDIUMTEXT NOT NULL,
    `ResultJson` MEDIUMTEXT NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_BingFileGovernanceAudits_Scope_Created` (`ScopeKey`, `CreatedAt`, `Id`),
    INDEX `IX_BingFileGovernanceAudits_Scope_Target_Created` (`ScopeKey`, `TargetId`, `CreatedAt`, `Id`)
) ENGINE=InnoDB;

CREATE TABLE `BingFileHistoryArchives` (
    `Id` CHAR(36) NOT NULL PRIMARY KEY,
    `ScopeKey` VARCHAR(80) NOT NULL,
    `Kind` ENUM('Operation','Event','MigrationBatch') NOT NULL,
    `EntityId` CHAR(36) NOT NULL,
    `SourceVersion` BIGINT NOT NULL,
    `ArchivedAt` BIGINT NOT NULL,
    `ActorId` VARCHAR(200) NOT NULL,
    UNIQUE INDEX `UX_BingFileHistoryArchives_Kind_Entity` (`Kind`, `EntityId`),
    INDEX `IX_BingFileHistoryArchives_Scope_Kind_Archived` (`ScopeKey`, `Kind`, `ArchivedAt`, `Id`)
) ENGINE=InnoDB;
