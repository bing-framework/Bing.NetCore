using Bing.Applications.Trees;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;

namespace Bing.DbDesigner.Service.Abstractions.Commons {
    /// <summary>
    /// 地区服务
    /// </summary>
    public interface IAreaService : ITreeService<AreaDto, AreaQuery> {
    }
}