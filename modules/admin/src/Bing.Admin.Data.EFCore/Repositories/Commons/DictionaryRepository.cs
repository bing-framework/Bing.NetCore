using Bing.Datas.EntityFramework.Core;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;

namespace Bing.Admin.Data.Repositories.Commons
{
    /// <summary>
    /// 字典 仓储
    /// </summary>
    public class DictionaryRepository : RepositoryBase<Dictionary>, IDictionaryRepository
    {
        /// <summary>
        /// 初始化一个<see cref="DictionaryRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public DictionaryRepository( IAdminUnitOfWork unitOfWork ) : base( unitOfWork ) { }
    }
}