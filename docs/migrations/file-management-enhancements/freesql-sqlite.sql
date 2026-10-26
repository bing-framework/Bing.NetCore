BEGIN IMMEDIATE;

ALTER TABLE "BingFileRecords" ADD COLUMN "ContentPolicyJson" TEXT NOT NULL DEFAULT '{}';
UPDATE "BingFileRecords"
SET "ContentPolicyJson" = '{}'
WHERE "ContentPolicyJson" IS NULL OR "ContentPolicyJson" = '';

ALTER TABLE "BingFileOperations" ADD COLUMN "CreatedAt" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "BingFileOperations" ADD COLUMN "FinishedAt" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "BingFileOperations" ADD COLUMN "RecoveryOperationId" CHARACTER(36) NULL;
ALTER TABLE "BingFileOperations" ADD COLUMN "ParentOperationId" CHARACTER(36) NULL;
UPDATE "BingFileOperations" SET "CreatedAt" = 0 WHERE "CreatedAt" IS NULL;
UPDATE "BingFileOperations" SET "FinishedAt" = 0 WHERE "FinishedAt" IS NULL;

DROP INDEX IF EXISTS "IX_BingFileRecords_Scope_Creator_Created";
CREATE INDEX "IX_BingFileRecords_Scope_Creator_State_Created"
    ON "BingFileRecords" ("ScopeKey", "CreatorId", "State", "CreatedAt", "RequestKey");
CREATE INDEX "IX_BingFileRecords_Scope_State_Created"
    ON "BingFileRecords" ("ScopeKey", "State", "CreatedAt", "RequestKey");
CREATE INDEX "IX_BingFileRecords_Scope_Container_State"
    ON "BingFileRecords" ("ScopeKey", "Container", "State", "CreatedAt");
CREATE INDEX "IX_BingFileUploadSessions_Owner_Expires"
    ON "BingFileUploadSessions" ("OwnerId", "ExpiresAt", "FileId");
CREATE INDEX "IX_BingFileOperations_Created" ON "BingFileOperations" ("CreatedAt", "Id");
CREATE INDEX "IX_BingFileOperations_Recovery" ON "BingFileOperations" ("RecoveryOperationId");

CREATE TABLE "BingFileOperationAudits" (
    "Id" CHARACTER(36) NOT NULL PRIMARY KEY,
    "RequestKey" NVARCHAR(80) NOT NULL,
    "ScopeKey" NVARCHAR(80) NOT NULL,
    "OperationId" CHARACTER(36) NOT NULL,
    "ReplacementId" CHARACTER(36) NOT NULL,
    "FileId" CHARACTER(36) NOT NULL,
    "ActorId" NVARCHAR(200) NOT NULL,
    "Reason" NVARCHAR(500) NOT NULL,
    "TraceId" NVARCHAR(64) NOT NULL,
    "CreatedAt" INTEGER NOT NULL
);
CREATE UNIQUE INDEX "UX_BingFileOperationAudits_Request" ON "BingFileOperationAudits" ("RequestKey");
CREATE UNIQUE INDEX "UX_BingFileOperationAudits_Operation" ON "BingFileOperationAudits" ("OperationId");
CREATE INDEX "IX_BingFileOperationAudits_Scope_Operation"
    ON "BingFileOperationAudits" ("ScopeKey", "OperationId", "CreatedAt");

CREATE TABLE "BingFileMigrationBatches" (
    "Id" CHARACTER(36) NOT NULL PRIMARY KEY,
    "RequestKey" NVARCHAR(80) NOT NULL,
    "ScopeKey" NVARCHAR(80) NOT NULL,
    "ActorId" NVARCHAR(200) NOT NULL,
    "SourceStore" NVARCHAR(64) NOT NULL,
    "TargetStore" NVARCHAR(64) NOT NULL,
    "Container" NVARCHAR(64) NULL,
    "State" MEDIUMINT NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "AfterCreatedAt" INTEGER NULL,
    "AfterKey" NVARCHAR(80) NULL,
    "DiscoveryCompleted" BOOLEAN NOT NULL,
    "Version" INTEGER NOT NULL
);
CREATE UNIQUE INDEX "UX_BingFileMigrationBatches_Request" ON "BingFileMigrationBatches" ("RequestKey");
CREATE INDEX "IX_BingFileMigrationBatches_Scope_Created"
    ON "BingFileMigrationBatches" ("ScopeKey", "CreatedAt", "Id");
CREATE INDEX "IX_BingFileMigrationBatches_State_Created"
    ON "BingFileMigrationBatches" ("State", "CreatedAt", "Id");

CREATE TABLE "BingFileMigrationBatchItems" (
    "BatchId" CHARACTER(36) NOT NULL,
    "FileId" CHARACTER(36) NOT NULL,
    "SourceVersion" INTEGER NOT NULL,
    "Length" INTEGER NOT NULL,
    "OperationId" CHARACTER(36) NULL,
    "State" MEDIUMINT NOT NULL,
    "ErrorCode" NVARCHAR(80) NULL,
    "Version" INTEGER NOT NULL,
    PRIMARY KEY ("BatchId", "FileId")
);
CREATE INDEX "IX_BingFileMigrationBatchItems_State" ON "BingFileMigrationBatchItems" ("BatchId", "State", "FileId");
CREATE INDEX "IX_BingFileMigrationBatchItems_Operation" ON "BingFileMigrationBatchItems" ("OperationId");

CREATE TABLE "BingFileControlLocks" (
    "Id" NVARCHAR(80) NOT NULL PRIMARY KEY,
    "Version" INTEGER NOT NULL
);

CREATE TABLE "BingFileUploadReservations" (
    "FileId" CHARACTER(36) NOT NULL PRIMARY KEY,
    "ScopeKey" NVARCHAR(80) NOT NULL,
    "OwnerId" NVARCHAR(200) NOT NULL,
    "Bytes" INTEGER NOT NULL,
    "CreatedAt" INTEGER NOT NULL
);
CREATE INDEX "IX_BingFileUploadReservations_Scope_Owner" ON "BingFileUploadReservations" ("ScopeKey", "OwnerId");

CREATE TABLE "BingFileIoLeases" (
    "Id" CHARACTER(36) NOT NULL PRIMARY KEY,
    "PoolKey" NVARCHAR(128) NOT NULL,
    "Token" NVARCHAR(128) NOT NULL,
    "ExpiresAt" INTEGER NOT NULL
);
CREATE INDEX "IX_BingFileIoLeases_Pool_Expires" ON "BingFileIoLeases" ("PoolKey", "ExpiresAt");

CREATE TABLE "BingFileOutboxEvents" (
    "Id" CHARACTER(36) NOT NULL PRIMARY KEY,
    "FileId" CHARACTER(36) NOT NULL,
    "ScopeKey" NVARCHAR(80) NOT NULL,
    "FileVersion" INTEGER NOT NULL,
    "Kind" NVARCHAR(80) NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "NextAttemptAt" INTEGER NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "LeaseToken" NVARCHAR(128) NULL,
    "LeaseUntil" INTEGER NOT NULL,
    "Delivered" BOOLEAN NOT NULL,
    "Version" INTEGER NOT NULL,
    "ErrorCode" NVARCHAR(200) NULL
);
CREATE UNIQUE INDEX "UX_BingFileOutboxEvents_File_Version_Kind"
    ON "BingFileOutboxEvents" ("FileId", "FileVersion", "Kind");
CREATE INDEX "IX_BingFileOutboxEvents_Due" ON "BingFileOutboxEvents" ("Delivered", "NextAttemptAt", "LeaseUntil");

INSERT INTO "BingFileUploadReservations" ("FileId", "ScopeKey", "OwnerId", "Bytes", "CreatedAt")
SELECT file."Id", file."ScopeKey", file."CreatorId", file."Length", file."CreatedAt"
FROM "BingFileRecords" AS file
INNER JOIN "BingFileUploadSessions" AS session ON session."FileId" = file."Id"
WHERE (file."State" IN (0, 1, 2, 8) AND session."State" IN (0, 1, 2, 8))
   OR (file."State" IN (6, 7) AND session."State" IN (6, 7)
       AND NOT EXISTS (
           SELECT 1 FROM "BingFileOperations" AS abort
           WHERE abort."FileId" = file."Id" AND abort."Kind" = 2 AND abort."State" = 2
       ));

COMMIT;
