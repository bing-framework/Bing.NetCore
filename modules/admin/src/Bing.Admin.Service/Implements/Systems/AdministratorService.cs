using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Requests.Systems.Extensions;
using Bing.Admin.Systems.Domain.Parameters;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.ObjectMapping;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 管理员 服务
    /// </summary>
    public class AdministratorService : Bing.Application.Services.AppServiceBase, IAdministratorService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 管理员管理
        /// </summary>
        protected IAdministratorManager AdministratorManager { get; }

        /// <summary>
        /// 角色管理
        /// </summary>
        protected IRoleManager RoleManager { get; }

        /// <summary>
        /// 初始化一个<see cref="AdministratorService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="administratorManager">管理员管理</param>
        /// <param name="roleManager">角色管理</param>
        public AdministratorService( IAdminUnitOfWork unitOfWork
            , IAdministratorManager administratorManager
            , IRoleManager roleManager)
        {
            UnitOfWork = unitOfWork;
            AdministratorManager = administratorManager;
            RoleManager = roleManager;
        }

        /// <summary>
        /// 创建
        /// </summary>
        /// <param name="request">请求</param>
        public async Task<Guid> CreateAsync(AdministratorCreateRequest request)
        {
            //var user = await AdministratorManager.CreateAsync(request.ToParameter());
            var user = await AdministratorManager.CreateAsync(request.MapTo<UserParameter>());
            await RoleManager.AddUserToRoleAsync(user.Id, RoleCode.Admin);
            await UnitOfWork.CommitAsync();
            return user.Id;
        }

        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="request">请求</param>
        public async Task UpdateAsync(AdministratorUpdateRequest request)
        {
            await AdministratorManager.UpdateAsync(request.ToParameter());
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 启用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        public async Task EnableAsync(List<Guid> ids)
        {
            await AdministratorManager.EnableAsync(ids);
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 禁用
        /// </summary>
        /// <param name="ids">用户标识列表</param>
        public async Task DisableAsync(List<Guid> ids)
        {
            await AdministratorManager.DisableAsync(ids);
            await UnitOfWork.CommitAsync();
        }
    }
}
