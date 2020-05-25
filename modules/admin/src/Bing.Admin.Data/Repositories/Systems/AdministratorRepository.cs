using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Systems
{
    /// <summary>
    /// 管理员 仓储
    /// </summary>
    public class AdministratorRepository : RepositoryBase<Administrator>, IAdministratorRepository
    {
        /// <summary>
        /// 初始化一个<see cref="AdministratorRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public AdministratorRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}