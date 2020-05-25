using Bing.Webs.Controllers;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Apis.Commons
{
    /// <summary>
    /// 文件 控制器
    /// </summary>
    public class FileController : ApiControllerBase
    {
        /// <summary>
        /// 文件 服务
        /// </summary>
        public IFileService FileService { get; }
    
        /// <summary>
        /// 文件 查询服务
        /// </summary>
        public IQueryFileService QueryFileService { get; }

        /// <summary>
        /// 初始化一个<see cref="FileController"/>类型的实例
        /// </summary>
        /// <param name="service">文件服务</param>
        /// <param name="queryService">文件查询服务</param>
        public FileController( IFileService service, IQueryFileService queryService)
        {
            FileService = service;
            QueryFileService = queryService;
        }
    }
}
