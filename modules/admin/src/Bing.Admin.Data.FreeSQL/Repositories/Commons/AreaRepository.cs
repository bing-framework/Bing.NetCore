using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Commons
{
    /// <summary>
    /// 地区 仓储
    /// </summary>
    public class AreaRepository : RepositoryBase<Area>, IAreaRepository
    {
        /// <summary>
        /// 初始化一个<see cref="AreaRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public AreaRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}
