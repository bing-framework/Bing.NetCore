using System.Data;

namespace Bing.Data.Transaction;

/// <summary>
/// 事务操作管理器
/// </summary>
public class TransactionActionManager : ITransactionActionManager
{
    /// <summary>
    /// 操作列表
    /// </summary>
    private readonly List<Func<IDbTransaction, Task>> _actions;

    /// <inheritdoc />
    public int Count => _actions.Count;

    /// <summary>
    /// 初始化一个 <see cref="TransactionActionManager"/> 类型的实例。
    /// </summary>
    public TransactionActionManager() => _actions = new List<Func<IDbTransaction, Task>>();

    /// <inheritdoc />
    public void Register(Func<IDbTransaction, Task> action)
    {
        if (action == null)
            return;
        _actions.Add(action);
    }

    /// <inheritdoc />
    public async Task CommitAsync(IDbTransaction transaction)
    {
        foreach (var action in _actions)
            await action(transaction);
        _actions.Clear();
    }
}