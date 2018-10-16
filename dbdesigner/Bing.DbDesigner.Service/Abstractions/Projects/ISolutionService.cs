using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Projects;
using Bing.DbDesigner.Service.Queries.Projects;

namespace Bing.DbDesigner.Service.Abstractions.Projects {
    /// <summary>
    /// 解决方案服务
    /// </summary>
    public interface ISolutionService : ICrudService<SolutionDto, SolutionQuery> {
    }
}