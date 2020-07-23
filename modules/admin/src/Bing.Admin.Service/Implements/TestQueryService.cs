using Bing.Admin.Service.Abstractions;
using Bing.Admin.Service.Queries.Systems;
using Bing.Admin.Service.Responses.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Applications;

namespace Bing.Admin.Service.Implements
{
    /// <summary>
    /// 测试 查询服务
    /// </summary>
    public class TestQueryService : QueryServiceBase<Administrator, AdministratorResponse, AdministratorQuery>, ITestQueryService
    {
        /// <summary>
        /// 初始化一个<see cref="QueryServiceBase{TEntity,TDto,TQueryParameter}"/>类型的实例
        /// </summary>
        /// <param name="store">查询存储器</param>
        public TestQueryService(IAdministratorRepository store) : base(store)
        {
        }
    }
}
