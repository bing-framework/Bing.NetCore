using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Systems;
using Bing.DbDesigner.Service.Queries.Systems;

namespace Bing.DbDesigner.Service.Abstractions.Systems {
    /// <summary>
    /// 权限服务
    /// </summary>
    public interface IPermissionService : ICrudService<PermissionDto, PermissionQuery> {
    }
}