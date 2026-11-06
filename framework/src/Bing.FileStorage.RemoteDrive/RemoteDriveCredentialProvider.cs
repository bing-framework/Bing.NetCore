using System.Text.Json;

namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 管理远程盘凭据刷新与轮换的提供程序。
/// </summary>
/// <remarks>
/// 刷新采用数据库租约与版本检查；轮换结果未知时要求重新授权，不重放旧刷新令牌。
/// </remarks>
public sealed class RemoteDriveCredentialProvider : IRemoteDriveCredentialProvider
{
    /// <summary>
    /// 保存远程盘凭据状态存储。
    /// </summary>
    private readonly IRemoteDriveStateStore _store;
    /// <summary>
    /// 保存远程盘凭据保护器。
    /// </summary>
    private readonly IRemoteDriveCredentialProtector _protector;
    /// <summary>
    /// 保存按模板键索引的远程盘模板。
    /// </summary>
    private readonly IReadOnlyDictionary<string, RemoteDriveTemplate> _templates;
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveCredentialProvider" /> 类型的实例。
    /// </summary>
    /// <param name="store">用于读取或保存实例数据的存储实现。</param>
    /// <param name="protector">用于保护和还原敏感数据的保护器。</param>
    /// <param name="templates">用于配置远端存储连接的模板集合。</param>
    public RemoteDriveCredentialProvider(IRemoteDriveStateStore store, IRemoteDriveCredentialProtector protector, IEnumerable<RemoteDriveTemplate> templates)
    { _store = store; _protector = protector; _templates = templates.ToDictionary(x => x.Name); }
    /// <inheritdoc />
    public async Task<RemoteDriveCredentials> GetAsync(Guid connectionId, bool refresh = false, CancellationToken ct = default)
    {
        for (var retry = 0; retry < 40; retry++)
        {
            var c = await _store.FindConnectionAsync(connectionId, ct) ?? throw Denied();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (c.State == RemoteDriveConnectionState.ReauthorizationRequired && c.LeaseUntil > now)
            { await Task.Delay(250, ct); refresh = false; continue; }
            if (c.State != RemoteDriveConnectionState.Active || !_templates.TryGetValue(c.TemplateName, out var template)) throw Denied();
            var credentials = JsonSerializer.Deserialize<RemoteDriveCredentials>(_protector.Unprotect(c.Id, c.EncryptedCredentials)) ?? throw Denied();
            if (!refresh && credentials.ExpiresAt > now + 120000) return credentials;
            if (c.LeaseUntil > now) { await Task.Delay(250, ct); refresh = false; continue; }
            var token = Guid.NewGuid().ToString("N");
            if (!await _store.TryClaimConnectionAsync(c.Id, c.Version, token, now + 60000, ct)) continue;
            c = await _store.FindConnectionAsync(c.Id, ct) ?? throw Denied();
            // 在发出轮换请求前持久化不确定状态。进程崩溃后禁止重放一次性刷新令牌。
            c.State = RemoteDriveConnectionState.ReauthorizationRequired;
            if (!await _store.TryUpdateConnectionAsync(c, c.Version, token, ct)) throw Denied();
            var next = await template.Client.RefreshAsync(credentials, ct);
            if (string.IsNullOrEmpty(next.AccessToken) || string.IsNullOrEmpty(next.RefreshToken) || next.ExpiresAt <= now) throw Denied();
            c.EncryptedCredentials = _protector.Protect(c.Id, JsonSerializer.Serialize(next)); c.CredentialExpiresAt = next.ExpiresAt;
            c.State = RemoteDriveConnectionState.Active; c.LeaseToken = null; c.LeaseUntil = 0;
            if (!await _store.TryUpdateConnectionAsync(c, c.Version, token, ct)) throw Denied();
            return next;
        }
        throw new FileStorageException(FileStorageError.Unavailable, "连接凭据正在刷新，请稍后重试。");
    }
    /// <summary>
    /// 创建表示远程存储连接未授权的异常。
    /// </summary>
    /// <returns>访问拒绝错误对应的文件存储异常。</returns>
    private static FileStorageException Denied() => new(FileStorageError.AccessDenied, "网盘连接不可用或需要重新授权。");
}
