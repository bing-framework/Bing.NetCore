using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Service.Abstractions.Systems;
using Bing.Admin.Service.Shared.Responses.Systems;
using Bing.Data.Sql;
using Bing.Extensions;

namespace Bing.Admin.Service.Implements.Systems
{
    /// <summary>
    /// 权限 查询服务
    /// </summary>
    public class QueryPermissionService : Bing.Application.Services.AppServiceBase, IQueryPermissionService
    {
        /// <summary>
        /// Sql查询对象
        /// </summary>
        protected ISqlQuery SqlQuery { get; set; }

        /// <summary>
        /// 权限仓储
        /// </summary>
        protected IPermissionRepository PermissionRepository { get; set; }

        /// <summary>
        /// 模块仓储
        /// </summary>
        protected IModuleRepository ModuleRepository { get; set; }

        /// <summary>
        /// 操作仓储
        /// </summary>
        protected IOperationRepository OperationRepository { get; set; }

        /// <summary>
        /// 角色仓储
        /// </summary>
        protected IRoleRepository RoleRepository { get; set; }

        /// <summary>
        /// 初始化一个<see cref="QueryPermissionService"/>类型的实例
        /// </summary>
        /// <param name="sqlQuery">Sql查询对象</param>
        /// <param name="permissionRepository">权限仓储</param>
        /// <param name="moduleRepository">模块仓储</param>
        /// <param name="operationRepository">操作仓储</param>
        /// <param name="roleRepository">角色仓储</param>
        public QueryPermissionService( ISqlQuery sqlQuery
            , IPermissionRepository permissionRepository
            , IModuleRepository moduleRepository
            , IOperationRepository operationRepository
            , IRoleRepository roleRepository)
        {
            SqlQuery = sqlQuery;
            PermissionRepository = permissionRepository;
            ModuleRepository = moduleRepository;
            OperationRepository = operationRepository;
            RoleRepository = roleRepository;
        }

        /// <summary>
        /// 获取所有菜单
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        public async Task<List<SelectModuleResponse>> GetAllMenusAsync(Guid applicationId, Guid roleId)
        {
            var modules = await ModuleRepository.GetModulesAsync(applicationId);
            var operations = await OperationRepository.GetAllOperationsAsync(applicationId);
            var permissions = await PermissionRepository.GetResourceIdsAsync(applicationId, roleId, false);
            var result = new SelectMenuResult(modules, operations, permissions);
            return result.GetResult();
        }

        /// <summary>
        /// 获取角色菜单
        /// </summary>
        /// <param name="applicationId">应用程序标识</param>
        /// <param name="roleId">角色标识</param>
        public async Task<List<SelectModuleResponse>> GetRoleMenusAsync(Guid applicationId, Guid roleId)
        {
            var role = await RoleRepository.FindAsync(roleId);
            var roleIds = new List<Guid>() { roleId, role.ParentId.SafeValue() };
            var modules = await ModuleRepository.GetModulesAsync(applicationId, roleIds);
            var operations = await OperationRepository.GetOperationsAsync(applicationId, roleIds);
            var permissions = await PermissionRepository.GetResourceIdsAsync(applicationId, roleId, false);
            var result = new SelectMenuResult(modules, operations, permissions);
            return result.GetResult();
        }
    }
}
