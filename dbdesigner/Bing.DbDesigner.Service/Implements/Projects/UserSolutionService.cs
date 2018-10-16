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
    /// 用户解决方案服务
    /// </summary>
    public class UserSolutionService : CrudServiceBase<UserSolution, UserSolutionDto, UserSolutionQuery>, IUserSolutionService {
        /// <summary>
        /// 初始化用户解决方案服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userSolutionRepository">用户解决方案仓储</param>
        public UserSolutionService( IDbDesignerUnitOfWork unitOfWork, IUserSolutionRepository userSolutionRepository )
            : base( unitOfWork, userSolutionRepository ) {
            UserSolutionRepository = userSolutionRepository;
        }
        
        /// <summary>
        /// 用户解决方案仓储
        /// </summary>
        public IUserSolutionRepository UserSolutionRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<UserSolution> CreateQuery( UserSolutionQuery param ) {
            return new Query<UserSolution>( param );
        }
    }
}