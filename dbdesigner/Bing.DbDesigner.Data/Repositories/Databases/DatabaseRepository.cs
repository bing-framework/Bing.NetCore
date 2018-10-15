using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Databases {
    /// <summary>
    /// 数据库仓储
    /// </summary>
    public class DatabaseRepository : RepositoryBase<Database>, IDatabaseRepository {
        /// <summary>
        /// 初始化数据库仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public DatabaseRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}