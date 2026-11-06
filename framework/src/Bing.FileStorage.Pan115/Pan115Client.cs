using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bing.FileStorage.RemoteDrive;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage.Pan115;

/// <summary>
/// 115 网盘客户端与中转存储配置。
/// </summary>
public sealed class Pan115Options : RemoteDriveOptions
{
    /// <summary>
    /// 初始化一个 <see cref="Pan115Options" /> 类型的实例。
    /// </summary>
    public Pan115Options() { RootFolderId = "0"; }
    /// <summary>
    /// 获取或设置网盘开放平台应用标识。
    /// </summary>
    /// <remarks>默认值为空字符串。</remarks>
    public string ClientId { get; set; } = "";
    /// <summary>
    /// 获取或设置网盘请求使用的 User-Agent。
    /// </summary>
    /// <remarks>默认值为 <c>&quot;Bing.FileStorage.Pan115/1.0&quot;</c>。</remarks>
    public string UserAgent { get; set; } = "Bing.FileStorage.Pan115/1.0";
}

/// <summary>
/// 提供 Pan115 文件存储后端的依赖注入扩展。
/// </summary>
public static class Pan115Registration
{
    /// <summary>
    /// 注册 Pan115 远程网盘存储模板。
    /// </summary>
    /// <param name="services">要添加远程网盘服务的依赖注入服务集合。</param>
    /// <param name="templateName">远程网盘模板名称。</param>
    /// <param name="configure">配置 Pan115 选项的委托。</param>
    /// <returns>原依赖注入服务集合。</returns>
    /// <remarks>
    /// 注册失败时释放本方法创建的客户端。
    /// </remarks>
    public static IServiceCollection AddBingPan115Storage(this IServiceCollection services, string templateName, Action<Pan115Options> configure)
    {
        var options = new Pan115Options(); configure(options);
        var client = new Pan115Client(options);
        try { return services.AddBingRemoteDrive(new RemoteDriveTemplate(templateName, options, client, ownsClient: true)); }
        catch { client.Dispose(); throw; }
    }
}

/// <summary>
/// 115 开放平台自有应用客户端。
/// </summary>
/// <remarks>
/// 115 自有应用协议；全文件/前缀/挑战摘要都从持久中转实际内容计算。
/// </remarks>
public sealed partial class Pan115Client : IRemoteDriveClient, IDisposable
{
    /// <summary>
    /// 定义 115 网盘开放 API 根地址。
    /// </summary>
    private const string Api = "https://proapi.115.com";
    /// <summary>
    /// 定义 115 网盘通行证 API 根地址。
    /// </summary>
    private const string Passport = "https://passportapi.115.com";
    /// <summary>
    /// 保存 115 网盘客户端标识和用户代理。
    /// </summary>
    private readonly string _clientId, _userAgent;
    /// <summary>
    /// 保存 115 网盘请求使用的 HTTP 客户端。
    /// </summary>
    private readonly HttpClient _http;
    /// <summary>
    /// 保存可选的对象存储客户端工厂，用于分片上传。
    /// </summary>
    private readonly Func<AlibabaCloud.OSS.V2.Configuration, AlibabaCloud.OSS.V2.Client>? _ossClientFactory;
    /// <summary>
    /// 初始化一个 <see cref="Pan115Client" /> 类型的实例。
    /// </summary>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="apiClient">用于发送 API 请求的 HTTP 客户端。</param>
    /// <param name="ossClientFactory">用于根据阿里云 OSS 配置创建客户端的工厂。</param>
    public Pan115Client(Pan115Options options, HttpClient? apiClient = null,
        Func<AlibabaCloud.OSS.V2.Configuration, AlibabaCloud.OSS.V2.Client>? ossClientFactory = null)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.UserAgent) || options.UserAgent.Any(char.IsControl))
            throw new ArgumentException("必须配置自有 115 应用与有效 User-Agent。");
        _clientId = options.ClientId; _userAgent = options.UserAgent;
        _ossClientFactory = ossClientFactory;
        _http = apiClient ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    }
    /// <inheritdoc />
    public string Provider => "pan115";
    /// <summary>
    /// 根据键值对创建请求表单字典。
    /// </summary>
    /// <param name="values">请求表单键值对。</param>
    /// <returns>包含指定键值对的字典。</returns>
    private static Dictionary<string, string> Form(params (string Key, string Value)[] values) => values.ToDictionary(x => x.Key, x => x.Value);
    /// <summary>
    /// 发送 Pan115 API 请求并映射传输不确定性。
    /// </summary>
    /// <param name="url">API 请求地址。</param>
    /// <param name="get">是否发送 GET 请求。</param>
    /// <param name="form">请求表单。</param>
    /// <param name="token">可选的访问令牌。</param>
    /// <param name="ct">用于取消请求的令牌。</param>
    /// <returns>解析后的 JSON 响应元素。</returns>
    /// <remarks>
    /// 网络异常、JSON 解析异常或非调用方取消导致的取消会映射为结果未知错误。
    /// </remarks>
    private async Task<JsonElement> Send(string url, bool get, Dictionary<string, string> form, string? token, CancellationToken ct)
    {
        try { return await SendCore(url, get, form, token, ct); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new FileStorageException(FileStorageError.OutcomeUnknown, "远端 API 响应无法确认，保留检查点后重试。"); }
    }
    /// <summary>
    /// 发送 GET 或 POST 请求并验证 Pan115 API 响应。
    /// </summary>
    /// <param name="url">API 请求地址。</param>
    /// <param name="get">是否发送 GET 请求。</param>
    /// <param name="form">请求表单。</param>
    /// <param name="token">可选的访问令牌。</param>
    /// <param name="ct">用于取消请求的令牌。</param>
    /// <returns>克隆后的 JSON 响应根元素。</returns>
    /// <remarks>
    /// GET 请求将表单写入查询字符串；POST 请求使用表单正文。HTTP 失败或响应 state 不表示成功时映射为访问拒绝或服务不可用。
    /// </remarks>
    private async Task<JsonElement> SendCore(string url, bool get, Dictionary<string, string> form, string? token, CancellationToken ct)
    {
        if (get && form.Count != 0) url += "?" + string.Join("&", form.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
        using var request = new HttpRequestMessage(get ? HttpMethod.Get : HttpMethod.Post, url);
        if (!get) request.Content = new FormUrlEncodedContent(form);
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = json.RootElement;
        if (!response.IsSuccessStatusCode || root.Text("state") is not ("True" or "true" or "1"))
        {
            var code = root.Number("code"); if (code == 0) code = root.Number("errno");
            var error = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized || code == 99 || code.ToString().StartsWith("401", StringComparison.Ordinal)
                ? FileStorageError.AccessDenied : FileStorageError.Unavailable;
            throw new FileStorageException(error, "115 请求未成功，HTTP " + (int)response.StatusCode);
        }
        return root.Clone();
    }
    /// <summary>
    /// 使用当前或刷新后的凭据调用 Pan115 API。
    /// </summary>
    /// <param name="context">提供远程存储连接和凭据的上下文。</param>
    /// <param name="path">API 路径。</param>
    /// <param name="get">是否发送 GET 请求。</param>
    /// <param name="form">请求表单。</param>
    /// <param name="ct">用于取消凭据获取和 API 请求的令牌。</param>
    /// <returns>Pan115 API 的 JSON 响应根元素。</returns>
    /// <remarks>
    /// 首次请求被判定为访问拒绝时刷新凭据并重试一次。
    /// </remarks>
    private async Task<JsonElement> Call(RemoteDriveContext context, string path, bool get, Dictionary<string, string> form, CancellationToken ct)
    {
        var token = await context.Credentials(false, ct);
        try { return await Send(Api + path, get, form, token.AccessToken, ct); }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.AccessDenied)
        { token = await context.Credentials(true, ct); return await Send(Api + path, get, form, token.AccessToken, ct); }
    }
    /// <inheritdoc />
    public async Task<RemoteDriveAuthorization> BeginAuthorizationAsync(string redirectUri, string state, CancellationToken ct = default)
    {
        var verifier = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(verifier)));
        var response = (await Send(Passport + "/open/authDeviceCode", false,
            Form(("client_id", _clientId), ("code_challenge", challenge), ("code_challenge_method", "sha256")), null, ct)).Child("data");
        if (response.Text("uid").Length == 0) throw new FileStorageException(FileStorageError.OutcomeUnknown, "115 未返回授权设备标识。");
        return new RemoteDriveAuthorization { State = state, Verifier = verifier, DeviceId = response.Text("uid"), DeviceTime = response.Text("time"),
            DeviceSign = response.Text("sign"), Url = response.Text("qrcode"), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds() };
    }
    /// <inheritdoc />
    public async Task<RemoteDriveCredentials?> CompleteAuthorizationAsync(RemoteDriveAuthorization authorization, string? code, CancellationToken ct = default)
    {
        var response = (await Send("https://qrcodeapi.115.com/get/status/", true,
            Form(("uid", authorization.DeviceId), ("time", authorization.DeviceTime), ("sign", authorization.DeviceSign)), null, ct)).Child("data");
        var status = response.Number("status");
        if (status is 0 or 1) return null;
        if (status != 2) throw new FileStorageException(FileStorageError.AccessDenied, "115 扫码授权未确认或已过期。");
        return RemoteDriveJson.Credentials((await Send(Passport + "/open/deviceCodeToToken", false,
            Form(("uid", authorization.DeviceId), ("code_verifier", authorization.Verifier)), null, ct)).Child("data"));
    }
    /// <inheritdoc />
    public async Task<RemoteDriveCredentials> RefreshAsync(RemoteDriveCredentials credentials, CancellationToken ct = default)
        => RemoteDriveJson.Credentials((await Send(Passport + "/open/refreshToken", false, Form(("refresh_token", credentials.RefreshToken)), null, ct)).Child("data"));
    /// <inheritdoc />
    public async Task<RemoteDriveAccount> GetAccountAsync(RemoteDriveCredentials credentials, CancellationToken ct = default)
    {
        var result = (await Send(Api + "/open/user/info", true, Form(), credentials.AccessToken, ct)).Child("data");
        if (result.Text("user_id").Length == 0) throw new FileStorageException(FileStorageError.AccessDenied, "115 未返回账号身份。");
        return new RemoteDriveAccount { AccountId = result.Text("user_id"), DriveId = result.Text("user_id") };
    }
    /// <inheritdoc />
    public async Task<RemoteDriveFile?> GetFileAsync(RemoteDriveContext context, string fileId, string parentId, CancellationToken ct = default)
    {
        // 使用父目录分页同时确认范围与完成标志，避免把未上传完成的对象发布为附件。
        return await Find(context, parentId, file => file.Id == fileId, ct);
    }
    /// <inheritdoc />
    public Task<RemoteDriveFile?> FindByNameAsync(RemoteDriveContext context, string parentId, string name, CancellationToken ct = default)
        => Find(context, parentId, file => file.Name == name, ct);
    /// <summary>
    /// 分页查找父目录中唯一匹配的远程文件。
    /// </summary>
    /// <param name="context">提供连接凭据和作用域的上下文。</param>
    /// <param name="parentId">待搜索的父目录标识。</param>
    /// <param name="match">判断远程文件是否匹配的谓词。</param>
    /// <param name="ct">用于取消请求的令牌。</param>
    /// <returns>唯一匹配的远程文件；没有匹配项时为 null。</returns>
    /// <remarks>
    /// 重复文件标识导致分页结果无效；存在多个匹配文件时抛出冲突错误。
    /// </remarks>
    private async Task<RemoteDriveFile?> Find(RemoteDriveContext context, string parentId, Func<RemoteDriveFile, bool> match, CancellationToken ct)
    {
        long offset = 0; RemoteDriveFile? found = null; var seen = new HashSet<string>();
        while (true)
        {
            var page = await Call(context, "/open/ufile/files", true, Form(("cid", parentId), ("limit", "100"), ("offset", offset.ToString()),
                ("asc", "1"), ("o", "file_name"), ("show_dir", "0")), ct);
            var items = page.Child("data").EnumerateArray().ToArray();
            if (items.Length == 0) return found;
            foreach (var item in items)
            {
                if (!seen.Add(item.Text("fid"))) throw new FileStorageException(FileStorageError.Unavailable, "115 分页内容重复，请重新查询。");
                if (item.Text("fc") != "1" || item.Text("fta") != "1" || item.Text("pid") != parentId) continue;
                var file = new RemoteDriveFile { Id = item.Text("fid"), ParentId = item.Text("pid"), Name = item.Text("fn"), PickCode = item.Text("pc"),
                    ContentHash = item.Text("sha1"), Length = item.Number("fs"), Version = item.Text("upt"), ModifiedAt = DateTimeOffset.FromUnixTimeSeconds(item.Number("upt")) };
                if (match(file)) { if (found != null) throw new FileStorageException(FileStorageError.Conflict, "115 存在重复传输对象。"); found = file; }
            }
            offset = checked(offset + items.Length);
            if (offset >= page.Number("count")) return found;
        }
    }
    /// <inheritdoc />
    public async Task<RemoteDriveDownload> DownloadAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default)
    {
        var data = (await Call(context, "/open/ufile/downurl", false, Form(("pick_code", file.PickCode ?? "")), ct)).Child("data");
        var item = data.Child(file.Id);
        return new RemoteDriveDownload { Url = item.Child("url").Text("url"), Headers = new Dictionary<string, string> { ["User-Agent"] = _userAgent } };
    }
    /// <inheritdoc />
    public async Task DeleteAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default)
    {
        if (await GetFileAsync(context, file.Id, file.ParentId, ct) == null) return;
        await Call(context, "/open/ufile/delete", false, Form(("file_ids", file.Id), ("parent_id", file.ParentId)), ct);
    }
    /// <summary>
    /// 计算远程内容指定范围的 SHA-1 值。
    /// </summary>
    /// <param name="content">提供范围读取能力的远程内容。</param>
    /// <param name="offset">参与计算的起始偏移量。</param>
    /// <param name="length">参与计算的字节数。</param>
    /// <param name="ct">用于取消读取和哈希计算的令牌。</param>
    /// <returns>范围内容的十六进制 SHA-1 值。</returns>
    private static async Task<string> Sha1(RemoteDriveContent content, long offset, long length, CancellationToken ct)
    {
        string? result = null;
        await content.ReadRangeAsync(offset, length, async (stream, token) =>
        { using var hash = SHA1.Create(); result = Convert.ToHexString(await hash.ComputeHashAsync(stream, token)); }, ct);
        return result!;
    }
    /// <inheritdoc />
    public async Task<RemoteDriveFile> UploadAsync(RemoteDriveContext context, RemoteDriveContent content, RemoteDriveCheckpoint checkpoint,
        Func<RemoteDriveCheckpoint, CancellationToken, Task> persist, CancellationToken ct = default)
    {
        var existing = await FindByNameAsync(context, context.Connection.RootFolderId, checkpoint.Name, ct);
        if (existing != null) return existing;
        if (checkpoint.Sha1 == null)
        {
            checkpoint.Sha1 = await Sha1(content, 0, content.Length, ct);
            checkpoint.PrefixSha1 = await Sha1(content, 0, Math.Min(content.Length, 128 * 1024), ct);
            await persist(checkpoint, ct);
        }
        if (checkpoint.PickCode == null)
        {
            var form = Form(("file_name", checkpoint.Name), ("file_size", content.Length.ToString()), ("target", "U_1_" + context.Connection.RootFolderId),
                ("fileid", checkpoint.Sha1), ("preid", checkpoint.PrefixSha1!), ("topupload", "0"));
            for (var proofAttempt = 0; proofAttempt < 4; proofAttempt++)
            {
                var init = (await Call(context, "/open/upload/init", false, form, ct)).Child("data");
                var status = init.Number("status");
                if (status is 6 or 7 or 8)
                {
                    var range = init.Text("sign_check").Split('-');
                    if (range.Length != 2 || !long.TryParse(range[0], out var start) || !long.TryParse(range[1], out var end)
                        || start < 0 || end < start || end >= content.Length) throw new FileStorageException(FileStorageError.InvalidPart, "115 内容证明范围无效。");
                    form["sign_key"] = init.Text("sign_key"); form["sign_val"] = await Sha1(content, start, end - start + 1, ct); continue;
                }
                checkpoint.PickCode = init.Text("pick_code"); checkpoint.FileId = init.Text("file_id");
                if (status == 2)
                { await persist(checkpoint, ct); return await Confirm(context, checkpoint, ct); }
                ApplyUpload(init, checkpoint); await persist(checkpoint, ct); break;
            }
        }
        else
        {
            var resume = (await Call(context, "/open/upload/resume", false, Form(("file_size", content.Length.ToString()),
                ("target", "U_1_" + context.Connection.RootFolderId), ("fileid", checkpoint.Sha1), ("pick_code", checkpoint.PickCode)), ct)).Child("data");
            var previous = checkpoint.ObjectKey; ApplyUpload(resume, checkpoint);
            if (checkpoint.ObjectKey != previous) { checkpoint.UploadId = null; checkpoint.Parts.Clear(); }
            await persist(checkpoint, ct);
        }
        if (string.IsNullOrEmpty(checkpoint.Bucket) || string.IsNullOrEmpty(checkpoint.ObjectKey)) throw new FileStorageException(FileStorageError.Unavailable, "115 上传握手未完成。");
        try { await UploadOss(context, content, checkpoint, persist, ct); }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.OutcomeUnknown)
        { /* OSS 完成并不代表网盘登记成功，统一以网盘侧结果对账。 */ }
        return await Confirm(context, checkpoint, ct);
    }
    /// <summary>
    /// 将上传响应中的对象及回调信息写入检查点。
    /// </summary>
    /// <param name="response">Pan115 上传响应。</param>
    /// <param name="checkpoint">接收上传信息的远程存储检查点。</param>
    private static void ApplyUpload(JsonElement response, RemoteDriveCheckpoint checkpoint)
    {
        checkpoint.Bucket = response.Text("bucket"); checkpoint.ObjectKey = response.Text("object");
        checkpoint.Callback = response.Child("callback").Text("callback"); checkpoint.CallbackVariables = response.Child("callback").Text("callback_var");
    }
    /// <summary>
    /// 按检查点名称查找已完成的远程文件。
    /// </summary>
    /// <param name="context">提供连接凭据和作用域的上下文。</param>
    /// <param name="checkpoint">包含目标文件名的上传检查点。</param>
    /// <param name="ct">用于取消远程查询的令牌。</param>
    /// <returns>找到的远程文件。</returns>
    /// <remarks>
    /// 没有找到对应文件时以结果未知错误结束。
    /// </remarks>
    private async Task<RemoteDriveFile> Confirm(RemoteDriveContext context, RemoteDriveCheckpoint checkpoint, CancellationToken ct)
        => await FindByNameAsync(context, context.Connection.RootFolderId, checkpoint.Name, ct)
            ?? throw new FileStorageException(FileStorageError.OutcomeUnknown, "115 网盘登记尚未确认，保留中转与检查点继续对账。");
    /// <inheritdoc />
    public async Task AbortAsync(RemoteDriveContext context, RemoteDriveCheckpoint checkpoint, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(checkpoint.UploadId)) await AbortOss(context, checkpoint, ct);
        if (checkpoint.Name.Length > 0)
        {
            var file = await FindByNameAsync(context, context.Connection.RootFolderId, checkpoint.Name, ct);
            if (file != null) await DeleteAsync(context, file, ct);
        }
    }
    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
