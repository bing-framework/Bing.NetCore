using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications.Trees;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;
using Bing.DbDesigner.Service.Abstractions.Commons;

namespace Bing.DbDesigner.Service.Implements.Commons {
    /// <summary>
    /// 字典服务
    /// </summary>
    public class DictionaryService : TreeServiceBase<Dictionary, DictionaryDto, DictionaryQuery>, IDictionaryService {
        /// <summary>
        /// 初始化字典服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="dictionaryRepository">字典仓储</param>
        public DictionaryService( IDbDesignerUnitOfWork unitOfWork, IDictionaryRepository dictionaryRepository )
            : base( unitOfWork, dictionaryRepository ) {
            DictionaryRepository = dictionaryRepository;
        }
        
        /// <summary>
        /// 字典仓储
        /// </summary>
        public IDictionaryRepository DictionaryRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Dictionary> CreateQuery( DictionaryQuery param ) {
            return new Query<Dictionary>( param );
        }
    }
}