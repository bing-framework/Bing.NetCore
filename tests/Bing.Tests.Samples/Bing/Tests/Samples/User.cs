using System;
using Bing.Domain.Entities;

namespace Bing.Tests.Samples;

/// <summary>
/// 用户
/// </summary>
public class User:AggregateRoot<User>
{
    /// <summary>
    /// 初始化一个<see cref="User"/>类型的实例
    /// </summary>
    public User() : this(Guid.Empty) { }

    /// <summary>
    /// 初始化一个<see cref="User"/>类型的实例
    /// </summary>
    /// <param name="id">标识</param>
    public User(Guid id) : base(id)
    {
    }
}