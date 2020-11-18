using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Commons
{
    /// <summary>
    /// 用户信息 仓储
    /// </summary>
    public class UserInfoRepository : RepositoryBase<UserInfo>, IUserInfoRepository
    {
        /// <summary>
        /// 初始化一个<see cref="UserInfoRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserInfoRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}
