using System.Text;
using System.Text.Json;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Bing.Biz.FileManagement.FreeSql;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using FreeSql;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class RemoteDriveSchemaUpgradeTests
{
    [Fact]
    public async Task EfCoreSqliteRemoteDriveUpgrade_PreservesFilesAndLargeJson()
        => await UpgradeAsync(useFreeSql: false);

    [Fact]
    public async Task FreeSqlSqliteRemoteDriveUpgrade_PreservesFilesAndLargeJson()
        => await UpgradeAsync(useFreeSql: true);

    private static async Task UpgradeAsync(bool useFreeSql)
    {
        var root = Path.Combine(Path.GetTempPath(), "bing-file-management-remote-drive-schema-upgrade", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var connectionString = "Data Source=" + Path.Combine(root, "legacy.db");
        var fileId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "ai_docs/tasks/20261024-file-storage-design/evidence/fix-round-1/schema-FreeSql-sqlite.sql"
                    : "ai_docs/tasks/20261024-file-storage-design/evidence/fix-round-1/schema-EFCore-sqlite.sql"));
                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "docs/migrations/file-management-enhancements/freesql-sqlite.sql"
                    : "docs/migrations/file-management-enhancements/efcore-sqlite.sql"));
                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "docs/migrations/file-management-governance/freesql-sqlite.sql"
                    : "docs/migrations/file-management-governance/efcore-sqlite.sql"));
                await InsertLegacyFileAsync(connection, fileId, sessionId, now, useFreeSql);

                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "docs/migrations/file-management-remote-drives/freesql-sqlite.sql"
                    : "docs/migrations/file-management-remote-drives/efcore-sqlite.sql"));

                await ExecuteSqlAsync(connection, ReadRepositoryFile(useFreeSql
                    ? "docs/migrations/file-management-remote-drives/freesql-cleanup-retry-sqlite.sql"
                    : "docs/migrations/file-management-remote-drives/efcore-cleanup-retry-sqlite.sql"));
                Assert.True(await ColumnExistsAsync(connection, "BingRemoteDriveTransfers", "CleanupNextAttemptAt"));
                Assert.True(await ColumnExistsAsync(connection, "BingRemoteDriveTransfers", "CleanupErrorCode"));
                Assert.True(await ColumnExistsAsync(connection, "BingFileRecords", "ConnectionId"));
                Assert.True(await ColumnExistsAsync(connection, "BingFileRecords", "Ownership"));
                Assert.True(await ColumnExistsAsync(connection, "BingFileUploadSessions", "RequestedStorageTargetId"));
                await VerifyRemoteObjectAddressIndexAsync(connection);
            }

            if (useFreeSql)
            {
                var orm = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, connectionString)
                    .UseAutoSyncStructure(false).Build();
                try
                {
                    var store = new FreeSqlFileManagementStore(orm);
                    await VerifyUpgradedDataAsync(store, (IRemoteDriveStateStore)store, fileId, sessionId, now);
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
                await using var provider = services.BuildServiceProvider();
                var store = provider.GetRequiredService<IFileManagementStore>();
                await VerifyUpgradedDataAsync(store, (IRemoteDriveStateStore)store, fileId, sessionId, now);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task VerifyUpgradedDataAsync(IFileManagementStore files, IRemoteDriveStateStore remote,
        Guid fileId, Guid sessionId, long now)
    {
        var oldEntry = await files.FindAsync(fileId);
        Assert.NotNull(oldEntry);
        Assert.Equal("legacy-before-remote-drives.bin", oldEntry!.File.FileName);
        Assert.Equal(73, oldEntry.File.Length);
        Assert.Equal("host:remote-drive-upgrade", oldEntry.File.ScopeKey);
        Assert.Equal(FileState.Ready, oldEntry.File.State);
        Assert.Equal(RemoteDriveOwnership.Shared, oldEntry.File.Ownership);
        Assert.Null(oldEntry.File.ConnectionId);
        Assert.Equal(sessionId, oldEntry.Session!.Id);
        Assert.Null(oldEntry.Session.RequestedStorageTargetId);

        var connection = new RemoteDriveConnection
        {
            Id = Guid.NewGuid(), TemplateName = "remote-drive-test", Provider = "aliyun-drive", StoreName = "remote-drive-test-v1",
            ScopeKind = StorageScopeKind.Host, ScopeKey = "host:remote-drive-upgrade", Ownership = RemoteDriveOwnership.Shared,
            OwnerId = "admin", DisplayName = "schema test", RemoteAccountId = "account", DriveId = "drive", RootFolderId = "root",
            EncryptedCredentials = "protected", EncryptedAuthorization = "{}", State = RemoteDriveConnectionState.Active,
            CreatedAt = now, Version = 1
        };
        Assert.True(await remote.TryCreateConnectionAsync(connection));

        var partDigests = Enumerable.Range(1, 10_000)
            .ToDictionary(number => number, number => new string((char)('a' + number % 26), 64));
        var parts = Enumerable.Range(1, 10_000).Select(number => new RemoteDrivePartReceipt
        {
            Number = number, Length = 16 * 1024 * 1024, ETag = new string('e', 64), Sha256 = new string('f', 64)
        }).ToArray();
        var manifestJson = JsonSerializer.Serialize(parts);
        var checkpointJson = JsonSerializer.Serialize(new RemoteDriveCheckpoint
        {
            Name = "large-manifest.bin", PartSize = 16 * 1024 * 1024, Parts = partDigests
        });
        var transfer = new RemoteDriveTransfer
        {
            Id = Guid.NewGuid(), ConnectionId = connection.Id, StoreName = connection.StoreName, ScopeKey = connection.ScopeKey,
            Container = "attachments", ObjectKey = Guid.NewGuid().ToString("N"), StageStoreName = "stage-v1",
            StageContainer = "stage", StageObjectKey = Guid.NewGuid().ToString("N"), StageMetadataJson = "{}", MetadataJson = "{}",
            ManifestJson = manifestJson, CheckpointJson = checkpointJson, ContentType = "application/octet-stream",
            Length = 100L * 1024 * 1024 * 1024, PartSize = 16 * 1024 * 1024,
            State = RemoteDriveTransferState.Receiving, ExpiresAt = now + 60_000, CreatedAt = now, Version = 1
        };
        Assert.True(await remote.TryCreateTransferAsync(transfer));

        var saved = await remote.FindTransferAsync(transfer.Id);
        Assert.NotNull(saved);
        Assert.Equal(0, saved!.CleanupNextAttemptAt);
        Assert.Null(saved.CleanupErrorCode);
        saved.State = RemoteDriveTransferState.Aborted;
        Assert.True(await remote.TryUpdateTransferAsync(saved, saved.Version));
        Assert.True(await remote.TryDeferCleanupAsync(saved.Id, saved.Version, now, now + 300000, "connection_unavailable"));
        var deferred = (await remote.FindTransferAsync(saved.Id))!;
        Assert.Equal(now + 300000, deferred.CleanupNextAttemptAt);
        Assert.Equal("connection_unavailable", deferred.CleanupErrorCode);
        Assert.Equal(manifestJson, saved.ManifestJson);
        Assert.Equal(checkpointJson, saved.CheckpointJson);
        Assert.Equal(10_000, JsonSerializer.Deserialize<RemoteDrivePartReceipt[]>(saved.ManifestJson)!.Length);
        Assert.Equal(10_000, JsonSerializer.Deserialize<RemoteDriveCheckpoint>(saved.CheckpointJson)!.Parts.Count);
    }

    private static async Task InsertLegacyFileAsync(SqliteConnection connection, Guid fileId, Guid sessionId, long now, bool useFreeSql)
    {
        string Id(Guid value) => useFreeSql ? value.ToString("D") : value.ToString("D").ToUpperInvariant();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO BingFileRecords
                    (Id, RequestKey, ScopeKey, TenantId, ScopeKind, StoreName, Container, ObjectKey, FileName,
                     Length, ContentType, Visibility, State, CreatorId, CreatedAt, Version, Sha256)
                VALUES ($id, $request, 'host:remote-drive-upgrade', NULL, $scopeKind, 'local-v1', 'attachments', $objectKey,
                        'legacy-before-remote-drives.bin', 73, 'application/octet-stream', 0, $state, 'legacy-owner', $createdAt, 1, NULL);
                """;
            command.Parameters.AddWithValue("$id", Id(fileId));
            command.Parameters.AddWithValue("$request", Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("$scopeKind", (int)StorageScopeKind.Host);
            command.Parameters.AddWithValue("$objectKey", fileId.ToString("N"));
            command.Parameters.AddWithValue("$state", (int)FileState.Ready);
            command.Parameters.AddWithValue("$createdAt", now);
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO BingFileUploadSessions
                    (Id, FileId, OwnerId, BackendUploadId, BackendMetadataJson, TotalLength, PartSize,
                     State, WriteMode, ExpiresAt, AbsoluteExpiresAt, LastActivityAt, ClientLastModified, Version)
                VALUES ($id, $fileId, 'legacy-owner', NULL, '{}', 73, 5, $state, 0, $expiresAt, $expiresAt, $createdAt, 0, 1);
                """;
            command.Parameters.AddWithValue("$id", Id(sessionId));
            command.Parameters.AddWithValue("$fileId", Id(fileId));
            command.Parameters.AddWithValue("$state", (int)FileState.Ready);
            command.Parameters.AddWithValue("$expiresAt", now + 60_000);
            command.Parameters.AddWithValue("$createdAt", now);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static async Task VerifyRemoteObjectAddressIndexAsync(SqliteConnection connection)
    {
        var idIsPrimaryKey = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(\"BingRemoteDriveObjects\")";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                if (string.Equals(reader.GetString(1), "Id", StringComparison.OrdinalIgnoreCase))
                    idIsPrimaryKey = reader.GetInt32(5) == 1;
        }

        Assert.True(idIsPrimaryKey);
        var objectIndexes = await ReadIndexesAsync(connection, "BingRemoteDriveObjects");
        Assert.Equal(1, objectIndexes.Count(index => index.Unique));
        Assert.DoesNotContain(objectIndexes, index => index.Columns.SequenceEqual(AddressColumns));

        var transferIndexes = await ReadIndexesAsync(connection, "BingRemoteDriveTransfers");
        Assert.DoesNotContain(transferIndexes, index => index.Columns.SequenceEqual(AddressColumns));

        var addressId = RemoteDriveObject.Key("store", "scope", new FileAddress("attachments", "folder/object"));
        Assert.Equal(64, addressId.Length);
        Assert.Equal(addressId, RemoteDriveObject.Key("store", "scope", new FileAddress("attachments", "folder/object")));
        Assert.NotEqual(addressId, RemoteDriveObject.Key("store", "scope", new FileAddress("attachments", "folder/other")));
    }

    private static readonly string[] AddressColumns = { "StoreName", "ScopeKey", "Container", "ObjectKey" };

    private static async Task<IReadOnlyList<SqliteIndex>> ReadIndexesAsync(SqliteConnection connection, string table)
    {
        var indexNames = new List<(string Name, bool Unique)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA index_list(\"{table}\")";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                indexNames.Add((reader.GetString(1), reader.GetInt32(2) == 1));
        }

        var indexes = new List<SqliteIndex>();
        foreach (var (name, unique) in indexNames)
        {
            var quotedName = name.Replace("\"", "\"\"", StringComparison.Ordinal);
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA index_info(\"{quotedName}\")";
            await using var reader = await command.ExecuteReaderAsync();
            var columns = new List<string>();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(2));
            indexes.Add(new SqliteIndex(unique, columns));
        }

        return indexes;
    }

    private sealed record SqliteIndex(bool Unique, IReadOnlyList<string> Columns);

    private static async Task ExecuteSqlAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
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
}
