using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Commons
{
    /// <summary>
    /// 文件 仓储
    /// </summary>
    public class FileRepository : RepositoryBase<File>, IFileRepository
    {
        /// <summary>
        /// 初始化一个<see cref="FileRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public FileRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}
