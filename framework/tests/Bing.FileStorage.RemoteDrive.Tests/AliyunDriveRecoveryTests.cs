using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage.RemoteDrive;
using Xunit;

namespace Bing.FileStorage.RemoteDrive.Tests;

/// <summary>
/// 覆盖输入在计时外创建；CompareSnapshot始终使用同一个未变旧快照等测试场景。
/// </summary>
public sealed class AliyunDriveRecoveryTests
{
    /// <summary>
    /// 验证重启后分页核对远程分片且仅重传缺失分片。
    /// </summary>
    /// <param name="completionFailure">完成阶段模拟的失败类型。</param>
    [Theory]
    [InlineData("InvalidPart")]
    [InlineData("Unavailable")]
    [InlineData("LostResponse")]
    public async Task RestartPagesRemotePartsAndReuploadsOnlyMissingPart(string completionFailure)
    {
        using var server = new UploadServer { CompletionFailure = completionFailure };
        using var client = Client(server);
        var checkpoint = new RemoteDriveCheckpoint { Name = "object", PartSize = 2 };
        string? saved = null;
        Task Persist(RemoteDriveCheckpoint value, CancellationToken _) { saved = JsonSerializer.Serialize(value); return Task.CompletedTask; }
        await Assert.ThrowsAsync<FileStorageException>(() => client.UploadAsync(Context(), Content(), checkpoint, Persist));
        Assert.Equal(new[] { 1, 2 }, server.Uploads);
        server.RemoteParts.Remove(1); // 本地成功回执仍在，模拟远端丢片。
        checkpoint = JsonSerializer.Deserialize<RemoteDriveCheckpoint>(saved!)!;
        server.CompletionFailure = null;
        Assert.Equal("file", (await client.UploadAsync(Context(), Content(), checkpoint, Persist)).Id);
        Assert.Equal(new[] { 1, 2, 1 }, server.Uploads);
        Assert.Equal(2, server.ListPages);
        Assert.Equal(1, server.Creates);
        Assert.Equal(0, server.Deletes);
        Assert.Equal("\"part-2\"", checkpoint.Parts[2]);
    }

    /// <summary>
    /// 验证重启后仅重传回执不匹配的远程分片。
    /// </summary>
    /// <param name="mismatch">需要模拟的分片回执不一致类型。</param>
    [Theory]
    [InlineData("etag")]
    [InlineData("length")]
    [InlineData("unproven")]
    public async Task RestartReuploadsOnlyMismatchedReceipt(string mismatch)
    {
        using var server = new UploadServer();
        server.RemoteParts[1] = mismatch == "etag" ? (2, "different") : mismatch == "length" ? (1, "part-1") : (2, "part-1");
        server.RemoteParts[2] = (2, "part-2");
        using var client = Client(server);
        var checkpoint = ExistingCheckpoint();
        if (mismatch == "unproven") checkpoint.Parts[1] = "uploaded";
        await client.UploadAsync(Context(), Content(), checkpoint, (_, _) => Task.CompletedTask);
        Assert.Equal(new[] { 1 }, server.Uploads);
        Assert.Equal(0, server.Creates);
    }

    /// <summary>
    /// 验证远程分片列表不完整时不修改检查点或开始上传。
    /// </summary>
    /// <param name="failure">需要模拟的失败场景。</param>
    [Theory]
    [InlineData("unavailable")]
    [InlineData("repeat")]
    [InlineData("missing")]
    public async Task IncompleteRemoteListDoesNotMutateCheckpointOrUpload(string failure)
    {
        using var server = new UploadServer { ListFailure = failure };
        server.RemoteParts[2] = (2, "part-2");
        using var client = Client(server);
        var checkpoint = ExistingCheckpoint();
        var before = JsonSerializer.Serialize(checkpoint);
        var persisted = 0;
        await Assert.ThrowsAsync<FileStorageException>(() => client.UploadAsync(Context(), Content(), checkpoint, (_, _) => { persisted++; return Task.CompletedTask; }));
        Assert.Equal(before, JsonSerializer.Serialize(checkpoint));
        Assert.Equal(0, persisted);
        Assert.Empty(server.Uploads);
        Assert.Equal(0, server.Completions);
    }

    /// <summary>
    /// 验证远程上传缺失时先核对已完成对象再重置检查点。
    /// </summary>
    /// <param name="completed">受控操作是否已完成。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingRemoteUploadReconcilesCompletedObjectBeforeReset(bool completed)
    {
        using var server = new UploadServer { ListFailure = "expired", CompletedOnList = completed };
        using var client = Client(server);
        var checkpoint = ExistingCheckpoint();
        var persisted = 0;
        Task Persist(RemoteDriveCheckpoint _, CancellationToken token) { persisted++; return Task.CompletedTask; }
        if (completed)
        {
            Assert.Equal("file", (await client.UploadAsync(Context(), Content(), checkpoint, Persist)).Id);
            Assert.Equal(0, server.Deletes);
            Assert.Equal(0, persisted);
        }
        else
        {
            var error = await Assert.ThrowsAsync<FileStorageException>(() => client.UploadAsync(Context(), Content(), checkpoint, Persist));
            Assert.Equal(FileStorageError.Unavailable, error.Error);
            Assert.Null(checkpoint.UploadId);
            Assert.Empty(checkpoint.Parts);
            Assert.Equal(1, server.Deletes);
            Assert.Equal(1, persisted);
        }
    }

    /// <summary>
    /// 验证授权开始与完成入口均拒绝被替换的回调地址。
    /// </summary>
    /// <param name="callback">授权回调地址。</param>
    [Theory]
    [InlineData("https://application.test:444/files")]
    [InlineData("https://application.test/other")]
    [InlineData("https://application.test/Files")]
    [InlineData("https://application.test/files?next=attacker")]
    [InlineData("https://user@application.test/files")]
    [InlineData("https://application.test/files#fragment")]
    [InlineData("https://application.test.attacker.test/files")]
    [InlineData("http://application.test/files")]
    public async Task AuthorizationRejectsChangedCallbackAtBothEntrypoints(string callback)
    {
        using var client = new AliyunDriveClient(Options());
        await Assert.ThrowsAsync<ArgumentException>(() => client.BeginAuthorizationAsync(callback, "state"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.CompleteAuthorizationAsync(new() { RedirectUri = callback }, "code"));
    }

    /// <summary>
    /// 验证授权配置拒绝不受支持的回环地址协议。
    /// </summary>
    /// <param name="callback">授权回调地址。</param>
    [Theory]
    [InlineData("ftp://localhost/files")]
    [InlineData("file:///files")]
    public void ConfigurationRejectsNonHttpLoopbackScheme(string callback)
        => Assert.Throws<ArgumentException>(() => new AliyunDriveClient(new() { ClientId = "client", ClientSecret = "secret", AllowedRedirectUris = new() { callback } }));

    /// <summary>
    /// 验证授权使用冻结的回调白名单及明确的回环端口。
    /// </summary>
    [Fact]
    public async Task AuthorizationUsesImmutableWhitelistAndExplicitLoopbackPort()
    {
        var options = Options();
        options.AllowedRedirectUris.Add("http://localhost:5173/files");
        using var client = new AliyunDriveClient(options);
        options.AllowedRedirectUris.Add("https://attacker.test/files");
        Assert.Equal("https://application.test/files", (await client.BeginAuthorizationAsync("https://APPLICATION.test:443/files", "state")).RedirectUri);
        Assert.Equal("http://localhost:5173/files", (await client.BeginAuthorizationAsync("http://localhost:5173/files", "state")).RedirectUri);
        await Assert.ThrowsAsync<ArgumentException>(() => client.BeginAuthorizationAsync("http://localhost:5174/files", "state"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.BeginAuthorizationAsync("https://attacker.test/files", "state"));
    }

    /// <summary>
    /// 验证没有回调白名单时授权默认拒绝。
    /// </summary>
    [Fact]
    public async Task AuthorizationWithoutWhitelistFailsClosed()
    {
        using var client = new AliyunDriveClient(new() { ClientId = "client", ClientSecret = "secret" });
        var error = await Assert.ThrowsAsync<FileStorageException>(() => client.BeginAuthorizationAsync("https://application.test/files", "state"));
        Assert.Equal(FileStorageError.Configuration, error.Error);
    }

    /// <summary>
    /// 创建阿里云盘恢复测试选项。
    /// </summary>
    /// <returns>包含固定客户端及回调地址的测试选项。</returns>
    private static AliyunDriveOptions Options() => new() { ClientId = "client", ClientSecret = "secret", AllowedRedirectUris = new() { "https://application.test/files" } };
    /// <summary>
    /// 创建使用指定 HTTP 处理器的阿里云盘客户端。
    /// </summary>
    /// <param name="handler">执行测试操作的处理器。</param>
    /// <returns>使用指定 HTTP 处理器的阿里云盘客户端。</returns>
    private static AliyunDriveClient Client(HttpMessageHandler handler) => new(Options(), new HttpClient(handler, false), new RemoteDriveTransport(new HttpClient(handler, false)));
    /// <summary>
    /// 创建包含已有分片的远程上传检查点。
    /// </summary>
    /// <returns>包含文件、上传会话及已有分片回执的检查点。</returns>
    private static RemoteDriveCheckpoint ExistingCheckpoint() => new() { Name = "object", FileId = "file", UploadId = "upload", PartSize = 2, Parts = new() { [1] = "\"part-1\"", [2] = "\"part-2\"" } };
    /// <summary>
    /// 创建使用固定凭据的远程网盘测试上下文。
    /// </summary>
    /// <returns>使用固定连接及凭据回调的网盘上下文。</returns>
    private static RemoteDriveContext Context() => new(new RemoteDriveConnection { DriveId = "drive", RootFolderId = "root" }, (_, _) => Task.FromResult(new RemoteDriveCredentials { AccessToken = "token" }));
    /// <summary>
    /// 创建支持分段读取的远程上传测试内容。
    /// </summary>
    /// <returns>支持按偏移及长度打开内容段的上传描述。</returns>
    private static RemoteDriveContent Content() => new(4, async (offset, length, consume, ct) => { using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 }, (int)offset, (int)length, false); await consume(stream, ct); });

    /// <summary>
    /// 为AliyunDriveRecoveryTests测试提供 HTTP 请求处理替身。
    /// </summary>
    private sealed class UploadServer : HttpMessageHandler
    {
        /// <summary>
        /// 保存上传服务器模拟的完成和列表失败消息。
        /// </summary>
        public string? CompletionFailure, ListFailure;
        /// <summary>
        /// 指示上传服务器是否在列表时返回已完成状态。
        /// </summary>
        public bool CompletedOnList;
        /// <summary>
        /// 指示上传服务器是否已完成上传。
        /// </summary>
        private bool _completed;
        /// <summary>
        /// 记录上传服务器的列表页数、创建、删除和完成次数。
        /// </summary>
        public int ListPages, Creates, Deletes, Completions;
        /// <summary>
        /// 获取已记录的文件上传请求。
        /// </summary>
        public List<int> Uploads { get; } = new();
        /// <summary>
        /// 获取远端返回的分片信息。
        /// </summary>
        public Dictionary<int, (int Length, string ETag)> RemoteParts { get; } = new();
        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.Host == "upload.test")
            {
                var number = int.Parse(request.RequestUri.AbsolutePath.Trim('/'));
                Assert.Equal(new byte[] { (byte)(2 * number - 1), (byte)(2 * number) }, await request.Content!.ReadAsByteArrayAsync(ct));
                RemoteParts[number] = (2, "part-" + number);
                Uploads.Add(number);
                var result = Json(new { }); result.Headers.ETag = new EntityTagHeaderValue("\"part-" + number + "\""); return result;
            }
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var body = document.RootElement;
            switch (request.RequestUri.AbsolutePath.Split('/').Last())
            {
                case "list": return Json(new { items = Array.Empty<object>(), next_marker = "" });
                case "create": Creates++; return Json(new { file_id = "file", upload_id = "upload" });
                case "get": return Json(new { file_id = "file", parent_file_id = "root", name = "object", status = _completed ? "available" : "uploading", size = 4 });
                case "getUploadUrl":
                    var number = body.GetProperty("part_info_list")[0].GetProperty("part_number").GetInt32();
                    return Json(new { part_info_list = new[] { new { part_number = number, upload_url = "https://upload.test/" + number } } });
                case "listUploadedParts":
                    ListPages++;
                    Assert.Equal("drive", body.GetProperty("drive_id").GetString());
                    Assert.Equal("file", body.GetProperty("file_id").GetString());
                    Assert.Equal("upload", body.GetProperty("upload_id").GetString());
                    if (ListFailure == "expired") { _completed = CompletedOnList; return Json(new { code = "NoSuchUpload" }, HttpStatusCode.NotFound); }
                    var secondPage = body.TryGetProperty("part_number_marker", out var marker);
                    if (secondPage) Assert.Equal("1", marker.GetString());
                    if (secondPage && ListFailure == "unavailable") return Json(new { code = "Unavailable" }, HttpStatusCode.ServiceUnavailable);
                    if (secondPage && ListFailure == "missing") return Json(new { next_part_number_marker = "" });
                    var selected = secondPage ? 2 : 1;
                    var parts = RemoteParts.Where(p => p.Key == selected).Select(p => new { part_number = p.Key, part_size = p.Value.Length, etag = p.Value.ETag }).ToArray();
                    return Json(new { file_id = "file", upload_id = "upload", uploaded_parts = parts,
                        next_part_number_marker = !secondPage || ListFailure == "repeat" ? "1" : "" });
                case "complete":
                    Completions++;
                    if (CompletionFailure == "LostResponse") throw new HttpRequestException("injected response loss");
                    if (CompletionFailure != null) return Json(new { code = CompletionFailure }, HttpStatusCode.BadRequest);
                    Assert.Equal(2, RemoteParts.Count); _completed = true; return Json(new { });
                case "trash": Deletes++; return Json(new { });
                default: throw new InvalidOperationException(request.RequestUri.AbsolutePath);
            }
        }
        /// <summary>
        /// 创建指定值的 JSON HTTP 响应。
        /// </summary>
        /// <param name="value">需要格式化或作为 SQL 参数的测试值。</param>
        /// <param name="status">模拟 HTTP 响应的状态码。</param>
        /// <returns>构造或分派得到的 HTTP 响应。</returns>
        private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
