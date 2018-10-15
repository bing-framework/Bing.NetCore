using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Databases {
    /// <summary>
    /// 数据类型字典仓储
    /// </summary>
    public class DataTypeDictionaryRepository : RepositoryBase<DataTypeDictionary>, IDataTypeDictionaryRepository {
        /// <summary>
        /// 初始化数据类型字典仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public DataTypeDictionaryRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}