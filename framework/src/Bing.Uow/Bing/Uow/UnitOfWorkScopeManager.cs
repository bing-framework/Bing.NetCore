using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Uow;

internal interface IUnitOfWorkRootStartingObserver
{
    void OnRootStarting(IUnitOfWorkScope scope, IUnitOfWorkScope parentScope);

    /// <summary>
    /// 在 root 正常退出当前异步执行上下文后释放该 root 的启动观察状态。
    /// </summary>
    void OnRootExited(IUnitOfWorkScope scope);

    /// <summary>
    /// 在 root 发布到当前异步执行上下文之前启动失败时清理该 root 的观察状态。
    /// </summary>
    void OnRootStartFailed(IUnitOfWorkScope scope);
}

/// <summary>
/// 为一个 DI scope 管理可沿异步调用传播的工作单元 frame 链。
/// </summary>
internal sealed class UnitOfWorkScopeManager : IUnitOfWorkOperationManager
{
    private readonly AsyncLocal<UnitOfWorkScopeFrame> _current = new();
    private readonly IServiceProvider _serviceProvider;

    public UnitOfWorkScopeManager(IServiceProvider serviceProvider) =>
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc />
    public IUnitOfWorkScope Current => _current.Value?.Root;

    /// <inheritdoc />
    public IUnitOfWorkScope Begin(UnitOfWorkScopeOptions options = null, bool requiresNew = false)
    {
        var parent = _current.Value;
        if (requiresNew || parent == null)
        {
            return BeginRoot(options, parent, CancellationToken.None, createExecutionScope: false);
        }

        parent.Root.EnsureCanParticipate(options);
        var participant = parent.Root.CreateParticipant(GetCurrentExecutionCancellationToken(parent),
            CancellationToken.None);
        var participantFrame = new UnitOfWorkScopeFrame(parent.Root, parent, participant);
        participant.AttachFrame(participantFrame);
        _current.Value = participantFrame;
        return participant;
    }

    /// <inheritdoc />
    public IUnitOfWorkExecutionScope BeginOperation(UnitOfWorkScopeOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var parent = _current.Value;
        if (parent != null)
        {
            parent.Root.EnsureCanParticipate(options);
            var participant = parent.Root.CreateParticipant(GetCurrentExecutionCancellationToken(parent),
                cancellationToken);
            var participantFrame = new UnitOfWorkScopeFrame(parent.Root, parent, participant);
            participant.AttachFrame(participantFrame);
            _current.Value = participantFrame;
            return participant;
        }

        return (IUnitOfWorkExecutionScope)BeginRoot(options, parent, cancellationToken,
            createExecutionScope: true);
    }

    private UnitOfWorkScope BeginRoot(UnitOfWorkScopeOptions options, UnitOfWorkScopeFrame parent,
        CancellationToken cancellationToken, bool createExecutionScope)
    {
        var snapshot = CloneAndValidateOptions(options ?? new UnitOfWorkScopeOptions());
        var cancellation = createExecutionScope
            ? CreateRootCancellation(cancellationToken, snapshot.Timeout)
            : RootCancellation.None;
        var savingEnabled = _serviceProvider.GetService<UnitOfWorkOperationsRegistrationMarker>() != null;
        var handlers = savingEnabled
            ? _serviceProvider.GetServices<IUnitOfWorkSaveChangesHandler>().ToArray()
            : Array.Empty<IUnitOfWorkSaveChangesHandler>();
        var root = new UnitOfWorkScope(this, snapshot, handlers, savingEnabled,
            cancellation.Token, cancellation.DeadlineSource, cancellation.LinkedSource);
        var frame = new UnitOfWorkScopeFrame(root, parent);
        root.AttachFrame(frame);
        if (savingEnabled && parent?.Root.IsActive == true)
        {
            var observers = _serviceProvider.GetServices<IUnitOfWorkRootStartingObserver>().ToArray();
            try
            {
                foreach (var observer in observers)
                    observer.OnRootStarting(root, parent.Root);
            }
            catch
            {
                foreach (var observer in observers)
                {
                    try { observer.OnRootStartFailed(root); }
                    catch { }
                }
                throw;
            }
        }
        _current.Value = frame;
        return root;
    }

    internal UnitOfWorkScopeFrame CurrentFrame => _current.Value;

    internal CancellationToken CurrentExecutionCancellationToken =>
        GetCurrentExecutionCancellationToken(_current.Value);

    private static CancellationToken GetCurrentExecutionCancellationToken(UnitOfWorkScopeFrame frame) =>
        frame?.Participant?.CancellationToken ?? frame?.Root.CancellationToken ?? CancellationToken.None;

    internal void ValidateExit(UnitOfWorkScopeFrame frame)
    {
        if (!ReferenceEquals(_current.Value, frame))
            throw new InvalidOperationException("工作单元范围必须按 LIFO 顺序退出当前异步执行上下文。");
    }

    internal void Exit(UnitOfWorkScopeFrame frame)
    {
        ValidateExit(frame);
        _current.Value = frame.Parent;
        if (frame.Participant == null)
            NotifyRootExited(frame.Root);
    }

    private void NotifyRootExited(UnitOfWorkScope root)
    {
        try
        {
            if (_serviceProvider.GetService<UnitOfWorkOperationsRegistrationMarker>() == null)
                return;

            foreach (var observer in _serviceProvider.GetServices<IUnitOfWorkRootStartingObserver>())
            {
                try { observer.OnRootExited(root); }
                catch { }
            }
        }
        catch
        {
            // 观察器清理不能覆盖 root 的资源释放结果。
        }
    }

    internal static UnitOfWorkScopeOptions CloneAndValidateOptions(UnitOfWorkScopeOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (!options.IsTransactional && options.IsolationLevel != null)
            throw new ArgumentException("非事务工作单元不能指定事务隔离级别。", nameof(options));
        if (options.IsolationLevel is System.Data.IsolationLevel.Chaos or System.Data.IsolationLevel.Unspecified)
            throw new ArgumentOutOfRangeException(nameof(options), "工作单元不支持 Chaos 或 Unspecified 隔离级别。");
        if (options.IsolationLevel != null && !Enum.IsDefined(typeof(System.Data.IsolationLevel), options.IsolationLevel.Value))
            throw new ArgumentOutOfRangeException(nameof(options), "工作单元隔离级别无效。");
        if (options.Timeout.HasValue &&
            (options.Timeout.Value <= TimeSpan.Zero || options.Timeout.Value > TimeSpan.FromMilliseconds(int.MaxValue)))
            throw new ArgumentOutOfRangeException(nameof(options), "工作单元超时必须大于零且处于所有目标运行时支持的计时范围内。");
        return new UnitOfWorkScopeOptions
        {
            IsTransactional = options.IsTransactional,
            IsolationLevel = options.IsolationLevel,
            Timeout = options.Timeout
        };
    }

    private static RootCancellation CreateRootCancellation(CancellationToken callerToken, TimeSpan? timeout)
    {
        if (!timeout.HasValue)
            return new RootCancellation(callerToken, null, null);

        var deadline = new CancellationTokenSource();
        deadline.CancelAfter(timeout.Value);
        if (!callerToken.CanBeCanceled)
            return new RootCancellation(deadline.Token, deadline, null);

        var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, deadline.Token);
        return new RootCancellation(linked.Token, deadline, linked);
    }

    internal static ParticipantCancellation CreateParticipantCancellation(CancellationToken rootToken,
        CancellationToken participantToken)
    {
        if (!participantToken.CanBeCanceled || participantToken == rootToken)
            return new ParticipantCancellation(rootToken, null);
        if (!rootToken.CanBeCanceled)
            return new ParticipantCancellation(participantToken, null);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(rootToken, participantToken);
        return new ParticipantCancellation(linked.Token, linked);
    }

    private readonly struct RootCancellation
    {
        public RootCancellation(CancellationToken token, CancellationTokenSource deadlineSource,
            CancellationTokenSource linkedSource)
        {
            Token = token;
            DeadlineSource = deadlineSource;
            LinkedSource = linkedSource;
        }

        public static RootCancellation None => new(CancellationToken.None, null, null);
        public CancellationToken Token { get; }
        public CancellationTokenSource DeadlineSource { get; }
        public CancellationTokenSource LinkedSource { get; }
    }

    internal readonly struct ParticipantCancellation
    {
        public ParticipantCancellation(CancellationToken token, CancellationTokenSource source)
        {
            Token = token;
            Source = source;
        }

        public CancellationToken Token { get; }
        public CancellationTokenSource Source { get; }
    }
}

/// <summary>
/// 不可变的异步执行上下文范围节点。
/// </summary>
internal sealed class UnitOfWorkScopeFrame
{
    public UnitOfWorkScopeFrame(UnitOfWorkScope root, UnitOfWorkScopeFrame parent,
        UnitOfWorkScopeParticipant participant = null)
    {
        Root = root;
        Parent = parent;
        Participant = participant;
    }

    public UnitOfWorkScope Root { get; }

    public UnitOfWorkScopeFrame Parent { get; }

    public UnitOfWorkScopeParticipant Participant { get; }
}
