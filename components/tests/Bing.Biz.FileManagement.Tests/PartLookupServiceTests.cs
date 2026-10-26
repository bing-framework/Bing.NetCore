using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Bing.FileStorage;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

/// <summary>直接验证热路径使用单条查询，同时保留摘要冲突、鉴权与真实上传行为。</summary>
public sealed class PartLookupServiceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PartLookup_RegistrationAndUpload_UseOnePartAndPreserveContracts(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var original = env.Get<FileManagementService>();
        var session = await original.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "part.bin", Length = 1 });
        await env.RunAsync();
        var proxy = PartLookupStoreProxy.Wrap(env.Get<IFileManagementStore>());
        var service = new FileManagementService((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), env.Authorizer, env.Get<FileManagementOptions>());
        var hash = Convert.ToBase64String(SHA256.HashData(new byte[] { 7 }));

        // 未登记不能上传，且只查询目标分片。
        var missing = await Assert.ThrowsAsync<FileManagementException>(() => service.UploadPartAsync(session.Id, 1, Stream.Null));
        Assert.Equal("invalid", missing.Code); Assert.Equal(1, proxy.PartQueries);
        Assert.Null(await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = hash }));
        Assert.Null(await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = hash }));
        Assert.Equal(3, proxy.PartQueries);
        var conflict = await Assert.ThrowsAsync<FileManagementException>(() => service.CreatePartRequestAsync(session.Id,
            new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(new byte[] { 8 })) }));
        Assert.Equal("conflict", conflict.Code); Assert.Equal(4, proxy.PartQueries);

        using var input = new MemoryStream(new byte[] { 7 });
        var uploaded = await service.UploadPartAsync(session.Id, 1, input);
        Assert.Equal(1, uploaded.Length); Assert.True(input.CanRead); Assert.Equal(5, proxy.PartQueries);
        Assert.Equal(hash, (await env.Get<IFileManagementStore>().FindPartAsync(session.Id, 1))!.Sha256);

        env.Identity.UserId = "other-user";
        var forbidden = await Assert.ThrowsAsync<FileManagementException>(() => service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = hash }));
        Assert.Equal("not_found", forbidden.Code); Assert.Equal(5, proxy.PartQueries);
        env.Identity.UserId = "user-a"; env.Identity.Tenant = "other-tenant";
        await Assert.ThrowsAsync<FileManagementException>(() => service.UploadPartAsync(session.Id, 1, Stream.Null));
        Assert.Equal(5, proxy.PartQueries);
        env.Identity.Tenant = "tenant-a";

        // 完成与恢复有意保留全量核验，使用原服务验证完整真实链路。
        await original.CompleteUploadAsync(session.Id); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await original.GetSessionAsync(session.Id)).State);
        await original.DeleteAsync(session.FileId); await env.RunAsync();
        Assert.Equal(FileState.Deleted, (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!.File.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PartLookup_CasRetry_RequeriesWithoutFullList(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql); await env.InitializeAsync();
        var session = await env.Get<FileManagementService>().CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = "retry.bin", Length = 1 });
        await env.RunAsync();
        var proxy = PartLookupStoreProxy.Wrap(env.Get<IFileManagementStore>()); proxy.RejectNextUpdate = true;
        var service = new FileManagementService((IFileManagementStore)(object)proxy, env.Get<IFileStorageFactory>(), env.Identity, env.Identity,
            env.Get<IFilePermissionChecker>(), env.Authorizer, env.Get<FileManagementOptions>());
        await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(new byte[] { 1 })) });
        Assert.Equal(2, proxy.PartQueries);
        Assert.Single(await env.Get<IFileManagementStore>().ListPartsAsync(session.Id));
    }
}

public class PartLookupStoreProxy : DispatchProxy
{
    private IFileManagementStore _inner = null!;
    public int PartQueries { get; private set; }
    public bool RejectNextUpdate { get; set; }
    public static PartLookupStoreProxy Wrap(IFileManagementStore inner)
    { var proxy = (PartLookupStoreProxy)Create<IFileManagementStore, PartLookupStoreProxy>(); proxy._inner = inner; return proxy; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == nameof(IFileManagementStore.ListPartsAsync)) throw new InvalidOperationException("单分片路径不得读取完整清单。");
        if (method.Name == nameof(IFileManagementStore.FindPartAsync)) PartQueries++;
        if (method.Name == nameof(IFileManagementStore.TryUpdateAsync) && RejectNextUpdate)
        { RejectNextUpdate = false; return Task.FromResult(false); }
        try { return method.Invoke(_inner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
}
