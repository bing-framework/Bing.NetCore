using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 应用程序 仓储
    /// </summary>
    public class ApplicationRepository : RepositoryBase<Application>, IApplicationRepository
    {
        /// <summary>
        /// 初始化一个<see cref="ApplicationRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ApplicationRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}