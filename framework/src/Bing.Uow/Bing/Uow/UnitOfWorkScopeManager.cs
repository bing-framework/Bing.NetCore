using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Uow;

/// <summary>
/// 工作单元 root 启动观察器契约。
/// </summary>
/// <remarks>
/// 在嵌套 root 启动前、正常退出后和启动失败时通知观察方，用于维护与当前异步执行上下文绑定的作用域状态。
/// </remarks>
internal interface IUnitOfWorkRootStartingObserver
{
    /// <summary>
    /// 在嵌套 root 启动前通知观察方。
    /// </summary>
    /// <param name="scope">即将启动的 root 工作单元。</param>
    /// <param name="parentScope">当前异步执行上下文中的父 root 工作单元。</param>
    void OnRootStarting(IUnitOfWorkScope scope, IUnitOfWorkScope parentScope);

    /// <summary>
    /// 在 root 正常退出当前异步执行上下文后释放该 root 的启动观察状态。
    /// </summary>
    /// <param name="scope">已退出的 root 工作单元。</param>
    void OnRootExited(IUnitOfWorkScope scope);

    /// <summary>
    /// 在 root 发布到当前异步执行上下文之前启动失败时清理该 root 的观察状态。
    /// </summary>
    /// <param name="scope">启动失败的 root 工作单元。</param>
    void OnRootStartFailed(IUnitOfWorkScope scope);
}

/// <summary>
/// 为一个依赖注入作用域管理可沿异步调用传播的工作单元 frame 链。
/// </summary>
/// <remarks>
/// 使用 <see cref="AsyncLocal{T}" /> 保存当前 frame；根作用域结束时释放取消资源，参与者按 LIFO 顺序退出。
/// </remarks>
internal sealed class UnitOfWorkScopeManager : IUnitOfWorkOperationManager
{
    /// <summary>
    /// 当前异步执行上下文中的工作单元 frame。
    /// </summary>
    private readonly AsyncLocal<UnitOfWorkScopeFrame> _current = new();

    /// <summary>
    /// 用于解析保存变更处理器和启动观察器的当前依赖注入作用域服务提供程序。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkScopeManager" /> 类的新实例。
    /// </summary>
    /// <param name="serviceProvider">当前依赖注入作用域的服务提供程序。</param>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider" /> 为 <see langword="null" /> 时抛出。</exception>
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

    /// <summary>
    /// 在指定选项下创建新的 root 工作单元，并将 frame 发布到当前异步执行上下文。
    /// </summary>
    /// <param name="options">工作单元事务与超时选项；为 <see langword="null" /> 时使用默认选项。</param>
    /// <param name="parent">当前异步执行上下文中的父 frame，可为 <see langword="null" />。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <param name="createExecutionScope">是否创建带超时和取消边界的执行作用域。</param>
    /// <returns>已发布的 root 工作单元。</returns>
    /// <exception cref="ArgumentException">非事务工作单元指定隔离级别时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">隔离级别或超时无效时抛出。</exception>
    private UnitOfWorkScope BeginRoot(UnitOfWorkScopeOptions options, UnitOfWorkScopeFrame parent,
        CancellationToken cancellationToken, bool createExecutionScope)
    {
        var snapshot = CloneAndValidateOptions(options ?? new UnitOfWorkScopeOptions());
        var cancellation = createExecutionScope
            ? CreateRootCancellation(cancellationToken, snapshot.Timeout)
            : RootCancellation.None;
        try
        {
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
        catch (Exception error)
        {
            // root 尚未发布到当前异步上下文；启动失败时释放本次创建但未交接的取消资源。
            ReleaseUnhandedCancellation(cancellation, error);
            throw;
        }
    }

    /// <summary>
    /// 释放尚未交给 root 所有的取消资源，并保留原始启动异常。
    /// </summary>
    /// <param name="cancellation">本次启动创建的取消资源。</param>
    /// <param name="error">导致启动失败的主异常。</param>
    private static void ReleaseUnhandedCancellation(RootCancellation cancellation, Exception error)
    {
        var cleanupErrors = new List<Exception>();
        DisposeCancellationSource(cancellation.LinkedSource, cleanupErrors);
        DisposeCancellationSource(cancellation.DeadlineSource, cleanupErrors);
        if (cleanupErrors.Count == 1)
            error.Data["Bing.Uow.RootCancellationCleanupError"] = cleanupErrors[0];
        else if (cleanupErrors.Count > 1)
            error.Data["Bing.Uow.RootCancellationCleanupError"] = new AggregateException(cleanupErrors);
    }

    /// <summary>
    /// 释放取消资源并收集清理异常。
    /// </summary>
    /// <param name="source">待释放的取消资源。</param>
    /// <param name="cleanupErrors">清理异常收集列表。</param>
    private static void DisposeCancellationSource(CancellationTokenSource source, List<Exception> cleanupErrors)
    {
        if (source == null)
            return;
        try
        {
            source.Dispose();
        }
        catch (Exception error)
        {
            cleanupErrors.Add(error);
        }
    }

    /// <summary>
    /// 获取当前异步执行上下文中的 frame。
    /// </summary>
    internal UnitOfWorkScopeFrame CurrentFrame => _current.Value;

    /// <summary>
    /// 获取当前 frame 参与者或 root 的取消令牌；无活动范围时返回 <see cref="CancellationToken.None" />。
    /// </summary>
    internal CancellationToken CurrentExecutionCancellationToken =>
        GetCurrentExecutionCancellationToken(_current.Value);

    /// <summary>
    /// 解析指定 frame 当前应使用的取消令牌。
    /// </summary>
    /// <param name="frame">待解析的 frame，可为 <see langword="null" />。</param>
    /// <returns>参与者令牌、root 令牌或 <see cref="CancellationToken.None" />。</returns>
    private static CancellationToken GetCurrentExecutionCancellationToken(UnitOfWorkScopeFrame frame) =>
        frame?.Participant?.CancellationToken ?? frame?.Root.CancellationToken ?? CancellationToken.None;

    /// <summary>
    /// 校验指定 frame 仍位于当前异步执行上下文的栈顶。
    /// </summary>
    /// <param name="frame">准备退出的 frame。</param>
    /// <exception cref="InvalidOperationException">frame 不是当前执行上下文中的最新节点时抛出。</exception>
    internal void ValidateExit(UnitOfWorkScopeFrame frame)
    {
        if (!ReferenceEquals(_current.Value, frame))
            throw new InvalidOperationException("工作单元范围必须按 LIFO 顺序退出当前异步执行上下文。");
    }

    /// <summary>
    /// 退出指定 frame，并在 root 退出时通知启动观察器。
    /// </summary>
    /// <param name="frame">准备退出的 frame。</param>
    /// <exception cref="InvalidOperationException">frame 不是当前执行上下文中的最新节点时抛出。</exception>
    internal void Exit(UnitOfWorkScopeFrame frame)
    {
        ValidateExit(frame);
        _current.Value = frame.Parent;
        if (frame.Participant == null)
            NotifyRootExited(frame.Root);
    }

    /// <summary>
    /// 通知启动观察器当前 root 已退出，并隔离观察器异常。
    /// </summary>
    /// <param name="root">已退出的 root 工作单元。</param>
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

    /// <summary>
    /// 克隆并校验工作单元选项，避免调用方后续修改影响已启动的范围。
    /// </summary>
    /// <param name="options">待克隆的选项。</param>
    /// <returns>独立的工作单元选项副本。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> 为 <see langword="null" /> 时抛出。</exception>
    /// <exception cref="ArgumentException">非事务工作单元指定隔离级别时抛出。</exception>
    /// <exception cref="ArgumentOutOfRangeException">隔离级别或超时无效时抛出。</exception>
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

    /// <summary>
    /// 为 root 执行作用域创建调用方令牌、截止时间和链接令牌资源。
    /// </summary>
    /// <param name="callerToken">调用方传入的取消令牌。</param>
    /// <param name="timeout">可选的工作单元超时。</param>
    /// <returns>root 的取消令牌及其需要释放的令牌源。</returns>
    private static RootCancellation CreateRootCancellation(CancellationToken callerToken, TimeSpan? timeout)
    {
        if (!timeout.HasValue)
            return new RootCancellation(callerToken, null, null);

        var deadline = new CancellationTokenSource();
        try
        {
            deadline.CancelAfter(timeout.Value);
            if (!callerToken.CanBeCanceled)
                return new RootCancellation(deadline.Token, deadline, null);

            var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, deadline.Token);
            return new RootCancellation(linked.Token, deadline, linked);
        }
        catch
        {
            // 创建 linked/deadline 过程中已取得的资源必须在抛出前释放。
            deadline.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 为参与者创建与 root 令牌组合的取消资源。
    /// </summary>
    /// <param name="rootToken">root 工作单元令牌。</param>
    /// <param name="participantToken">参与者调用方令牌。</param>
    /// <returns>参与者的取消令牌及其需要释放的链接令牌源。</returns>
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

    /// <summary>
    /// 保存 root 工作单元令牌及其需要释放的取消资源。
    /// </summary>
    private readonly struct RootCancellation
    {
        /// <summary>
        /// 初始化一个 <see cref="RootCancellation" /> 结构的新实例。
        /// </summary>
        /// <param name="token">root 工作单元使用的取消令牌。</param>
        /// <param name="deadlineSource">超时截止时间令牌源，可为 <see langword="null" />。</param>
        /// <param name="linkedSource">调用方与截止时间组合的令牌源，可为 <see langword="null" />。</param>
        public RootCancellation(CancellationToken token, CancellationTokenSource deadlineSource,
            CancellationTokenSource linkedSource)
        {
            Token = token;
            DeadlineSource = deadlineSource;
            LinkedSource = linkedSource;
        }

        /// <summary>
        /// 获取不取消、无附加资源的空取消状态。
        /// </summary>
        public static RootCancellation None => new(CancellationToken.None, null, null);

        /// <summary>
        /// 获取 root 工作单元使用的取消令牌。
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>
        /// 获取超时截止时间令牌源；无超时时为 <see langword="null" />。
        /// </summary>
        public CancellationTokenSource DeadlineSource { get; }

        /// <summary>
        /// 获取调用方与截止时间组合的令牌源；无需组合时为 <see langword="null" />。
        /// </summary>
        public CancellationTokenSource LinkedSource { get; }
    }

    /// <summary>
    /// 保存参与者取消令牌及其需要释放的链接令牌源。
    /// </summary>
    internal readonly struct ParticipantCancellation
    {
        /// <summary>
        /// 初始化一个 <see cref="ParticipantCancellation" /> 结构的新实例。
        /// </summary>
        /// <param name="token">参与者使用的取消令牌。</param>
        /// <param name="source">需要释放的链接令牌源，可为 <see langword="null" />。</param>
        public ParticipantCancellation(CancellationToken token, CancellationTokenSource source)
        {
            Token = token;
            Source = source;
        }

        /// <summary>
        /// 获取参与者使用的取消令牌。
        /// </summary>
        public CancellationToken Token { get; }

        /// <summary>
        /// 获取需要释放的链接令牌源；未创建链接时为 <see langword="null" />。
        /// </summary>
        public CancellationTokenSource Source { get; }
    }
}

/// <summary>
/// 不可变的异步执行上下文范围节点。
/// </summary>
internal sealed class UnitOfWorkScopeFrame
{
    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkScopeFrame" /> 类的新实例。
    /// </summary>
    /// <param name="root">当前 frame 所属的 root 工作单元。</param>
    /// <param name="parent">上一级 frame，可为 <see langword="null" />。</param>
    /// <param name="participant">从父 root 创建的参与者，可为 <see langword="null" /> 表示 root 节点。</param>
    public UnitOfWorkScopeFrame(UnitOfWorkScope root, UnitOfWorkScopeFrame parent,
        UnitOfWorkScopeParticipant participant = null)
    {
        Root = root;
        Parent = parent;
        Participant = participant;
    }

    /// <summary>
    /// 获取当前 frame 所属的 root 工作单元。
    /// </summary>
    public UnitOfWorkScope Root { get; }

    /// <summary>
    /// 获取上一级 frame；根节点为 <see langword="null" />。
    /// </summary>
    public UnitOfWorkScopeFrame Parent { get; }

    /// <summary>
    /// 获取当前 frame 的参与者；root 节点为 <see langword="null" />。
    /// </summary>
    public UnitOfWorkScopeParticipant Participant { get; }
}
