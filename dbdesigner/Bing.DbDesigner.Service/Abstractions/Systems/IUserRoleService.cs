using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Systems;
using Bing.DbDesigner.Service.Queries.Systems;

namespace Bing.DbDesigner.Service.Abstractions.Systems {
    /// <summary>
    /// 用户角色服务
    /// </summary>
    public interface IUserRoleService : ICrudService<UserRoleDto, UserRoleQuery> {
    }
}