using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 权限 服务
    /// </summary>
    public class PermissionService : ServiceBase, IPermissionService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 权限仓储
        /// </summary>
        protected IPermissionRepository PermissionRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="PermissionService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="permissionRepository">权限仓储</param>
        public PermissionService( IAdminUnitOfWork unitOfWork, IPermissionRepository permissionRepository )
        {
            UnitOfWork = unitOfWork;
            PermissionRepository = permissionRepository;
        }
    }
}