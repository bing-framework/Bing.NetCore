using Bing.Applications;
using Bing.Datas.Sql;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 用户信息 查询服务
    /// </summary>
    public class QueryUserInfoService : Bing.Application.Services.AppServiceBase, IQueryUserInfoService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 用户信息仓储
        /// </summary>
        protected IUserInfoRepository UserInfoRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryUserInfoService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="userInfoRepository">用户信息仓储</param>
        public QueryUserInfoService( ISqlQuery sqlQuery, IUserInfoRepository userInfoRepository )
        {
            SqlQuery = sqlQuery;
            UserInfoRepository = userInfoRepository;
        }
    }
}
