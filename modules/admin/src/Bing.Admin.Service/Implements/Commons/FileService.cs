using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 文件 服务
    /// </summary>
    public class FileService : ServiceBase, IFileService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 文件仓储
        /// </summary>
        protected IFileRepository FileRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="FileService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="fileRepository">文件仓储</param>
        public FileService( IAdminUnitOfWork unitOfWork, IFileRepository fileRepository )
        {
            UnitOfWork = unitOfWork;
            FileRepository = fileRepository;
        }
    }
}
