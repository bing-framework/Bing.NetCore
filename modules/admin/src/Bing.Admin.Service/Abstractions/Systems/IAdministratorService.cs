using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Service.Requests.Systems;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 管理员 服务
    /// </summary>
    public interface IAdministratorService : Bing.Application.Services.IAppService
    {
        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">请求</param>
        Task<Guid> CreateAsync(AdministratorCreateRequest request);

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="request">请求</param>
        Task UpdateAsync(AdministratorUpdateRequest request);

        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        Task EnableAsync(List<Guid> ids);

        /// <summary>
        /// 禁用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        Task DisableAsync(List<Guid> ids);
    }
}
