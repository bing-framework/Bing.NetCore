using System.Threading;

namespace Bing.Uow;

/// <summary>
/// 读取当前工作单元执行 frame 的有效取消令牌。
/// </summary>
internal static class UnitOfWorkOperationCancellation
{
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

    public static CancellationToken GetCurrentToken(IUnitOfWorkScopeManager manager)
    {
        if (manager is UnitOfWorkScopeManager scopeManager)
            return scopeManager.CurrentExecutionCancellationToken;

        return manager?.Current is IUnitOfWorkExecutionScope executionScope
            ? executionScope.CancellationToken
            : CancellationToken.None;
    }

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
    private CancellationTokenSource _source;

    internal UnitOfWorkOperationCancellationLease(CancellationToken token, CancellationTokenSource source)
    {
        Token = token;
        _source = source;
    }

    public CancellationToken Token { get; }

    public void Dispose() => Interlocked.Exchange(ref _source, null)?.Dispose();
}
