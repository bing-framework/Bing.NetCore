using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Datas.EntityFramework.Core;

namespace Bing.Admin.Data.Stores.Implements.Systems
{
    /// <summary>
    /// 应用程序存储器
    /// </summary>
    public class ApplicationPoStore : StoreBase<ApplicationPo>, IApplicationPoStore
    {
        /// <summary>
        /// 初始化一个<see cref="ApplicationPoStore"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ApplicationPoStore(IAdminUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }
}
