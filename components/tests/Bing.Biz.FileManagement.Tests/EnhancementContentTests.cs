using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Bing.FileStorage;
using Xunit;

namespace Bing.Biz.FileManagement.Tests;

public sealed class EnhancementContentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentPolicy_FreezesAtCreationAndQuarantinesUntilActualHeaderPasses(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql) { ConfigureManagement = o =>
        {
            o.Containers["attachments"].AllowedExtensions.Add(".pdf");
            o.Containers["attachments"].AllowedContentTypes.Add("application/pdf");
            o.Containers["attachments"].FileHeaders[".pdf"] = "25504446";
        } };
        await env.InitializeAsync(); var service = env.Get<FileManagementService>();
        Assert.Equal("content_policy", (await Assert.ThrowsAsync<FileManagementException>(() => service.CreateUploadAsync(
            new() { RequestId = Guid.NewGuid(), FileName = "bad.exe", ContentType = "application/pdf", Length = 1 }))).Code);
        var good = await StageAsync(env, Encoding.ASCII.GetBytes("%PDF document"), "one.pdf", "application/pdf");
        Assert.Equal(FileState.Quarantined, (await service.GetSessionAsync(good.Id)).State);
        await Assert.ThrowsAsync<FileManagementException>(() => service.AuthorizeReadAsync(good.FileId));
        Assert.Empty(await env.Get<IFileManagementStore>().ListDueEventsAsync(long.MaxValue, 100));
        // 配置变化不能放宽已经创建的会话策略。
        env.ConfigureManagement = _ => { };
        await env.RestartAsync(); await env.RunAsync();
        Assert.Equal(FileState.Ready, (await env.Get<FileManagementService>().GetAsync(good.FileId)).State);
        Assert.Equal("FileReady", Assert.Single(await env.Get<IFileManagementStore>().ListDueEventsAsync(long.MaxValue, 100)).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HeaderRejection_FailsClosedAndCleansActualObject(bool freeSql)
    {
        await using var env = new FileTestEnvironment(freeSql) { ConfigureManagement = o => o.Containers["public"].FileHeaders[".pdf"] = "25504446" };
        await env.InitializeAsync();
        var session = await StageAsync(env, Encoding.ASCII.GetBytes("fake PDF"), "bad.pdf", "application/pdf", "public");
        Assert.Equal(FileState.Quarantined, (await env.Get<FileManagementService>().GetSessionAsync(session.Id)).State);
        await Assert.ThrowsAsync<FileManagementException>(() => env.Get<FileManagementService>().AuthorizeReadAsync(session.FileId, true));
        await env.RunAsync(3);
        var entry = (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!;
        Assert.Equal(FileState.Failed, entry.File.State);
        Assert.Null(await env.Get<IFileStorageFactory>().GetStorage(entry.File.StoreName, entry.File.Scope()).GetPropertiesAsync(entry.File.Address()));
        Assert.Empty(await env.Get<IFileManagementStore>().ListDueEventsAsync(long.MaxValue, 100));
        Assert.Equal(0, (await env.Get<IFileManagementStore>().GetOperationalSnapshotAsync()).ReservedBytes);
    }

    [Theory]
    [InlineData("stream: OK\0", FileState.Ready, 150000)]
    [InlineData("stream: OK\0", FileState.Ready, 0)]
    [InlineData("stream: Eicar-Test-Signature FOUND\0", FileState.Failed, 150000)]
    [InlineData("UNKNOWN COMMAND\0", FileState.Failed, 150000)]
    public async Task ClamProtocol_StreamsExactBytesAndRequiresExplicitCleanResponse(string response, FileState expected, int length)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var data = Enumerable.Range(0, length).Select(x => (byte)x).ToArray();
        var received = ReceiveScanAsync(listener, response, timeout.Token);
        await using var env = new FileTestEnvironment { ConfigureManagement = o =>
        {
            o.Containers["attachments"].RequireMalwareScan = true;
            o.Containers["attachments"].MaxFileLength = 1024 * 1024;
            o.ClamAv.Host = "127.0.0.1"; o.ClamAv.Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        } };
        await env.InitializeAsync(); var session = await StageAsync(env, data);
        await env.RunAsync(2);
        Assert.Equal(data, await received);
        Assert.Equal(expected, (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!.File.State);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public async Task ScannerUnavailable_KeepsFileQuarantinedAndDoesNotEmitReady()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        await using var env = new FileTestEnvironment { ConfigureManagement = o =>
        { o.Containers["attachments"].RequireMalwareScan = true; o.Containers["attachments"].MaxFileLength = 1024; o.ClamAv.Host = "127.0.0.1"; o.ClamAv.Port = port; } };
        await env.InitializeAsync(); var session = await StageAsync(env, new byte[] { 1 }); await env.RunAsync(1);
        Assert.Equal(FileState.Quarantined, (await env.Get<IFileManagementStore>().FindAsync(session.FileId))!.File.State);
        await Assert.ThrowsAsync<FileManagementException>(() => env.Get<FileManagementService>().AuthorizeReadAsync(session.FileId));
        Assert.Empty(await env.Get<IFileManagementStore>().ListDueEventsAsync(long.MaxValue, 100));
    }

    [ClamAvFact]
    public async Task RealClamAv_CleanAndEicar_ArePublishedAndRejectedRespectively()
    {
        await using var env = new FileTestEnvironment(minio: true) { ConfigureManagement = o =>
        { o.Containers["attachments"].RequireMalwareScan = true; o.Containers["attachments"].MaxFileLength = 1024 * 1024;
            o.ClamAv.Host = Environment.GetEnvironmentVariable("BING_FILE_CLAMAV_HOST");
            o.ClamAv.Port = int.Parse(Environment.GetEnvironmentVariable("BING_FILE_CLAMAV_PORT") ?? "3310"); } };
        await env.InitializeAsync();
        var clean = await StageAsync(env, Encoding.ASCII.GetBytes("ordinary test document")); await env.RunAsync(2);
        Assert.Equal(FileState.Ready, (await env.Get<IFileManagementStore>().FindAsync(clean.FileId))!.File.State);
        var eicar = Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");
        var infected = await StageAsync(env, eicar); await env.RunAsync(3);
        Assert.Equal(FileState.Failed, (await env.Get<IFileManagementStore>().FindAsync(infected.FileId))!.File.State);
        Assert.Equal(clean.FileId, Assert.Single(await env.Get<IFileManagementStore>().ListDueEventsAsync(long.MaxValue, 100)).FileId);
    }

    private static async Task<byte[]> ReceiveScanAsync(TcpListener listener, string response, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync().WaitAsync(ct); await using var network = client.GetStream();
        var command = new byte[10]; await ReadExactlyAsync(network, command, ct); Assert.Equal("zINSTREAM\0", Encoding.ASCII.GetString(command));
        using var output = new MemoryStream(); var size = new byte[4];
        while (true)
        {
            await ReadExactlyAsync(network, size, ct); var length = BinaryPrimitives.ReadUInt32BigEndian(size); if (length == 0) break;
            Assert.InRange(length, 1u, 65536u); var chunk = new byte[length]; await ReadExactlyAsync(network, chunk, ct); await output.WriteAsync(chunk, ct);
        }
        await network.WriteAsync(Encoding.UTF8.GetBytes(response), ct); return output.ToArray();
    }
    private static async Task ReadExactlyAsync(Stream stream, byte[] bytes, CancellationToken ct)
    {
        var offset = 0;
        while (offset < bytes.Length) { var read = await stream.ReadAsync(bytes.AsMemory(offset), ct); if (read == 0) throw new EndOfStreamException(); offset += read; }
    }
    private static async Task<UploadSessionView> StageAsync(FileTestEnvironment env, byte[] data, string name = "scan.bin", string type = "application/octet-stream", string container = "attachments")
    {
        var service = env.Get<FileManagementService>();
        var session = await service.CreateUploadAsync(new() { RequestId = Guid.NewGuid(), FileName = name, ContentType = type, Container = container, Length = data.Length });
        await env.RunAsync(1);
        if (data.Length > 0)
        {
        var signed = await service.CreatePartRequestAsync(session.Id, new() { Number = 1, Sha256 = Convert.ToBase64String(SHA256.HashData(data)) });
        if ((await service.GetSessionAsync(session.Id)).DirectUpload)
        {
            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Put, signed!.Url) { Content = new ByteArrayContent(data) };
            foreach (var header in signed.Headers) request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            using var response = await http.SendAsync(request); response.EnsureSuccessStatusCode();
        }
        else await service.UploadPartAsync(session.Id, 1, new MemoryStream(data));
        }
        await service.CompleteUploadAsync(session.Id);
        await env.RunAsync(1); return session;
    }
}
public sealed class ClamAvFactAttribute : FactAttribute
{
    public ClamAvFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_FILE_CLAMAV_HOST")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_FILE_STORAGE_MINIO_ENDPOINT")))
            Skip = "需要隔离的 clamd 和 MinIO 实例；测试样本保存在专用对象存储。";
    }
}
