#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Biz.FileManagement;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
using System.Data;
using System.Data.Common;
using Bing.Biz.FileManagement.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
#endif

#if BING_ADMIN_EFCORE_TESTS
namespace Bing.Admin.EFCore.Tests;
#else
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>验证两种 ORM 对 MySQL 8 RemoteDrive 增量脚本及持久化映射的兼容性。</summary>
public sealed class AdminRemoteDriveMySqlIntegrationTests
{
    [AdminFileManagementMySqlFact]
    public async Task MySql8RemoteDriveUpgrade_AddsIncrementalSchemaAndPreservesExistingFiles()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!;
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(serverConnectionString);
        await using var environment = new AdminFileHttpEnvironment(database.DatabaseConnectionString);
        var services = new ServiceCollection();
        environment.Configure(services, configureTestAuthentication: false);
        await using var provider = services.BuildServiceProvider();
        await environment.InitializeDatabaseAsync(provider);

        var scope = StorageScope.Host;
        var legacy = CreateFile(scope, "legacy-personal.bin", RemoteDriveOwnership.Personal);
        using (var seedScope = provider.CreateScope())
        {
            var store = seedScope.ServiceProvider.GetRequiredService<IFileManagementStore>();
            Assert.True(await store.TryCreateAsync(new FileEntry { File = legacy }));
        }

        await using (var connection = new MySqlConnection(database.DatabaseConnectionString))
        {
            await connection.OpenAsync();
            await AssertMySql8Async(connection);
            await DropRemoteDriveSchemaAsync(connection);
            await ExecuteSqlScriptAsync(connection, ReadRepositoryFile(IsEfCore
                ? "docs/migrations/file-management-remote-drives/efcore-mysql8.sql"
                : "docs/migrations/file-management-remote-drives/freesql-mysql8.sql"));
            await ExecuteSqlScriptAsync(connection, ReadRepositoryFile(IsEfCore
                ? "docs/migrations/file-management-remote-drives/efcore-cleanup-retry-mysql8.sql"
                : "docs/migrations/file-management-remote-drives/freesql-cleanup-retry-mysql8.sql"));

            foreach (var table in new[]
            {
                "BingRemoteDriveConnections", "BingRemoteDriveObjects", "BingRemoteDriveTransfers",
                "BingFileStoragePreferences", "BingFileBindingCopyRequests"
            })
                Assert.True(await TableExistsAsync(connection, table), $"Missing table {table}.");
            Assert.True(await ColumnExistsAsync(connection, "BingFileRecords", "ConnectionId"));
            Assert.True(await ColumnExistsAsync(connection, "BingFileRecords", "Ownership"));
            Assert.True(await ColumnExistsAsync(connection, "BingFileUploadSessions", "RequestedStorageTargetId"));
            Assert.True(await ColumnExistsAsync(connection, "BingRemoteDriveTransfers", "CleanupNextAttemptAt"));
            Assert.True(await ColumnExistsAsync(connection, "BingRemoteDriveTransfers", "CleanupErrorCode"));
            Assert.True(await IndexExistsAsync(connection, "BingRemoteDriveTransfers", "IX_BingRemoteDriveTransfers_CleanupRetry"));
        }

        using (var verifyScope = provider.CreateScope())
        {
            var scoped = verifyScope.ServiceProvider;
            var store = scoped.GetRequiredService<IFileManagementStore>();
            var remote = scoped.GetRequiredService<IRemoteDriveStateStore>();
            var targets = scoped.GetRequiredService<IFileStorageTargetStore>();
            var restored = await store.FindAsync(legacy.Id);
            Assert.NotNull(restored);
            Assert.Equal("legacy-personal.bin", restored!.File.FileName);
            Assert.Equal(FileState.Ready, restored.File.State);
            Assert.Equal(RemoteDriveOwnership.Shared, restored.File.Ownership);
            Assert.Null(restored.File.ConnectionId);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var connection = new RemoteDriveConnection
            {
                Id = Guid.NewGuid(), TemplateName = "my-drive", Provider = "mysql-test", StoreName = "mysql-drive-v1",
                ScopeKind = scope.Kind, ScopeKey = scope.Key, Ownership = RemoteDriveOwnership.Shared, OwnerId = "mysql-admin",
                DisplayName = "MySQL schema test", RemoteAccountId = "account", DriveId = "drive", RootFolderId = "root",
                EncryptedCredentials = "protected", EncryptedAuthorization = "{}", State = RemoteDriveConnectionState.Active,
                CreatedAt = now, Version = 1
            };
            Assert.True(await remote.TryCreateConnectionAsync(connection));
            Assert.Equal(connection.Id, (await remote.FindConnectionAsync(connection.Id))!.Id);

            var preference = new FileStoragePreference
            {
                Id = Guid.NewGuid().ToString("N"), ScopeKey = scope.Key, OwnerId = "", Container = "attachments",
                TargetId = connection.StoreName, Version = 1
            };
            Assert.True(await targets.SavePreferenceAsync(preference, expectedVersion: 0));
            Assert.Equal(connection.StoreName,
                (await targets.FindPreferenceAsync(scope.Key, "", "attachments"))!.TargetId);

            var transfer = new RemoteDriveTransfer
            {
                Id = Guid.NewGuid(), ConnectionId = connection.Id, StoreName = connection.StoreName, ScopeKey = scope.Key,
                Container = "attachments", ObjectKey = Guid.NewGuid().ToString("N"), StageStoreName = "stage-v1",
                StageContainer = "stage", StageObjectKey = Guid.NewGuid().ToString("N"), Length = 0, PartSize = 16 * 1024 * 1024,
                ContentType = "application/octet-stream", State = RemoteDriveTransferState.Aborted,
                ExpiresAt = now - 1, CreatedAt = now, Version = 1
            };
            Assert.True(await remote.TryCreateTransferAsync(transfer));
            Assert.True(await remote.TryDeferCleanupAsync(transfer.Id, transfer.Version, now, now + 300_000,
                "connection_unavailable"));
            var deferred = (await remote.FindTransferAsync(transfer.Id))!;
            Assert.Equal(now + 300_000, deferred.CleanupNextAttemptAt);
            Assert.Equal("connection_unavailable", deferred.CleanupErrorCode);

            var source = CreateFile(scope, "personal-source.bin", RemoteDriveOwnership.Personal);
            Assert.True(await store.TryCreateAsync(new FileEntry { File = source }));
            var (copyRequest, copyTarget, operation) = CreateCopy(source, now);
            Assert.True(await targets.CreateCopyAsync(copyRequest, copyTarget, operation));
            var savedRequest = (await targets.FindCopyAsync(copyRequest.Id))!;
            var savedTarget = (await store.FindAsync(copyRequest.CopyFileId))!;
            savedRequest.State = FileBindingCopyState.Copied;
            savedTarget.File.State = FileState.Ready;
            Assert.True(await targets.UpdateCopyAsync(savedRequest, savedRequest.Version, savedTarget,
                savedTarget.File.Version, validateSource: true));
            Assert.Equal(FileBindingCopyState.Copied, (await targets.FindCopyAsync(copyRequest.Id))!.State);
        }
    }

    private static FileRecord CreateFile(StorageScope scope, string name, RemoteDriveOwnership ownership)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new FileRecord
        {
            Id = id, RequestKey = Guid.NewGuid().ToString("N"), ScopeKey = scope.Key, TenantId = scope.TenantId,
            ScopeKind = scope.Kind, StoreName = "migration-source", Container = "attachments", ObjectKey = id.ToString("N"),
            FileName = name, Length = 0, ContentType = "application/octet-stream", Visibility = FileVisibility.Private,
            State = FileState.Ready, CreatorId = "mysql-owner", CreatedAt = now, Version = 1, Ownership = ownership
        };
    }

    private static (FileBindingCopyRequest Request, FileEntry Target, FileOperation Operation) CreateCopy(FileRecord source, long now)
    {
        var copyFileId = Guid.NewGuid();
        var request = new FileBindingCopyRequest
        {
            Id = Guid.NewGuid(), RequestKey = Guid.NewGuid().ToString("N"), ScopeKey = source.ScopeKey,
            OwnerId = source.CreatorId, SourceFileId = source.Id, SourceVersion = source.Version,
            CopyFileId = copyFileId, BusinessType = "test", BusinessId = "mysql-copy", CreatedAt = now, Version = 1
        };
        var file = new FileRecord
        {
            Id = copyFileId, RequestKey = Guid.NewGuid().ToString("N"), ScopeKey = source.ScopeKey,
            TenantId = source.TenantId, ScopeKind = source.ScopeKind, StoreName = "mysql-public-v1",
            Container = source.Container, ObjectKey = copyFileId.ToString("N"), FileName = source.FileName,
            Length = source.Length, ContentType = source.ContentType, Visibility = FileVisibility.Private,
            State = FileState.Pending, CreatorId = source.CreatorId, CreatedAt = now, Version = 1,
            Ownership = RemoteDriveOwnership.Shared
        };
        var operation = new FileOperation
        {
            Id = Guid.NewGuid(), FileId = copyFileId, Kind = FileOperationKind.CopyForBinding,
            State = FileOperationState.Pending, Payload = request.Id.ToString("N"), NextAttemptAt = now,
            Version = 1, CreatedAt = now
        };
        return (request, new FileEntry { File = file }, operation);
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

    private static async Task AssertMySql8Async(MySqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT VERSION()";
        var version = Convert.ToString(await command.ExecuteScalarAsync());
        Assert.StartsWith("8.", version, StringComparison.Ordinal);
    }

    private static async Task DropRemoteDriveSchemaAsync(MySqlConnection connection)
    {
        foreach (var table in new[]
        {
            "BingFileBindingCopyRequests", "BingFileStoragePreferences", "BingRemoteDriveObjects",
            "BingRemoteDriveTransfers", "BingRemoteDriveConnections"
        })
            await ExecuteStatementAsync(connection, $"DROP TABLE `{table}`");
        await ExecuteStatementAsync(connection,
            "ALTER TABLE `BingFileRecords` DROP INDEX `IX_BingFileRecords_ConnectionId`, DROP COLUMN `ConnectionId`, DROP COLUMN `Ownership`");
        await ExecuteStatementAsync(connection,
            "ALTER TABLE `BingFileUploadSessions` DROP COLUMN `RequestedStorageTargetId`");
    }

    private static Task<bool> TableExistsAsync(MySqlConnection connection, string table)
        => ExistsAsync(connection,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @name", table);

    private static Task<bool> ColumnExistsAsync(MySqlConnection connection, string table, string column)
        => ExistsAsync(connection,
            "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @name",
            table, column);

    private static Task<bool> IndexExistsAsync(MySqlConnection connection, string table, string index)
        => ExistsAsync(connection,
            "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND INDEX_NAME = @name",
            table, index);

    private static async Task<bool> ExistsAsync(MySqlConnection connection, string sql, string name, string? second = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (sql.Contains("@table", StringComparison.Ordinal)) command.Parameters.AddWithValue("@table", name);
        if (second == null) command.Parameters.AddWithValue("@name", name);
        else command.Parameters.AddWithValue("@name", second);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task ExecuteSqlScriptAsync(MySqlConnection connection, string script)
    {
        var withoutLineComments = string.Join("\n", script.Split('\n')
            .Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        foreach (var statement in withoutLineComments.Split(';'))
        {
            var sql = statement.Trim();
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

#if BING_ADMIN_EFCORE_TESTS
/// <summary>在 EF Core MySQL 上验证复制事务与并发源文件删除的串行化顺序。</summary>
public sealed class AdminRemoteDriveMySqlConcurrencyTests
{
    [AdminFileManagementMySqlFact]
    public async Task MySqlSerializableCopyCreationAndPublication_OrderBeforeSourceDeletion()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable("BING_FILE_MANAGEMENT_MYSQL_TEST_CONNECTION")!;
        await using var database = await AdminFileMySqlTestDatabase.CreateAsync(serverConnectionString);
        var readBarrier = new SourceReadBarrier();
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 40));
        var primaryOptions = new DbContextOptionsBuilder<FileManagementDbContext>()
            .UseMySql(database.DatabaseConnectionString, serverVersion)
            .AddInterceptors(readBarrier)
            .Options;

        await using (var db = new FileManagementDbContext(primaryOptions))
            await db.Database.EnsureCreatedAsync();

        var primary = new EfFileManagementStore(new ContextFactory(primaryOptions));
        await AssertCopyDeletionRaceAsync(primary, readBarrier, database.DatabaseConnectionString, updateCopy: false);
        await AssertCopyDeletionRaceAsync(primary, readBarrier, database.DatabaseConnectionString, updateCopy: true);
    }

    private static async Task AssertCopyDeletionRaceAsync(EfFileManagementStore primary, SourceReadBarrier readBarrier,
        string connectionString, bool updateCopy)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var source = new FileRecord
        {
            Id = Guid.NewGuid(), RequestKey = "source-" + Guid.NewGuid().ToString("N"), ScopeKey = "scope-a",
            ScopeKind = StorageScopeKind.Host, StoreName = "personal-store", Container = "attachments",
            ObjectKey = Guid.NewGuid().ToString("N"), FileName = "source.txt", Length = 12, ContentType = "text/plain",
            Visibility = FileVisibility.Private, State = FileState.Ready, CreatorId = "user-a",
            Ownership = RemoteDriveOwnership.Personal, CreatedAt = now, Version = 1
        };
        Assert.True(await primary.TryCreateAsync(new FileEntry { File = source }));

        var copyFileId = Guid.NewGuid();
        var request = new FileBindingCopyRequest
        {
            Id = Guid.NewGuid(), RequestKey = "copy-" + Guid.NewGuid().ToString("N"), ScopeKey = source.ScopeKey,
            OwnerId = source.CreatorId, SourceFileId = source.Id, SourceVersion = source.Version,
            CopyFileId = copyFileId, BusinessType = "orders", BusinessId = "mysql-race", CreatedAt = now + 1
        };
        var target = new FileEntry
        {
            File = new FileRecord
            {
                Id = copyFileId, RequestKey = "target-" + request.RequestKey, ScopeKey = source.ScopeKey,
                ScopeKind = source.ScopeKind, StoreName = "shared-store", Container = source.Container,
                ObjectKey = "copy-" + copyFileId.ToString("N"), FileName = source.FileName, Length = source.Length,
                ContentType = source.ContentType, Visibility = FileVisibility.Private, State = FileState.Pending,
                CreatorId = source.CreatorId, Ownership = RemoteDriveOwnership.Shared, CreatedAt = now + 1, Version = 1
            }
        };
        var operation = new FileOperation
        {
            Id = Guid.NewGuid(), FileId = copyFileId, Kind = FileOperationKind.CopyForBinding,
            State = FileOperationState.Pending, Payload = request.Id.ToString("N"), NextAttemptAt = now,
            Version = 1, CreatedAt = now + 1
        };
        var targets = (IFileStorageTargetStore)primary;
        FileBindingCopyRequest? publishRequest = null;
        FileEntry? publishTarget = null;
        if (updateCopy)
        {
            Assert.True(await targets.CreateCopyAsync(request, target, operation));
            publishRequest = (await targets.FindCopyAsync(request.Id))!;
            publishTarget = (await primary.FindAsync(copyFileId))!;
            publishRequest.State = FileBindingCopyState.Copied;
            publishTarget.File.State = FileState.Ready;
        }

        readBarrier.Arm();
        var copyTask = Task.Run(async () => await CaptureAsync(async () =>
        {
            if (!updateCopy) return await targets.CreateCopyAsync(request, target, operation);
            return await targets.UpdateCopyAsync(publishRequest!, publishRequest!.Version, publishTarget!,
                publishTarget!.File.Version, validateSource: true);
        }));
        Task<Attempt<bool>>? mutationTask = null;
        try
        {
            await readBarrier.SourceRead.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal<IsolationLevel?>(IsolationLevel.Serializable, readBarrier.SourceReadIsolation);

            var mutationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            mutationTask = Task.Run(async () => await CaptureAsync(async () =>
            {
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE `BingFileRecords` SET `State` = @state, `Version` = `Version` + 1 WHERE `Id` = @id AND `Version` = @version";
                command.Parameters.Add("@state", MySqlDbType.Int32).Value = (int)FileState.DeletePending;
                command.Parameters.Add("@id", MySqlDbType.VarChar).Value = source.Id.ToString("D");
                command.Parameters.Add("@version", MySqlDbType.Int64).Value = source.Version;
                mutationStarted.TrySetResult(true);
                return await command.ExecuteNonQueryAsync() == 1;
            }));
            await mutationStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAny(mutationTask, Task.Delay(TimeSpan.FromMilliseconds(250)));
            Assert.False(mutationTask.IsCompleted, "源删除在复制事务释放源读取之前已提交。");

            readBarrier.Release();
            var copyResult = await copyTask.WaitAsync(TimeSpan.FromSeconds(20));
            var mutationResult = await mutationTask.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Null(copyResult.Error);
            Assert.True(copyResult.Value, copyResult.Error?.ToString());
            Assert.Null(mutationResult.Error);
            Assert.True(mutationResult.Value, mutationResult.Error?.ToString());
            Assert.Equal(FileState.DeletePending, (await primary.FindAsync(source.Id))!.File.State);
            if (updateCopy)
                Assert.Equal(FileBindingCopyState.Copied, (await targets.FindCopyAsync(request.Id))!.State);
            else
                Assert.Equal(FileBindingCopyState.Pending, (await targets.FindCopyAsync(request.Id))!.State);
        }
        finally
        {
            readBarrier.Release();
            readBarrier.Disarm();
            await copyTask;
            if (mutationTask != null) await mutationTask;
        }
    }

    private static async Task<Attempt<T>> CaptureAsync<T>(Func<Task<T>> action)
    {
        try { return new Attempt<T>(await action(), null); }
        catch (Exception exception) { return new Attempt<T>(default!, exception); }
    }

    private sealed record Attempt<T>(T Value, Exception? Error);

    private sealed class ContextFactory : IDbContextFactory<FileManagementDbContext>
    {
        private readonly DbContextOptions<FileManagementDbContext> _options;
        public ContextFactory(DbContextOptions<FileManagementDbContext> options) => _options = options;
        public FileManagementDbContext CreateDbContext() => new(_options);
        public Task<FileManagementDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class SourceReadBarrier : DbCommandInterceptor
    {
        private readonly object _sync = new();
        private TaskCompletionSource<bool> _sourceRead = NewSignal();
        private TaskCompletionSource<bool> _release = NewSignal();
        private int _fileReads;
        private volatile bool _armed;

        public Task SourceRead => _sourceRead.Task;
        public IsolationLevel? SourceReadIsolation { get; private set; }

        public void Arm()
        {
            lock (_sync)
            {
                _sourceRead = NewSignal();
                _release = NewSignal();
                _fileReads = 0;
                SourceReadIsolation = null;
                _armed = true;
            }
        }

        public void Release() => _release.TrySetResult(true);
        public void Disarm() => _armed = false;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!_armed || !command.CommandText.Contains("BingFileRecords", StringComparison.OrdinalIgnoreCase) ||
                Interlocked.Increment(ref _fileReads) != 2) return result;
            SourceReadIsolation = eventData.Context?.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel;
            _sourceRead.TrySetResult(true);
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }

        private static TaskCompletionSource<bool> NewSignal()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

}
#endif
