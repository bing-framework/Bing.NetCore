using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 资源 服务
    /// </summary>
    public class ResourceService : ServiceBase, IResourceService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 资源仓储
        /// </summary>
        protected IResourceRepository ResourceRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="ResourceService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="resourceRepository">资源仓储</param>
        public ResourceService( IAdminUnitOfWork unitOfWork, IResourceRepository resourceRepository )
        {
            UnitOfWork = unitOfWork;
            ResourceRepository = resourceRepository;
        }
    }
}