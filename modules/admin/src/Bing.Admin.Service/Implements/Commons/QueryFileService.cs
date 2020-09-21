using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;
using Bing.Data.Sql;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 文件 查询服务
    /// </summary>
    public class QueryFileService : Bing.Application.Services.AppServiceBase, IQueryFileService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 文件仓储
        /// </summary>
        protected IFileRepository FileRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="QueryFileService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="fileRepository">文件仓储</param>
        public QueryFileService( ISqlQuery sqlQuery, IFileRepository fileRepository )
        {
            SqlQuery = sqlQuery;
            FileRepository = fileRepository;
        }
    }
}
