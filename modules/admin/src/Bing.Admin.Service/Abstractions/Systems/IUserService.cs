using System;
using System.Threading.Tasks;
using Bing.Admin.Service.Requests.Systems;
using Bing.Applications;

namespace Bing.Admin.Service.Abstractions.Systems
{
    /// <summary>
    /// 用户 服务
    /// </summary>
    public interface IUserService : Bing.Application.Services.IApplicationService
    {
        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="request">请求</param>
        Task ResetPasswordAsync(ResetPasswordRequest request);

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="currentPassword">当前密码</param>
        /// <param name="newPassword">新密码</param>
        Task ChangePasswordAsync(Guid? userId, string currentPassword, string newPassword);
    }
}
