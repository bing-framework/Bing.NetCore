using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 管理员 服务
    /// </summary>
    public class AdministratorService : ServiceBase, IAdministratorService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 管理员仓储
        /// </summary>
        protected IAdministratorRepository AdministratorRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="AdministratorService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="administratorRepository">管理员仓储</param>
        public AdministratorService( IAdminUnitOfWork unitOfWork, IAdministratorRepository administratorRepository )
        {
            UnitOfWork = unitOfWork;
            AdministratorRepository = administratorRepository;
        }
    }
}