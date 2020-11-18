using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 用户登录日志 仓储
    /// </summary>
    public class UserLoginLogRepository : RepositoryBase<UserLoginLog>, IUserLoginLogRepository
    {
        /// <summary>
        /// 初始化一个<see cref="UserLoginLogRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserLoginLogRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}