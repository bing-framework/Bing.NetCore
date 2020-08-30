using System;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Aspects;
using Bing.Validations.Aspects;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 应用程序 服务
    /// </summary>
    public interface IApplicationService : Bing.Application.Services.IAppService
    {
        /// <summary>
        /// 创建应用程序
        /// </summary>
        /// <param name="request">请求</param>
        Task<Guid> CreateAsync([NotNull][Valid] CreateApplicationRequest request);

        /// <summary>
        /// 修改应用程序
        /// </summary>
        /// <param name="request">请求</param>
        Task UpdateAsync([NotNull][Valid] UpdateApplicationRequest request);

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        Task DeleteAsync(string ids);
    }
}
