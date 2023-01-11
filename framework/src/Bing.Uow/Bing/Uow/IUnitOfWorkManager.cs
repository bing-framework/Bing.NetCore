﻿using Bing.DependencyInjection;

namespace Bing.Uow;

/// <summary>
/// 工作单元管理器
/// </summary>
public interface IUnitOfWorkManager : IScopedDependency
{
    /// <summary>
    /// 提交
    /// </summary>
    void Commit();

    /// <summary>
    /// 提交
    /// </summary>
    Task CommitAsync();

    /// <summary>
    /// 注册工作单元
    /// </summary>
    /// <param name="unitOfWork">工作单元</param>
    void Register(IUnitOfWork unitOfWork);

    /// <summary>
    /// 获取工作单元集合
    /// </summary>
    IReadOnlyCollection<IUnitOfWork> GetUnitOfWorks();
}
