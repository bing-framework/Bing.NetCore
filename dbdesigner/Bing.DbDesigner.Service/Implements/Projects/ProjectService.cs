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
    /// 项目服务
    /// </summary>
    public class ProjectService : CrudServiceBase<Project, ProjectDto, ProjectQuery>, IProjectService {
        /// <summary>
        /// 初始化项目服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="projectRepository">项目仓储</param>
        public ProjectService( IDbDesignerUnitOfWork unitOfWork, IProjectRepository projectRepository )
            : base( unitOfWork, projectRepository ) {
            ProjectRepository = projectRepository;
        }
        
        /// <summary>
        /// 项目仓储
        /// </summary>
        public IProjectRepository ProjectRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Project> CreateQuery( ProjectQuery param ) {
            return new Query<Project>( param );
        }
    }
}