using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Databases {
    /// <summary>
    /// 用户数据库仓储
    /// </summary>
    public class UserDatabaseRepository : RepositoryBase<UserDatabase>, IUserDatabaseRepository {
        /// <summary>
        /// 初始化用户数据库仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public UserDatabaseRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}