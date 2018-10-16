using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Databases;
using Bing.DbDesigner.Service.Queries.Databases;

namespace Bing.DbDesigner.Service.Abstractions.Databases {
    /// <summary>
    /// 用户数据库服务
    /// </summary>
    public interface IUserDatabaseService : ICrudService<UserDatabaseDto, UserDatabaseQuery> {
    }
}