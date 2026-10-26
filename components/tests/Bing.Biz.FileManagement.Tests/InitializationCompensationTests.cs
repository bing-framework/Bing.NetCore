using System.Security.Cryptography;
using Bing.FileStorage;
using Bing.FileStorage.Qiniu.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>两套真实 SQLite 仓储与 S3 Provider；故障注入在数据库提交边界和 SDK 传输边界。</summary>
public sealed class InitializationCompensationTests
{
    private static FileTestEnvironment Environment(bool freeSql, RecordingS3Client client)
    {
        var env = new FileTestEnvironment(freeSql);
        env.ConfigureManagement = o => o.MaxOperationAttempts = 1;
        env.CustomizeServices = services =>
        {
            services.AddSingleton(new BingFileStorageOptions { DefaultStore = "qiniu-proxy" });
            services.AddBingFileStorageBackend("qiniu-proxy", _ => new S3FileStorageBackend("qiniu-proxy", new BingS3StorageOptions
            { UploadMode = S3UploadMode.ServerProxy }, client));
        };
        return env;
    }
    private static Task<UploadSessionView> CreateAsync(FileTestEnvironment env) => env.Get<FileManagementService>().CreateUploadAsync(
        new() { RequestId = Guid.NewGuid(), FileName = "init.bin", Length = 3 });
    private static FileOperationProcessor Processor(FileTestEnvironment env, ReviewStoreProxy proxy) => new(
        (IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), env.Get<FileMigrationService>(),
        env.Get<FileManagementOptions>(), NullLogger<FileOperationProcessor>.Instance);

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task DatabaseFailure_ReconcilesCommitBeforeCompensating(bool freeSql, bool afterCommit)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var store = env.Get<IFileManagementStore>(); var proxy = ReviewStoreProxy.Wrap(store);
        proxy.OnUpdate = async (entry, commit) =>
        {
            if (entry.File.State != FileState.Uploading) return await commit();
            if (afterCommit) Assert.True(await commit());
            throw new ReviewDbException();
        };
        await Processor(env, proxy).RunBatchAsync();
        await env.RestartAsync(); await env.RunAsync(); store = env.Get<IFileManagementStore>();
        var entry = (await store.FindAsync(session.FileId))!;
        var operation = (await ReviewRows.ReadAsync<FileOperation>(env)).Single(o => o.Kind == FileOperationKind.Initialize);
        Assert.Equal(nameof(ReviewDbException), operation.LastError); // 原异常没有被补偿吞掉。
        if (afterCommit)
        {
            Assert.Equal(FileState.Uploading, entry.File.State); Assert.Equal(0, client.AbortCalls);
            Assert.Equal(entry.Session!.BackendUploadId, Assert.Single(client.Uploads).Key);
            await FinishAsync(env, session.Id); // 已提交的会话仍可实际上传和完成。
        }
        else
        {
            Assert.Equal(FileState.Failed, entry.File.State); Assert.Null(entry.Session!.BackendUploadId);
            Assert.Empty(client.Uploads); Assert.True(client.AbortCalls > 0);
            Assert.Contains(await ReviewRows.ReadAsync<FileOperation>(env), o => o.Kind == FileOperationKind.Abort && o.Payload.Length > 0);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CompensationCommitResponseLost_DoesNotDuplicateCleanupTask(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var proxy = ReviewStoreProxy.Wrap(env.Get<IFileManagementStore>());
        var lost = false;
        proxy.OnUpdate = async (entry, commit) =>
        {
            if (entry.File.State == FileState.Uploading) throw new ReviewDbException();
            var updated = await commit();
            if (!lost) { lost = true; throw new ReviewDbException(); }
            return updated;
        };
        await Processor(env, proxy).RunBatchAsync(); await env.RunAsync();
        Assert.True(lost); Assert.Empty(client.Uploads);
        Assert.Single(await ReviewRows.ReadAsync<FileOperation>(env), o => o.Kind == FileOperationKind.Abort && o.Payload.Length > 0);
        Assert.Equal(FileState.Failed, (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!.File.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Cancellation_UsesIndependentRecoveryToken_AndRetryHasOnlyOneLiveUpload(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var store = env.Get<IFileManagementStore>(); var proxy = ReviewStoreProxy.Wrap(store);
        using var stop = new CancellationTokenSource();
        proxy.OnUpdate = (entry, commit) =>
        {
            if (entry.File.State != FileState.Uploading) return commit();
            stop.Cancel(); throw new OperationCanceledException(stop.Token);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(env, proxy).RunBatchAsync(stop.Token));
        Assert.Empty(client.Uploads);
        var operation = (await ReviewRows.ReadAsync<FileOperation>(env)).Single(o => o.Kind == FileOperationKind.Initialize);
        Assert.Equal(FileOperationState.Running, operation.State);
        Assert.Contains(await ReviewRows.ReadAsync<FileOperation>(env), o => o.Kind == FileOperationKind.Abort && o.Payload.Length > 0);
        // 释放仍有效的测试租约模拟停机后的重新调度，无需实际等待租约时长。
        Assert.True(await store.FinishOperationAsync(operation.Id, operation.LeaseToken!, FileOperationState.Pending, 0, null));
        await env.RestartAsync(); await env.RunAsync();
        var entry = (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!;
        Assert.Equal(FileState.Uploading, entry.File.State); Assert.Equal(entry.Session!.BackendUploadId, Assert.Single(client.Uploads).Key);
        await FinishAsync(env, session.Id);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailedAbort_IsDurableAndRecoverableAfterRestart(bool freeSql)
    {
        using var client = new RecordingS3Client { BeforeAbort = () => Task.FromException(new HttpRequestException("abort unavailable")) };
        await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var store = env.Get<IFileManagementStore>(); var proxy = ReviewStoreProxy.Wrap(store);
        proxy.OnUpdate = (entry, commit) => entry.File.State == FileState.Uploading ? Task.FromException<bool>(new ReviewDbException()) : commit();
        await Processor(env, proxy).RunBatchAsync(); await env.RunAsync();
        var cleanup = (await ReviewRows.ReadAsync<FileOperation>(env)).Single(o => o.Kind == FileOperationKind.Abort && o.Payload.Length > 0);
        Assert.Equal(FileOperationState.Failed, cleanup.State);
        Assert.Contains(Assert.Single(client.Uploads).Key, cleanup.Payload);
        await env.RestartAsync(); client.BeforeAbort = null;
        var entry = (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!;
        await env.Get<FileOperationAdministration>().RecoverAsync(entry.File.ScopeKey, "operator", cleanup.Id,
            new() { RequestId = Guid.NewGuid(), ExpectedVersion = cleanup.Version, Reason = "backend restored" });
        await env.RunAsync(); Assert.Empty(client.Uploads);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LosingInitializer_AbortsOnlyItsOwnUpload_AndPreservesWinner(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var store = env.Get<IFileManagementStore>(); var proxy = ReviewStoreProxy.Wrap(store);
        string? winnerId = null;
        proxy.OnUpdate = async (entry, commit) =>
        {
            if (entry.File.State != FileState.Uploading || winnerId != null) return await commit();
            var original = (await store.FindAsync(session.FileId))!;
            var storage = (IMultipartFileStorage)env.Get<IFileStorageFactory>().GetStorage(original.File.StoreName, original.File.Scope());
            var winner = await storage.CreateMultipartAsync(original.WriteRequest(), original.Session!.PartSize);
            winnerId = winner.UploadId; original.Session.BackendUploadId = winnerId;
            original.Session.BackendMetadataJson = System.Text.Json.JsonSerializer.Serialize(winner.Request.Metadata);
            original.File.State = original.Session.State = FileState.Uploading;
            Assert.True(await store.TryUpdateAsync(original, original.File.Version));
            return await commit(); // 实际根 CAS 冲突。
        };
        await Processor(env, proxy).RunBatchAsync(); await env.RunAsync();
        Assert.Equal(winnerId, Assert.Single(client.Uploads).Key);
        Assert.Equal(winnerId, (await store.FindAsync(session.FileId))!.Session!.BackendUploadId);
        await FinishAsync(env, session.Id);
    }

    private static async Task FinishAsync(FileTestEnvironment env, Guid sessionId)
    {
        var service = env.Get<FileManagementService>(); var bytes = new byte[] { 1, 2, 3 };
        await service.CreatePartRequestAsync(sessionId, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(bytes)) });
        await service.UploadPartAsync(sessionId, 1, new MemoryStream(bytes)); await service.CompleteUploadAsync(sessionId); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(sessionId)).State);
    }
}
