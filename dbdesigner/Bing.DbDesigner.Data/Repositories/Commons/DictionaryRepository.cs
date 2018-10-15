using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Commons {
    /// <summary>
    /// 字典仓储
    /// </summary>
    public class DictionaryRepository : RepositoryBase<Dictionary>, IDictionaryRepository {
        /// <summary>
        /// 初始化字典仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public DictionaryRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}