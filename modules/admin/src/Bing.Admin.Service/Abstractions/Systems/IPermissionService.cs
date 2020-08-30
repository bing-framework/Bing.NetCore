using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Applications;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 权限 服务
    /// </summary>
    public interface IPermissionService : Bing.Application.Services.IAppService
    {
        /// <summary>
        /// 获取资源标识列表
        /// </summary>
        /// <param name="query">查询参数</param>
        Task<List<Guid>> GetResourceIdsAsync(PermissionQuery query);

        /// <summary>
        /// 保存权限
        /// </summary>
        /// <param name="request">请求</param>
        Task SaveAsync(SavePermissionRequest request);
    }
}
