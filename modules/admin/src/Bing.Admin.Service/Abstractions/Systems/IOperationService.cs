using System;
using System.Threading.Tasks;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Application.Services;
using Bing.Aspects;
using Bing.Validations.Aspects;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 操作服务
    /// </summary>
    public interface IOperationService : IAppService
    {
        /// <summary>
        /// 创建操作
        /// </summary>
        /// <param name="request">请求</param>
        Task<Guid> CreateAsync([NotNull][Valid] CreateOperationRequest request);

        /// <summary>
        /// 修改操作
        /// </summary>
        /// <param name="request">请求</param>
        Task UpdateAsync([NotNull][Valid] UpdateOperationRequest request);

        /// <summary>
        /// 删除操作
        /// </summary>
        /// <param name="ids">用逗号分隔的Id列表。范例："1,2"</param>
        Task DeleteAsync(string ids);
    }
}
