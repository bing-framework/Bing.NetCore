using Bing.Datas.Sql;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 用户登录日志 查询服务
    /// </summary>
    public class QueryUserLoginLogService : Bing.Application.Services.AppServiceBase, IQueryUserLoginLogService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 用户登录日志仓储
        /// </summary>
        protected IUserLoginLogRepository UserLoginLogRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryUserLoginLogService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="userLoginLogRepository">用户登录日志仓储</param>
        public QueryUserLoginLogService( ISqlQuery sqlQuery, IUserLoginLogRepository userLoginLogRepository )
        {
            SqlQuery = sqlQuery;
            UserLoginLogRepository = userLoginLogRepository;
        }
    }
}
