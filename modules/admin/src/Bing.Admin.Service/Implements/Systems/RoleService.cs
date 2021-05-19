using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Dtos.Systems;
using Bing.Admin.Service.Shared.Queries.Systems;
using Bing.Admin.Service.Shared.Requests.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Application.Services;
using Bing.Data.Queries;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.ObjectMapping;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 角色 服务
    /// </summary>
    public class RoleService : DeleteAppServiceBase<Role, RoleDto, RoleQuery>, IRoleService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; set; }

        /// <summary>
        /// 角色管理
        /// </summary>
        protected IRoleManager RoleManager { get; set; }

        /// <summary>
        /// 初始化一个<see cref="RoleService"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="roleRepository">角色仓储</param>
        /// <param name="roleManager">角色管理</param>
        public RoleService(IAdminUnitOfWork unitOfWork
            , IRoleRepository roleRepository
            , IRoleManager roleManager) : base(unitOfWork, roleRepository)
        {
            UnitOfWork = unitOfWork;
            RoleRepository = roleRepository;
            RoleManager = roleManager;
        }

        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="parameter">查询参数</param>
        protected override IQueryBase<Role> CreateQuery(RoleQuery parameter)
        {
            return new Query<Role>(parameter)
                .WhereIfNotEmpty(t => t.Code.Contains(parameter.Code))
                .WhereIfNotEmpty(t => t.Name.Contains(parameter.Name))
                .WhereIfNotEmpty(t => t.Type.Contains(parameter.Type))
                .WhereIfNotEmpty(t => t.IsAdmin == parameter.IsAdmin)
                .WhereIfNotEmpty(t => t.Enabled == parameter.Enabled);
        }

        /// <summary>
        /// 获取用户的角色列表
        /// </summary>
        /// <param name="userId">用户标识</param>
        public async Task<List<RoleDto>> GetRolesAsync(Guid userId)
        {
            var result = await RoleRepository.GetRolesAsync(userId);
            return result.MapToList<RoleDto>();
        }

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="request">创建角色请求</param>
        public async Task<Guid> CreateAsync(CreateRoleRequest request)
        {
            var role = request.MapTo<Role>();
            if (request.Type == RoleTypeCode.TenantRole)
            {
                var tenantRole = await RoleRepository.GetByCodeAsync(RoleCode.TenantAdmin);
                if (tenantRole == null)
                    throw new Warning("找不到租户角色，请联系管理员");
                role.ParentId = tenantRole.Id;
            }

            await RoleManager.CreateAsync(role);
            await UnitOfWork.CommitAsync();
            return role.Id;
        }

        /// <summary>
        /// 修改角色
        /// </summary>
        /// <param name="request">修改角色请求</param>
        public async Task UpdateAsync(UpdateRoleRequest request)
        {
            var entity = await RoleRepository.FindAsync(request.Id.ToGuid());
            request.MapTo(entity);
            await RoleManager.UpdateAsync(entity);
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 添加用户到角色
        /// </summary>
        /// <param name="request">用户角色请求</param>
        public async Task AddUsersToRoleAsync(UserRoleRequest request)
        {
            await RoleManager.AddUsersToRoleAsync(request.RoleId, request.UserIds.ToGuidList());
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 从角色移除用户
        /// </summary>
        /// <param name="request">用户角色请求</param>
        public async Task RemoveUsersFromRoleAsync(UserRoleRequest request)
        {
            await RoleManager.RemoveUsersFromRoleAsync(request.RoleId, request.UserIds.ToGuidList());
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 获取角色列表
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="tenantId">租户标识</param>
        public async Task<List<RoleResponse>> GetListAsync(string type, string tenantId)
        {
            var result = await RoleRepository.FindAllAsync(x => x.Type == type);
            //var result = await RoleRepository.GetListAsync(type, tenantId);
            return result.MapTo<List<RoleResponse>>();
        }
    }
}
