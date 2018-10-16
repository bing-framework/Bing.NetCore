using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;

namespace Bing.DbDesigner.Service.Abstractions.Commons {
    /// <summary>
    /// 用户信息服务
    /// </summary>
    public interface IUserInfoService : ICrudService<UserInfoDto, UserInfoQuery> {
    }
}