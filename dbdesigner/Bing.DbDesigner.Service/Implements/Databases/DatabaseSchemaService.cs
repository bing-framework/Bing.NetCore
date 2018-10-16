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
    /// 数据模式服务
    /// </summary>
    public class DatabaseSchemaService : CrudServiceBase<DatabaseSchema, DatabaseSchemaDto, DatabaseSchemaQuery>, IDatabaseSchemaService {
        /// <summary>
        /// 初始化数据模式服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="databaseSchemaRepository">数据模式仓储</param>
        public DatabaseSchemaService( IDbDesignerUnitOfWork unitOfWork, IDatabaseSchemaRepository databaseSchemaRepository )
            : base( unitOfWork, databaseSchemaRepository ) {
            DatabaseSchemaRepository = databaseSchemaRepository;
        }
        
        /// <summary>
        /// 数据模式仓储
        /// </summary>
        public IDatabaseSchemaRepository DatabaseSchemaRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<DatabaseSchema> CreateQuery( DatabaseSchemaQuery param ) {
            return new Query<DatabaseSchema>( param );
        }
    }
}