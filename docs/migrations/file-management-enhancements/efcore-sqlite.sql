BEGIN IMMEDIATE;

ALTER TABLE "BingFileRecords" ADD COLUMN "ContentPolicyJson" TEXT NOT NULL DEFAULT '{}';
UPDATE "BingFileRecords"
SET "ContentPolicyJson" = '{}'
WHERE "ContentPolicyJson" IS NULL OR "ContentPolicyJson" = '';

ALTER TABLE "BingFileOperations" ADD COLUMN "CreatedAt" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "BingFileOperations" ADD COLUMN "FinishedAt" INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "BingFileOperations" ADD COLUMN "RecoveryOperationId" TEXT NULL;
ALTER TABLE "BingFileOperations" ADD COLUMN "ParentOperationId" TEXT NULL;
UPDATE "BingFileOperations" SET "CreatedAt" = 0 WHERE "CreatedAt" IS NULL;
UPDATE "BingFileOperations" SET "FinishedAt" = 0 WHERE "FinishedAt" IS NULL;

DROP INDEX IF EXISTS "IX_BingFileRecords_ScopeKey_CreatorId_CreatedAt";
CREATE INDEX "IX_BingFileRecords_ScopeKey_CreatorId_State_CreatedAt_RequestKey"
    ON "BingFileRecords" ("ScopeKey", "CreatorId", "State", "CreatedAt", "RequestKey");
CREATE INDEX "IX_BingFileRecords_ScopeKey_State_CreatedAt_RequestKey"
    ON "BingFileRecords" ("ScopeKey", "State", "CreatedAt", "RequestKey");
CREATE INDEX "IX_BingFileRecords_ScopeKey_Container_State_CreatedAt"
    ON "BingFileRecords" ("ScopeKey", "Container", "State", "CreatedAt");
CREATE INDEX "IX_BingFileUploadSessions_OwnerId_ExpiresAt_FileId"
    ON "BingFileUploadSessions" ("OwnerId", "ExpiresAt", "FileId");
CREATE INDEX "IX_BingFileOperations_CreatedAt_Id"
    ON "BingFileOperations" ("CreatedAt", "Id");
CREATE INDEX "IX_BingFileOperations_RecoveryOperationId"
    ON "BingFileOperations" ("RecoveryOperationId");

CREATE TABLE "BingFileOperationAudits" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BingFileOperationAudits" PRIMARY KEY,
    "RequestKey" TEXT NOT NULL,
    "ScopeKey" TEXT NOT NULL,
    "OperationId" TEXT NOT NULL,
    "ReplacementId" TEXT NOT NULL,
    "FileId" TEXT NOT NULL,
    "ActorId" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "TraceId" TEXT NOT NULL,
    "CreatedAt" INTEGER NOT NULL
);
CREATE UNIQUE INDEX "IX_BingFileOperationAudits_RequestKey" ON "BingFileOperationAudits" ("RequestKey");
CREATE UNIQUE INDEX "IX_BingFileOperationAudits_OperationId" ON "BingFileOperationAudits" ("OperationId");
CREATE INDEX "IX_BingFileOperationAudits_ScopeKey_OperationId_CreatedAt"
    ON "BingFileOperationAudits" ("ScopeKey", "OperationId", "CreatedAt");

CREATE TABLE "BingFileMigrationBatches" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BingFileMigrationBatches" PRIMARY KEY,
    "RequestKey" TEXT NOT NULL,
    "ScopeKey" TEXT NOT NULL,
    "ActorId" TEXT NOT NULL,
    "SourceStore" TEXT NOT NULL,
    "TargetStore" TEXT NOT NULL,
    "Container" TEXT NULL,
    "State" INTEGER NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "AfterCreatedAt" INTEGER NULL,
    "AfterKey" TEXT NULL,
    "DiscoveryCompleted" INTEGER NOT NULL,
    "Version" INTEGER NOT NULL
);
CREATE UNIQUE INDEX "IX_BingFileMigrationBatches_RequestKey" ON "BingFileMigrationBatches" ("RequestKey");
CREATE INDEX "IX_BingFileMigrationBatches_ScopeKey_CreatedAt_Id"
    ON "BingFileMigrationBatches" ("ScopeKey", "CreatedAt", "Id");
CREATE INDEX "IX_BingFileMigrationBatches_State_CreatedAt_Id"
    ON "BingFileMigrationBatches" ("State", "CreatedAt", "Id");

CREATE TABLE "BingFileMigrationBatchItems" (
    "BatchId" TEXT NOT NULL,
    "FileId" TEXT NOT NULL,
    "SourceVersion" INTEGER NOT NULL,
    "Length" INTEGER NOT NULL,
    "OperationId" TEXT NULL,
    "State" INTEGER NOT NULL,
    "ErrorCode" TEXT NULL,
    "Version" INTEGER NOT NULL,
    CONSTRAINT "PK_BingFileMigrationBatchItems" PRIMARY KEY ("BatchId", "FileId")
);
CREATE INDEX "IX_BingFileMigrationBatchItems_BatchId_State_FileId"
    ON "BingFileMigrationBatchItems" ("BatchId", "State", "FileId");
CREATE INDEX "IX_BingFileMigrationBatchItems_OperationId"
    ON "BingFileMigrationBatchItems" ("OperationId");

CREATE TABLE "BingFileControlLocks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BingFileControlLocks" PRIMARY KEY,
    "Version" INTEGER NOT NULL
);

CREATE TABLE "BingFileUploadReservations" (
    "FileId" TEXT NOT NULL CONSTRAINT "PK_BingFileUploadReservations" PRIMARY KEY,
    "ScopeKey" TEXT NOT NULL,
    "OwnerId" TEXT NOT NULL,
    "Bytes" INTEGER NOT NULL,
    "CreatedAt" INTEGER NOT NULL
);
CREATE INDEX "IX_BingFileUploadReservations_ScopeKey_OwnerId"
    ON "BingFileUploadReservations" ("ScopeKey", "OwnerId");

CREATE TABLE "BingFileIoLeases" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BingFileIoLeases" PRIMARY KEY,
    "PoolKey" TEXT NOT NULL,
    "Token" TEXT NOT NULL,
    "ExpiresAt" INTEGER NOT NULL
);
CREATE INDEX "IX_BingFileIoLeases_PoolKey_ExpiresAt"
    ON "BingFileIoLeases" ("PoolKey", "ExpiresAt");

CREATE TABLE "BingFileOutboxEvents" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BingFileOutboxEvents" PRIMARY KEY,
    "FileId" TEXT NOT NULL,
    "ScopeKey" TEXT NOT NULL,
    "FileVersion" INTEGER NOT NULL,
    "Kind" TEXT NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "NextAttemptAt" INTEGER NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "LeaseToken" TEXT NULL,
    "LeaseUntil" INTEGER NOT NULL,
    "Delivered" INTEGER NOT NULL,
    "Version" INTEGER NOT NULL,
    "ErrorCode" TEXT NULL
);
CREATE UNIQUE INDEX "IX_BingFileOutboxEvents_FileId_FileVersion_Kind"
    ON "BingFileOutboxEvents" ("FileId", "FileVersion", "Kind");
CREATE INDEX "IX_BingFileOutboxEvents_Delivered_NextAttemptAt_LeaseUntil"
    ON "BingFileOutboxEvents" ("Delivered", "NextAttemptAt", "LeaseUntil");

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
