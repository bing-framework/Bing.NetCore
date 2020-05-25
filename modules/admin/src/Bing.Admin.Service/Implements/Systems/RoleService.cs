using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 角色 服务
    /// </summary>
    public class RoleService : ServiceBase, IRoleService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="RoleService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="roleRepository">角色仓储</param>
        public RoleService( IAdminUnitOfWork unitOfWork, IRoleRepository roleRepository )
        {
            UnitOfWork = unitOfWork;
            RoleRepository = roleRepository;
        }
    }
}