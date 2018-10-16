using Bing.Applications.Trees;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;

namespace Bing.DbDesigner.Service.Abstractions.Commons {
    /// <summary>
    /// 字典服务
    /// </summary>
    public interface IDictionaryService : ITreeService<DictionaryDto, DictionaryQuery> {
    }
}