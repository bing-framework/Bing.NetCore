using Bing.Admin.Data;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Commons;

namespace Bing.Admin.Service.Implements.Commons
{
    /// <summary>
    /// 字典 服务
    /// </summary>
    public class DictionaryService : Bing.Application.Services.AppServiceBase, IDictionaryService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }
        
        /// <summary>
        /// 字典仓储
        /// </summary>
        protected IDictionaryRepository DictionaryRepository { get; set; }
    
        /// <summary>
        /// 初始化一个<see cref="DictionaryService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="dictionaryRepository">字典仓储</param>
        public DictionaryService( IAdminUnitOfWork unitOfWork, IDictionaryRepository dictionaryRepository )
        {
            UnitOfWork = unitOfWork;
            DictionaryRepository = dictionaryRepository;
        }
    }
}
