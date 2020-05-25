using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 地区 服务
    /// </summary>
    public class AreaService : ServiceBase, IAreaService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 地区仓储
        /// </summary>
        protected IAreaRepository AreaRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="AreaService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="areaRepository">地区仓储</param>
        public AreaService( IAdminUnitOfWork unitOfWork, IAreaRepository areaRepository )
        {
            UnitOfWork = unitOfWork;
            AreaRepository = areaRepository;
        }
    }
}