using Bing.DbDesigner.Projects.Domain.Models;
using Bing.DbDesigner.Projects.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Projects {
    /// <summary>
    /// 用户解决方案仓储
    /// </summary>
    public class UserSolutionRepository : RepositoryBase<UserSolution>, IUserSolutionRepository {
        /// <summary>
        /// 初始化用户解决方案仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserSolutionRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}