using System;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Biz.FileManagement;
using Bing.MultiTenancy;
using Bing.Users;

namespace Bing.Admin.Service.FileStorage;

/// <summary>校验用户资料附件的所有权和显式管理权限。</summary>
public sealed class AdminUserInfoFileAuthorizer : IFileAccessAuthorizer
{
    /// <summary>用户资料附件的业务类型。</summary>
    public const string BusinessType = "user-info";

    private readonly IUserInfoRepository _userInfos;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IFilePermissionChecker _permissions;

    /// <summary>创建用户资料附件授权器。</summary>
    public AdminUserInfoFileAuthorizer(IUserInfoRepository userInfos, ICurrentUser currentUser,
        ICurrentTenant currentTenant, IFilePermissionChecker permissions)
    {
        _userInfos = userInfos;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(string businessType, string businessId, FileBusinessAccess access,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!string.Equals(businessType, BusinessType, StringComparison.Ordinal) ||
            !Enum.IsDefined(typeof(FileBusinessAccess), access) ||
            !Guid.TryParseExact(businessId, "D", out var ownerId) || ownerId == Guid.Empty ||
            !string.Equals(businessId, ownerId.ToString("D"), StringComparison.Ordinal))
            return false;

        if (!_currentUser.IsAuthenticated || _currentTenant.IsAvailable ||
            !string.IsNullOrEmpty(_currentUser.TenantId) ||
            !Guid.TryParse(_currentUser.UserId, out var currentUserId) || currentUserId == Guid.Empty)
            return false;

        var userInfo = await _userInfos.SingleAsync(info => info.Id == ownerId && !info.IsDeleted, ct);
        if (userInfo == null)
            return false;

        if (ownerId == currentUserId)
            return true;

        return await _permissions.IsGrantedAsync(AdminFilePermissionCatalog.ManageUserInfoAttachments, ct);
    }
}
