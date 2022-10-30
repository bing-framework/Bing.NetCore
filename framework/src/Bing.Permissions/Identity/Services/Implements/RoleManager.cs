using System.Threading.Tasks;
using Bing.Domain.Services;
using Bing.Exceptions;
using Bing.Extensions;
using Bing;
using Bing.Permissions.Identity.Extensions;
using Bing.Permissions.Identity.Models;
using Bing.Permissions.Identity.Repositories;
using Bing.Permissions.Identity.Services.Abstractions;
using Bing.Permissions.Properties;
using Microsoft.AspNetCore.Identity;

namespace Bing.Permissions.Identity.Services.Implements;

/// <summary>
/// 角色管理
/// </summary>
/// <typeparam name="TRole">角色类型</typeparam>
/// <typeparam name="TKey">角色标识类型</typeparam>
/// <typeparam name="TParentId">角色父标识类型</typeparam>
public abstract class RoleManager<TRole, TKey, TParentId> : DomainServiceBase, IRoleManager<TRole, TKey, TParentId> 
    where TRole : RoleBase<TRole, TKey, TParentId>
{
    /// <summary>
    /// Identity角色管理
    /// </summary>
    private RoleManager<TRole> Manager { get; }

    /// <summary>
    /// 角色仓储
    /// </summary>
    private IRoleRepository<TRole, TKey, TParentId> Repository { get; }

    /// <summary>
    /// 初始化一个<see cref="RoleManager{TRole,TKey,TParentId}"/>类型的实例
    /// </summary>
    /// <param name="roleManager">Identity角色管理</param>
    /// <param name="repository">角色仓储</param>
    protected RoleManager(RoleManager<TRole> roleManager, IRoleRepository<TRole, TKey, TParentId> repository)
    {
        Manager = roleManager;
        Repository = repository;
    }

    #region CreateAsync(创建角色)

    /// <summary>
    /// 创建角色
    /// </summary>
    /// <param name="role">角色</param>
    public virtual async Task CreateAsync(TRole role)
    {
        await ValidateCreate(role);
        role.Init();
        var parent = await Repository.FindAsync(role.ParentId);
        role.InitPath(parent);
        role.SortId = await Repository.GenerateSortIdAsync(role.ParentId);
        var result = await Manager.CreateAsync(role);
        result.ThrowIfError();
    }

    /// <summary>
    /// 创建角色验证
    /// </summary>
    /// <param name="role">角色</param>
    protected virtual async Task ValidateCreate(TRole role)
    {
        role.CheckNotNull(nameof(role));
        if (await Repository.ExistsAsync(t => t.Code == role.Code))
            ThrowDuplicateCodeException(role.Code);
    }

    /// <summary>
    /// 抛出编码重复异常
    /// </summary>
    /// <param name="code">角色编码</param>
    /// <exception cref="Warning">应用程序异常</exception>
    protected virtual void ThrowDuplicateCodeException(string code) =>
        throw new Warning(string.Format(SecurityResources.DuplicateRoleCode, code));

    /// <summary>
    /// 抛出名称重复异常
    /// </summary>
    /// <param name="name">角色名称</param>
    /// <exception cref="Warning">应用程序异常</exception>
    protected virtual void ThrowDuplicateNameException(string name) =>
        throw new Warning(string.Format(Bing.Permissions.Properties.SecurityResources.DuplicateRoleName, name));

    #endregion

    #region UpdateAsync(修改角色)

    /// <summary>
    /// 修改角色
    /// </summary>
    /// <param name="role">角色</param>
    public virtual async Task UpdateAsync(TRole role)
    {
        role.CheckNotNull(nameof(role));
        await ValidateUpdate(role);
        role.InitPinYin();
        await Repository.UpdatePathAsync(role);
        var result = await Manager.UpdateAsync(role);
        result.ThrowIfError();
    }

    /// <summary>
    /// 修改角色验证
    /// </summary>
    /// <param name="role">角色</param>
    protected virtual Task ValidateUpdate(TRole role) => Task.CompletedTask;

    #endregion
}