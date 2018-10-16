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
    /// 数据库服务
    /// </summary>
    public class DatabaseService : CrudServiceBase<Database, DatabaseDto, DatabaseQuery>, IDatabaseService {
        /// <summary>
        /// 初始化数据库服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="databaseRepository">数据库仓储</param>
        public DatabaseService( IDbDesignerUnitOfWork unitOfWork, IDatabaseRepository databaseRepository )
            : base( unitOfWork, databaseRepository ) {
            DatabaseRepository = databaseRepository;
        }
        
        /// <summary>
        /// 数据库仓储
        /// </summary>
        public IDatabaseRepository DatabaseRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Database> CreateQuery( DatabaseQuery param ) {
            return new Query<Database>( param );
        }
    }
}