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
    /// 用户数据库服务
    /// </summary>
    public class UserDatabaseService : CrudServiceBase<UserDatabase, UserDatabaseDto, UserDatabaseQuery>, IUserDatabaseService {
        /// <summary>
        /// 初始化用户数据库服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userDatabaseRepository">用户数据库仓储</param>
        public UserDatabaseService( IDbDesignerUnitOfWork unitOfWork, IUserDatabaseRepository userDatabaseRepository )
            : base( unitOfWork, userDatabaseRepository ) {
            UserDatabaseRepository = userDatabaseRepository;
        }
        
        /// <summary>
        /// 用户数据库仓储
        /// </summary>
        public IUserDatabaseRepository UserDatabaseRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<UserDatabase> CreateQuery( UserDatabaseQuery param ) {
            return new Query<UserDatabase>( param );
        }
    }
}