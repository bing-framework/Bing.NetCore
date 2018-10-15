using Bing.DbDesigner.Projects.Domain.Models;
using Bing.DbDesigner.Projects.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Projects {
    /// <summary>
    /// 项目仓储
    /// </summary>
    public class ProjectRepository : RepositoryBase<Project>, IProjectRepository {
        /// <summary>
        /// 初始化项目仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ProjectRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}