using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Databases;
using Bing.DbDesigner.Service.Queries.Databases;
using Bing.DbDesigner.Service.Abstractions.Databases;

namespace Bing.DbDesigner.Service.Implements.Databases {
    /// <summary>
    /// 数据表服务
    /// </summary>
    public class DatabaseTableService : CrudServiceBase<DatabaseTable, DatabaseTableDto, DatabaseTableQuery>, IDatabaseTableService {
        /// <summary>
        /// 初始化数据表服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="databaseTableRepository">数据表仓储</param>
        public DatabaseTableService( IDbDesignerUnitOfWork unitOfWork, IDatabaseTableRepository databaseTableRepository )
            : base( unitOfWork, databaseTableRepository ) {
            DatabaseTableRepository = databaseTableRepository;
        }
        
        /// <summary>
        /// 数据表仓储
        /// </summary>
        public IDatabaseTableRepository DatabaseTableRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<DatabaseTable> CreateQuery( DatabaseTableQuery param ) {
            return new Query<DatabaseTable>( param );
        }
    }
}
