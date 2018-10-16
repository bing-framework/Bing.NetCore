using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Systems;
using Bing.DbDesigner.Service.Queries.Systems;

namespace Bing.DbDesigner.Service.Abstractions.Systems {
    /// <summary>
    /// 用户服务
    /// </summary>
    public interface IUserService : ICrudService<UserDto, UserQuery> {
    }
}