using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Databases {
    /// <summary>
    /// 数据模式仓储
    /// </summary>
    public class DatabaseSchemaRepository : RepositoryBase<DatabaseSchema>, IDatabaseSchemaRepository {
        /// <summary>
        /// 初始化数据模式仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public DatabaseSchemaRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}