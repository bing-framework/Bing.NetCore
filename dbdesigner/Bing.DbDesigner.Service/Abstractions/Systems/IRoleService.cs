using Bing.Applications.Trees;
using Bing.DbDesigner.Service.Dtos.Systems;
using Bing.DbDesigner.Service.Queries.Systems;

namespace Bing.DbDesigner.Service.Abstractions.Systems {
    /// <summary>
    /// 角色服务
    /// </summary>
    public interface IRoleService : ITreeService<RoleDto, RoleQuery> {
    }
}