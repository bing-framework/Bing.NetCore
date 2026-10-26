BEGIN IMMEDIATE;

CREATE TABLE "BingFileEventDeliveries" (
    "EventId" TEXT NOT NULL,
    "Consumer" TEXT NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "Delivered" INTEGER NOT NULL,
    "NextAttemptAt" INTEGER NOT NULL,
    "CompletedAt" INTEGER NOT NULL,
    "ErrorCode" TEXT NULL,
    CONSTRAINT "PK_BingFileEventDeliveries" PRIMARY KEY ("EventId", "Consumer")
);
CREATE INDEX "IX_BingFileEventDeliveries_Delivered_NextAttemptAt"
    ON "BingFileEventDeliveries" ("Delivered", "NextAttemptAt");

CREATE TABLE "BingFileGovernanceAudits" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BingFileGovernanceAudits" PRIMARY KEY,
    "ScopeKey" TEXT NOT NULL,
    "ActorId" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "TargetId" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "RequestJson" TEXT NOT NULL,
    "ResultJson" TEXT NOT NULL
);
CREATE INDEX "IX_BingFileGovernanceAudits_ScopeKey_CreatedAt_Id"
    ON "BingFileGovernanceAudits" ("ScopeKey", "CreatedAt", "Id");
CREATE INDEX "IX_BingFileGovernanceAudits_ScopeKey_TargetId_CreatedAt_Id"
    ON "BingFileGovernanceAudits" ("ScopeKey", "TargetId", "CreatedAt", "Id");

CREATE TABLE "BingFileHistoryArchives" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BingFileHistoryArchives" PRIMARY KEY,
    "ScopeKey" TEXT NOT NULL,
    "Kind" INTEGER NOT NULL,
    "EntityId" TEXT NOT NULL,
    "SourceVersion" INTEGER NOT NULL,
    "ArchivedAt" INTEGER NOT NULL,
    "ActorId" TEXT NOT NULL
);
CREATE UNIQUE INDEX "IX_BingFileHistoryArchives_Kind_EntityId"
    ON "BingFileHistoryArchives" ("Kind", "EntityId");
CREATE INDEX "IX_BingFileHistoryArchives_ScopeKey_Kind_ArchivedAt_Id"
    ON "BingFileHistoryArchives" ("ScopeKey", "Kind", "ArchivedAt", "Id");

COMMIT;
