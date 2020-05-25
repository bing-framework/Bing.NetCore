using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 权限 仓储
    /// </summary>
    public class PermissionRepository : RepositoryBase<Permission>, IPermissionRepository
    {
        /// <summary>
        /// 初始化一个<see cref="PermissionRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public PermissionRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}