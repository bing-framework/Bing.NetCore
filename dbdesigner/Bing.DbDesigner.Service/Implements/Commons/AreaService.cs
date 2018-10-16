using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications.Trees;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;
using Bing.DbDesigner.Service.Abstractions.Commons;

namespace Bing.DbDesigner.Service.Implements.Commons {
    /// <summary>
    /// 地区服务
    /// </summary>
    public class AreaService : TreeServiceBase<Area, AreaDto, AreaQuery>, IAreaService {
        /// <summary>
        /// 初始化地区服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="areaRepository">地区仓储</param>
        public AreaService( IDbDesignerUnitOfWork unitOfWork, IAreaRepository areaRepository )
            : base( unitOfWork, areaRepository ) {
            AreaRepository = areaRepository;
        }
        
        /// <summary>
        /// 地区仓储
        /// </summary>
        public IAreaRepository AreaRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Area> CreateQuery( AreaQuery param ) {
            return new Query<Area>( param );
        }
    }
}