using System.Threading;

namespace Bing.Uow;

/// <summary>
/// 读取当前工作单元执行 frame 的有效取消令牌。
/// </summary>
internal static class UnitOfWorkOperationCancellation
{
    /// <summary>
    /// 判断指定工作单元根作用域是否存在活动父级根作用域。
    /// </summary>
    /// <param name="manager">用于读取当前操作帧的工作单元管理器。</param>
    /// <param name="scope">要检查的根作用域。</param>
    /// <returns>存在不同于当前根作用域的活动父级时为 true；否则为 false。</returns>
    public static bool HasActiveParentRoot(IUnitOfWorkScopeManager manager, IUnitOfWorkScope scope)
    {
        if (manager is not UnitOfWorkScopeManager scopeManager || scope == null)
            return false;

        var frame = scopeManager.CurrentFrame;
        if (frame == null || !ReferenceEquals(frame.Root, scope))
            return false;

        for (var parent = frame.Parent; parent != null; parent = parent.Parent)
        {
            if (!ReferenceEquals(parent.Root, scope) && parent.Root.IsActive)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 获取工作单元管理器当前操作的取消令牌。
    /// </summary>
    /// <param name="manager">用于读取当前执行范围的工作单元管理器。</param>
    /// <returns>当前执行范围的取消令牌；没有活动执行范围时为 CancellationToken.None。</returns>
    public static CancellationToken GetCurrentToken(IUnitOfWorkScopeManager manager)
    {
        if (manager is UnitOfWorkScopeManager scopeManager)
            return scopeManager.CurrentExecutionCancellationToken;

        return manager?.Current is IUnitOfWorkExecutionScope executionScope
            ? executionScope.CancellationToken
            : CancellationToken.None;
    }

    /// <summary>
    /// 创建合并工作单元操作和调用方取消信号的租约。
    /// </summary>
    /// <param name="manager">用于读取当前操作取消令牌的工作单元管理器。</param>
    /// <param name="callerToken">调用方提供的取消令牌。</param>
    /// <returns>提供有效取消令牌并持有必要联结源的租约。</returns>
    /// <remarks>两个可取消且不同的令牌会创建联结源；否则复用可用令牌。</remarks>
    public static UnitOfWorkOperationCancellationLease CreateLease(IUnitOfWorkScopeManager manager,
        CancellationToken callerToken)
    {
        var operationToken = GetCurrentToken(manager);
        if (!operationToken.CanBeCanceled || operationToken == callerToken)
            return new UnitOfWorkOperationCancellationLease(callerToken, null);
        if (!callerToken.CanBeCanceled)
            return new UnitOfWorkOperationCancellationLease(operationToken, null);

        var source = CancellationTokenSource.CreateLinkedTokenSource(operationToken, callerToken);
        return new UnitOfWorkOperationCancellationLease(source.Token, source);
    }
}

/// <summary>
/// 持有当前操作与调用方取消令牌的合成资源。
/// </summary>
internal sealed class UnitOfWorkOperationCancellationLease : IDisposable
{
    /// <summary>
    /// 保存操作取消租约对应的取消源。
    /// </summary>
    private CancellationTokenSource _source;

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkOperationCancellationLease" /> 类型的实例。
    /// </summary>
    /// <param name="token">用于取消操作的令牌。</param>
    /// <param name="source">当前操作读取内容或状态的数据源。</param>
    internal UnitOfWorkOperationCancellationLease(CancellationToken token, CancellationTokenSource source)
    {
        Token = token;
        _source = source;
    }

    /// <summary>
    /// 获取当前操作的取消令牌。
    /// </summary>
    public CancellationToken Token { get; }

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref _source, null)?.Dispose();
}
