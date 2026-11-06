using System.Security.Cryptography;
using System.Text;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage.Pan115;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.FileStorage.RemoteDrive.Tests;

/// <summary>
/// 使用显式凭据门控的远程盘云端测试。
/// </summary>
/// <remarks>
/// 显式门控的真实账号隔离测试，访问令牌仅从进程环境读取。
/// </remarks>
public sealed class DriveLiveTests
{
    /// <summary>
    /// 验证阿里云盘实机上传、读取及删除。
    /// </summary>
    [DriveLiveFact("ALIYUNDRIVE")]
    public Task AliyunDrive_RealUploadReadVerifyAndDelete() => Exercise("ALIYUNDRIVE", new AliyunDriveClient(new()
        { ClientId = Required("ALIYUNDRIVE", "CLIENT_ID"), ClientSecret = Required("ALIYUNDRIVE", "CLIENT_SECRET") }));
    /// <summary>
    /// 验证 115 网盘实机上传、读取及删除。
    /// </summary>
    [DriveLiveFact("PAN115")]
    public Task Pan115_RealUploadReadVerifyAndDelete() => Exercise("PAN115", new Pan115Client(new() { ClientId = Required("PAN115", "CLIENT_ID") }));
    /// <summary>
    /// 读取指定网盘实机测试的必需环境配置。
    /// </summary>
    /// <param name="provider">测试使用的提供程序名称。</param>
    /// <param name="name">必需环境配置的名称。</param>
    /// <returns>指定网盘的必需环境配置值；缺失时抛出异常。</returns>
    private static string Required(string provider, string name) => Environment.GetEnvironmentVariable("BING_FILE_STORAGE_" + provider + "_" + name)
        ?? throw new InvalidOperationException("缺少测试环境配置：" + provider + "_" + name);
    /// <summary>
    /// 执行网盘实机上传、读取校验及删除。
    /// </summary>
    /// <param name="provider">测试使用的提供程序名称。</param>
    /// <param name="client">执行测试请求的客户端。</param>
    private static async Task Exercise(string provider, IRemoteDriveClient client)
    {
        using var owner = (IDisposable)client; using var ct = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var credentials = new RemoteDriveCredentials { AccessToken = Required(provider, "ACCESS_TOKEN"), ExpiresAt = long.MaxValue };
        var account = await client.GetAccountAsync(credentials, ct.Token);
        var context = new RemoteDriveContext(new RemoteDriveConnection { RemoteAccountId = account.AccountId, DriveId = account.DriveId,
            RootFolderId = Required(provider, "TEST_FOLDER_ID") }, (refresh, token) => refresh
                ? throw new FileStorageException(FileStorageError.AccessDenied, "此基础云端探针不轮换令牌，请通过应用连接刷新后重试。") : Task.FromResult(credentials));
        var bytes = Encoding.UTF8.GetBytes("Bing remote drive isolated " + Guid.NewGuid().ToString("N"));
        var checkpoint = new RemoteDriveCheckpoint { Name = "bing-probe-" + Guid.NewGuid().ToString("N") };
        var content = new RemoteDriveContent(bytes.Length, async (offset, length, consume, token) =>
        { using var stream = new MemoryStream(bytes, (int)offset, (int)length, false); await consume(stream, token); });
        RemoteDriveFile? file = null;
        try
        {
            file = await client.UploadAsync(context, content, checkpoint, (_, token) => { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }, ct.Token);
            using var transport = new RemoteDriveTransport(); string? hash = null;
            await transport.ReadAsync(await client.DownloadAsync(context, file, ct.Token), async (stream, token) => hash = await FileStorageStreams.CopyExactAsync(stream, Stream.Null, bytes.Length, token), ct.Token);
            Assert.Equal(Convert.ToBase64String(SHA256.HashData(bytes)), hash);
            Assert.NotNull(await client.GetFileAsync(context, file.Id, file.ParentId, ct.Token));
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            if (file != null) await client.DeleteAsync(context, file, cleanup.Token); else await client.AbortAsync(context, checkpoint, cleanup.Token);
        }
    }
}

/// <summary>
/// 仅在配置对应 RemoteDrive 测试账号时运行真实服务测试。
/// </summary>
public sealed class DriveLiveFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="DriveLiveFactAttribute" /> 类型的实例。
    /// </summary>
    /// <param name="provider">实例使用的数据或服务提供程序。</param>
    public DriveLiveFactAttribute(string provider)
    {
        if (Environment.GetEnvironmentVariable("BING_FILE_STORAGE_" + provider + "_RUN_LIVE") != "1")
            Skip = "未启用独立 " + provider + " 测试账号；本地协议通过不代表云端兼容通过。";
    }
}
