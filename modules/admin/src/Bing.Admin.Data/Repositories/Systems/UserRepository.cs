using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 用户 仓储
    /// </summary>
    public class UserRepository : RepositoryBase<User>, IUserRepository
    {
        /// <summary>
        /// 初始化一个<see cref="UserRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}