using System;
using System.Threading;
using Bing.MultiTenancy;
using Bing.Users;

namespace Bing.Admin.Service.FileStorage;

/// <summary>Admin 文件请求使用认证声明中的租户，受信后台代码可在异步作用域内显式切换。</summary>
public sealed class AdminFileCurrentTenant : ICurrentTenant
{
    private readonly ICurrentUser _user;
    private readonly AsyncLocal<Scope> _scope = new();
    private sealed class Scope { public string Id; public string Name; }
    /// <summary>使用已验证的当前用户，不从任意请求头接受租户。</summary>
    public AdminFileCurrentTenant(ICurrentUser user) => _user = user;
    /// <inheritdoc />
    public string Id => _scope.Value != null ? _scope.Value.Id : _user.TenantId;
    /// <inheritdoc />
    public string Name => _scope.Value?.Name;
    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrEmpty(Id);
    /// <inheritdoc />
    public IDisposable Change(string id, string name = null)
    {
        var previous = _scope.Value;
        _scope.Value = new Scope { Id = id, Name = name };
        return new Restore(this, previous);
    }
    private sealed class Restore : IDisposable
    {
        private readonly AdminFileCurrentTenant _tenant;
        private readonly Scope _previous;
        private bool _disposed;
        public Restore(AdminFileCurrentTenant tenant, Scope previous) { _tenant = tenant; _previous = previous; }
        public void Dispose() { if (_disposed) return; _disposed = true; _tenant._scope.Value = _previous; }
    }
}
