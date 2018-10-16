using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Systems;
using Bing.DbDesigner.Service.Queries.Systems;

namespace Bing.DbDesigner.Service.Abstractions.Systems {
    /// <summary>
    /// 应用程序服务
    /// </summary>
    public interface IApplicationService : ICrudService<ApplicationDto, ApplicationQuery> {
    }
}