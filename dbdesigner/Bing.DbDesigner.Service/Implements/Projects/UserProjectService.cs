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
    /// 用户项目服务
    /// </summary>
    public class UserProjectService : CrudServiceBase<UserProject, UserProjectDto, UserProjectQuery>, IUserProjectService {
        /// <summary>
        /// 初始化用户项目服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userProjectRepository">用户项目仓储</param>
        public UserProjectService( IDbDesignerUnitOfWork unitOfWork, IUserProjectRepository userProjectRepository )
            : base( unitOfWork, userProjectRepository ) {
            UserProjectRepository = userProjectRepository;
        }
        
        /// <summary>
        /// 用户项目仓储
        /// </summary>
        public IUserProjectRepository UserProjectRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<UserProject> CreateQuery( UserProjectQuery param ) {
            return new Query<UserProject>( param );
        }
    }
}