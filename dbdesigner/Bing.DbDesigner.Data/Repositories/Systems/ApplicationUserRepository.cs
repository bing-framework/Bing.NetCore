using Bing.DbDesigner.Systems.Domain.Models;
using Bing.DbDesigner.Systems.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Systems {
    /// <summary>
    /// 应用程序用户仓储
    /// </summary>
    public class ApplicationUserRepository : RepositoryBase<ApplicationUser>, IApplicationUserRepository {
        /// <summary>
        /// 初始化应用程序用户仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ApplicationUserRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}