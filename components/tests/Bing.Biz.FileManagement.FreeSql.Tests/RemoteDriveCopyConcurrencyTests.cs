using System.Data;
using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.FreeSql;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using FreeSql;
using MySqlConnector;
using Xunit;

namespace Bing.Biz.FileManagement.FreeSql.Tests;

public sealed class RemoteDriveCopyConcurrencyTests
{
    [Theory]
    [InlineData(FileState.DeletePending, 2)]
    [InlineData(FileState.Ready, 2)]
    public async Task CreateCopy_RejectsDeletedOrStaleSource(FileState state, long version)
    {
        await using var fixture = await StoreFixture.CreateSqliteAsync();
        var source = Source();
        await fixture.Primary.Insert(source).ExecuteAffrowsAsync();
        source.State = state;
        source.Version = version;
        Assert.Equal(1, await fixture.Primary.Update<FileRecord>().SetSource(source)
            .Where(x => x.Id == source.Id && x.Version == 1).ExecuteAffrowsAsync());

        var copy = Copy(source);
        copy.Request.SourceVersion = 1;
        Assert.False(await fixture.Store.CreateCopyAsync(copy.Request, copy.Target, copy.Operation));
        Assert.Equal(0, await fixture.Primary.Select<FileBindingCopyRequest>().CountAsync());
    }

    [Theory]
    [InlineData(FileState.DeletePending)]
    [InlineData(FileState.Ready)]
    public async Task PublishCopy_RejectsChangedSource_AndAcceptsUnchangedSource(FileState changedState)
    {
        await using var fixture = await StoreFixture.CreateSqliteAsync();
        var source = Source();
        await fixture.Primary.Insert(source).ExecuteAffrowsAsync();
        var copy = Copy(source);
        Assert.True(await fixture.Store.CreateCopyAsync(copy.Request, copy.Target, copy.Operation));

        source.State = changedState;
        source.Version++;
        Assert.Equal(1, await fixture.Primary.Update<FileRecord>().SetSource(source)
            .Where(x => x.Id == source.Id && x.Version == 1).ExecuteAffrowsAsync());

        var request = (await fixture.Targets.FindCopyAsync(copy.Request.Id))!;
        var target = (await fixture.Store.FindAsync(copy.Request.CopyFileId))!;
        request.State = FileBindingCopyState.Copied;
        target.File.State = FileState.Ready;
        Assert.False(await fixture.Targets.UpdateCopyAsync(request, request.Version, target, target.File.Version, validateSource: true));

        var readySource = Source();
        await fixture.Primary.Insert(readySource).ExecuteAffrowsAsync();
        var validCopy = Copy(readySource);
        Assert.True(await fixture.Store.CreateCopyAsync(validCopy.Request, validCopy.Target, validCopy.Operation));
        var validRequest = (await fixture.Targets.FindCopyAsync(validCopy.Request.Id))!;
        var validTarget = (await fixture.Store.FindAsync(validCopy.Request.CopyFileId))!;
        validRequest.State = FileBindingCopyState.Copied;
        validTarget.File.State = FileState.Ready;
        Assert.True(await fixture.Targets.UpdateCopyAsync(validRequest, validRequest.Version, validTarget,
            validTarget.File.Version, validateSource: true));
    }

    [Fact]
    public async Task SerializableCopyTransactions_OrderPublicationAndSourceDeletion()
    {
        await using var fixture = await StoreFixture.CreateSqliteAsync();
        await AssertCopySourceRaceAsync(fixture, updateCopy: false);
        await AssertCopySourceRaceAsync(fixture, updateCopy: true);
    }

    [MySqlIntegrationFact]
    public async Task MySqlSerializableCopyTransactions_OrderPublicationAndSourceDeletion()
    {
        await using var fixture = await StoreFixture.CreateMySqlAsync(
            Environment.GetEnvironmentVariable("BING_FILEMANAGEMENT_MYSQL_TEST_CONNECTION")!);
        await AssertCopySourceRaceAsync(fixture, updateCopy: false);
        await AssertCopySourceRaceAsync(fixture, updateCopy: true);
    }

    private static async Task AssertCopySourceRaceAsync(StoreFixture fixture, bool updateCopy)
    {
        var source = Source();
        await fixture.Primary.Insert(source).ExecuteAffrowsAsync();
        var copy = Copy(source);
        if (updateCopy)
            Assert.True(await fixture.Store.CreateCopyAsync(copy.Request, copy.Target, copy.Operation));
        var publishRequest = updateCopy ? (await fixture.Targets.FindCopyAsync(copy.Request.Id))! : null;
        var publishTarget = updateCopy ? (await fixture.Store.FindAsync(copy.Request.CopyFileId))! : null;

        var fileSelects = 0;
        IsolationLevel? sourceReadIsolation = null;
        var sourceRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSourceRead = new ManualResetEventSlim();
        var sourceWriteStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceWriteCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sourceReadHandler = new EventHandler<global::FreeSql.Aop.CommandAfterEventArgs>((_, args) =>
        {
            if (!args.Command.CommandText.Contains("BingFileRecords", StringComparison.OrdinalIgnoreCase))
                return;
            if (Interlocked.Increment(ref fileSelects) != 2)
                return;
            sourceReadIsolation = args.Command.Transaction?.IsolationLevel;
            sourceRead.TrySetResult(true);
            if (!releaseSourceRead.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("复制事务未及时释放源读取屏障。");
        });
        var writeBeforeHandler = new EventHandler<global::FreeSql.Aop.CommandBeforeEventArgs>((_, args) =>
        {
            if (args.Command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                args.Command.CommandText.Contains("BingFileRecords", StringComparison.OrdinalIgnoreCase))
                sourceWriteStarted.TrySetResult(true);
        });
        var writeAfterHandler = new EventHandler<global::FreeSql.Aop.CommandAfterEventArgs>((_, args) =>
        {
            if (args.Command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                args.Command.CommandText.Contains("BingFileRecords", StringComparison.OrdinalIgnoreCase))
                sourceWriteCompleted.TrySetResult(args.Exception == null);
        });

        fixture.Primary.Aop.CommandAfter += sourceReadHandler;
        fixture.Other.Aop.CommandBefore += writeBeforeHandler;
        fixture.Other.Aop.CommandAfter += writeAfterHandler;
        Task<Attempt<bool>>? copyTask = null;
        try
        {
            copyTask = Task.Run(() => CaptureAsync(async () =>
            {
                if (!updateCopy)
                    return await fixture.Targets.CreateCopyAsync(copy.Request, copy.Target, copy.Operation);

                publishRequest!.State = FileBindingCopyState.Copied;
                publishTarget!.File.State = FileState.Ready;
                return await fixture.Targets.UpdateCopyAsync(publishRequest, publishRequest.Version, publishTarget,
                    publishTarget.File.Version, validateSource: true);
            }));
            await sourceRead.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(IsolationLevel.Serializable, sourceReadIsolation);

            var changedSource = Clone(source);
            changedSource.State = FileState.DeletePending;
            changedSource.Version++;
            var mutationTask = Task.Run(() => CaptureAsync(async () => await fixture.Other.Update<FileRecord>().SetSource(changedSource)
                .Where(x => x.Id == source.Id && x.Version == source.Version).ExecuteAffrowsAsync() == 1));
            await sourceWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAny(sourceWriteCompleted.Task, Task.Delay(TimeSpan.FromMilliseconds(200)));
            var writerSqlSucceeded = sourceWriteCompleted.Task.IsCompletedSuccessfully && await sourceWriteCompleted.Task;
            var writerCommittedBeforeRelease = writerSqlSucceeded && (await mutationTask).Error == null &&
                                               (await mutationTask).Value;
            Assert.False(writerCommittedBeforeRelease);

            releaseSourceRead.Set();
            var copyResult = await copyTask;
            Assert.True(copyResult.Error == null || IsExpectedContention(copyResult.Error), copyResult.Error?.ToString());
            var mutationResult = await mutationTask;
            Assert.True(mutationResult.Error == null || IsExpectedContention(mutationResult.Error), mutationResult.Error?.ToString());

            Assert.True(copyResult.Value, copyResult.Error?.ToString());

            if (!mutationResult.Value)
            {
                var current = await fixture.Primary.Select<FileRecord>().Where(x => x.Id == source.Id).FirstAsync();
                if (current?.State != FileState.DeletePending || current.Version != changedSource.Version)
                {
                    var retry = await CaptureAsync(async () => await fixture.Other.Update<FileRecord>().SetSource(changedSource)
                        .Where(x => x.Id == source.Id && x.Version == source.Version).ExecuteAffrowsAsync() == 1);
                    Assert.Null(retry.Error);
                    Assert.True(retry.Value);
                }
            }
            if (updateCopy)
                Assert.Equal(FileBindingCopyState.Copied, (await fixture.Targets.FindCopyAsync(copy.Request.Id))!.State);
        }
        finally
        {
            releaseSourceRead.Set();
            if (copyTask != null)
                await copyTask;
            fixture.Primary.Aop.CommandAfter -= sourceReadHandler;
            fixture.Other.Aop.CommandBefore -= writeBeforeHandler;
            fixture.Other.Aop.CommandAfter -= writeAfterHandler;
            releaseSourceRead.Dispose();
        }
    }

    private static bool IsExpectedContention(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            var type = current.GetType();
            var code = type.GetProperty("SQLiteErrorCode")?.GetValue(current) ??
                       type.GetProperty("SqliteErrorCode")?.GetValue(current) ??
                       type.GetProperty("ExtendedErrorCode")?.GetValue(current) ??
                       type.GetProperty("ResultCode")?.GetValue(current) ??
                       type.GetProperty("ErrorCode")?.GetValue(current);
            if ((type.Name.Contains("SQLiteException", StringComparison.OrdinalIgnoreCase) ||
                 type.Name.Contains("SqliteException", StringComparison.OrdinalIgnoreCase)) && code != null &&
                (Convert.ToInt32(code) & 0xff) is 5 or 6)
                return true;
            if (current is MySqlException mysql && mysql.Number is 1205 or 1213)
                return true;
        }
        return false;
    }

    private static async Task<Attempt<T>> CaptureAsync<T>(Func<Task<T>> action)
    {
        try { return new Attempt<T>(await action(), null); }
        catch (Exception exception) { return new Attempt<T>(default!, exception); }
    }

    private static FileRecord Source() => new()
    {
        Id = Guid.NewGuid(), RequestKey = "source-" + Guid.NewGuid().ToString("N"), ScopeKey = "scope-a",
        ScopeKind = StorageScopeKind.Host, StoreName = "personal-store", Container = "attachments",
        ObjectKey = Guid.NewGuid().ToString("N"), FileName = "source.txt", Length = 12, ContentType = "text/plain",
        Visibility = FileVisibility.Private, State = FileState.Ready, CreatorId = "user-a",
        Ownership = RemoteDriveOwnership.Personal, CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Version = 1
    };

    private static (FileBindingCopyRequest Request, FileEntry Target, FileOperation Operation) Copy(FileRecord source)
    {
        var id = Guid.NewGuid();
        var request = new FileBindingCopyRequest
        {
            Id = Guid.NewGuid(), RequestKey = "copy-" + Guid.NewGuid().ToString("N"), ScopeKey = source.ScopeKey,
            OwnerId = source.CreatorId, SourceFileId = source.Id, SourceVersion = source.Version,
            CopyFileId = id, BusinessType = "orders", BusinessId = "order-1", CreatedAt = source.CreatedAt + 1
        };
        var target = new FileRecord
        {
            Id = id, RequestKey = "target-" + request.RequestKey, ScopeKey = source.ScopeKey, ScopeKind = source.ScopeKind,
            StoreName = "shared-store", Container = source.Container, ObjectKey = "copy-" + id.ToString("N"),
            FileName = source.FileName, Length = source.Length, ContentType = source.ContentType, Visibility = FileVisibility.Private,
            State = FileState.Pending, CreatorId = source.CreatorId, Ownership = RemoteDriveOwnership.Shared,
            CreatedAt = request.CreatedAt, Version = 1
        };
        var operation = new FileOperation
        {
            Id = Guid.NewGuid(), FileId = id, Kind = FileOperationKind.CopyForBinding,
            State = FileOperationState.Pending, Payload = request.Id.ToString("N"), NextAttemptAt = 0, Version = 1,
            CreatedAt = request.CreatedAt
        };
        return (request, new FileEntry { File = target }, operation);
    }

    private static FileRecord Clone(FileRecord value) => new()
    {
        Id = value.Id, RequestKey = value.RequestKey, ScopeKey = value.ScopeKey, TenantId = value.TenantId,
        ScopeKind = value.ScopeKind, StoreName = value.StoreName, ConnectionId = value.ConnectionId,
        Ownership = value.Ownership, Container = value.Container, ObjectKey = value.ObjectKey,
        FileName = value.FileName, Length = value.Length, ContentType = value.ContentType, Visibility = value.Visibility,
        State = value.State, CreatorId = value.CreatorId, CreatedAt = value.CreatedAt, Version = value.Version,
        Sha256 = value.Sha256, ContentPolicyJson = value.ContentPolicyJson
    };

    private sealed record Attempt<T>(T Value, Exception? Error);

    private sealed class StoreFixture : IAsyncDisposable
    {
        private readonly string? _sqlitePath;
        private readonly string? _mysqlDatabase;
        private readonly string? _mysqlAdminConnectionString;

        private StoreFixture(IFreeSql primary, IFreeSql other, string? sqlitePath = null,
            string? mysqlDatabase = null, string? mysqlAdminConnectionString = null)
        {
            Primary = primary;
            Other = other;
            Store = new FreeSqlFileManagementStore(primary);
            Targets = (IFileStorageTargetStore)Store;
            _sqlitePath = sqlitePath;
            _mysqlDatabase = mysqlDatabase;
            _mysqlAdminConnectionString = mysqlAdminConnectionString;
        }

        internal IFreeSql Primary { get; }
        internal IFreeSql Other { get; }
        internal FreeSqlFileManagementStore Store { get; }
        internal IFileStorageTargetStore Targets { get; }

        internal static Task<StoreFixture> CreateSqliteAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"bing-file-copy-race-{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={path};Default Timeout=5";
            var primary = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, connectionString).UseAutoSyncStructure(false).Build();
            FileManagementFreeSqlSchema.SyncStructure(primary);
            var other = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, connectionString).UseAutoSyncStructure(false).Build();
            return Task.FromResult(new StoreFixture(primary, other, sqlitePath: path));
        }

        internal static async Task<StoreFixture> CreateMySqlAsync(string connectionString)
        {
            var database = "bing_file_copy_test_" + Guid.NewGuid().ToString("N");
            var admin = new MySqlConnectionStringBuilder(connectionString) { Database = string.Empty };
            await using (var connection = new MySqlConnection(admin.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
                await command.ExecuteNonQueryAsync();
            }

            var scoped = new MySqlConnectionStringBuilder(connectionString) { Database = database };
            IFreeSql? primary = null;
            IFreeSql? other = null;
            try
            {
                primary = new FreeSqlBuilder().UseConnectionString(DataType.MySql, scoped.ConnectionString)
                    .UseAutoSyncStructure(false).Build();
                FileManagementFreeSqlSchema.SyncStructure(primary);
                other = new FreeSqlBuilder().UseConnectionString(DataType.MySql, scoped.ConnectionString)
                    .UseAutoSyncStructure(false).Build();
                return new StoreFixture(primary, other, mysqlDatabase: database, mysqlAdminConnectionString: admin.ConnectionString);
            }
            catch
            {
                primary?.Dispose();
                other?.Dispose();
                await using var connection = new MySqlConnection(admin.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{database}`";
                await command.ExecuteNonQueryAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Primary.Dispose();
            Other.Dispose();
            if (_sqlitePath != null)
            {
                foreach (var suffix in new[] { "", "-shm", "-wal" })
                {
                    var path = _sqlitePath + suffix;
                    if (File.Exists(path)) File.Delete(path);
                }
            }
            if (_mysqlDatabase != null && _mysqlAdminConnectionString != null)
            {
                await using var connection = new MySqlConnection(_mysqlAdminConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{_mysqlDatabase}`";
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}

public sealed class MySqlIntegrationFactAttribute : FactAttribute
{
    public MySqlIntegrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BING_FILEMANAGEMENT_MYSQL_ALLOW_TESTS"), "1", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_FILEMANAGEMENT_MYSQL_TEST_CONNECTION")))
            Skip = "设置 BING_FILEMANAGEMENT_MYSQL_ALLOW_TESTS=1 和专用 BING_FILEMANAGEMENT_MYSQL_TEST_CONNECTION 后执行。";
    }
}
