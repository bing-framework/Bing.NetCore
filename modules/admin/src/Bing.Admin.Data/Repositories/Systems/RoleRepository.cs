using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 角色 仓储
    /// </summary>
    public class RoleRepository : RepositoryBase<Role>, IRoleRepository
    {
        /// <summary>
        /// 初始化一个<see cref="RoleRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public RoleRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}