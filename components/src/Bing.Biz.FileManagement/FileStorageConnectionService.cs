using System.Security.Cryptography;
using System.Text.Json;
using Bing.FileStorage;
using Bing.FileStorage.RemoteDrive;
using Bing.MultiTenancy;
using Bing.Users;

namespace Bing.Biz.FileManagement;

public sealed class RemoteDriveConnectionView
{
    public Guid Id { get; set; }
    public string Template { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public RemoteDriveOwnership Ownership { get; set; }
    public RemoteDriveConnectionState State { get; set; }
    public string? AuthorizationUrl { get; set; }
    public string? AuthorizationState { get; set; }
    public string? AuthorizationQrPng { get; set; }
}

/// <summary>连接授权只向所有者返回授权地址，从不向管理接口返回凭据。</summary>
public sealed class FileStorageConnectionService
{
    private readonly IRemoteDriveStateStore _store;
    private readonly IFileStorageTargetStore _targets;
    private readonly IRemoteDriveCredentialProtector _protector;
    private readonly ICurrentUser _user;
    private readonly ICurrentTenant _tenant;
    private readonly IFilePermissionChecker _permissions;
    private readonly IFileUploadStoreSelector _selector;
    private readonly RemoteDriveTemplate[] _templates;
    public FileStorageConnectionService(IRemoteDriveStateStore store, IFileStorageTargetStore targets, IRemoteDriveCredentialProtector protector,
        ICurrentUser user, ICurrentTenant tenant, IFilePermissionChecker permissions, IFileUploadStoreSelector selector, IEnumerable<RemoteDriveTemplate> templates)
    { _store = store; _targets = targets; _protector = protector; _user = user; _tenant = tenant; _permissions = permissions; _selector = selector; _templates = templates.ToArray(); }
    private async Task<StorageScope> Scope(CancellationToken ct)
    {
        if (!_user.IsAuthenticated || string.IsNullOrEmpty(_user.UserId)) throw new FileManagementException("unauthenticated", "需要登录。");
        if (_tenant.IsAvailable && !string.IsNullOrEmpty(_tenant.Id)) return StorageScope.ForTenant(_tenant.Id);
        await Permission("files.host", ct); return StorageScope.Host;
    }
    private async Task Permission(string permission, CancellationToken ct)
    { if (!await _permissions.IsGrantedAsync(permission, ct)) throw new FileManagementException("forbidden", "缺少连接操作权限。"); }
    private RemoteDriveTemplate Template(string name) => _templates.SingleOrDefault(t => t.Name == name) ?? throw new FileManagementException("invalid", "未配置的网盘模板。");
    private async Task<RemoteDriveConnection> Connection(Guid id, bool manageDisable, CancellationToken ct)
    {
        var scope = await Scope(ct); var c = await _store.FindConnectionAsync(id, ct);
        if (c == null || c.ScopeKey != scope.Key) throw new FileManagementException("not_found", "连接不存在。");
        if (c.Ownership == RemoteDriveOwnership.Shared || manageDisable && c.OwnerId != _user.UserId) await Permission("files.connections.manage", ct);
        else if (c.OwnerId != _user.UserId) throw new FileManagementException("forbidden", "个人连接仅供所有者操作。");
        return c;
    }
    private static RemoteDriveConnectionView View(RemoteDriveConnection c) => new() { Id = c.Id, Template = c.TemplateName, DisplayName = c.DisplayName, Ownership = c.Ownership, State = c.State };
    public async Task<IReadOnlyList<RemoteDriveConnectionView>> ListAsync(bool manage = false, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var scope = await Scope(ct); if (manage) await Permission("files.connections.manage", ct);
        return (await _store.ListConnectionsAsync(scope.Key, manage ? null : _user.UserId, true, Math.Max(0, skip), Math.Clamp(take, 1, 100), ct)).Select(View).ToArray();
    }
    public async Task<RemoteDriveConnectionView> StartAsync(string templateName, string displayName, RemoteDriveOwnership ownership,
        string redirectUri, Guid? reconnectId = null, CancellationToken ct = default)
    {
        var scope = await Scope(ct); await Permission("files.upload", ct);
        if (!Enum.IsDefined(typeof(RemoteDriveOwnership), ownership) || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100 || displayName.Any(char.IsControl))
            throw new FileManagementException("invalid", "连接名称或归属无效。");
        if (ownership == RemoteDriveOwnership.Shared) await Permission("files.connections.manage", ct);
        var template = Template(templateName);
        var c = reconnectId.HasValue ? await Connection(reconnectId.Value, false, ct) : new RemoteDriveConnection { Id = Guid.NewGuid(),
            TemplateName = template.Name, Provider = template.Client.Provider, ScopeKind = scope.Kind, TenantId = scope.TenantId, ScopeKey = scope.Key,
            Ownership = ownership, OwnerId = _user.UserId, DisplayName = displayName, RootFolderId = template.RootFolderId, CreatedAt = FileManagementService.Now };
        if (c.TemplateName != templateName || c.Ownership != ownership) throw new FileManagementException("invalid", "不能更换已有连接的模板或归属。");
        if (c.LeaseUntil > FileManagementService.Now) throw new FileManagementException("conflict", "连接正在授权或刷新。");
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var auth = await template.Client.BeginAuthorizationAsync(redirectUri, state, ct);
        c.StoreName = template.StoreName(c.Id); c.EncryptedAuthorization = _protector.Protect(c.Id, JsonSerializer.Serialize(auth));
        c.AuthorizationExpiresAt = auth.ExpiresAt; c.State = RemoteDriveConnectionState.Authorizing;
        c.LeaseToken = null; c.LeaseUntil = 0;
        var saved = reconnectId.HasValue ? await _store.TryUpdateConnectionAsync(c, c.Version, ct: ct) : await _store.TryCreateConnectionAsync(c, ct);
        if (!saved) throw new FileManagementException("conflict", "连接授权竞争，请重试。");
        var view = View(c); view.AuthorizationUrl = auth.Url; view.AuthorizationState = state; return view;
    }
    public async Task<RemoteDriveConnectionView> CompleteAsync(Guid id, string state, string? code, CancellationToken ct = default)
    {
        var c = await Connection(id, false, ct);
        if (c.State != RemoteDriveConnectionState.Authorizing || c.AuthorizationExpiresAt <= FileManagementService.Now)
            throw new FileManagementException("conflict", "授权已过期，请重新发起。");
        var auth = JsonSerializer.Deserialize<RemoteDriveAuthorization>(_protector.Unprotect(c.Id, c.EncryptedAuthorization))!;
        if (state != auth.State) throw new FileManagementException("forbidden", "授权状态不匹配。");
        var lease = Guid.NewGuid().ToString("N");
        if (!await _store.TryClaimConnectionAsync(c.Id, c.Version, lease, FileManagementService.Now + 60000, ct))
            throw new FileManagementException("conflict", "连接授权正在处理。");
        c = (await _store.FindConnectionAsync(c.Id, ct))!;
        var credentials = await Template(c.TemplateName).Client.CompleteAuthorizationAsync(auth, code, ct);
        if (credentials == null)
        {
            c.LeaseToken = null; c.LeaseUntil = 0;
            if (!await _store.TryUpdateConnectionAsync(c, c.Version, lease, ct)) throw new FileManagementException("conflict", "授权状态变化。");
            return View(c);
        }
        var account = await Template(c.TemplateName).Client.GetAccountAsync(credentials, ct);
        if (c.RemoteAccountId.Length != 0 && (c.RemoteAccountId != account.AccountId || c.DriveId != account.DriveId))
            throw new FileManagementException("account_mismatch", "重新授权必须使用原账号与原网盘，换账号请创建新连接。");
        c.RemoteAccountId = account.AccountId; c.DriveId = account.DriveId;
        c.EncryptedCredentials = _protector.Protect(c.Id, JsonSerializer.Serialize(credentials)); c.CredentialExpiresAt = credentials.ExpiresAt;
        c.EncryptedAuthorization = ""; c.State = RemoteDriveConnectionState.Active; c.LeaseToken = null; c.LeaseUntil = 0;
        if (!await _store.TryUpdateConnectionAsync(c, c.Version, lease, ct)) throw new FileManagementException("conflict", "授权结果无法保存，请重新授权。");
        return View(c);
    }
    public async Task DisableAsync(Guid id, CancellationToken ct = default)
    {
        var c = await Connection(id, true, ct); c.State = RemoteDriveConnectionState.Disabled;
        c.LeaseToken = null; c.LeaseUntil = 0; c.EncryptedAuthorization = "";
        if (!await _store.TryUpdateConnectionAsync(c, c.Version, ct: ct)) throw new FileManagementException("conflict", "连接状态变化。");
    }
    public async Task SetPreferenceAsync(string container, string targetId, bool scopeDefault = false, CancellationToken ct = default)
    {
        var scope = await Scope(ct); if (scopeDefault) await Permission("files.connections.manage", ct);
        var chosen = await _selector.SelectAsync(scope, _user.UserId, container, targetId, ct: ct);
        if (scopeDefault && chosen.Ownership == RemoteDriveOwnership.Personal) throw new FileManagementException("invalid", "公共默认目标不能使用个人连接。");
        var owner = scopeDefault ? "" : _user.UserId;
        var current = await _targets.FindPreferenceAsync(scope.Key, owner, container, ct);
        var preference = new FileStoragePreference { Id = FileManagementService.HashKey(scope.Key + "\n" + owner + "\n" + container),
            ScopeKey = scope.Key, OwnerId = owner, Container = container, TargetId = targetId };
        if (!await _targets.SavePreferenceAsync(preference, current?.Version ?? 0, ct)) throw new FileManagementException("conflict", "偏好更新竞争。");
    }
}
