using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Bing.FileStorage.FileSystem;
using Bing.FileStorage.RemoteDrive;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>真实 Local 中转与两套 SQLite；远端传输边界注入可观察的内容服务。</summary>
public sealed partial class RemoteDriveWorkflowTests
{
    private static FileTestEnvironment Setup(bool freeSql, DriveFixture client, TimeSpan? leaseDuration = null, RemoteDriveTransportOptions? transportTimeouts = null)
    {
        var env = new FileTestEnvironment(freeSql);
        env.CustomizeServices = services =>
        {
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(env.Root, "keys")));
            services.AddBingFileSystemStorage("local-c-backup", o => o.RootPath = Path.Combine(env.Root, "backup"));
            services.AddScoped<IRemoteDriveCredentialProtector, RemoteDriveCredentialProtection>();
            services.AddBingRemoteDrive(new RemoteDriveTemplate("drive", new RemoteDriveOptions
            {
                MaximumFileLength = 1024 * 1024 * 1024,
                LeaseDuration = leaseDuration ?? TimeSpan.FromMinutes(2),
                TransportTimeouts = transportTimeouts ?? new()
            }, client));
            services.AddSingleton(_ => new RemoteDriveTransport(new HttpClient(client, false)));
        };
        return env;
    }
    private static async Task<RemoteDriveConnection> Connect(FileTestEnvironment env, RemoteDriveOwnership ownership = RemoteDriveOwnership.Personal)
    {
        var id = Guid.NewGuid(); var scope = StorageScope.ForTenant(env.Identity.Tenant!);
        var connection = new RemoteDriveConnection { Id = id, StoreName = "drive-c-" + id.ToString("N"), TemplateName = "drive", Provider = "fixture",
            ScopeKey = scope.Key, ScopeKind = scope.Kind, TenantId = scope.TenantId, OwnerId = env.Identity.UserId, Ownership = ownership,
            State = RemoteDriveConnectionState.Active, RemoteAccountId = "account", DriveId = "disk", CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            EncryptedCredentials = env.Get<IRemoteDriveCredentialProtector>().Protect(id, JsonSerializer.Serialize(new RemoteDriveCredentials
                { AccessToken = "access", RefreshToken = "refresh", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds() })) };
        Assert.True(await env.Get<IRemoteDriveStateStore>().TryCreateConnectionAsync(connection)); return connection;
    }
    private static async Task<UploadSessionView> Upload(FileTestEnvironment env, RemoteDriveConnection connection, bool restart = false)
    {
        var service = env.Get<FileManagementService>(); var bytes = new byte[] { 3, 1, 4, 1, 5 };
        var view = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "个人附件.bin", Length = bytes.Length, StorageTargetId = connection.Id.ToString("N") });
        await env.RunAsync();
        await service.CreatePartRequestAsync(view.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
        await service.UploadPartAsync(view.Id, 1, new MemoryStream(bytes));
        if (restart) { await env.RestartAsync(); service = env.Get<FileManagementService>(); }
        Assert.Single((await service.GetSessionAsync(view.Id)).Parts);
        await service.CompleteUploadAsync(view.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(view.Id)).State); return view;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PersonalUpload_RestartsCopiesBindsAndSurvivesSourceDisconnect(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var connection = await Connect(env); var session = await Upload(env, connection, true);
        var files = env.Get<FileManagementService>();
        env.Authorizer.Granted = true;
        Assert.Equal("requires_copy", (await Assert.ThrowsAsync<FileManagementException>(() => files.BindAsync(session.FileId, "orders", "1"))).Code);
        var copies = env.Get<FileBindingCopyService>(); var requestId = Guid.NewGuid();
        var copy = await copies.CreateAsync(session.FileId, requestId, "orders", "1");
        Assert.Equal(copy.Id, (await copies.CreateAsync(session.FileId, requestId, "orders", "1")).Id);
        await env.RunAsync(); Assert.Equal(FileBindingCopyState.Copied, (await copies.GetAsync(copy.Id)).State);
        env.Authorizer.Granted = false;
        await Assert.ThrowsAsync<FileManagementException>(() => copies.BindAsync(copy.Id));
        env.Authorizer.Granted = true;
        Assert.Equal(copy.CopyFileId, await copies.BindAsync(copy.Id));
        Assert.Equal(copy.CopyFileId, await copies.BindAsync(copy.Id));
        await env.Get<FileStorageConnectionService>().DisableAsync(connection.Id);
        var entry = await files.AuthorizeReadAsync(copy.CopyFileId);
        Assert.Equal(RemoteDriveOwnership.Shared, entry.File.Ownership); Assert.Single(entry.Bindings);
        Assert.Equal(FileState.Ready, (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!.File.State);
        var data = new MemoryStream(); await env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope()).CopyToAsync(entry.File.Address(), data);
        Assert.Equal(new byte[] { 3, 1, 4, 1, 5 }, data.ToArray());
        await files.UnbindAsync(entry.File.Id, entry.Bindings.Single().Id); await files.DeleteAsync(entry.File.Id); await env.RunAsync();
        Assert.Equal(FileState.Deleted, (await env.Get<IFileManagementStore>().FindAsync(entry.File.Id))!.File.State);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PersonalOwnership_BlocksManagerAndCrossTenantAndExplicitUnavailableTarget(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var connection = await Connect(env); var uploaded = await Upload(env, connection);
        env.Identity.Permissions.Add("files.manage"); env.Identity.UserId = "user-b";
        var service = env.Get<FileManagementService>();
        await Assert.ThrowsAsync<FileManagementException>(() => service.AuthorizeReadAsync(uploaded.FileId));
        await Assert.ThrowsAsync<FileManagementException>(() => service.DeleteAsync(uploaded.FileId));
        Assert.Empty(await service.ListAsync(manage: true));
        Assert.Empty((await service.SearchAsync(new() { Manage = true })).Items);
        await Assert.ThrowsAsync<FileManagementException>(() => env.Get<IFileUploadStoreSelector>().SelectAsync(StorageScope.ForTenant("tenant-a"), "user-b", "attachments", connection.Id.ToString("N")));
        var foreign = env.Get<IFileStorageFactory>().GetStorage(connection.StoreName, StorageScope.ForTenant("tenant-b"));
        Assert.Equal(FileStorageError.AccessDenied, (await Assert.ThrowsAsync<FileStorageException>(() => foreign.GetPropertiesAsync(new("attachments", "x")))).Error);
        await Assert.ThrowsAsync<FileManagementException>(() => env.Get<FileMigrationService>().QueueMigrationAsync(uploaded.FileId, "local-v2"));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task TargetPreference_IdempotentReplayKeepsSavedLocationAndCancelCannotPublish(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var c = await Connect(env); var connections = env.Get<FileStorageConnectionService>();
        await connections.SetPreferenceAsync("attachments", c.Id.ToString("N"));
        var request = new CreateFileUpload { RequestId = Guid.NewGuid(), FileName = "empty.txt", Length = 0 };
        var first = await env.Get<FileManagementService>().CreateUploadAsync(request);
        await connections.SetPreferenceAsync("attachments", "local-v2");
        Assert.Equal(first.FileId, (await env.Get<FileManagementService>().CreateUploadAsync(request)).FileId);
        Assert.Equal(c.StoreName, (await env.Get<IFileManagementStore>().FindAsync(first.FileId))!.File.StoreName);
        await connections.SetPreferenceAsync("attachments", "local-c-backup");
        var shared = await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "shared", Length = 0, StorageTargetId = "local-v1" });
        await env.RunAsync(); await env.Get<FileManagementService>().CompleteUploadAsync(shared.Id); await env.RunAsync();
        await env.Get<FileMigrationService>().QueueMigrationAsync(shared.FileId, "local-c-backup"); await env.RunAsync();
        Assert.Equal("local-c-backup", (await env.Get<IFileManagementStore>().FindAsync(shared.FileId))!.File.StoreName);
        var full = await Upload(env, c); env.Authorizer.Granted = true;
        var copies = env.Get<FileBindingCopyService>(); var copy = await copies.CreateAsync(full.FileId, Guid.NewGuid(), "orders", "2");
        await copies.CancelAsync(copy.Id); await env.RunAsync();
        Assert.Equal(FileBindingCopyState.Cancelled, (await copies.GetAsync(copy.Id)).State);
        Assert.Equal(FileState.Deleted, (await env.Get<IFileManagementStore>().FindAsync(copy.CopyFileId))!.File.State);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CredentialRefresh_IsSingleWriterAndContentWaitsForRotation(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var c = await Connect(env); var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Refresh = async (_, ct) => { entered.TrySetResult(true); await release.Task.WaitAsync(ct); return FreshCredentials(); };
        var credentials = env.Get<IRemoteDriveCredentialProvider>();
        var first = credentials.GetAsync(c.Id, true); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = credentials.GetAsync(c.Id, true);
        var content = env.Get<IFileStorageFactory>().GetStorage(c.StoreName, c.Scope()).GetPropertiesAsync(new("attachments", "missing"));
        release.TrySetResult(true);
        Assert.Equal("rotated-access", (await first).AccessToken); Assert.Equal("rotated-access", (await second).AccessToken);
        Assert.Null(await content); Assert.Equal(1, client.RefreshCount);
        await env.RestartAsync(); Assert.Equal("rotated-access", (await env.Get<IRemoteDriveCredentialProvider>().GetAsync(c.Id)).AccessToken);
        var cipher = (await env.Get<IRemoteDriveStateStore>().FindConnectionAsync(c.Id))!.EncryptedCredentials;
        Assert.DoesNotContain("rotated-access", cipher);
        Assert.Throws<CryptographicException>(() => env.Get<IRemoteDriveCredentialProtector>().Unprotect(Guid.NewGuid(), cipher));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CredentialRefresh_LateResponseCannotReactivateDisabledConnection(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var c = await Connect(env); var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Refresh = async (_, ct) => { entered.TrySetResult(true); await release.Task.WaitAsync(ct); return FreshCredentials(); };
        var refresh = env.Get<IRemoteDriveCredentialProvider>().GetAsync(c.Id, true); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await env.Get<FileStorageConnectionService>().DisableAsync(c.Id); release.TrySetResult(true);
        Assert.Equal(FileStorageError.AccessDenied, (await Assert.ThrowsAsync<FileStorageException>(() => refresh)).Error);
        var saved = (await env.Get<IRemoteDriveStateStore>().FindConnectionAsync(c.Id))!;
        Assert.Equal(RemoteDriveConnectionState.Disabled, saved.State); Assert.Equal(c.EncryptedCredentials, saved.EncryptedCredentials);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CredentialRefresh_SaveFailureDoesNotReplayOldRefreshToken(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var c = await Connect(env); client.Refresh = (_, _) => Task.FromResult(FreshCredentials());
        var store = env.Get<IRemoteDriveStateStore>();
        var provider = new RemoteDriveCredentialProvider(store, new FailingProtector(env.Get<IRemoteDriveCredentialProtector>()), env.Provider.GetServices<RemoteDriveTemplate>());
        await Assert.ThrowsAsync<IOException>(() => provider.GetAsync(c.Id, true));
        var saved = (await store.FindConnectionAsync(c.Id))!; Assert.Equal(RemoteDriveConnectionState.ReauthorizationRequired, saved.State);
        saved.LeaseUntil = 0; Assert.True(await store.TryUpdateConnectionAsync(saved, saved.Version));
        await Assert.ThrowsAsync<FileStorageException>(() => env.Get<IRemoteDriveCredentialProvider>().GetAsync(c.Id));
        Assert.Equal(1, client.RefreshCount);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Authorization_RejectsForeignOwnerWrongStateAndChangedAccount(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var c = await Connect(env); var connections = env.Get<FileStorageConnectionService>();
        env.Identity.UserId = "user-b"; env.Identity.Permissions.Add("files.connections.manage");
        await Assert.ThrowsAsync<FileManagementException>(() => connections.StartAsync("drive", "name", RemoteDriveOwnership.Personal, "https://app.test", c.Id));
        env.Identity.UserId = "user-a";
        var started = await connections.StartAsync("drive", "name", RemoteDriveOwnership.Personal, "https://app.test", c.Id);
        await Assert.ThrowsAsync<FileManagementException>(() => connections.CompleteAsync(c.Id, "wrong", "code"));
        client.AccountId = "changed-account";
        Assert.Equal("account_mismatch", (await Assert.ThrowsAsync<FileManagementException>(() => connections.CompleteAsync(c.Id, started.AuthorizationState!, "code"))).Code);
        Assert.Equal("account", (await env.Get<IRemoteDriveStateStore>().FindConnectionAsync(c.Id))!.RemoteAccountId);
        var response = JsonSerializer.Serialize(await connections.ListAsync()); Assert.DoesNotContain("Encrypted", response); Assert.DoesNotContain("refresh", response);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Provider_EmptyNonSeekOwnershipInvalidLengthAndPrivateLimits(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var c = await Connect(env); var storage = env.Get<IFileStorageFactory>().GetStorage(c.StoreName, c.Scope());
        var empty = new FileAddress("attachments", "empty"); await storage.SaveAsync(new(empty, 0, FileWriteMode.Overwrite), Stream.Null);
        Assert.Equal(0, (await storage.GetPropertiesAsync(empty))!.Length);
        var source = new NonSeekInput(new byte[] { 1, 2, 3 }); var address = new FileAddress("attachments", "nonseek");
        await storage.SaveAsync(new(address, 3, FileWriteMode.Overwrite), source); Assert.False(source.Disposed);
        var bad = new FileAddress("attachments", "bad");
        Assert.Equal(FileStorageError.InvalidLength, (await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(new(bad, 2, FileWriteMode.Overwrite), new NonSeekInput(new byte[] { 1, 2, 3 })))).Error);
        Assert.Null(await storage.GetPropertiesAsync(bad));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.GetPropertiesAsync(address, cancel.Token));
        await storage.DeleteAsync(address); await storage.DeleteAsync(address); Assert.Null(await storage.GetPropertiesAsync(address));
        var limits = await ((IFileStorageLimitProvider)storage).GetLimitsAsync(); Assert.True(limits!.PrivateContainersOnly);
        await Assert.ThrowsAsync<FileManagementException>(() => env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "public", Length = 0, Container = "public", StorageTargetId = c.Id.ToString("N") }));
        client.CorruptDownload = true;
        var corrupt = new FileAddress("attachments", "corrupt");
        Assert.Equal(FileStorageError.InvalidChecksum, (await Assert.ThrowsAsync<FileStorageException>(() => storage.SaveAsync(new(corrupt, 3, FileWriteMode.Overwrite), new MemoryStream(new byte[] { 1, 2, 3 })))).Error);
        Assert.Null(await storage.GetPropertiesAsync(corrupt));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task LongUpload_HeartbeatSurvivesRepeatedCheckpointsAndPublishesAfterInitialExpiry(bool freeSql)
    {
        using var client = new DriveFixture();
        await using var env = Setup(freeSql, client, TimeSpan.FromSeconds(30));
        await env.InitializeAsync();
        var connection = await Connect(env);
        var store = env.Get<IRemoteDriveStateStore>();
        Guid transferId = default;
        client.BeforeUploadCheckpoint = async (checkpoint, persist, ct) =>
        {
            transferId = Guid.ParseExact(checkpoint.Name[5..], "N");
            var original = (await store.FindTransferAsync(transferId, ct))!;
            var previousUntil = original.LeaseUntil;
            // 使用真实的 30 秒租约和 10 秒心跳，跨过初始期限并连续保存检查点。
            for (var part = 1; part <= 3; part++)
            {
                await Task.Delay(TimeSpan.FromSeconds(11), ct);
                var renewed = (await store.FindTransferAsync(transferId, ct))!;
                Assert.True(renewed.LeaseUntil > previousUntil);
                checkpoint.Parts[part] = "checkpoint-" + part;
                await persist(checkpoint, ct);
                var saved = (await store.FindTransferAsync(transferId, ct))!;
                Assert.Equal(original.LeaseToken, saved.LeaseToken);
                Assert.True(saved.LeaseUntil >= renewed.LeaseUntil);
                Assert.False(await store.TryClaimTransferAsync(transferId, saved.Version, "competitor",
                    DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds(), ct));
                previousUntil = saved.LeaseUntil;
            }
            Assert.True(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > original.LeaseUntil);
        };
        var storage = env.Get<IFileStorageFactory>().GetStorage(connection.StoreName, connection.Scope());
        var address = new FileAddress("attachments", "long-heartbeat");
        var bytes = new byte[] { 9, 2, 6, 5 };
        using var input = new MemoryStream(bytes);
        await storage.SaveAsync(new(address, bytes.Length, FileWriteMode.Overwrite), input);
        var transfer = (await store.FindTransferAsync(transferId))!;
        Assert.Equal(RemoteDriveTransferState.Completed, transfer.State);
        Assert.True(transfer.CleanupCompleted);
        Assert.Null(transfer.LeaseToken);
        Assert.Equal(0, transfer.LeaseUntil);
        Assert.Equal(transferId, (await store.FindObjectAsync(RemoteDriveObject.Key(connection.StoreName, connection.ScopeKey, address)))!.TransferId);
        using var output = new MemoryStream();
        await storage.CopyToAsync(address, output);
        Assert.Equal(bytes, output.ToArray());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task UploadCancellation_ReleasesLeaseAndCleansStagingWithoutPublishing(bool freeSql)
    {
        using var client = new DriveFixture();
        await using var env = Setup(freeSql, client);
        await env.InitializeAsync();
        var connection = await Connect(env);
        var entered = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.BeforeUploadCheckpoint = async (checkpoint, _, ct) =>
        {
            entered.SetResult(Guid.ParseExact(checkpoint.Name[5..], "N"));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        };
        var storage = env.Get<IFileStorageFactory>().GetStorage(connection.StoreName, connection.Scope());
        var address = new FileAddress("attachments", "cancel-heartbeat");
        using var input = new MemoryStream(new byte[] { 1, 2, 3 });
        using var cancellation = new CancellationTokenSource();
        var saving = storage.SaveAsync(new(address, 3, FileWriteMode.Overwrite), input, cancellation.Token);
        var transferId = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => saving);
        var store = env.Get<IRemoteDriveStateStore>();
        var transfer = (await store.FindTransferAsync(transferId))!;
        Assert.Equal(RemoteDriveTransferState.Aborted, transfer.State);
        Assert.True(transfer.CleanupCompleted);
        Assert.Null(transfer.LeaseToken);
        Assert.Equal(0, transfer.LeaseUntil);
        Assert.Null(await store.FindObjectAsync(RemoteDriveObject.Key(connection.StoreName, connection.ScopeKey, address)));
        var staging = env.Get<IFileStorageFactory>().GetStorage(transfer.StageStoreName, connection.Scope());
        Assert.Null(await staging.GetPropertiesAsync(new(transfer.StageContainer, transfer.StageObjectKey)));
    }

    private static RemoteDriveCredentials FreshCredentials() => new() { AccessToken = "rotated-access", RefreshToken = "rotated-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds() };
    private sealed class FailingProtector : IRemoteDriveCredentialProtector
    {
        private readonly IRemoteDriveCredentialProtector _inner;
        public FailingProtector(IRemoteDriveCredentialProtector inner) { _inner = inner; }
        public string Protect(Guid id, string plaintext) => throw new IOException("injected credential persistence failure");
        public string Unprotect(Guid id, string ciphertext) => _inner.Unprotect(id, ciphertext);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Cleanup_FailedBatchDoesNotStarveLaterTransfers_AndRecoversAfterRestart(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var bad = await Connect(env); var healthy = await Connect(env);
        var old = new List<RemoteDriveTransfer>();
        for (var i = 0; i < 50; i++) old.Add(await ExpiredTransfer(env, bad, "blocked-" + i));
        var next = await ExpiredTransfer(env, healthy, "healthy", 1);
        var store = env.Get<IRemoteDriveStateStore>();
        bad.State = RemoteDriveConnectionState.Disabled;
        Assert.True(await store.TryUpdateConnectionAsync(bad, bad.Version));
        Assert.Equal(0, await env.Get<RemoteDriveMaintenance>().CleanupAsync());
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var blocked in old)
        {
            var saved = (await store.FindTransferAsync(blocked.Id))!;
            Assert.False(saved.CleanupCompleted);
            Assert.True(saved.CleanupNextAttemptAt > now);
            Assert.Equal("connection_unavailable", saved.CleanupErrorCode);
            Assert.Equal(blocked.CheckpointJson, saved.CheckpointJson);
        }
        await env.RestartAsync(); store = env.Get<IRemoteDriveStateStore>();
        Assert.Equal(next.Id, Assert.Single(await store.ListCleanupAsync(now, 50)).Id);
        Assert.Equal(1, await env.Get<RemoteDriveMaintenance>().CleanupAsync());
        Assert.True((await store.FindTransferAsync(next.Id))!.CleanupCompleted);

        bad = (await store.FindConnectionAsync(bad.Id))!; bad.State = RemoteDriveConnectionState.Active;
        Assert.True(await store.TryUpdateConnectionAsync(bad, bad.Version));
        // 模拟退避到期，不依赖真实等待五分钟。
        var retry = (await store.FindTransferAsync(old[0].Id))!; retry.CleanupNextAttemptAt = 0;
        Assert.True(await store.TryUpdateTransferAsync(retry, retry.Version));
        Assert.Equal(1, await env.Get<RemoteDriveMaintenance>().CleanupAsync());
        var cleaned = (await store.FindTransferAsync(retry.Id))!;
        Assert.True(cleaned.CleanupCompleted); Assert.Null(cleaned.CleanupErrorCode);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Cleanup_MissingConnectionAndInvalidCheckpointAreDeferred(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var missing = await Connect(env); var broken = await Connect(env); var healthy = await Connect(env);
        var missingTransfer = await ExpiredTransfer(env, missing, "missing");
        var brokenTransfer = await ExpiredTransfer(env, broken, "broken");
        var healthyTransfer = await ExpiredTransfer(env, healthy, "healthy");
        if (freeSql) await env.Get<IFreeSql>().Delete<RemoteDriveConnection>().Where(c => c.Id == missing.Id).ExecuteAffrowsAsync();
        else
        {
            await using var db = await env.Get<Microsoft.EntityFrameworkCore.IDbContextFactory<Bing.Biz.FileManagement.EntityFrameworkCore.FileManagementDbContext>>().CreateDbContextAsync();
            db.Remove(missing); await db.SaveChangesAsync();
        }
        // 在进入远端上传前失败（无效持久化元数据），亦不能阻塞其他连接。
        var store = env.Get<IRemoteDriveStateStore>(); brokenTransfer.MetadataJson = "invalid";
        Assert.True(await store.TryUpdateTransferAsync(brokenTransfer, brokenTransfer.Version));
        Assert.Equal(1, await env.Get<RemoteDriveMaintenance>().CleanupAsync());
        Assert.Equal("connection_missing", (await store.FindTransferAsync(missingTransfer.Id))!.CleanupErrorCode);
        Assert.Equal("configuration", (await store.FindTransferAsync(brokenTransfer.Id))!.CleanupErrorCode);
        Assert.True((await store.FindTransferAsync(healthyTransfer.Id))!.CleanupCompleted);
        Assert.Empty(await store.ListCleanupAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 50));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Cleanup_RemoteAbortFailureReleasesLeaseAndDoesNotStarveLaterTransfers(bool freeSql)
    {
        using var client = new DriveFixture { AbortFailuresRemaining = 1 };
        await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var connection = await Connect(env);
        var failed = await ExpiredTransfer(env, connection, "abort-failed");
        var healthy = await ExpiredTransfer(env, connection, "abort-healthy", 1);
        var store = env.Get<IRemoteDriveStateStore>();

        Assert.Equal(1, await env.Get<RemoteDriveMaintenance>().CleanupAsync());
        var failedSaved = (await store.FindTransferAsync(failed.Id))!;
        Assert.False(failedSaved.CleanupCompleted);
        Assert.True(failedSaved.CleanupNextAttemptAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Assert.Equal(nameof(FileStorageError.AccessDenied), failedSaved.CleanupErrorCode);
        Assert.Null(failedSaved.LeaseToken);
        Assert.Equal(0, failedSaved.LeaseUntil);
        Assert.True((await store.FindTransferAsync(healthy.Id))!.CleanupCompleted);

        client.AbortFailuresRemaining = 0;
        failedSaved.CleanupNextAttemptAt = 0;
        Assert.True(await store.TryUpdateTransferAsync(failedSaved, failedSaved.Version));
        Assert.Equal(1, await env.Get<RemoteDriveMaintenance>().CleanupAsync());
        Assert.True((await store.FindTransferAsync(failed.Id))!.CleanupCompleted);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task CleanupRetry_RejectsStaleVersionLiveLeaseAndCompletedTransfer(bool freeSql)
    {
        using var client = new DriveFixture(); await using var env = Setup(freeSql, client); await env.InitializeAsync();
        var connection = await Connect(env); var t = await ExpiredTransfer(env, connection, "lease");
        var store = env.Get<IRemoteDriveStateStore>(); var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var leaseUntil = now + 1000;
        Assert.True(await store.TryClaimTransferAsync(t.Id, t.Version, "owner", leaseUntil));
        Assert.False(await store.TryDeferCleanupAsync(t.Id, t.Version, now, now + 300000, "stale"));
        t = (await store.FindTransferAsync(t.Id))!;
        Assert.False(await store.TryDeferCleanupAsync(t.Id, t.Version, now, now + 300000, "busy"));
        Assert.Equal("owner", (await store.FindTransferAsync(t.Id))!.LeaseToken);
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, leaseUntil - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 30)));
        now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var outcomes = await Task.WhenAll(
            store.TryDeferCleanupAsync(t.Id, t.Version, now, now + 300000, "retry_a"),
            store.TryDeferCleanupAsync(t.Id, t.Version, now, now + 300000, "retry_b"));
        Assert.Single(outcomes, x => x);
        Assert.DoesNotContain(await store.ListCleanupAsync(now, 50), x => x.Id == t.Id);
        Assert.Contains(await store.ListCleanupAsync(now + 300000, 50), x => x.Id == t.Id);
        var actual = (await store.FindTransferAsync(t.Id))!;
        Assert.Equal(t.CheckpointJson, actual.CheckpointJson);
        actual.State = RemoteDriveTransferState.Completed; actual.CleanupCompleted = true;
        Assert.True(await store.TryUpdateTransferAsync(actual, actual.Version));
        Assert.False(await store.TryDeferCleanupAsync(actual.Id, actual.Version, now, now + 300000, "completed"));
        Assert.True((await store.FindTransferAsync(actual.Id))!.CleanupCompleted);
    }

    private static async Task<RemoteDriveTransfer> ExpiredTransfer(FileTestEnvironment env, RemoteDriveConnection c, string key, int order = 0)
    {
        var backend = (IMultipartFileStorage)env.Get<IFileStorageFactory>().GetStorage(c.StoreName, c.Scope());
        var upload = await backend.CreateMultipartAsync(new FileWriteRequest(new FileAddress("attachments", key), 0, FileWriteMode.Overwrite), 16 * 1024 * 1024);
        var store = env.Get<IRemoteDriveStateStore>();
        var t = (await store.FindTransferAsync(Guid.ParseExact(upload.UploadId, "N")))!;
        t.ExpiresAt = 100 + order;
        Assert.True(await store.TryUpdateTransferAsync(t, t.Version));
        return t;
    }

    private sealed class NonSeekInput : MemoryStream
    {
        public NonSeekInput(byte[] bytes) : base(bytes) { }
        public bool Disposed { get; private set; }
        public override bool CanSeek => false;
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class DriveFixture : HttpMessageHandler, IRemoteDriveClient
    {
        private readonly Dictionary<string, (RemoteDriveFile File, byte[] Bytes)> _files = new();
        public string Provider => "fixture";
        public Func<RemoteDriveCredentials, CancellationToken, Task<RemoteDriveCredentials>>? Refresh { get; set; }
        public int RefreshCount { get; private set; }
        public string AccountId { get; set; } = "account";
        public bool CorruptDownload { get; set; }
        public Func<CancellationToken, Task>? BeforeDownloadResponse { get; set; }
        public int AbortFailuresRemaining { get; set; }
        public Func<RemoteDriveCheckpoint, Func<RemoteDriveCheckpoint, CancellationToken, Task>, CancellationToken, Task>? BeforeUploadCheckpoint { get; set; }
        public Task<RemoteDriveAuthorization> BeginAuthorizationAsync(string redirectUri, string state, CancellationToken ct = default) => Task.FromResult(new RemoteDriveAuthorization { State = state, Url = "https://auth.test", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds() });
        public Task<RemoteDriveCredentials?> CompleteAuthorizationAsync(RemoteDriveAuthorization authorization, string? code, CancellationToken ct = default) => Task.FromResult<RemoteDriveCredentials?>(FreshCredentials());
        public Task<RemoteDriveCredentials> RefreshAsync(RemoteDriveCredentials credentials, CancellationToken ct = default) { RefreshCount++; return Refresh!(credentials, ct); }
        public Task<RemoteDriveAccount> GetAccountAsync(RemoteDriveCredentials credentials, CancellationToken ct = default) => Task.FromResult(new RemoteDriveAccount { AccountId = AccountId, DriveId = "disk" });
        public Task<RemoteDriveFile?> GetFileAsync(RemoteDriveContext context, string fileId, string parentId, CancellationToken ct = default) => Task.FromResult(_files.TryGetValue(fileId, out var item) ? item.File : null);
        public Task<RemoteDriveFile?> FindByNameAsync(RemoteDriveContext context, string parentId, string name, CancellationToken ct = default) => Task.FromResult<RemoteDriveFile?>(_files.Values.FirstOrDefault(x => x.File.Name == name).File);
        public async Task<RemoteDriveFile> UploadAsync(RemoteDriveContext context, RemoteDriveContent content, RemoteDriveCheckpoint checkpoint, Func<RemoteDriveCheckpoint, CancellationToken, Task> persist, CancellationToken ct = default)
        {
            using var bytes = new MemoryStream(); await content.ReadAsync((stream, token) => stream.CopyToAsync(bytes, token), ct);
            if (BeforeUploadCheckpoint != null) await BeforeUploadCheckpoint(checkpoint, persist, ct);
            var file = new RemoteDriveFile { Id = checkpoint.Name, Name = checkpoint.Name, ParentId = context.Connection.RootFolderId, Length = content.Length, Version = "1", ModifiedAt = DateTimeOffset.UtcNow };
            _files[file.Id] = (file, bytes.ToArray()); checkpoint.FileId = file.Id; checkpoint.Parts[1] = "remote-etag"; await persist(checkpoint, ct); return file;
        }
        public Task<RemoteDriveDownload> DownloadAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default) => Task.FromResult(new RemoteDriveDownload { Url = "https://downloads.test/" + file.Id });
        public Task DeleteAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default) { _files.Remove(file.Id); return Task.CompletedTask; }
        public Task AbortAsync(RemoteDriveContext context, RemoteDriveCheckpoint checkpoint, CancellationToken ct = default)
        {
            if (AbortFailuresRemaining > 0) { AbortFailuresRemaining--; throw new FileStorageException(FileStorageError.AccessDenied, "fixture abort denied"); }
            if (checkpoint.FileId != null) _files.Remove(checkpoint.FileId); return Task.CompletedTask;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (BeforeDownloadResponse != null) await BeforeDownloadResponse(cancellationToken);
            var bytes = _files[request.RequestUri!.AbsolutePath[1..]].Bytes.ToArray();
            if (CorruptDownload && bytes.Length > 0) bytes[0] ^= 1;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }
    }
}
