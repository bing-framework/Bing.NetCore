using Bing;
using Bing.Extensions.AutoMapper;
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
    /// 数据列服务
    /// </summary>
    public class DatabaseColumnService : CrudServiceBase<DatabaseColumn, DatabaseColumnDto, DatabaseColumnQuery>, IDatabaseColumnService {
        /// <summary>
        /// 初始化数据列服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="databaseColumnRepository">数据列仓储</param>
        public DatabaseColumnService( IDbDesignerUnitOfWork unitOfWork, IDatabaseColumnRepository databaseColumnRepository )
            : base( unitOfWork, databaseColumnRepository ) {
            DatabaseColumnRepository = databaseColumnRepository;
        }
        
        /// <summary>
        /// 数据列仓储
        /// </summary>
        public IDatabaseColumnRepository DatabaseColumnRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<DatabaseColumn> CreateQuery( DatabaseColumnQuery param ) {
            return new Query<DatabaseColumn>( param );
        }
    }
}