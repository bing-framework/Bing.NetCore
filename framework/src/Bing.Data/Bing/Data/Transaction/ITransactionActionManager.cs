using System;
using System.Data;
using System.Threading.Tasks;
using Bing.Aspects;
using Bing.DependencyInjection;

namespace Bing.Data.Transaction;

/// <summary>
/// 事务操作管理器
/// </summary>
[Ignore]
public interface ITransactionActionManager : IScopedDependency
{
    /// <summary>
    /// 事务操作数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 注册事务操作
    /// </summary>
    /// <param name="action">事务操作</param>
    void Register(Func<IDbTransaction, Task> action);

    /// <summary>
    /// 提交
    /// </summary>
    /// <param name="transaction">事务</param>
    Task CommitAsync(IDbTransaction transaction);
}