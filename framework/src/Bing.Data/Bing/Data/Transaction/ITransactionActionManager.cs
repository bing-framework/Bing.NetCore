using System.Data;
using Bing.Aspects;
using Bing.DependencyInjection;

namespace Bing.Data.Transaction;

/// <summary>
/// 管理与当前事务完成阶段关联的异步操作。
/// </summary>
[IgnoreAspect]
public interface ITransactionActionManager : IScopedDependency
{
    /// <summary>
    /// 获取当前作用域已登记的事务操作数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 注册在事务完成阶段执行的异步操作。
    /// </summary>
    /// <param name="action">接收当前数据库事务并执行的异步操作。</param>
    void Register(Func<IDbTransaction, Task> action);

    /// <summary>
    /// 使用指定事务执行已登记的操作。
    /// </summary>
    /// <param name="transaction">传递给已登记操作的数据库事务。</param>
    Task CommitAsync(IDbTransaction transaction);
}
