using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Systems {
    /// <summary>
    /// 应用程序角色仓储
    /// </summary>
    public class ApplicationRoleRepository : RepositoryBase<ApplicationRole>, IApplicationRoleRepository {
        /// <summary>
        /// 初始化应用程序角色仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ApplicationRoleRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}