using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bing.FileStorage.AliyunDrive;
using Bing.FileStorage.AliyunOss.Tests;
using Bing.FileStorage.Pan115;
using Bing.FileStorage.RemoteDrive;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.RemoteDrive.Tests;

/// <summary>
/// 覆盖多调用点投影、自连接、显式别名及单段和双段读取应生成可编译拦截器与继承元数据等测试场景。
/// </summary>
public sealed class DriveProtocolTests
{
    /// <summary>
    /// 验证网盘模板注册仅释放自有客户端并拒绝重复注册。
    /// </summary>
    /// <param name="ownsClient">注册项是否拥有并负责释放客户端。</param>
    [Theory][InlineData(false)][InlineData(true)]
    public async Task TemplateRegistration_DisposesOnlyOwnedClientsAndRejectsDuplicates(bool ownsClient)
    {
        using var handler = new ApiHandler(_ => throw new InvalidOperationException());
        using var client = new Pan115Client(new() { ClientId = "test" }, new HttpClient(handler, false));
        var services = new ServiceCollection();
        var template = new RemoteDriveTemplate("test", new() { MaximumFileLength = 1024 }, client, ownsClient);
        services.AddBingRemoteDrive(template);
        Assert.Throws<ArgumentException>(() => services.AddBingRemoteDrive(template));
        using (var provider = services.BuildServiceProvider()) Assert.Same(template, Assert.Single(provider.GetServices<RemoteDriveTemplate>()));
        // 已拥有的 HttpClient 会在模板生命周期结束时拒绝发送；借用模式仍能进入 handler。
        if (ownsClient) await Assert.ThrowsAsync<ObjectDisposedException>(() => client.BeginAuthorizationAsync("", "state"));
        else await Assert.ThrowsAsync<InvalidOperationException>(() => client.BeginAuthorizationAsync("", "state"));
    }
    /// <summary>
    /// 创建使用固定凭据的远程网盘测试上下文。
    /// </summary>
    /// <returns>使用固定连接及凭据回调的网盘上下文。</returns>
    private static RemoteDriveContext Context() => new(new RemoteDriveConnection { RemoteAccountId = "account", DriveId = "drive", RootFolderId = "root" },
        (_, _) => Task.FromResult(new RemoteDriveCredentials { AccessToken = "bearer", RefreshToken = "refresh", ExpiresAt = long.MaxValue }));
    /// <summary>
    /// 创建支持分段读取的远程上传测试内容。
    /// </summary>
    /// <param name="bytes">待上传或读取的字节内容。</param>
    /// <returns>支持按偏移及长度打开内容段的上传描述。</returns>
    private static RemoteDriveContent Content(byte[] bytes) => new(bytes.Length, async (offset, length, consume, ct) =>
    { using var stream = new MemoryStream(bytes, (int)offset, (int)length, false); await consume(stream, ct); });
    /// <summary>
    /// 验证阿里云盘上传刷新过期地址并核对丢失的完成响应。
    /// </summary>
    [Fact]
    public async Task AliyunDrive_UploadRefreshesExpiredUrlAndReconcilesLostCompleteResponse()
    {
        var bytes = Encoding.UTF8.GetBytes("upload-content"); var urlCount = 0; var puts = 0; var completed = false; var persisted = new List<string>();
        using var handler = new ApiHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.RequestUri.Host == "upload.test")
            {
                Assert.Null(request.Headers.Authorization); Assert.Equal(bytes, await request.Content!.ReadAsByteArrayAsync());
                return ++puts == 1 ? new(HttpStatusCode.Forbidden) : new(HttpStatusCode.OK);
            }
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement;
            switch (path)
            {
                case "/adrive/v1.0/openFile/list": return Json(new { items = Array.Empty<object>(), next_marker = "" });
                case "/adrive/v1.0/openFile/create":
                    Assert.Equal("refuse", body.Text("check_name_mode")); Assert.False(body.TryGetProperty("proof_code", out _));
                    return Json(new { file_id = "file", upload_id = "upload" });
                case "/adrive/v1.0/openFile/getUploadUrl":
                    Assert.Single(body.Child("part_info_list").EnumerateArray());
                    return Json(new { part_info_list = new[] { new { part_number = 1, upload_url = "https://upload.test/part?signature=" + ++urlCount } } });
                case "/adrive/v1.0/openFile/complete": completed = true; return Json(new { code = "NoSuchUpload" }, HttpStatusCode.NotFound);
                case "/adrive/v1.0/openFile/get":
                    Assert.True(completed); return Json(new { file_id = "file", parent_file_id = "root", name = "object", status = "available", size = bytes.Length, content_hash = "hash", updated_at = "2026-01-01T00:00:00Z" });
                default: throw new InvalidOperationException(path);
            }
        });
        using var client = new AliyunDriveClient(new() { ClientId = "client", ClientSecret = "secret" }, new HttpClient(handler, false), new RemoteDriveTransport(new HttpClient(handler, false)));
        var checkpoint = new RemoteDriveCheckpoint { Name = "object" };
        var file = await client.UploadAsync(Context(), Content(bytes), checkpoint, (p, _) => { persisted.Add(JsonSerializer.Serialize(p)); return Task.CompletedTask; });
        Assert.Equal(bytes.Length, file.Length); Assert.Equal(2, puts); Assert.Single(checkpoint.Parts);
        Assert.All(persisted, json => { Assert.DoesNotContain("signature", json); Assert.DoesNotContain("bearer", json); });
    }
    /// <summary>
    /// 验证阿里云盘使用本应用授权且支持列表分页。
    /// </summary>
    [Fact]
    public async Task AliyunDrive_AuthorizationUsesOwnApplicationAndListPaginates()
    {
        var pages = 0;
        using var handler = new ApiHandler(async request =>
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement;
            if (request.RequestUri!.AbsolutePath == "/oauth/access_token")
            { Assert.Equal("client", body.Text("client_id")); Assert.Equal("authorization_code", body.Text("grant_type")); return Json(new { access_token = "a", refresh_token = "r", expires_in = 3600 }); }
            Assert.Equal(pages == 0 ? "" : "next", body.Text("marker")); pages++;
            return pages == 1 ? Json(new { items = Array.Empty<object>(), next_marker = "next" })
                : Json(new { items = new[] { new { file_id = "f", parent_file_id = "root", name = "wanted", status = "available", size = 2 } }, next_marker = "" });
        });
        using var client = new AliyunDriveClient(new() { ClientId = "client", ClientSecret = "secret", AllowedRedirectUris = new() { "https://application.test/files" } }, new HttpClient(handler));
        var auth = await client.BeginAuthorizationAsync("https://application.test/files", "csrf");
        Assert.StartsWith("https://openapi.alipan.com/oauth/authorize?", auth.Url); Assert.Contains("state=csrf", auth.Url);
        Assert.Equal("a", (await client.CompleteAuthorizationAsync(auth, "code"))!.AccessToken);
        Assert.Equal("f", (await client.FindByNameAsync(Context(), "root", "wanted"))!.Id); Assert.Equal(2, pages);
    }
    /// <summary>
    /// 验证阿里云盘授权拒绝未登记的回调地址。
    /// </summary>
    [Fact]
    public async Task AliyunDrive_AuthorizationRejectsUnregisteredRedirectUri()
    {
        using var handler = new ApiHandler(_ => throw new InvalidOperationException());
        using var client = new AliyunDriveClient(new() { ClientId = "client", ClientSecret = "secret", AllowedRedirectUris = new() { "https://application.test/files" } }, new HttpClient(handler));
        await Assert.ThrowsAsync<ArgumentException>(() => client.BeginAuthorizationAsync("https://attacker.test/callback", "csrf"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.BeginAuthorizationAsync("https://application.test/files#fragment", "csrf"));
    }
    /// <summary>
    /// 验证无效远程分片保留可恢复的阿里云盘检查点。
    /// </summary>
    [Fact]
    public async Task AliyunDrive_InvalidRemotePartReconcilesCheckpointForResume()
    {
        var bytes = Encoding.UTF8.GetBytes("upload-content"); var puts = 0; var creates = 0; var aborts = 0; var completed = false; var allowComplete = false;
        using var handler = new ApiHandler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.RequestUri.Host == "upload.test") { puts++; return new(HttpStatusCode.OK); }
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement;
            switch (path)
            {
                case "/adrive/v1.0/openFile/list": return Json(new { items = Array.Empty<object>(), next_marker = "" });
                case "/adrive/v1.0/openFile/listUploadedParts": return Json(new { uploaded_parts = Array.Empty<object>(), next_part_number_marker = "" });
                case "/adrive/v1.0/openFile/create": creates++; return Json(new { file_id = "file-" + creates, upload_id = "upload-" + creates });
                case "/adrive/v1.0/openFile/getUploadUrl":
                    return Json(new { part_info_list = new[] { new { part_number = body.Child("part_info_list").EnumerateArray().Single().Number("part_number"), upload_url = "https://upload.test/part" } } });
                case "/adrive/v1.0/openFile/complete":
                    if (!allowComplete) return Json(new { code = "InvalidPart" }, HttpStatusCode.BadRequest);
                    completed = true;
                    return Json(new { });
                case "/adrive/v1.0/openFile/get":
                    return completed ? Json(new { file_id = "file-1", parent_file_id = "root", name = "object", status = "available", size = bytes.Length, content_hash = "hash" })
                        : Json(new { code = "FileNotFound" }, HttpStatusCode.NotFound);
                case "/adrive/v1.0/openFile/recyclebin/trash": aborts++; return Json(new { });
                default: throw new InvalidOperationException(path);
            }
        });
        using var client = new AliyunDriveClient(new() { ClientId = "client", ClientSecret = "secret" }, new HttpClient(handler, false), new RemoteDriveTransport(new HttpClient(handler, false)));
        var checkpoint = new RemoteDriveCheckpoint { Name = "object" }; var snapshots = new List<RemoteDriveCheckpoint>();
        async Task Persist(RemoteDriveCheckpoint value, CancellationToken _)
        {
            snapshots.Add(JsonSerializer.Deserialize<RemoteDriveCheckpoint>(JsonSerializer.Serialize(value))!);
            await Task.CompletedTask;
        }
        var error = await Assert.ThrowsAsync<FileStorageException>(() => client.UploadAsync(Context(), Content(bytes), checkpoint, Persist));
        Assert.Equal(FileStorageError.Unavailable, error.Error);
        Assert.Equal("file-1", checkpoint.FileId); Assert.Equal("upload-1", checkpoint.UploadId); Assert.Single(checkpoint.Parts);
        Assert.Equal(0, aborts); Assert.Equal(1, creates); Assert.Equal(1, puts);
        allowComplete = true;
        var file = await client.UploadAsync(Context(), Content(bytes), checkpoint, Persist);
        Assert.Equal("file-1", file.Id); Assert.Equal(1, creates); Assert.Equal(2, puts); Assert.True(snapshots.Count >= 4);
    }
    /// <summary>
    /// 验证丢失创建响应可回收草稿且完成状态过期后允许重启。
    /// </summary>
    [Fact]
    public async Task AliyunDrive_LostCreateResponseReclaimsDraftAndExpiredCompletionRestarts()
    {
        var creates = 0; var deletes = 0; var completed = false; string? draft = null;
        using var handler = new ApiHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.RequestUri.Host == "upload.test") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            if (path.EndsWith("/list")) return Task.FromResult(Json(new { items = draft == null ? Array.Empty<object>() : new object[] { new { file_id = draft, name = "object", status = "uploading", parent_file_id = "root" } }, next_marker = "" }));
            if (path.EndsWith("/create"))
            {
                draft = "file-" + ++creates;
                if (creates == 1) throw new HttpRequestException("injected response loss");
                return Task.FromResult(Json(new { file_id = draft, upload_id = "upload-" + creates }));
            }
            if (path.EndsWith("/get")) return Task.FromResult(Json(new { file_id = draft, name = "object", parent_file_id = "root", status = completed ? "available" : "uploading", size = 2 }));
            if (path.EndsWith("/trash")) { deletes++; draft = null; return Task.FromResult(Json(new { })); }
            if (path.EndsWith("/getUploadUrl")) return Task.FromResult(Json(new { part_info_list = new[] { new { part_number = 1, upload_url = "https://upload.test/part" } } }));
            if (path.EndsWith("/complete"))
            {
                if (creates == 2) return Task.FromResult(Json(new { code = "NoSuchUpload" }, HttpStatusCode.NotFound));
                completed = true; return Task.FromResult(Json(new { }));
            }
            throw new InvalidOperationException(path);
        });
        using var client = new AliyunDriveClient(new() { ClientId = "client", ClientSecret = "secret" }, new HttpClient(handler, false), new RemoteDriveTransport(new HttpClient(handler, false)));
        var checkpoint = new RemoteDriveCheckpoint { Name = "object" };
        Func<RemoteDriveCheckpoint, CancellationToken, Task> persist = (_, _) => Task.CompletedTask;
        Assert.Equal(FileStorageError.OutcomeUnknown, (await Assert.ThrowsAsync<FileStorageException>(() => client.UploadAsync(Context(), Content(new byte[] { 1, 2 }), checkpoint, persist))).Error);
        Assert.Null(checkpoint.FileId);
        await Assert.ThrowsAsync<FileStorageException>(() => client.UploadAsync(Context(), Content(new byte[] { 1, 2 }), checkpoint, persist));
        Assert.Null(checkpoint.UploadId); Assert.Empty(checkpoint.Parts);
        Assert.Equal("file-3", (await client.UploadAsync(Context(), Content(new byte[] { 1, 2 }), checkpoint, persist)).Id);
        Assert.Equal(2, deletes); Assert.Equal(3, creates);
    }
    /// <summary>
    /// 验证网盘证明挑战使用包含端点的范围且先完成 OSS 回调。
    /// </summary>
    /// <param name="injectExpiredCredentialAndUnknownCallback">是否注入过期凭据及未知回调以验证拒绝行为。</param>
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Pan115_ProofChallengeUsesInclusiveRangeAndOssCallbackBeforeRegistration(bool injectExpiredCredentialAndUnknownCallback)
    {
        var bytes = Encoding.UTF8.GetBytes("0123456789abcdef"); var inits = 0; var listings = 0; var tokens = 0; var expired = false;
        using var oss = new FakeOssHandler();
        using var handler = new ApiHandler(async request =>
        {
            Assert.Equal("Bing.Test/1.0", request.Headers.UserAgent.ToString());
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/open/ufile/files":
                    listings++; return listings == 1 ? Json(new { state = true, count = 0, data = Array.Empty<object>() }) : Json(new { state = true, count = 1,
                        data = new[] { new { fid = "file", pid = "root", fc = "1", fta = "1", fn = "object", fs = bytes.Length, pc = "pick", sha1 = "hash", upt = 1 } } });
                case "/open/upload/init":
                    var form = ParseForm(await request.Content!.ReadAsStringAsync());
                    Assert.Equal(Convert.ToHexString(SHA1.HashData(bytes)), form["fileid"]); Assert.Equal(form["fileid"], form["preid"]);
                    if (++inits == 1) return Json(new { state = true, data = new { status = 7, sign_key = "proof", sign_check = "2-5" } });
                    Assert.Equal(Convert.ToHexString(SHA1.HashData(bytes[2..6])), form["sign_val"]);
                    return Json(new { state = true, data = new { status = 1, pick_code = "pick", bucket = "test-bucket", @object = "upload/object",
                        callback = new { callback = "{\"callbackUrl\":\"https://115.test/callback\"}", callback_var = "{}" } } });
                case "/open/upload/get_token": tokens++; return Json(new { state = true, data = new { endpoint = "https://oss-cn-hangzhou.aliyuncs.com", AccessKeyId = "temporary", AccessKeySecret = "secret", SecurityToken = "sts" } });
                case "/open/ufile/downurl": return Json(new { state = true, data = new Dictionary<string, object> { ["file"] = new { url = new { url = "https://download.test/content" } } } });
                default: throw new InvalidOperationException(request.RequestUri.AbsolutePath);
            }
        });
        using var client = new Pan115Client(new() { ClientId = "client", UserAgent = "Bing.Test/1.0" }, new HttpClient(handler), config =>
        {
            Assert.Equal("v4", config.SignatureVersion); Assert.False(config.DisableUploadCrc64Check);
            return FakeOssHandler.CreateClient(new Forward(oss, request =>
            {
                if (injectExpiredCredentialAndUnknownCallback && !expired && request.Method == HttpMethod.Put)
                {
                    expired = true; return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("<Error><Code>SecurityTokenExpired</Code><Message>expired</Message><RequestId>test</RequestId></Error>", Encoding.UTF8, "application/xml") };
                }
                return null;
            }, injectExpiredCredentialAndUnknownCallback));
        });
        var checkpoint = new RemoteDriveCheckpoint { Name = "object" }; var snapshots = new List<string>();
        var file = await client.UploadAsync(Context(), Content(bytes), checkpoint, (p, _) => { snapshots.Add(JsonSerializer.Serialize(p)); return Task.CompletedTask; });
        Assert.Equal("file", file.Id); Assert.Equal(2, inits);
        if (injectExpiredCredentialAndUnknownCallback) { Assert.True(expired); Assert.True(tokens >= 4); }
        var completion = Assert.Single(oss.Requests, r => r.Method == "POST" && r.Query.ContainsKey("uploadId"));
        Assert.Equal("{\"callbackUrl\":\"https://115.test/callback\"}", Encoding.UTF8.GetString(Convert.FromBase64String(completion.Headers["x-oss-callback"])));
        Assert.All(snapshots, json => { Assert.DoesNotContain("AccessKey", json); Assert.DoesNotContain("SecurityToken", json); });
        Assert.Equal("Bing.Test/1.0", (await client.DownloadAsync(Context(), file)).Headers["User-Agent"]);
    }

    /// <summary>
    /// 验证网盘中止操作将 OSS 拒绝访问映射为稳定存储错误。
    /// </summary>
    [Fact]
    public async Task Pan115_AbortMapsOssAccessDeniedToStableStorageError()
    {
        using var oss = new FakeOssHandler { DenyAbort = true };
        using var handler = new ApiHandler(request =>
        {
            Assert.Equal("/open/upload/get_token", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(new { state = true, data = new { endpoint = "https://oss-cn-hangzhou.aliyuncs.com",
                AccessKeyId = "temporary", AccessKeySecret = "secret", SecurityToken = "sts" } }));
        });
        using var client = new Pan115Client(new() { ClientId = "client" }, new HttpClient(handler), configuration =>
            FakeOssHandler.CreateClient(new Forward(oss)));
        var error = await Assert.ThrowsAsync<FileStorageException>(() => client.AbortAsync(Context(),
            new RemoteDriveCheckpoint { UploadId = "upload", Bucket = "bucket", ObjectKey = "object" }));
        Assert.Equal(FileStorageError.AccessDenied, error.Error);
        Assert.Single(oss.Requests, request => request.Method == "DELETE" && request.Query.ContainsKey("uploadId"));
    }

    /// <summary>
    /// 验证网盘中止不存在的远程上传保持幂等。
    /// </summary>
    [Fact]
    public async Task Pan115_AbortMissingUploadIsIdempotent()
    {
        using var oss = new FakeOssHandler();
        using var handler = new ApiHandler(request => Task.FromResult(Json(new { state = true, data = new { endpoint = "https://oss-cn-hangzhou.aliyuncs.com",
            AccessKeyId = "temporary", AccessKeySecret = "secret", SecurityToken = "sts" } })));
        using var client = new Pan115Client(new() { ClientId = "client" }, new HttpClient(handler), _ => FakeOssHandler.CreateClient(new Forward(oss)));
        await client.AbortAsync(Context(), new RemoteDriveCheckpoint { UploadId = "missing", Bucket = "bucket", ObjectKey = "object" });
        Assert.Single(oss.Requests, request => request.Method == "DELETE" && request.Query.ContainsKey("uploadId"));
    }
    /// <summary>
    /// 验证二维码授权使用 SHA256 校验器并等待用户确认。
    /// </summary>
    [Fact]
    public async Task Pan115_QrAuthorizationUsesSha256VerifierAndWaitsForConfirmation()
    {
        string? challenge = null; var polls = 0;
        using var handler = new ApiHandler(async request =>
        {
            var form = request.Content == null ? new Dictionary<string, string>() : ParseForm(await request.Content.ReadAsStringAsync());
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/open/authDeviceCode": challenge = form["code_challenge"]; Assert.Equal("sha256", form["code_challenge_method"]); return Json(new { state = 1, data = new { uid = "uid", time = 1, sign = "sign", qrcode = "https://115.com/scan/test" } });
                case "/get/status/": return Json(new { state = 1, data = new { status = ++polls == 1 ? 1 : 2 } });
                case "/open/deviceCodeToToken": Assert.Equal(challenge, Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(form["code_verifier"])))); return Json(new { state = 1, data = new { access_token = "a", refresh_token = "r", expires_in = 3600 } });
                default: throw new InvalidOperationException();
            }
        });
        using var client = new Pan115Client(new() { ClientId = "client" }, new HttpClient(handler));
        var auth = await client.BeginAuthorizationAsync("", "csrf");
        Assert.Null(await client.CompleteAuthorizationAsync(auth, null));
        Assert.Equal("a", (await client.CompleteAuthorizationAsync(auth, null))!.AccessToken);
    }
    /// <summary>
    /// 验证网盘下载拒绝不安全的地址。
    /// </summary>
    /// <param name="url">客户端的服务基地址。</param>
    [Theory][InlineData("http://downloads.test/a")][InlineData("https://127.0.0.1/a")][InlineData("https://user:password@downloads.test/a")]
    public void DownloadRejectsUnsafeAddresses(string url) => Assert.Throws<FileStorageException>(() => RemoteDriveTransport.ValidateUrl(url));
    /// <summary>
    /// 创建指定值的 JSON HTTP 响应。
    /// </summary>
    /// <param name="value">需要格式化或作为 SQL 参数的测试值。</param>
    /// <param name="status">模拟 HTTP 响应的状态码。</param>
    /// <returns>构造或分派得到的 HTTP 响应。</returns>
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    /// <summary>
    /// 解析 URL 编码的表单键值。
    /// </summary>
    /// <param name="form">URL 编码的表单正文。</param>
    /// <returns>解析或构造的键值集合。</returns>
    private static Dictionary<string, string> ParseForm(string form) => form.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=', 2))
        .ToDictionary(x => Uri.UnescapeDataString(x[0]), x => Uri.UnescapeDataString(x[1].Replace('+', ' ')));
    /// <summary>
    /// 为DriveProtocolTests测试提供 HTTP 请求处理替身。
    /// </summary>
    private sealed class ApiHandler : HttpMessageHandler
    {
        /// <summary>
        /// 保存模拟 API 处理器的发送委托。
        /// </summary>
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _send;
        /// <summary>
        /// 初始化一个 <see cref="ApiHandler" /> 类型的实例。
        /// </summary>
        /// <param name="send">用于发送 HTTP 请求的委托。</param>
        public ApiHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) { _send = send; }
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { ct.ThrowIfCancellationRequested(); return _send(request); }
    }
    /// <summary>
    /// 为DriveProtocolTests测试提供 HTTP 请求处理替身。
    /// </summary>
    private sealed class Forward : HttpMessageHandler
    {
        /// <summary>
        /// 保存被包装的 HTTP 消息调用器。
        /// </summary>
        private readonly HttpMessageInvoker _inner;
        /// <summary>
        /// 保存请求转发前调用的拦截委托。
        /// </summary>
        private readonly Func<HttpRequestMessage, HttpResponseMessage?>? _before;
        /// <summary>
        /// 指示转发器是否接受未知回调。
        /// </summary>
        private readonly bool _unknownCallback;
        /// <summary>
        /// 初始化一个 <see cref="Forward" /> 类型的实例。
        /// </summary>
        /// <param name="handler">用于处理相应请求或事件的处理器。</param>
        /// <param name="before">发送请求前执行的委托。</param>
        /// <param name="unknownCallback">指示测试是否模拟未知的协议回调。</param>
        public Forward(HttpMessageHandler handler, Func<HttpRequestMessage, HttpResponseMessage?>? before = null, bool unknownCallback = false)
        { _inner = new(handler, false); _before = before; _unknownCallback = unknownCallback; }
        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var injected = _before?.Invoke(request); if (injected != null) return injected;
            var response = await _inner.SendAsync(request, ct);
            if (_unknownCallback && request.Method == HttpMethod.Post && request.RequestUri!.Query.Contains("uploadId"))
            { response.Dispose(); return new HttpResponseMessage(HttpStatusCode.NonAuthoritativeInformation) { Content = new StringContent("callback result unknown") }; }
            return response;
        }
        /// <inheritdoc />
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
