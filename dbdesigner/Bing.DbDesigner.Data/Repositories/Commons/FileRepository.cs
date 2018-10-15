using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Commons {
    /// <summary>
    /// 文件仓储
    /// </summary>
    public class FileRepository : RepositoryBase<File>, IFileRepository {
        /// <summary>
        /// 初始化文件仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public FileRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}