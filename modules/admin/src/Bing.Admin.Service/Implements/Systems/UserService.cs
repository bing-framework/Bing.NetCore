using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 用户 服务
    /// </summary>
    public class UserService : ServiceBase, IUserService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="UserService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userRepository">用户仓储</param>
        public UserService( IAdminUnitOfWork unitOfWork, IUserRepository userRepository )
        {
            UnitOfWork = unitOfWork;
            UserRepository = userRepository;
        }
    }
}