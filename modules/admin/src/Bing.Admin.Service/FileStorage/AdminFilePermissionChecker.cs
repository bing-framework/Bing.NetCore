using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Biz.FileManagement;
using Bing.Security.Claims;
using Bing.Users;

namespace Bing.Admin.Service.FileStorage;

/// <summary>通过 Admin 登录令牌中的应用和角色标识查询文件管理权限。</summary>
public sealed class AdminFilePermissionChecker : IFilePermissionChecker
{
    private readonly ICurrentUser _currentUser;
    private readonly IModuleRepository _modules;
    private readonly IOperationRepository _operations;
    private readonly IPermissionRepository _permissions;

    /// <summary>创建 Admin 文件权限检查器。</summary>
    public AdminFilePermissionChecker(ICurrentUser currentUser, IModuleRepository modules,
        IOperationRepository operations, IPermissionRepository permissions)
    {
        _currentUser = currentUser;
        _modules = modules;
        _operations = operations;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task<bool> IsGrantedAsync(string permission, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!AdminFilePermissionCatalog.Contains(permission) || !_currentUser.IsAuthenticated)
            return false;

        var applicationCode = _currentUser.FindClaims(BingClaimTypes.ApplicationCode);
        var applicationIds = _currentUser.FindClaims(BingClaimTypes.ApplicationId);
        var roleClaims = _currentUser.FindClaims(BingClaimTypes.RoleIds);
        if (applicationCode.Length != 1 || applicationCode[0].Value != ApplicationCode.Admin ||
            applicationIds.Length != 1 || !Guid.TryParse(applicationIds[0].Value, out var applicationId) ||
            roleClaims.Length != 1)
            return false;

        var roleIds = new HashSet<Guid>();
        foreach (var value in roleClaims[0].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(value, out var roleId))
                return false;
            roleIds.Add(roleId);
        }
        if (roleIds.Count == 0)
            return false;

        var modules = await _modules.GetModulesAsync(applicationId);
        var fileModules = modules.Where(module => string.Equals(module.Url, AdminFilePermissionCatalog.ModuleUri,
            StringComparison.Ordinal)).ToArray();
        if (fileModules.Length != 1 || fileModules[0].Enabled != true)
            return false;

        var allowIds = new HashSet<Guid>();
        var denyIds = new HashSet<Guid>();
        foreach (var roleId in roleIds)
        {
            ct.ThrowIfCancellationRequested();
            allowIds.UnionWith(await _permissions.GetResourceIdsAsync(applicationId, roleId, isDeny: false));
            denyIds.UnionWith(await _permissions.GetResourceIdsAsync(applicationId, roleId, isDeny: true));
        }
        if (allowIds.Count == 0 && denyIds.Count == 0)
            return false;

        var candidateIds = allowIds.Concat(denyIds).Distinct().ToArray();
        var candidates = await _operations.FindByIdsAsync(candidateIds, ct);
        bool Matches(Operation operation, string code) =>
            operation.ApplicationId == applicationId &&
            operation.ModuleId == fileModules[0].Id &&
            operation.Enabled == true &&
            string.Equals(operation.Code, code, StringComparison.Ordinal);

        var denied = candidates.Any(operation => denyIds.Contains(operation.Id) && Matches(operation, permission));
        if (denied)
            return false;
        return candidates.Any(operation => allowIds.Contains(operation.Id) && Matches(operation, permission));
    }
}
