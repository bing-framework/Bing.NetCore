using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Databases.Domain.Models;
using Bing.DbDesigner.Databases.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Databases;
using Bing.DbDesigner.Service.Queries.Databases;
using Bing.DbDesigner.Service.Abstractions.Databases;

namespace Bing.DbDesigner.Service.Implements.Databases {
    /// <summary>
    /// 数据类型字典服务
    /// </summary>
    public class DataTypeDictionaryService : CrudServiceBase<DataTypeDictionary, DataTypeDictionaryDto, DataTypeDictionaryQuery>, IDataTypeDictionaryService {
        /// <summary>
        /// 初始化数据类型字典服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="dataTypeDictionaryRepository">数据类型字典仓储</param>
        public DataTypeDictionaryService( IDbDesignerUnitOfWork unitOfWork, IDataTypeDictionaryRepository dataTypeDictionaryRepository )
            : base( unitOfWork, dataTypeDictionaryRepository ) {
            DataTypeDictionaryRepository = dataTypeDictionaryRepository;
        }
        
        /// <summary>
        /// 数据类型字典仓储
        /// </summary>
        public IDataTypeDictionaryRepository DataTypeDictionaryRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<DataTypeDictionary> CreateQuery( DataTypeDictionaryQuery param ) {
            return new Query<DataTypeDictionary>( param );
        }
    }
}