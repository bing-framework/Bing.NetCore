using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 应用程序 服务
    /// </summary>
    public class ApplicationService : ServiceBase, IApplicationService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 应用程序仓储
        /// </summary>
        protected IApplicationRepository ApplicationRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="ApplicationService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="applicationRepository">应用程序仓储</param>
        public ApplicationService( IAdminUnitOfWork unitOfWork, IApplicationRepository applicationRepository )
        {
            UnitOfWork = unitOfWork;
            ApplicationRepository = applicationRepository;
        }
    }
}