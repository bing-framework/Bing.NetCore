using Bing.Applications;
using Bing.Samples.Service.Dtos.Systems;
using Bing.Samples.Service.Queries.Systems;

namespace Bing.Samples.Service.Abstractions.Systems
{
    /// <summary>
    /// 应用程序服务
    /// </summary>
    public interface IApplicationService : ICrudService<ApplicationDto, ApplicationQuery>
    {
    }
}
