using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Application.Services;

namespace Bing.Admin.Service.Abstractions
{
    /// <summary>
    /// 测试 查询服务
    /// </summary>
    public interface ITestQueryService : IQueryAppService<AdministratorResponse, AdministratorQuery>
    {
    }
}
