using Bing.Biz.FileManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Admin.Service.FileStorage;

/// <summary>注册 Admin 文件管理权限适配和显式资源初始化入口。</summary>
public static class AdminFileStoragePermissionRegistration
{
    /// <summary>使用 Admin 权限仓储替换默认的声明式文件权限检查器。</summary>
    public static IServiceCollection AddBingAdminFileStoragePermissions(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IFilePermissionChecker, AdminFilePermissionChecker>());
        services.TryAddScoped<IAdminFilePermissionResourceInitializer, AdminFilePermissionResourceInitializer>();
        return services;
    }
}
