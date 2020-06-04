using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Admin.Systems.Domain.Services.Abstractions;
using Bing.Datas.Seed;
using Bing.Datas.UnitOfWorks;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Data.Seed
{
    /// <summary>
    /// 角色种子数据初始化
    /// </summary>
    public class RoleSeedDataInitializer : SeedDataInitializerBase<Role, Guid>
    {
        /// <summary>
        /// 服务提供程序
        /// </summary>
        private readonly IServiceProvider _rootProvider;

        /// <summary>
        /// 种子数据初始化顺序
        /// </summary>
        public override int Order => 1;

        /// <summary>
        /// 初始化一个<see cref="SeedDataInitializerBase{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="rootProvider">服务提供程序</param>
        public RoleSeedDataInitializer(IServiceProvider rootProvider) : base(rootProvider)
        {
            _rootProvider = rootProvider;
        }

        /// <summary>
        /// 重写以提供要初始化的种子数据
        /// </summary>
        protected override Role[] SeedData()
        {
            var list = new List<Role>();
            list.Add(AddRole(RoleCode.SuperAdmin, "超级管理员", RoleTypeCode.SystemRole, true, true, false));
            list.Add(AddRole(RoleCode.Admin, "普通管理员", RoleTypeCode.SystemRole, true, true, true));
            return list.ToArray();
        }

        /// <summary>
        /// 添加角色
        /// </summary>
        /// <param name="code">编码</param>
        /// <param name="name">名称</param>
        /// <param name="type">类型</param>
        /// <param name="isAdmin">是否管理角色</param>
        /// <param name="isSystem">是否系统角色</param>
        /// <param name="isDefault">是否默认角色</param>
        private Role AddRole(string code, string name, string type, bool isAdmin, bool isSystem, bool isDefault)
        {
            var role = new Role
            {
                Code = code,
                Name = name,
                Type = type,
                Enabled = true,
                IsAdmin = isAdmin,
                IsSystem = isSystem,
                IsDefault = isDefault,
                Remark = "系统初始化",
                Creator = "系统初始化",
                CreatorId = Guid.Empty,
                LastModifier = "系统初始化",
                LastModifierId = Guid.Empty
            };
            role.Init();
            return role;
        }

        /// <summary>
        /// 重写以提供判断某个实体是否存在的表达式
        /// </summary>
        /// <param name="entity">要判断的实体</param>
        protected override Expression<Func<Role, bool>> ExistingExpression(Role entity) => x => x.Code == entity.Code && x.Type == entity.Type;

        /// <summary>
        /// 将种子数据初始化到数据库
        /// </summary>
        /// <param name="entities">实体集合</param>
        protected override void SyncToDatabase(Role[] entities)
        {
            if (entities == null || entities.Length == 0)
                return;
            using var scope = _rootProvider.CreateScope();
            var scopeProvider = scope.ServiceProvider;
            using var unitOfWork = scopeProvider.GetService<IUnitOfWork>();
            var repository = scopeProvider.GetService<IRoleRepository>();
            var manager = scopeProvider.GetService<IRoleManager>();
            foreach (var entity in entities)
            {
                if (repository.Exists(ExistingExpression(entity)))
                    continue;
                manager.CreateAsync(entity).GetAwaiter().GetResult();
            }
            unitOfWork.Commit();
        }
    }
}
