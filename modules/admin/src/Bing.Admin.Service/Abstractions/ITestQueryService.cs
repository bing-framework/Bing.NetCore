using Bing.Admin.Service.Queries.Systems;
using Bing.Admin.Service.Responses.Systems;
using Bing.Applications;

namespace Bing.Admin.Service.Abstractions
{
    /// <summary>
    /// 测试 查询服务
    /// </summary>
    public interface ITestQueryService : IQueryService<AdministratorResponse, AdministratorQuery>
    {
    }
}
