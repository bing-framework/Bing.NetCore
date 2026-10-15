using System.Data;

namespace Bing.Data.Transaction;

/// <summary>
/// 事务操作管理器
/// </summary>
public class TransactionActionManager : ITransactionActionManager
{
    /// <summary>
    /// 单次提交允许的最大回调轮数。
    /// </summary>
    private const int MaxCommitRounds = 32;

    /// <summary>
    /// 待执行的事务操作。
    /// </summary>
    private readonly List<Func<IDbTransaction, Task>> _actions;

    /// <summary>
    /// 保护待执行操作和提交状态的同步对象。
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// 是否正在执行提交回调。
    /// </summary>
    private bool _isCommitting;

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_sync)
                return _actions.Count;
        }
    }

    /// <summary>
    /// 初始化一个 <see cref="TransactionActionManager" /> 类型的实例。
    /// </summary>
    public TransactionActionManager() => _actions = new List<Func<IDbTransaction, Task>>();

    /// <inheritdoc />
    public void Register(Func<IDbTransaction, Task> action)
    {
        if (action == null)
            return;
        lock (_sync)
            _actions.Add(action);
    }

    /// <inheritdoc />
    public async Task CommitAsync(IDbTransaction transaction)
    {
        lock (_sync)
        {
            if (_isCommitting)
                throw new InvalidOperationException("事务操作正在提交，不能重复提交。");
            _isCommitting = true;
        }

        try
        {
            for (var round = 0; round < MaxCommitRounds; round++)
            {
                Func<IDbTransaction, Task>[] batch;
                lock (_sync)
                {
                    if (_actions.Count == 0)
                    {
                        _isCommitting = false;
                        return;
                    }
                    batch = _actions.ToArray();
                    _actions.Clear();
                }

                foreach (var action in batch)
                    await action(transaction);
            }

            lock (_sync)
            {
                if (_actions.Count == 0)
                {
                    _isCommitting = false;
                    return;
                }
            }
            throw new InvalidOperationException($"事务操作超过 {MaxCommitRounds} 轮，可能存在循环登记。");
        }
        catch
        {
            lock (_sync)
            {
                _actions.Clear();
                _isCommitting = false;
            }
            throw;
        }
    }
}
