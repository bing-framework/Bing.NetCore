#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Biz.FileManagement;
using Bing.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>验证 MySQL 8 上从基础文件管理 schema 升级到增强 schema。</summary>
public sealed class AdminFileEnhancementUpgradeTests
{
    [AdminFileManagementMySqlFact]
    public async Task MySql8EnhancementUpgrade_BackfillsLegacyRowsAndSupportsNewOperations()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!;
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(serverConnectionString);
        await using var environment = new AdminFileHttpEnvironment(database.DatabaseConnectionString);
        var services = new ServiceCollection();
        environment.Configure(services, configureTestAuthentication: false);
        await using var provider = services.BuildServiceProvider();
        await environment.InitializeDatabaseAsync(provider);

        var scope = StorageScope.Host;
        var activeUpload = CreateLegacyEntry(scope, FileState.Pending, "legacy-uploader", 13,
            includeSession: true, contentPolicyJson: "{\"legacy\":true}");
        var initialize = NewOperation(activeUpload.File.Id, FileOperationKind.Initialize, FileOperationState.Pending);
        var readyForBatch = CreateLegacyEntry(scope, FileState.Ready, "legacy-owner", 19, includeSession: false);
        var deletePending = CreateLegacyEntry(scope, FileState.DeletePending, "legacy-owner", 23, includeSession: false);
        var failedDelete = NewOperation(deletePending.File.Id, FileOperationKind.Delete, FileOperationState.Failed);
        failedDelete.LastError = "storage:Unavailable";

        using (var seedScope = provider.CreateScope())
        {
            var store = seedScope.ServiceProvider.GetRequiredService<IFileManagementStore>();
            Assert.True(await store.TryCreateAsync(activeUpload, new[] { initialize }));
            Assert.True(await store.TryCreateAsync(readyForBatch));
            Assert.True(await store.TryCreateAsync(deletePending, new[] { failedDelete }));
        }

        await using (var connection = new MySqlConnection(database.DatabaseConnectionString))
        {
            await connection.OpenAsync();
            await RevertCurrentSchemaToBaseAsync(connection);
            var migration = IsEfCore
                ? "docs/migrations/file-management-enhancements/efcore-mysql8.sql"
                : "docs/migrations/file-management-enhancements/freesql-mysql8.sql";
            await ExecuteSqlScriptAsync(connection, ReadRepositoryFile(migration));
            await ExecuteSqlScriptAsync(connection, ReadRepositoryFile(IsEfCore
                ? "docs/migrations/file-management-governance/efcore-mysql8.sql"
                : "docs/migrations/file-management-governance/freesql-mysql8.sql"));
            await AssertNewTableCollationsAsync(connection);
        }

        using (var verifyScope = provider.CreateScope())
        {
            var scoped = verifyScope.ServiceProvider;
            var store = scoped.GetRequiredService<IFileManagementStore>();
            var oldActive = await store.FindAsync(activeUpload.File.Id);
            Assert.NotNull(oldActive);
            Assert.Equal(FileState.Pending, oldActive!.File.State);
            Assert.Equal("{}", oldActive.File.ContentPolicyJson);
            Assert.Equal(activeUpload.File.Length, oldActive.File.Length);

            var oldInitialize = await store.FindOperationAsync(initialize.Id, scope.Key);
            Assert.NotNull(oldInitialize);
            Assert.Equal(0, oldInitialize!.CreatedAt);
            Assert.Equal(0, oldInitialize.FinishedAt);

            var snapshot = await store.GetOperationalSnapshotAsync();
            Assert.Equal(activeUpload.File.Length, snapshot.ReservedBytes);

            var failed = await store.FindOperationAsync(failedDelete.Id, scope.Key);
            Assert.NotNull(failed);
            Assert.Equal(0, failed!.CreatedAt);
            var replacementId = await scoped.GetRequiredService<FileOperationAdministration>().RecoverAsync(
                scope.Key, "upgrade-test-operator", failedDelete.Id,
                new RecoverFileOperation { RequestId = Guid.NewGuid(), ExpectedVersion = failed.Version, Reason = "upgrade integration test" });
            var audit = Assert.Single(await store.ListOperationAuditAsync(scope.Key, failedDelete.Id));
            Assert.Equal(replacementId, audit.ReplacementId);

            var batchService = scoped.GetRequiredService<FileMigrationBatchService>();
            var batch = await batchService.CreateAsync(scope, "upgrade-test-operator", new CreateMigrationBatch
            {
                RequestId = Guid.NewGuid(),
                SourceStore = readyForBatch.File.StoreName,
                TargetStore = "admin-test-target",
                Container = readyForBatch.File.Container
            });
            Assert.Equal(FileMigrationBatchState.Running, (await store.FindBatchAsync(batch.Id))!.State);
            Assert.Single(await store.ListActiveBatchesAsync(10));
            Assert.Single(await store.QueryFilesAsync(new() { ScopeKey = scope.Key, StoreName = "admin-test-local", Container = "attachments", CreatedTo = batch.CreatedAt }));
            await batchService.AdvanceAsync();
            var progress = await batchService.GetAsync(scope.Key, batch.Id);
            Assert.Equal(1, progress.Total);
            Assert.Equal(1, progress.Queued);

            var newUpload = CreateLegacyEntry(scope, FileState.Pending, "new-uploader", 7, includeSession: true);
            newUpload.Admission = new FileUploadAdmission();
            var newInitialize = NewOperation(newUpload.File.Id, FileOperationKind.Initialize, FileOperationState.Pending);
            Assert.True(await store.TryCreateAsync(newUpload, new[] { newInitialize }));
            Assert.False(await store.TryCreateAsync(newUpload, new[] { newInitialize }));
            Assert.Equal(activeUpload.File.Length + newUpload.File.Length,
                (await store.GetOperationalSnapshotAsync()).ReservedBytes);
            var inspected = (await store.FindAsync(newUpload.File.Id))!;
            inspected.File.State = inspected.Session!.State = FileState.Quarantined;
            var inspect = NewOperation(inspected.File.Id, FileOperationKind.Inspect, FileOperationState.Pending);
            Assert.True(await store.TryUpdateAsync(inspected, inspected.File.Version, operations: new[] { inspect }));
            Assert.Equal(FileState.Quarantined, (await store.FindAsync(inspected.File.Id))!.File.State);
            Assert.Equal(FileOperationKind.Inspect, (await store.FindOperationAsync(inspect.Id))!.Kind);
        }
    }

    private static bool IsEfCore
    {
        get
        {
#if BING_ADMIN_EFCORE_TESTS
            return true;
#else
            return false;
#endif
        }
    }

    private static FileEntry CreateLegacyEntry(StorageScope scope, FileState state, string ownerId, long length,
        bool includeSession, string contentPolicyJson = "{\"pre-upgrade\":true}")
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 60_000;
        return new FileEntry
        {
            File = new FileRecord
            {
                Id = id,
                RequestKey = Guid.NewGuid().ToString("N"),
                ScopeKey = scope.Key,
                TenantId = scope.TenantId,
                ScopeKind = scope.Kind,
                StoreName = "admin-test-local",
                Container = "attachments",
                ObjectKey = id.ToString("N"),
                FileName = $"legacy-{state}.bin",
                Length = length,
                ContentType = "application/octet-stream",
                Visibility = FileVisibility.Private,
                State = state,
                CreatorId = ownerId,
                CreatedAt = now,
                Version = 1,
                ContentPolicyJson = contentPolicyJson
            },
            Session = includeSession
                ? new FileUploadSession
                {
                    Id = Guid.NewGuid(),
                    FileId = id,
                    OwnerId = ownerId,
                    BackendMetadataJson = "{}",
                    TotalLength = length,
                    PartSize = 5,
                    State = state,
                    WriteMode = FileWriteMode.Overwrite,
                    ExpiresAt = now + 3_600_000,
                    AbsoluteExpiresAt = now + 86_400_000,
                    LastActivityAt = now,
                    Version = 1
                }
                : null
        };
    }

    private static FileOperation NewOperation(Guid fileId, FileOperationKind kind, FileOperationState state)
        => new()
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Kind = kind,
            State = state,
            Payload = "{}",
            Version = 1,
            NextAttemptAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

    private static async Task RevertCurrentSchemaToBaseAsync(MySqlConnection connection)
    {
        foreach (var table in new[]
        {
            "BingFileOperationAudits", "BingFileMigrationBatches", "BingFileMigrationBatchItems",
            "BingFileControlLocks", "BingFileUploadReservations", "BingFileIoLeases", "BingFileOutboxEvents",
            "BingFileEventDeliveries", "BingFileGovernanceAudits", "BingFileHistoryArchives"
        })
            await ExecuteStatementAsync(connection, $"DROP TABLE `{table}`");

        if (IsEfCore)
        {
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileRecords` DROP INDEX `IX_BingFileRecords_ScopeKey_CreatorId_State_CreatedAt_RequestKey`, DROP INDEX `IX_BingFileRecords_ScopeKey_State_CreatedAt_RequestKey`, DROP INDEX `IX_BingFileRecords_ScopeKey_Container_State_CreatedAt`, ADD INDEX `IX_BingFileRecords_ScopeKey_CreatorId_CreatedAt` (`ScopeKey`, `CreatorId`, `CreatedAt`)");
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileUploadSessions` DROP INDEX `IX_BingFileUploadSessions_OwnerId_ExpiresAt_FileId`");
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileOperations` DROP INDEX `IX_BingFileOperations_CreatedAt_Id`, DROP INDEX `IX_BingFileOperations_RecoveryOperationId`");
        }
        else
        {
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileRecords` MODIFY COLUMN `State` ENUM('Pending','Uploading','Completing','Ready','DeletePending','Deleted','Aborted','Failed') NOT NULL");
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileUploadSessions` MODIFY COLUMN `State` ENUM('Pending','Uploading','Completing','Ready','DeletePending','Deleted','Aborted','Failed') NOT NULL");
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileOperations` MODIFY COLUMN `Kind` ENUM('Initialize','Complete','Abort','Delete','Migrate','DeleteReplica') NOT NULL");
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileRecords` DROP INDEX `IX_BingFileRecords_Scope_Creator_State_Created`, DROP INDEX `IX_BingFileRecords_Scope_State_Created`, DROP INDEX `IX_BingFileRecords_Scope_Container_State`, ADD INDEX `IX_BingFileRecords_Scope_Creator_Created` (`ScopeKey`, `CreatorId`, `CreatedAt`)");
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileUploadSessions` DROP INDEX `IX_BingFileUploadSessions_Owner_Expires`");
            await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileOperations` DROP INDEX `IX_BingFileOperations_Created`, DROP INDEX `IX_BingFileOperations_Recovery`");
        }

        await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileRecords` DROP COLUMN `ContentPolicyJson`");
        await ExecuteStatementAsync(connection, "ALTER TABLE `BingFileOperations` DROP COLUMN `CreatedAt`, DROP COLUMN `FinishedAt`, DROP COLUMN `RecoveryOperationId`, DROP COLUMN `ParentOperationId`");
    }

    private static async Task AssertNewTableCollationsAsync(MySqlConnection connection)
    {
        // 专用库采用 unicode_ci，而 MySQL 8 服务器默认采用 0900_ai_ci；升级必须继承库配置。
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DEFAULT_COLLATION_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = DATABASE()";
        var expected = (string)(await command.ExecuteScalarAsync())!;
        foreach (var table in new[]
        {
            "BingFileOperationAudits", "BingFileMigrationBatches", "BingFileMigrationBatchItems",
            "BingFileControlLocks", "BingFileUploadReservations", "BingFileIoLeases", "BingFileOutboxEvents",
            "BingFileEventDeliveries", "BingFileGovernanceAudits", "BingFileHistoryArchives"
        })
        {
            command.CommandText = "SELECT TABLE_COLLATION FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@table", table);
            Assert.Equal(expected, await command.ExecuteScalarAsync());
        }
    }

    private static async Task ExecuteSqlScriptAsync(MySqlConnection connection, string script)
    {
        foreach (var statement in script.Split(';'))
        {
            var sql = string.Join("\n", statement.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal))).Trim();
            if (sql.Length > 0) await ExecuteStatementAsync(connection, sql);
        }
    }

    private static async Task ExecuteStatementAsync(MySqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var relative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent!)
        {
            var path = Path.Combine(current.FullName, relative);
            if (File.Exists(path)) return File.ReadAllText(path, Encoding.UTF8);
        }
        throw new FileNotFoundException($"未找到仓库文件：{relativePath}");
    }
}
