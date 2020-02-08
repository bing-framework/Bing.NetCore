using System.Threading.Tasks;
using Bing.Domains.Services;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Samples.Domain.Models;
using Bing.Samples.Domain.Repositories;
using Bing.Samples.Domain.Services.Abstractions;
using Bing.Extensions;

namespace Bing.Samples.Domain.Services.Implements
{
    /// <summary>
    /// 初始化角色服务
    /// </summary>
    public class RoleManager : DomainServiceBase, IRoleManager
    {
        /// <summary>
        /// 初始化角色服务
        /// </summary>
        /// <param name="repository">角色仓储</param>
        public RoleManager(IRoleRepository repository)
        {
            Repository = repository;
        }

        /// <summary>
        /// 角色仓储
        /// </summary>
        private IRoleRepository Repository { get; }

        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="role">角色</param>
        public virtual async Task CreateAsync(Role role)
        {
            await ValidateCreate(role);
            role.Init();
            var parent = await Repository.FindAsync(role.ParentId);
            role.InitPath(parent);
            role.SortId = await Repository.GenerateSortIdAsync(role.ParentId);
            await Repository.AddAsync(role);
        }

        /// <summary>
        /// 创建角色验证
        /// </summary>
        /// <param name="role">角色</param>
        protected virtual async Task ValidateCreate(Role role)
        {
            role.CheckNull(nameof(role));
            if (await Repository.ExistsAsync(t => t.Code == role.Code))
                ThrowDuplicateCodeException(role.Code);
        }

        /// <summary>
        /// 抛出编码重复异常
        /// </summary>
        protected void ThrowDuplicateCodeException(string code)
        {
            throw new Warning($"角色编码 {code} 已存在");
        }

        /// <summary>
        /// 修改角色
        /// </summary>
        public async Task UpdateAsync(Role role)
        {
            role.CheckNull(nameof(role));
            role.InitPinYin();
            await Repository.UpdatePathAsync(role);
            await Repository.UpdateAsync(role);
        }
    }
}
