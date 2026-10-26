using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Service.FileStorage;

/// <summary>按需建立文件管理权限资源；调用方必须先完成 Admin 数据库建表。</summary>
public sealed class AdminFilePermissionResourceInitializer : IAdminFilePermissionResourceInitializer
{
    private readonly IAdminUnitOfWork _unitOfWork;
    private readonly IApplicationRepository _applications;
    private readonly IModuleRepository _modules;
    private readonly IOperationRepository _operations;

    /// <summary>创建资源初始化服务。</summary>
    public AdminFilePermissionResourceInitializer(IAdminUnitOfWork unitOfWork,
        IApplicationRepository applications, IModuleRepository modules, IOperationRepository operations)
    {
        _unitOfWork = unitOfWork;
        _applications = applications;
        _modules = modules;
        _operations = operations;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var application = await _applications.GetByCodeAsync(ApplicationCode.Admin)
            ?? throw new InvalidOperationException("Admin 应用尚未初始化，无法创建文件权限资源。");
        var modules = await _modules.GetModulesAsync(application.Id);
        var matches = modules.Where(module => string.Equals(module.Url, AdminFilePermissionCatalog.ModuleUri,
            StringComparison.Ordinal)).ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException("Admin 文件管理资源模块 URI 重复。");

        var changed = false;
        var module = matches.SingleOrDefault();
        if (module == null)
        {
            module = new Module(Guid.NewGuid(), "", 0)
            {
                ApplicationId = application.Id,
                Name = "文件管理",
                Url = AdminFilePermissionCatalog.ModuleUri,
                Enabled = true,
                SortId = await _modules.GenerateSortIdAsync(application.Id, parentId: null)
            };
            module.InitPath();
            module.Init();
            await _modules.AddAsync(module, cancellationToken);
            changed = true;
        }

        var operations = await _operations.GetAllOperationsAsync(application.Id);
        foreach (var definition in AdminFilePermissionCatalog.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operationMatches = operations.Where(operation => string.Equals(operation.Code, definition.Code,
                StringComparison.Ordinal)).ToArray();
            if (operationMatches.Length > 1)
                throw new InvalidOperationException($"Admin 文件管理权限资源编码重复：{definition.Code}");
            if (operationMatches.Length == 1)
            {
                if (operationMatches[0].ModuleId != module.Id)
                    throw new InvalidOperationException($"Admin 文件权限编码已被其他模块占用：{definition.Code}");
                continue;
            }

            var operation = new Operation(Guid.NewGuid())
            {
                ApplicationId = application.Id,
                ModuleId = module.Id,
                Code = definition.Code,
                Name = definition.Name,
                Enabled = true,
                SortId = await _operations.GenerateSortIdAsync(application.Id, module.Id)
            };
            operation.Init();
            await _operations.AddAsync(operation, cancellationToken);
            changed = true;
        }

        if (changed)
            await _unitOfWork.CommitAsync(cancellationToken);
    }
}
