using System;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Requests.Systems;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Extensions;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 用户 服务
    /// </summary>
    public class UserService : Bing.Application.Services.AppServiceBase, IUserService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 用户仓储
        /// </summary>
        protected IUserRepository UserRepository { get; set; }

        /// <summary>
        /// 用户管理
        /// </summary>
        protected IUserManager UserManager { get; set; }

        /// <summary>
        /// 初始化一个<see cref="UserService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="userRepository">用户仓储</param>
        /// <param name="userManager">用户管理</param>
        public UserService(IAdminUnitOfWork unitOfWork
            , IUserRepository userRepository
            , IUserManager userManager)
        {
            UnitOfWork = unitOfWork;
            UserRepository = userRepository;
            UserManager = userManager;
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <param name="request">请求</param>
        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await UserRepository.FindAsync(request.UserId);
            await UserManager.ChangePasswordAsync(user, request.Password);
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="currentPassword">当前密码</param>
        /// <param name="newPassword">新密码</param>
        public async Task ChangePasswordAsync(Guid? userId, string currentPassword, string newPassword)
        {
            if (userId == null)
                userId = CurrentUser.UserId.ToGuidOrNull();
            var user = await UserRepository.FindAsync(userId);
            await UserManager.ChangePasswordAsync(user, currentPassword, newPassword);
            await UnitOfWork.CommitAsync();
        }
    }
}
