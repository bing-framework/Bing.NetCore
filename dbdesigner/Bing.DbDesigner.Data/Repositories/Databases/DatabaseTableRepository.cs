using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Databases {
    /// <summary>
    /// 数据表仓储
    /// </summary>
    public class DatabaseTableRepository : RepositoryBase<DatabaseTable>, IDatabaseTableRepository {
        /// <summary>
        /// 初始化数据表仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public DatabaseTableRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}