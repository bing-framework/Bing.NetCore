using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Systems {
    /// <summary>
    /// 用户角色仓储
    /// </summary>
    public class UserRoleRepository : RepositoryBase<UserRole>, IUserRoleRepository {
        /// <summary>
        /// 初始化用户角色仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserRoleRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}