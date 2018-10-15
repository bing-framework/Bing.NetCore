using Bing.DbDesigner.Projects.Domain.Models;
using Bing.DbDesigner.Projects.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Projects {
    /// <summary>
    /// 解决方案仓储
    /// </summary>
    public class SolutionRepository : RepositoryBase<Solution>, ISolutionRepository {
        /// <summary>
        /// 初始化解决方案仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public SolutionRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}