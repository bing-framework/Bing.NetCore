using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Databases {
    /// <summary>
    /// 数据列仓储
    /// </summary>
    public class DatabaseColumnRepository : RepositoryBase<DatabaseColumn>, IDatabaseColumnRepository {
        /// <summary>
        /// 初始化数据列仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public DatabaseColumnRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}