using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 用户登录日志 服务
    /// </summary>
    public class UserLoginLogService : Bing.Application.Services.AppServiceBase, IUserLoginLogService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 用户登录日志仓储
        /// </summary>
        protected IUserLoginLogRepository UserLoginLogRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="UserLoginLogService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userLoginLogRepository">用户登录日志仓储</param>
        public UserLoginLogService( IAdminUnitOfWork unitOfWork, IUserLoginLogRepository userLoginLogRepository )
        {
            UnitOfWork = unitOfWork;
            UserLoginLogRepository = userLoginLogRepository;
        }
    }
}
