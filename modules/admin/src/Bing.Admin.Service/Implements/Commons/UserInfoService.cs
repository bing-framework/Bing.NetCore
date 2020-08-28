using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 用户信息 服务
    /// </summary>
    public class UserInfoService : Bing.Application.Services.AppServiceBase, IUserInfoService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 用户信息仓储
        /// </summary>
        protected IUserInfoRepository UserInfoRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="UserInfoService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userInfoRepository">用户信息仓储</param>
        public UserInfoService( IAdminUnitOfWork unitOfWork, IUserInfoRepository userInfoRepository )
        {
            UnitOfWork = unitOfWork;
            UserInfoRepository = userInfoRepository;
        }
    }
}
