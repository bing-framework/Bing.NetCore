using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 用户 查询服务
    /// </summary>
    public class QueryUserService : ServiceBase, IQueryUserService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryUserService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="userRepository">用户仓储</param>
        public QueryUserService( ISqlQuery sqlQuery, IUserRepository userRepository )
        {
            SqlQuery = sqlQuery;
            UserRepository = userRepository;
        }
    }
}
