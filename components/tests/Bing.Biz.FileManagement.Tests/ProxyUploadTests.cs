using System.Security.Cryptography;
using Bing.FileStorage;
using Bing.FileStorage.Qiniu.Tests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>真实两 ORM SQLite + 真实 S3 Provider 的代理回执测试；SDK 边界使用可注入故障的测试客户端。</summary>
public sealed class ProxyUploadTests
{
    private static FileTestEnvironment Environment(bool freeSql, RecordingS3Client client, bool defaultProxy = true)
    {
        var env = new FileTestEnvironment(freeSql);
        env.CustomizeServices = services =>
        {
            if (defaultProxy) services.AddSingleton(new BingFileStorageOptions { DefaultStore = "qiniu-proxy" });
            services.AddBingFileStorageBackend("qiniu-proxy", _ => new S3FileStorageBackend("qiniu-proxy", new BingS3StorageOptions
            { UploadMode = S3UploadMode.ServerProxy, ContainerBuckets = new() { ["attachments"] = "actual-bucket", ["public"] = "actual-bucket" } }, client));
        };
        return env;
    }
    private static async Task<UploadSessionView> CreateAsync(FileTestEnvironment env)
    {
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "proxy.bin", Length = 3 });
        await env.RunAsync();
        await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Hash });
        return session;
    }
    private static string Hash => Convert.ToBase64String(SHA256.HashData(new byte[] { 1, 2, 3 }));
    private static Task<FilePart> Upload(FileManagementService service, Guid id) => service.UploadPartAsync(id, 1, new MemoryStream(new byte[] { 1, 2, 3 }));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Receipt_SurvivesReregistrationRestartAndCompletesWithoutRemoteChecksum(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var service = env.Get<FileManagementService>();
        Assert.False((await service.GetSessionAsync(session.Id)).DirectUpload);
        Assert.Empty((await service.GetSessionAsync(session.Id)).Parts);
        var uploaded = await Upload(service, session.Id);
        Assert.Equal(uploaded.ETag, (await env.Get<IFileManagementStore>().FindPartAsync(session.Id, 1))!.ETag);
        await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Hash });
        Assert.Equal(uploaded.ETag, (await env.Get<IFileManagementStore>().FindPartAsync(session.Id, 1))!.ETag);
        await env.RestartAsync(); service = env.Get<FileManagementService>();
        Assert.Equal(Hash, Assert.Single((await service.GetSessionAsync(session.Id)).Parts).Sha256);
        client.LoseCompleteResponse = true;
        await service.CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
        Assert.Equal(FileState.Ready, (await service.CompleteUploadAsync(session.Id)).State);
        await service.DeleteAsync(session.FileId); await env.RunAsync(); Assert.Empty(client.Objects);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MissingReceiptOrChangedRemotePart_IsNotResumableAndCannotComplete(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var service = env.Get<FileManagementService>(); var store = env.Get<IFileManagementStore>();
        var entry = (await store.FindSessionAsync(session.Id))!;
        var backend = (IMultipartFileStorage)env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope());
        // 模拟远端成功而数据库回执尚未提交即崩溃。
        await backend.UploadPartAsync(entry.Upload(), 1, new MemoryStream(new byte[] { 1, 2, 3 }), Hash);
        Assert.Empty((await service.GetSessionAsync(session.Id)).Parts);
        Assert.Equal("invalid_parts", (await Assert.ThrowsAsync<FileManagementException>(() => service.CompleteUploadAsync(session.Id))).Code);
        await Upload(service, session.Id);
        var part = client.Uploads[entry.Session!.BackendUploadId!].Parts[1];
        client.Uploads[entry.Session.BackendUploadId!].Parts[1] = part with { ETag = "different" };
        Assert.Empty((await service.GetSessionAsync(session.Id)).Parts);
        await Assert.ThrowsAsync<FileManagementException>(() => service.CompleteUploadAsync(session.Id));
        await Upload(service, session.Id); await service.CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OldUploadResponse_CannotOverwriteNewerReceipt(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var service = env.Get<FileManagementService>();
        FilePart? newer = null;
        client.AfterUpload = async () => { client.AfterUpload = null; newer = await Upload(service, session.Id); };
        Assert.Equal("conflict", (await Assert.ThrowsAsync<FileManagementException>(() => Upload(service, session.Id))).Code);
        Assert.Equal(newer!.ETag, (await env.Get<IFileManagementStore>().FindPartAsync(session.Id, 1))!.ETag);
        Assert.Equal(newer.ETag, Assert.Single((await service.GetSessionAsync(session.Id)).Parts).ETag);
    }

    [Theory]
    [InlineData(false, FileState.Aborted)] [InlineData(true, FileState.Aborted)]
    [InlineData(false, FileState.Completing)] [InlineData(true, FileState.Completing)]
    public async Task StateChangesDuringTransfer_PreventReceiptCommit(bool freeSql, FileState state)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var service = env.Get<FileManagementService>(); var store = env.Get<IFileManagementStore>();
        client.AfterUpload = async () =>
        {
            var current = (await store.FindSessionAsync(session.Id))!;
            current.File.State = current.Session!.State = state;
            Assert.True(await store.TryUpdateAsync(current, current.File.Version));
        };
        await Assert.ThrowsAsync<FileManagementException>(() => Upload(service, session.Id));
        Assert.Null((await store.FindPartAsync(session.Id, 1))!.ETag);
        Assert.Equal(state, (await store.FindSessionAsync(session.Id))!.File.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReceiptCasConflict_RechecksRemoteVersion(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var store = env.Get<IFileManagementStore>();
        var fault = ReviewStoreProxy.Wrap(store); var once = true;
        fault.OnUpdate = async (_, next) =>
        {
            if (!once) return await next(); once = false;
            var remote = client.Uploads.Values.Single(); var p = remote.Parts[1]; remote.Parts[1] = p with { ETag = "newer-remote" };
            return false;
        };
        var service = new FileManagementService((IFileManagementStore)(object)fault, env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), env.Authorizer, env.Get<FileManagementOptions>());
        await Assert.ThrowsAsync<FileManagementException>(() => Upload(service, session.Id));
        Assert.Null((await store.FindPartAsync(session.Id, 1))!.ETag);
        Assert.Equal(2, client.ListCalls);
        Assert.All(client.ListRequests, request => { Assert.Equal("0", request.Marker); Assert.Equal(1, request.MaxParts); });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LargeSession_ReceiptQueriesOnlyTarget_WhileResumeStillReadsAllPages(bool freeSql)
    {
        const int count = 6400, partSize = 16 * 1024 * 1024;
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client);
        env.ConfigureManagement = options =>
        {
            options.PartSize = partSize;
            options.Containers["attachments"].MaxFileLength = 100L * 1024 * 1024 * 1024;
        };
        await env.InitializeAsync();
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "many-parts.bin", Length = (long)(count - 1) * partSize + 3 });
        await env.RunAsync();
        await service.CreatePartRequestAsync(session.Id, new() { Number = count, Sha256 = Hash });
        // 只模拟已有远端清单；本测试实际传输尾片的三个字节，不构成 100 GiB 容量验证。
        var remote = client.Uploads.Values.Single();
        for (var number = 1; number < count; number++)
            remote.Parts[number] = new(Array.Empty<byte>(), "etag-" + number, Hash) { Length = partSize };
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        var receipt = await service.UploadPartAsync(session.Id, count, source);
        Assert.Equal(1, client.ListCalls); Assert.Equal(1, client.ListedParts);
        Assert.Equal(("6399", (int?)1), Assert.Single(client.ListRequests));
        Assert.Equal(receipt.ETag, (await env.Get<IFileManagementStore>().FindPartAsync(session.Id, count))!.ETag);

        await env.RestartAsync();
        service = env.Get<FileManagementService>();
        var resumed = await service.GetSessionAsync(session.Id);
        Assert.Equal(count, Assert.Single(resumed.Parts).Number);
        Assert.Equal(8, client.ListCalls); Assert.Equal(6401, client.ListedParts);
        Assert.All(client.ListRequests.Skip(1), request => Assert.Equal(1000, request.MaxParts));
        // 完成仍需全部已验证回执，不能因尾片点查成功而接受其余未登记分片。
        Assert.Equal("invalid_parts", (await Assert.ThrowsAsync<FileManagementException>(() => service.CompleteUploadAsync(session.Id))).Code);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailedReceiptTransaction_LeavesPartUnconfirmedUntilReupload(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var store = env.Get<IFileManagementStore>();
        var fault = ReviewStoreProxy.Wrap(store);
        fault.OnUpdate = (_, _) => throw new ReviewDbException();
        var service = new FileManagementService((IFileManagementStore)(object)fault, env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), env.Authorizer, env.Get<FileManagementOptions>());
        await Assert.ThrowsAsync<ReviewDbException>(() => Upload(service, session.Id));
        Assert.Single(client.Uploads.Values.Single().Parts);
        Assert.Null((await store.FindPartAsync(session.Id, 1))!.ETag);
        await env.RestartAsync(); service = env.Get<FileManagementService>();
        Assert.Empty((await service.GetSessionAsync(session.Id)).Parts);
        await Upload(service, session.Id); await service.CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await service.GetSessionAsync(session.Id)).State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExpiredDuringTransfer_PreventsReceiptCommit(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var store = env.Get<IFileManagementStore>();
        client.AfterUpload = async () =>
        {
            var entry = (await store.FindSessionAsync(session.Id))!; entry.Session!.ExpiresAt = 0;
            Assert.True(await store.TryUpdateAsync(entry, entry.File.Version));
        };
        await Assert.ThrowsAsync<FileManagementException>(() => Upload(env.Get<FileManagementService>(), session.Id));
        Assert.Null((await store.FindPartAsync(session.Id, 1))!.ETag);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AlreadyCommittedObject_PublishesWithoutListingConsumedUploadId(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client); await env.InitializeAsync();
        var session = await CreateAsync(env); var service = env.Get<FileManagementService>();
        await Upload(service, session.Id); await service.CompleteUploadAsync(session.Id);
        var store = env.Get<IFileManagementStore>(); var entry = (await store.FindSessionAsync(session.Id))!;
        var backend = (IMultipartFileStorage)env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope());
        await backend.CompleteMultipartAsync(entry.Upload(), (await store.ListPartsAsync(session.Id)).Select(p => p.ToStoragePart()).ToArray());
        var lists = client.ListCalls;
        await env.RestartAsync(); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await env.Get<FileManagementService>().GetSessionAsync(session.Id)).State);
        Assert.Equal(lists, client.ListCalls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task LocalToProxyMigration_VerifiesContentAndRetiresOldLocation(bool freeSql)
    {
        using var client = new RecordingS3Client(); await using var env = Environment(freeSql, client, false); await env.InitializeAsync();
        var uploaded = await env.UploadAsync(new byte[] { 1, 2, 3 });
        var before = (await env.Get<IFileManagementStore>().FindAsync(uploaded.FileId))!;
        await env.Get<FileMigrationService>().QueueMigrationAsync(uploaded.FileId, "qiniu-proxy");
        await env.RunAsync(4);
        var after = (await env.Get<IFileManagementStore>().FindAsync(uploaded.FileId))!;
        Assert.Equal("qiniu-proxy", after.File.StoreName); Assert.Equal(FileState.Ready, after.File.State);
        Assert.NotNull(after.File.Sha256);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(client.Objects).Value.Bytes);
        Assert.Null(await env.Get<IFileStorageFactory>().GetStorage("local-v1", before.File.Scope()).GetPropertiesAsync(before.File.Address()));
    }
}
