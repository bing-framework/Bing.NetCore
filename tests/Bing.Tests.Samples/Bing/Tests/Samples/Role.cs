using System;
using Bing.Domain.Entities;
using Bing.Domain.Repositories;

namespace Bing.Tests.Samples;

/// <summary>
/// 角色
/// </summary>
public class Role : TreeEntityBase<Role, Guid, Guid?>
{
    /// <summary>
    /// 初始化一个<see cref="Role"/>类型的实例
    /// </summary>
    public Role() : this(Guid.Empty, "", 0)
    {
    }

    /// <summary>
    /// 初始化一个<see cref="Role"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    /// <param name="path">路径</param>
    /// <param name="level">级数</param>
    public Role(Guid id, string path, int level) : base(id, path, level)
    {
    }
}

/// <summary>
/// 角色仓储
/// </summary>
public interface IRoleRepository : ITreeRepository<Role> { }