BEGIN IMMEDIATE;

CREATE TABLE "BingFileEventDeliveries" (
    "EventId" CHARACTER(36) NOT NULL,
    "Consumer" NVARCHAR(100) NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "Delivered" BOOLEAN NOT NULL,
    "NextAttemptAt" INTEGER NOT NULL,
    "CompletedAt" INTEGER NOT NULL,
    "ErrorCode" NVARCHAR(200) NULL,
    PRIMARY KEY ("EventId", "Consumer")
);
CREATE INDEX "IX_BingFileEventDeliveries_Delivered_NextAttemptAt"
    ON "BingFileEventDeliveries" ("Delivered", "NextAttemptAt");

CREATE TABLE "BingFileGovernanceAudits" (
    "Id" CHARACTER(36) NOT NULL PRIMARY KEY,
    "ScopeKey" NVARCHAR(80) NOT NULL,
    "ActorId" NVARCHAR(200) NOT NULL,
    "Action" NVARCHAR(80) NOT NULL,
    "TargetId" CHARACTER(36) NOT NULL,
    "Reason" NVARCHAR(500) NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "RequestJson" TEXT NOT NULL,
    "ResultJson" TEXT NOT NULL
);
CREATE INDEX "IX_BingFileGovernanceAudits_Scope_Created"
    ON "BingFileGovernanceAudits" ("ScopeKey", "CreatedAt", "Id");
CREATE INDEX "IX_BingFileGovernanceAudits_Scope_Target_Created"
    ON "BingFileGovernanceAudits" ("ScopeKey", "TargetId", "CreatedAt", "Id");

CREATE TABLE "BingFileHistoryArchives" (
    "Id" CHARACTER(36) NOT NULL PRIMARY KEY,
    "ScopeKey" NVARCHAR(80) NOT NULL,
    "Kind" MEDIUMINT NOT NULL,
    "EntityId" CHARACTER(36) NOT NULL,
    "SourceVersion" INTEGER NOT NULL,
    "ArchivedAt" INTEGER NOT NULL,
    "ActorId" NVARCHAR(200) NOT NULL
);
CREATE UNIQUE INDEX "UX_BingFileHistoryArchives_Kind_Entity" ON "BingFileHistoryArchives" ("Kind", "EntityId");
CREATE INDEX "IX_BingFileHistoryArchives_Scope_Kind_Archived"
    ON "BingFileHistoryArchives" ("ScopeKey", "Kind", "ArchivedAt", "Id");

COMMIT;
