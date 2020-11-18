using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 资源 仓储
    /// </summary>
    public class ResourceRepository : RepositoryBase<Resource>, IResourceRepository
    {
        /// <summary>
        /// 初始化一个<see cref="ResourceRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ResourceRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}
