using System;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Domains.Repositories;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 管理员 查询服务
    /// </summary>
    public interface IQueryAdministratorService : Bing.Application.Services.IAppService
    {
        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="parameter">查询参数</param>
        Task<PagerList<AdministratorResponse>> PagerQueryAsync(AdministratorQuery parameter);

        /// <summary>
        /// 通过标识获取
        /// </summary>
        /// <param name="id">用户标识</param>
        Task<AdministratorResponse> GetById(Guid id);
    }
}
