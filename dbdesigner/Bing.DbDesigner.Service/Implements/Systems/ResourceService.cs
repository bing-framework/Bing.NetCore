using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications.Trees;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Systems;
using Bing.DbDesigner.Service.Queries.Systems;
using Bing.DbDesigner.Service.Abstractions.Systems;

namespace Bing.DbDesigner.Service.Implements.Systems {
    /// <summary>
    /// 资源服务
    /// </summary>
    public class ResourceService : TreeServiceBase<Resource, ResourceDto, ResourceQuery>, IResourceService {
        /// <summary>
        /// 初始化资源服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="resourceRepository">资源仓储</param>
        public ResourceService( IDbDesignerUnitOfWork unitOfWork, IResourceRepository resourceRepository )
            : base( unitOfWork, resourceRepository ) {
            ResourceRepository = resourceRepository;
        }
        
        /// <summary>
        /// 资源仓储
        /// </summary>
        public IResourceRepository ResourceRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Resource> CreateQuery( ResourceQuery param ) {
            return new Query<Resource>( param );
        }
    }
}