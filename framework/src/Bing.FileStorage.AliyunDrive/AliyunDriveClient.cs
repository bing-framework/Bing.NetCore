using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bing.FileStorage.RemoteDrive;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.FileStorage.AliyunDrive;

/// <summary>
/// 阿里云盘授权与中转存储配置。
/// </summary>
public sealed class AliyunDriveOptions : RemoteDriveOptions
{
    /// <summary>
    /// 获取或设置网盘开放平台应用标识。
    /// </summary>
    /// <remarks>默认值为空字符串。</remarks>
    public string ClientId { get; set; } = "";
    /// <summary>
    /// 获取或设置网盘开放平台应用密钥。
    /// </summary>
    /// <remarks>默认值为空字符串。</remarks>
    public string ClientSecret { get; set; } = "";
    /// <summary>
    /// 获取或设置允许的 OAuth 回调地址。
    /// </summary>
    /// <remarks>生产环境必须显式配置，开发环境可配置回环地址。</remarks>
    public List<string> AllowedRedirectUris { get; set; } = new();
}

/// <summary>
/// 提供阿里云 Drive 文件存储后端的依赖注入扩展。
/// </summary>
public static class AliyunDriveRegistration
{
    /// <summary>
    /// 注册阿里云盘客户端并添加具名远程存储模板。
    /// </summary>
    /// <param name="services">接收远程存储服务的依赖注入集合。</param>
    /// <param name="templateName">此客户端使用的模板名称。</param>
    /// <param name="configure">阿里云盘客户端选项配置委托。</param>
    /// <returns>已注册 Aliyun Drive 模板的原依赖注入服务集合。</returns>
    /// <remarks>
    /// 模板注册失败时释放此方法创建的客户端，再向调用方传播异常。
    /// </remarks>
    public static IServiceCollection AddBingAliyunDriveStorage(this IServiceCollection services, string templateName, Action<AliyunDriveOptions> configure)
    {
        var options = new AliyunDriveOptions(); configure(options);
        var client = new AliyunDriveClient(options);
        try { return services.AddBingRemoteDrive(new RemoteDriveTemplate(templateName, options, client, ownsClient: true)); }
        catch { client.Dispose(); throw; }
    }
}

/// <summary>
/// Alipan 开放平台自有应用客户端。
/// </summary>
/// <remarks>
/// 自有应用调用 Alipan Open API；不依赖第三方令牌中转服务。
/// </remarks>
public sealed partial class AliyunDriveClient : IRemoteDriveClient, IDisposable
{
    /// <summary>
    /// 保存阿里云盘客户端标识和客户端密钥。
    /// </summary>
    private readonly string _clientId, _clientSecret;
    /// <summary>
    /// 保存阿里云盘请求使用的 HTTP 客户端。
    /// </summary>
    private readonly HttpClient _http;
    /// <summary>
    /// 保存阿里云盘请求使用的传输器。
    /// </summary>
    private readonly RemoteDriveTransport _transport;
    /// <summary>
    /// 保存阿里云盘授权允许的回调地址。
    /// </summary>
    private readonly Uri[] _allowedRedirectUris;
    /// <summary>
    /// 定义阿里云盘开放 API 根地址。
    /// </summary>
    private const string Api = "https://openapi.alipan.com";
    /// <summary>
    /// 初始化一个 <see cref="AliyunDriveClient" /> 类型的实例。
    /// </summary>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="apiClient">用于发送 API 请求的 HTTP 客户端。</param>
    /// <param name="transport">实例使用的 HTTP 传输实现。</param>
    public AliyunDriveClient(AliyunDriveOptions options, HttpClient? apiClient = null, RemoteDriveTransport? transport = null)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret)) throw new ArgumentException("必须配置自有阿里云盘应用。");
        _clientId = options.ClientId; _clientSecret = options.ClientSecret;
        _allowedRedirectUris = (options.AllowedRedirectUris ?? new List<string>()).Select(ParseRedirectUri).ToArray();
        _http = apiClient ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
        _transport = transport ?? new RemoteDriveTransport(options.TransportTimeouts);
    }
    /// <inheritdoc />
    public string Provider => "aliyundrive";
    /// <summary>
    /// 向阿里云盘 API 发送请求并映射无法确认结果的传输失败。
    /// </summary>
    /// <param name="path">API 相对路径。</param>
    /// <param name="body">序列化为 JSON 请求体的对象。</param>
    /// <param name="token">可选的访问令牌。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>阿里云盘成功响应 JSON 根元素的独立副本。</returns>
    /// <remarks>
    /// 网络错误、无效 JSON 或非主动取消的操作超时会转换为 OutcomeUnknown 文件存储异常，以保留检查点供后续核验。
    /// </remarks>
    private async Task<JsonElement> Send(string path, object body, string? token, CancellationToken ct)
    {
        try { return await SendCore(path, body, token, ct); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new FileStorageException(FileStorageError.OutcomeUnknown, "远端 API 响应无法确认，保留检查点后重试。"); }
    }
    /// <summary>
    /// 发送 JSON 请求并将阿里云盘响应错误映射为存储错误。
    /// </summary>
    /// <param name="path">API 相对路径。</param>
    /// <param name="body">序列化为 JSON 请求体的对象。</param>
    /// <param name="token">可选的访问令牌。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>阿里云盘成功响应 JSON 根元素的独立副本。</returns>
    /// <remarks>
    /// 主动取消会传播取消异常。
    /// </remarks>
    private async Task<JsonElement> SendCore(string path, object body, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Api + path);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var code = json.RootElement.Text("code");
        if (!response.IsSuccessStatusCode || code.Length != 0)
        {
            var error = code is "NotFound.File" or "FileNotFound" ? FileStorageError.NotFound
                : code is "UploadIdNotFound" or "NoSuchUpload" or "InvalidUploadId" ? FileStorageError.UploadNotFound
                : code is "InvalidPart" or "InvalidPartOrder" or "EntityTooSmall" ? FileStorageError.InvalidPart
                : response.StatusCode == HttpStatusCode.Unauthorized || code is "AccessTokenInvalid" or "AccessTokenExpired" ? FileStorageError.AccessDenied
                : response.StatusCode == HttpStatusCode.Forbidden ? FileStorageError.AccessDenied : FileStorageError.Unavailable;
            throw new FileStorageException(error, "阿里云盘请求未成功，HTTP " + (int)response.StatusCode);
        }
        return json.RootElement.Clone();
    }
    /// <summary>
    /// 使用当前访问令牌调用阿里云盘 API，并在鉴权失败后刷新重试一次。
    /// </summary>
    /// <param name="context">提供连接和访问凭据的远程驱动上下文。</param>
    /// <param name="path">API 相对路径。</param>
    /// <param name="body">序列化为 JSON 请求体的对象。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>阿里云盘成功响应 JSON 根元素的独立副本。</returns>
    /// <remarks>
    /// 仅 AccessDenied 会触发强制刷新凭据并重试；其他失败直接传播。
    /// </remarks>
    private async Task<JsonElement> Call(RemoteDriveContext context, string path, object body, CancellationToken ct)
    {
        var token = await context.Credentials(false, ct);
        try { return await Send(path, body, token.AccessToken, ct); }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.AccessDenied)
        { token = await context.Credentials(true, ct); return await Send(path, body, token.AccessToken, ct); }
    }
    /// <inheritdoc />
    public Task<RemoteDriveAuthorization> BeginAuthorizationAsync(string redirectUri, string state, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var callback = ValidateRedirectUri(redirectUri);
        var normalizedRedirectUri = callback.AbsoluteUri;
        return Task.FromResult(new RemoteDriveAuthorization { State = state, RedirectUri = normalizedRedirectUri,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds(),
            Url = Api + "/oauth/authorize?client_id=" + Uri.EscapeDataString(_clientId) + "&response_type=code&redirect_uri=" + Uri.EscapeDataString(normalizedRedirectUri)
                + "&scope=" + Uri.EscapeDataString("user:base,file:all:read,file:all:write") + "&state=" + Uri.EscapeDataString(state) });
    }
    /// <inheritdoc />
    public async Task<RemoteDriveCredentials?> CompleteAuthorizationAsync(RemoteDriveAuthorization authorization, string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("缺少授权码。");
        var callback = ValidateRedirectUri(authorization.RedirectUri);
        return RemoteDriveJson.Credentials(await Send("/oauth/access_token", new { client_id = _clientId, client_secret = _clientSecret,
            grant_type = "authorization_code", code, redirect_uri = callback.AbsoluteUri }, null, ct));
    }
    /// <inheritdoc />
    public async Task<RemoteDriveCredentials> RefreshAsync(RemoteDriveCredentials credentials, CancellationToken ct = default)
        => RemoteDriveJson.Credentials(await Send("/oauth/access_token", new { client_id = _clientId, client_secret = _clientSecret,
            grant_type = "refresh_token", refresh_token = credentials.RefreshToken }, null, ct));
    /// <inheritdoc />
    public async Task<RemoteDriveAccount> GetAccountAsync(RemoteDriveCredentials credentials, CancellationToken ct = default)
    {
        var result = await Send("/adrive/v1.0/user/getDriveInfo", new { }, credentials.AccessToken, ct);
        var account = new RemoteDriveAccount { AccountId = result.Text("user_id"), DriveId = result.Text("default_drive_id") };
        if (account.AccountId.Length == 0 || account.DriveId.Length == 0) throw new FileStorageException(FileStorageError.AccessDenied, "授权没有可用网盘。");
        return account;
    }
    /// <summary>
    /// 将阿里云盘 JSON 文件项转换为远程文件模型。
    /// </summary>
    /// <param name="json">包含云盘文件字段的 JSON 元素。</param>
    /// <returns>根据 JSON 文件信息映射的远程文件记录。</returns>
    /// <remarks>
    /// 更新时间无法解析时使用 DateTimeOffset.UnixEpoch 作为修改时间。
    /// </remarks>
    private static RemoteDriveFile File(JsonElement json) => new() { Id = json.Text("file_id"), ParentId = json.Text("parent_file_id"),
        Name = json.Text("name"), Length = json.Number("size"), ContentHash = json.Text("content_hash"), Version = json.Text("updated_at"),
        ModifiedAt = DateTimeOffset.TryParse(json.Text("updated_at"), out var date) ? date : DateTimeOffset.UnixEpoch };
    /// <inheritdoc />
    public async Task<RemoteDriveFile?> GetFileAsync(RemoteDriveContext context, string fileId, string parentId, CancellationToken ct = default)
    {
        try
        {
            var result = await Call(context, "/adrive/v1.0/openFile/get", new { drive_id = context.Connection.DriveId, file_id = fileId }, ct);
            if (result.Text("status") != "available") return null;
            var file = File(result);
            if (file.Id != fileId || file.ParentId != parentId) throw new FileStorageException(FileStorageError.Conflict, "网盘文件位置已改变。");
            return file;
        }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.NotFound) { return null; }
    }
    /// <inheritdoc />
    public async Task<RemoteDriveFile?> FindByNameAsync(RemoteDriveContext context, string parentId, string name, CancellationToken ct = default)
        => await FindByName(context, parentId, name, false, ct);
    /// <summary>
    /// 按名称分页查找阿里云盘远程目录中的文件。
    /// </summary>
    /// <param name="context">提供云盘连接和 API 访问的上下文。</param>
    /// <param name="parentId">待搜索目录的远程标识。</param>
    /// <param name="name">要查找的文件名。</param>
    /// <param name="includePending">是否同时匹配尚未完成状态的传输对象。</param>
    /// <param name="ct">分页请求取消令牌。</param>
    /// <returns>符合父目录、名称和状态条件的唯一远程文件；没有匹配项时返回 null。</returns>
    /// <remarks>
    /// 遍历全部分页；无匹配项时返回 null，出现重复同名对象或游标不推进时抛出文件存储异常。
    /// </remarks>
    private async Task<RemoteDriveFile?> FindByName(RemoteDriveContext context, string parentId, string name, bool includePending, CancellationToken ct)
    {
        var markers = new HashSet<string>(); var marker = ""; RemoteDriveFile? found = null;
        do
        {
            if (!markers.Add(marker)) throw new FileStorageException(FileStorageError.Unavailable, "网盘分页游标未推进。");
            var page = await Call(context, "/adrive/v1.0/openFile/list", new { drive_id = context.Connection.DriveId, parent_file_id = parentId, limit = 100, marker }, ct);
            foreach (var item in page.Child("items").EnumerateArray())
                if (item.Text("name") == name && (includePending || item.Text("status") == "available"))
                { if (found != null) throw new FileStorageException(FileStorageError.Conflict, "出现重复远端传输对象。"); found = File(item); }
            marker = page.Text("next_marker");
        } while (marker.Length != 0);
        return found;
    }
    /// <inheritdoc />
    public async Task<RemoteDriveFile> UploadAsync(RemoteDriveContext context, RemoteDriveContent content, RemoteDriveCheckpoint checkpoint,
        Func<RemoteDriveCheckpoint, CancellationToken, Task> persist, CancellationToken ct = default)
    {
        var parent = context.Connection.RootFolderId;
        var completed = checkpoint.FileId == null ? await FindByNameAsync(context, parent, checkpoint.Name, ct)
            : await GetFileAsync(context, checkpoint.FileId, parent, ct);
        if (completed != null) return completed; // 后续仍由中转层读回 SHA-256 验证。
        var count = content.Length == 0 ? 0 : checked((int)((content.Length - 1) / checkpoint.PartSize + 1));
        if (count > 10000) throw new FileStorageException(FileStorageError.CapacityExceeded, "远端分片数超限。");
        if (checkpoint.UploadId != null)
        {
            try { await ReconcilePartsAsync(context, content, checkpoint, persist, ct); }
            catch (FileStorageException ex) when (ex.Error == FileStorageError.UploadNotFound)
            {
                // 查询期间也可能完成上传，先对账成品，再回收真正失效的会话。
                completed = await GetFileAsync(context, checkpoint.FileId!, parent, ct);
                if (completed != null) return completed;
                await ResetUploadAsync(context, checkpoint, persist, ct);
                throw new FileStorageException(FileStorageError.Unavailable, "网盘上传已失效，将从中转重建。");
            }
        }
        if (checkpoint.UploadId == null)
        {
            // 创建响应丢失时，按本传输独占名称回收未完成草稿，然后重新创建。
            var abandoned = await FindByName(context, parent, checkpoint.Name, true, ct);
            if (abandoned != null)
            {
                var ready = await GetFileAsync(context, abandoned.Id, parent, ct);
                if (ready != null) return ready;
                await DeleteAsync(context, abandoned, ct);
            }
            var created = await Call(context, "/adrive/v1.0/openFile/create", new { drive_id = context.Connection.DriveId, parent_file_id = parent,
                name = checkpoint.Name, type = "file", check_name_mode = "refuse", size = content.Length,
                part_info_list = Enumerable.Range(1, count).Select(n => new { part_number = n }).ToArray() }, ct);
            checkpoint.FileId = created.Text("file_id"); checkpoint.UploadId = created.Text("upload_id");
            if (string.IsNullOrEmpty(checkpoint.FileId)) throw new FileStorageException(FileStorageError.OutcomeUnknown, "网盘创建结果缺少文件标识。");
            await persist(checkpoint, ct);
        }
        for (var number = 1; number <= count; number++)
        {
            if (checkpoint.Parts.ContainsKey(number)) continue;
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var urls = await Call(context, "/adrive/v1.0/openFile/getUploadUrl", new { drive_id = context.Connection.DriveId,
                        file_id = checkpoint.FileId, upload_id = checkpoint.UploadId, part_info_list = new[] { new { part_number = number } } }, ct);
                    var url = urls.Child("part_info_list").EnumerateArray().Single(x => x.Number("part_number") == number).Text("upload_url");
                    var offset = (long)(number - 1) * checkpoint.PartSize; var length = Math.Min(checkpoint.PartSize, content.Length - offset);
                    await content.ReadRangeAsync(offset, length, async (stream, token) => checkpoint.Parts[number] = await _transport.PutAsync(url, stream, length, token), ct);
                    await persist(checkpoint, ct); break;
                }
                catch (FileStorageException ex) when (attempt == 0 && ex.Error == FileStorageError.AccessDenied) { /* 地址过期，重新获取后重传本片。 */ }
                catch (FileStorageException ex) when (ex.Error == FileStorageError.UploadNotFound)
                {
                    // 已失效上传不能继续沿用旧回执；下次从持久中转建立新会话。
                    await ResetUploadAsync(context, checkpoint, persist, ct);
                    throw new FileStorageException(FileStorageError.Unavailable, "网盘上传已失效，将从中转重建。");
                }
            }
        }
        var expired = false; var invalidParts = false;
        try { await Call(context, "/adrive/v1.0/openFile/complete", new { drive_id = context.Connection.DriveId, file_id = checkpoint.FileId, upload_id = checkpoint.UploadId }, ct); }
        catch (FileStorageException ex) when (ex.Error is FileStorageError.UploadNotFound or FileStorageError.InvalidPart or FileStorageError.Unavailable or FileStorageError.OutcomeUnknown)
        { expired = ex.Error == FileStorageError.UploadNotFound; invalidParts = ex.Error == FileStorageError.InvalidPart; }
        var result = await GetFileAsync(context, checkpoint.FileId!, parent, ct);
        if (result != null) return result;
        if (expired) await ResetUploadAsync(context, checkpoint, persist, ct);
        if (invalidParts)
        {
            // 下一次执行先分页对账，只重传远端缺失或不匹配的片；不丢弃有效大文件分片。
            throw new FileStorageException(FileStorageError.Unavailable, "网盘远端分片异常，将查询清单后补传。");
        }
        throw new FileStorageException(FileStorageError.OutcomeUnknown, "网盘尚未确认上传完成。");
    }
    /// <summary>
    /// 解析并验证阿里云盘 OAuth 回调地址。
    /// </summary>
    /// <param name="value">待解析的回调 URI 文本。</param>
    /// <returns>通过 HTTPS 或环回 HTTP、用户信息及片段校验的重定向 URI。</returns>
    /// <remarks>
    /// 仅接受 HTTPS 或 HTTP 回环地址，并拒绝用户信息及片段。
    /// </remarks>
    private static Uri ParseRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("OAuth 回调必须使用 HTTPS、回环地址且不能包含用户信息或片段。", nameof(value));
        return uri;
    }
    /// <summary>
    /// 将 OAuth 回调地址与配置的白名单逐项校验。
    /// </summary>
    /// <param name="value">待校验的回调 URI 文本。</param>
    /// <returns>已通过允许列表匹配的 OAuth 回调 URI。</returns>
    /// <remarks>
    /// 必须配置非空回调白名单；回调不匹配或缺少配置时抛出异常。
    /// </remarks>
    private Uri ValidateRedirectUri(string value)
    {
        if (_allowedRedirectUris.Length == 0)
            throw new FileStorageException(FileStorageError.Configuration, "必须配置阿里云盘 OAuth 回调白名单。");
        var callback = ParseRedirectUri(value);
        if (!EquivalentRedirectUri(callback)) throw new ArgumentException("OAuth 回调地址未登记。", nameof(value));
        return callback;
    }
    /// <summary>
    /// 判断候选 OAuth 回调 URI 是否与白名单项完全匹配。
    /// </summary>
    /// <param name="candidate">已解析并验证的回调 URI。</param>
    /// <returns>候选 URI 与任一已登记 URI 的协议、主机、端口、路径和查询一致时返回 true，否则返回 false。</returns>
    /// <remarks>
    /// 比较协议、主机、端口、绝对路径和查询字符串；存在完全匹配项时返回 true，否则返回 false。
    /// </remarks>
    private bool EquivalentRedirectUri(Uri candidate)
        => _allowedRedirectUris.Any(allowed => allowed.Scheme.Equals(candidate.Scheme, StringComparison.OrdinalIgnoreCase)
            && allowed.Host.Equals(candidate.Host, StringComparison.OrdinalIgnoreCase) && allowed.Port == candidate.Port
            && allowed.AbsolutePath == candidate.AbsolutePath && allowed.Query == candidate.Query);
    /// <inheritdoc />
    public async Task<RemoteDriveDownload> DownloadAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default)
    {
        var result = await Call(context, "/adrive/v1.0/openFile/getDownloadUrl", new { drive_id = context.Connection.DriveId, file_id = file.Id, expire_sec = 300 }, ct);
        return new RemoteDriveDownload { Url = result.Text("url") };
    }
    /// <inheritdoc />
    public async Task DeleteAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default)
    {
        try { await Call(context, "/adrive/v1.0/openFile/recyclebin/trash", new { drive_id = context.Connection.DriveId, file_id = file.Id }, ct); }
        catch (FileStorageException ex) when (ex.Error == FileStorageError.NotFound) { }
    }
    /// <inheritdoc />
    public Task AbortAsync(RemoteDriveContext context, RemoteDriveCheckpoint checkpoint, CancellationToken ct = default)
        => checkpoint.FileId == null ? Task.CompletedTask : DeleteAsync(context, new RemoteDriveFile { Id = checkpoint.FileId }, ct);
    /// <inheritdoc />
    public void Dispose() { _http.Dispose(); _transport.Dispose(); }
}
