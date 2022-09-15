using System;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Application.Services;
using Bing.Aspects;
using Bing.Validation;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 模块服务
    /// </summary>
    public interface IModuleService : IAppService
    {
        /// <summary>
        /// 创建模块
        /// </summary>
        /// <param name="request">请求</param>
        Task<Guid> CreateAsync([NotNull][Valid] CreateModuleRequest request);

        /// <summary>
        /// 修改模块
        /// </summary>
        /// <param name="request">请求</param>
        Task UpdateAsync([NotNull][Valid] UpdateModuleRequest request);

        /// <summary>
        /// 删除模块
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        Task DeleteAsync(string ids);
    }
}
