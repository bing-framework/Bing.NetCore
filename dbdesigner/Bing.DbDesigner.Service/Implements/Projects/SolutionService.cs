using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Projects.Domain.Models;
using Bing.DbDesigner.Projects.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Projects;
using Bing.DbDesigner.Service.Queries.Projects;
using Bing.DbDesigner.Service.Abstractions.Projects;

namespace Bing.DbDesigner.Service.Implements.Projects {
    /// <summary>
    /// 解决方案服务
    /// </summary>
    public class SolutionService : CrudServiceBase<Solution, SolutionDto, SolutionQuery>, ISolutionService {
        /// <summary>
        /// 初始化解决方案服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="solutionRepository">解决方案仓储</param>
        public SolutionService( IDbDesignerUnitOfWork unitOfWork, ISolutionRepository solutionRepository )
            : base( unitOfWork, solutionRepository ) {
            SolutionRepository = solutionRepository;
        }
        
        /// <summary>
        /// 解决方案仓储
        /// </summary>
        public ISolutionRepository SolutionRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Solution> CreateQuery( SolutionQuery param ) {
            return new Query<Solution>( param );
        }
    }
}