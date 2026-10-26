using System.Threading;
using System.Threading.Tasks;

namespace Bing.Admin.Service.FileStorage;

/// <summary>显式创建 Admin 文件管理模块和权限资源，不创建角色权限记录。</summary>
public interface IAdminFilePermissionResourceInitializer
{
    /// <summary>在 Admin 数据库结构就绪后创建缺少的文件管理权限资源。</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
