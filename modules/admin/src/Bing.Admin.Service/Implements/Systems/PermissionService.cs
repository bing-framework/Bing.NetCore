using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Extensions;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 权限 服务
    /// </summary>
    public class PermissionService : Bing.Application.Services.AppServiceBase, IPermissionService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 权限管理
        /// </summary>
        protected IPermissionManager PermissionManager { get; set; }

        /// <summary>
        /// 权限仓储
        /// </summary>
        protected IPermissionRepository PermissionRepository { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="PermissionService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="permissionManager">权限管理</param>
        /// <param name="permissionRepository">权限仓储</param>
        /// <param name="roleRepository">角色仓储</param>
        public PermissionService( IAdminUnitOfWork unitOfWork
            , IPermissionManager permissionManager
            , IPermissionRepository permissionRepository
            , IRoleRepository roleRepository)
        {
            UnitOfWork = unitOfWork;
            PermissionManager = permissionManager;
            PermissionRepository = permissionRepository;
            RoleRepository = roleRepository;
        }

        /// <summary>
        /// 获取资源标识列表
        /// </summary>
        /// <param name="query">查询参数</param>
        public async Task<List<Guid>> GetResourceIdsAsync(PermissionQuery query) =>
            await PermissionRepository.GetResourceIdsAsync(query.ApplicationId.SafeValue(),
                query.RoleId.SafeValue(), query.IsDeny.SafeValue());

        /// <summary>
        /// 保存权限
        /// </summary>
        /// <param name="request">请求</param>
        public async Task SaveAsync(SavePermissionRequest request)
        {
            var roleId = request.RoleId.SafeValue();
            await PermissionManager.SaveAsync(request.ApplicationId.SafeValue(), roleId, request.ResourceIds.ToGuidList(), request.IsDeny.SafeValue());
            await UnitOfWork.CommitAsync();
        }
    }
}
