using Bing.Domain.Repositories;
using Bing.Admin.Systems.Domain.Models;

namespace Bing.Admin.Systems.Domain.Repositories
{
    /// <summary>
    /// 用户登录日志仓储
    /// </summary>
    public interface IUserLoginLogRepository : IRepository<UserLoginLog>
    {
    }
}