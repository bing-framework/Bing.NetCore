using Bing.DbDesigner.Projects.Domain.Models;
using Bing.DbDesigner.Projects.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Projects {
    /// <summary>
    /// 用户项目仓储
    /// </summary>
    public class UserProjectRepository : RepositoryBase<UserProject>, IUserProjectRepository {
        /// <summary>
        /// 初始化用户项目仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserProjectRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}