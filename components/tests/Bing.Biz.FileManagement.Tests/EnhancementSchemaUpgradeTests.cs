using System.Text;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.Biz.FileManagement.FreeSql;
using Bing.FileStorage;
using FreeSql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class EnhancementSchemaUpgradeTests
{
    [Fact]
    public async Task EfCoreSqliteUpgrade_BackfillsLegacyDataAndSupportsNewReservations()
        => await UpgradeLegacyDatabaseAsync(useFreeSql: false);

    [Fact]
    public async Task FreeSqlSqliteUpgrade_BackfillsLegacyDataAndSupportsNewReservations()
        => await UpgradeLegacyDatabaseAsync(useFreeSql: true);

    private static async Task UpgradeLegacyDatabaseAsync(bool useFreeSql)
    {
        var root = Path.Combine(Path.GetTempPath(), "bing-file-management-schema-upgrade", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "legacy.db");
        var connectionString = "Data Source=" + databasePath;
        var active = LegacyUpload.Create(FileState.Uploading, FileOperationKind.Complete, FileOperationState.Pending, 13);
        var cleanupPending = LegacyUpload.Create(FileState.Aborted, FileOperationKind.Abort, FileOperationState.Failed, 7);
        var cleanupConfirmed = LegacyUpload.Create(FileState.Aborted, FileOperationKind.Abort, FileOperationState.Succeeded, 5);
        var ready = LegacyUpload.Create(FileState.Ready, FileOperationKind.Complete, FileOperationState.Succeeded, 3);

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "ai_docs/tasks/20261024-file-storage-design/evidence/fix-round-1/schema-FreeSql-sqlite.sql"
                    : "ai_docs/tasks/20261024-file-storage-design/evidence/fix-round-1/schema-EFCore-sqlite.sql"));
                foreach (var upload in new[] { active, cleanupPending, cleanupConfirmed, ready })
                    await InsertLegacyUploadAsync(connection, upload, useFreeSql);

                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "docs/migrations/file-management-enhancements/freesql-sqlite.sql"
                    : "docs/migrations/file-management-enhancements/efcore-sqlite.sql"));
                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "docs/migrations/file-management-governance/freesql-sqlite.sql"
                    : "docs/migrations/file-management-governance/efcore-sqlite.sql"));
                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "docs/migrations/file-management-remote-drives/freesql-sqlite.sql"
                    : "docs/migrations/file-management-remote-drives/efcore-sqlite.sql"));
                var reservations = await ReadReservationsAsync(connection);
                Assert.Equal(new Dictionary<Guid, long>
                {
                    [active.FileId] = active.Length,
                    [cleanupPending.FileId] = cleanupPending.Length
                }.OrderBy(x => x.Key), reservations.OrderBy(x => x.Key));
            }

            if (useFreeSql)
            {
                var orm = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, connectionString)
                    .UseAutoSyncStructure(false).Build();
                try
                {
                    await VerifyUpgradedStoreAsync(new FreeSqlFileManagementStore(orm), active, cleanupPending);
                }
                finally
                {
                    orm.Dispose();
                }
            }
            else
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddBingFileManagementEntityFrameworkCore(options => options.UseSqlite(connectionString));
                var provider = services.BuildServiceProvider();
                try
                {
                    await VerifyUpgradedStoreAsync(provider.GetRequiredService<IFileManagementStore>(), active, cleanupPending);
                }
                finally
                {
                    await provider.DisposeAsync();
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task VerifyUpgradedStoreAsync(IFileManagementStore store, LegacyUpload active,
        LegacyUpload cleanupPending)
    {
        var oldEntry = await store.FindAsync(active.FileId);
        Assert.NotNull(oldEntry);
        Assert.Equal(FileState.Uploading, oldEntry!.File.State);
        Assert.Equal("{}", oldEntry.File.ContentPolicyJson);
        Assert.Equal(13, oldEntry.File.Length);

        var oldOperation = await store.FindOperationAsync(active.OperationId, active.ScopeKey);
        Assert.NotNull(oldOperation);
        Assert.Equal(0, oldOperation!.CreatedAt);
        Assert.Equal(0, oldOperation.FinishedAt);
        var failedAbort = await store.FindOperationAsync(cleanupPending.OperationId, cleanupPending.ScopeKey);
        Assert.NotNull(failedAbort);
        Assert.Equal(FileOperationKind.Abort, failedAbort!.Kind);
        Assert.Equal(FileOperationState.Failed, failedAbort.State);

        var snapshot = await store.GetOperationalSnapshotAsync();
        Assert.Equal(20, snapshot.ReservedBytes);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var newFileId = Guid.NewGuid();
        var newSessionId = Guid.NewGuid();
        var newEntry = new FileEntry
        {
            File = new FileRecord
            {
                Id = newFileId,
                RequestKey = Guid.NewGuid().ToString("N"),
                ScopeKey = active.ScopeKey,
                ScopeKind = StorageScopeKind.Host,
                StoreName = "local-v1",
                Container = "attachments",
                ObjectKey = newFileId.ToString("N"),
                FileName = "new.bin",
                Length = 3,
                ContentType = "application/octet-stream",
                Visibility = FileVisibility.Private,
                State = FileState.Pending,
                CreatorId = "new-owner",
                CreatedAt = now,
                Version = 1
            },
            Session = new FileUploadSession
            {
                Id = newSessionId,
                FileId = newFileId,
                OwnerId = "new-owner",
                BackendMetadataJson = "{}",
                TotalLength = 3,
                PartSize = 5,
                State = FileState.Pending,
                WriteMode = FileWriteMode.Overwrite,
                ExpiresAt = now + 60_000,
                AbsoluteExpiresAt = now + 60_000,
                LastActivityAt = now,
                Version = 1
            },
            Admission = new FileUploadAdmission()
        };
        var initialize = new FileOperation
        {
            Id = Guid.NewGuid(),
            FileId = newFileId,
            Kind = FileOperationKind.Initialize,
            State = FileOperationState.Pending,
            Payload = "{}",
            NextAttemptAt = now,
            Version = 1
        };

        Assert.True(await store.TryCreateAsync(newEntry, new[] { initialize }));
        Assert.False(await store.TryCreateAsync(newEntry, new[] { initialize }));
        Assert.Equal(23, (await store.GetOperationalSnapshotAsync()).ReservedBytes);
    }

    private static async Task InsertLegacyUploadAsync(SqliteConnection connection, LegacyUpload upload, bool useFreeSql)
    {
        // EF SQLite 使用大写 GUID 文本，FreeSql 使用小写；旧数据须模拟对应 Provider 的真实格式。
        string Id(Guid value) => useFreeSql ? value.ToString("D") : value.ToString("D").ToUpperInvariant();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO BingFileRecords
                    (Id, RequestKey, ScopeKey, TenantId, ScopeKind, StoreName, Container, ObjectKey, FileName,
                     Length, ContentType, Visibility, State, CreatorId, CreatedAt, Version, Sha256)
                VALUES ($id, $request, $scope, NULL, $scopeKind, 'local-v1', 'attachments', $objectKey, $fileName,
                        $length, 'application/octet-stream', 0, $state, 'legacy-owner', 100, 1, NULL);
                """;
            command.Parameters.AddWithValue("$id", Id(upload.FileId));
            command.Parameters.AddWithValue("$request", upload.RequestKey);
            command.Parameters.AddWithValue("$scope", upload.ScopeKey);
            command.Parameters.AddWithValue("$scopeKind", (int)StorageScopeKind.Host);
            command.Parameters.AddWithValue("$objectKey", upload.FileId.ToString("N"));
            command.Parameters.AddWithValue("$fileName", upload.FileName);
            command.Parameters.AddWithValue("$length", upload.Length);
            command.Parameters.AddWithValue("$state", (int)upload.FileState);
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO BingFileUploadSessions
                    (Id, FileId, OwnerId, BackendUploadId, BackendMetadataJson, TotalLength, PartSize,
                     State, WriteMode, ExpiresAt, AbsoluteExpiresAt, LastActivityAt, ClientLastModified, Version)
                VALUES ($id, $fileId, 'legacy-owner', NULL, '{}', $length, 5, $state, 0,
                        9999999999999, 9999999999999, 100, 0, 1);
                """;
            command.Parameters.AddWithValue("$id", Id(upload.SessionId));
            command.Parameters.AddWithValue("$fileId", Id(upload.FileId));
            command.Parameters.AddWithValue("$length", upload.Length);
            command.Parameters.AddWithValue("$state", (int)upload.FileState);
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO BingFileOperations
                    (Id, FileId, Kind, State, Payload, Attempts, NextAttemptAt, LeaseToken, LeaseUntil, LastError, Version)
                VALUES ($id, $fileId, $kind, $state, '{}', 0, 100, NULL, 0, NULL, 1);
                """;
            command.Parameters.AddWithValue("$id", Id(upload.OperationId));
            command.Parameters.AddWithValue("$fileId", Id(upload.FileId));
            command.Parameters.AddWithValue("$kind", (int)upload.OperationKind);
            command.Parameters.AddWithValue("$state", (int)upload.OperationState);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task ExecuteSqlAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Dictionary<Guid, long>> ReadReservationsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FileId, Bytes FROM BingFileUploadReservations";
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<Guid, long>();
        while (await reader.ReadAsync()) result.Add(Guid.Parse(reader.GetString(0)), reader.GetInt64(1));
        return result;
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        var relative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        while (current != null)
        {
            var path = Path.Combine(current.FullName, relative);
            if (File.Exists(path)) return File.ReadAllText(path, Encoding.UTF8);
            current = current.Parent!;
        }
        throw new FileNotFoundException($"未找到仓库文件：{relativePath}");
    }

    private sealed record LegacyUpload(Guid FileId, Guid SessionId, Guid OperationId, string ScopeKey,
        string RequestKey, string FileName, long Length, FileState FileState, FileOperationKind OperationKind,
        FileOperationState OperationState)
    {
        internal static LegacyUpload Create(FileState fileState, FileOperationKind operationKind,
            FileOperationState operationState, long length)
        {
            var id = Guid.NewGuid();
            return new LegacyUpload(id, Guid.NewGuid(), Guid.NewGuid(), "host:schema-upgrade",
                Guid.NewGuid().ToString("N"), $"legacy-{fileState}.bin", length, fileState, operationKind, operationState);
        }
    }
}
