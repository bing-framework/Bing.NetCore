using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bing.Admin.Systems.Domain.Services.Implements
{
    /// <summary>
    /// Identity 角色服务
    /// </summary>
    public class IdentityRoleManager : RoleManager<Role>
    {
        /// <summary>
        /// 初始化一个<see cref="IdentityRoleManager"/>类型的实例
        /// </summary>
        /// <param name="store">角色存储</param>
        /// <param name="roleValidators">角色校验器</param>
        /// <param name="keyNormalizer">键标准化器</param>
        /// <param name="errors">错误描述</param>
        /// <param name="logger">日志</param>
        public IdentityRoleManager(IRoleStore<Role> store
            , IEnumerable<IRoleValidator<Role>> roleValidators
            , ILookupNormalizer keyNormalizer
            , IdentityErrorDescriber errors
            , ILogger<RoleManager<Role>> logger)
            : base(store, roleValidators, keyNormalizer, errors, logger)
        {
        }

        /// <summary>
        /// 角色校验
        /// </summary>
        /// <param name="role">角色</param>
        protected override Task<IdentityResult> ValidateRoleAsync(Role role) => Task.FromResult(IdentityResult.Success);
    }
}
