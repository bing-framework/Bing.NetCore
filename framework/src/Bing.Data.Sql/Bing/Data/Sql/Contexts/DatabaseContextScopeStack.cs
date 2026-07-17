using System.Runtime.CompilerServices;

namespace Bing.Data.Sql;

/// <summary>
/// 数据库上下文作用域帧栈。
/// </summary>
internal static class DatabaseContextScopeStack
{
    /// <summary>
    /// 按上下文访问器保存的作用域栈。
    /// </summary>
    private static readonly ConditionalWeakTable<IDatabaseContextAccessor, ScopeStack> Stacks = new();

    /// <summary>
    /// 进入数据库上下文作用域。
    /// </summary>
    /// <param name="accessor">数据库上下文访问器。</param>
    /// <param name="context">目标数据库上下文。</param>
    /// <param name="snapshotFactory">数据库上下文快照工厂。</param>
    /// <returns>数据库上下文作用域。</returns>
    public static IDatabaseScope Enter(IDatabaseContextAccessor accessor, DatabaseContext context,
        IDatabaseContextSnapshotFactory snapshotFactory)
    {
        if (accessor == null)
            throw new ArgumentNullException(nameof(accessor));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        var stack = Stacks.GetValue(accessor, _ => new ScopeStack());
        return stack.Enter(accessor, context, snapshotFactory ?? new DefaultDatabaseContextSnapshotFactory());
    }

    /// <summary>
    /// 单个数据库上下文访问器的异步作用域栈。
    /// </summary>
    private sealed class ScopeStack
    {
        /// <summary>
        /// 当前异步执行流的栈顶帧。
        /// </summary>
        private readonly AsyncLocal<ScopeFrame> _current = new();

        /// <summary>
        /// 进入新的作用域帧。
        /// </summary>
        /// <param name="accessor">数据库上下文访问器。</param>
        /// <param name="context">目标数据库上下文。</param>
        /// <param name="snapshotFactory">数据库上下文快照工厂。</param>
        /// <returns>数据库上下文作用域。</returns>
        public IDatabaseScope Enter(IDatabaseContextAccessor accessor, DatabaseContext context,
            IDatabaseContextSnapshotFactory snapshotFactory)
        {
            var parent = _current.Value;
            var parentContext = parent?.Context ?? accessor.Current;
            var frame = new ScopeFrame(Guid.NewGuid(), parent, snapshotFactory.Create(parentContext),
                snapshotFactory.Create(context));
            _current.Value = frame;
            accessor.Current = frame.Context;
            return new Scope(accessor, this, frame, snapshotFactory);
        }

        /// <summary>
        /// 按严格 LIFO 顺序退出作用域帧。
        /// </summary>
        /// <param name="accessor">数据库上下文访问器。</param>
        /// <param name="frame">待退出的作用域帧。</param>
        /// <param name="snapshotFactory">数据库上下文快照工厂。</param>
        public void Exit(IDatabaseContextAccessor accessor, ScopeFrame frame,
            IDatabaseContextSnapshotFactory snapshotFactory)
        {
            var current = _current.Value;
            if (ReferenceEquals(current, frame) == false)
                throw CreateOutOfOrderException(frame, current);
            _current.Value = frame.Parent;
            accessor.Current = snapshotFactory.Create(frame.Parent?.Context ?? frame.ParentContext);
        }

        /// <summary>
        /// 创建作用域乱序释放异常。
        /// </summary>
        /// <param name="frame">待释放作用域帧。</param>
        /// <param name="current">当前栈顶作用域帧。</param>
        /// <returns>作用域乱序释放异常。</returns>
        private static InvalidOperationException CreateOutOfOrderException(ScopeFrame frame, ScopeFrame current)
        {
            var currentScopeId = current?.ScopeId.ToString() ?? "<无>";
            var currentDbKey = current?.Context?.DbKey ?? "<无>";
            var targetDbKey = frame?.Context?.DbKey ?? "<无>";
            return new InvalidOperationException(
                $"数据库作用域必须按 LIFO 顺序释放。当前作用域 ID: {frame?.ScopeId}。当前栈顶作用域 ID: {currentScopeId}。当前 dbKey: {currentDbKey}。目标 dbKey: {targetDbKey}。");
        }
    }

    /// <summary>
    /// 不可变数据库上下文作用域帧。
    /// </summary>
    private sealed class ScopeFrame
    {
        /// <summary>
        /// 初始化一个<see cref="ScopeFrame"/>类型的实例。
        /// </summary>
        /// <param name="scopeId">作用域标识。</param>
        /// <param name="parent">父级作用域帧。</param>
        /// <param name="parentContext">父级数据库上下文快照。</param>
        /// <param name="context">当前数据库上下文快照。</param>
        public ScopeFrame(Guid scopeId, ScopeFrame parent, DatabaseContext parentContext, DatabaseContext context)
        {
            ScopeId = scopeId;
            Parent = parent;
            ParentContext = parentContext;
            Context = context;
        }

        /// <summary>
        /// 作用域标识。
        /// </summary>
        public Guid ScopeId { get; }

        /// <summary>
        /// 父级作用域帧。
        /// </summary>
        public ScopeFrame Parent { get; }

        /// <summary>
        /// 父级数据库上下文快照。
        /// </summary>
        public DatabaseContext ParentContext { get; }

        /// <summary>
        /// 当前数据库上下文快照。
        /// </summary>
        public DatabaseContext Context { get; }
    }

    /// <summary>
    /// 数据库上下文作用域。
    /// </summary>
    private sealed class Scope : IDatabaseScope
    {
        /// <summary>
        /// 数据库上下文访问器。
        /// </summary>
        private readonly IDatabaseContextAccessor _accessor;

        /// <summary>
        /// 作用域帧栈。
        /// </summary>
        private readonly ScopeStack _stack;

        /// <summary>
        /// 当前作用域帧。
        /// </summary>
        private readonly ScopeFrame _frame;

        /// <summary>
        /// 数据库上下文快照工厂。
        /// </summary>
        private readonly IDatabaseContextSnapshotFactory _snapshotFactory;

        /// <summary>
        /// 是否已释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个<see cref="Scope"/>类型的实例。
        /// </summary>
        /// <param name="accessor">数据库上下文访问器。</param>
        /// <param name="stack">作用域帧栈。</param>
        /// <param name="frame">当前作用域帧。</param>
        /// <param name="snapshotFactory">数据库上下文快照工厂。</param>
        public Scope(IDatabaseContextAccessor accessor, ScopeStack stack, ScopeFrame frame,
            IDatabaseContextSnapshotFactory snapshotFactory)
        {
            _accessor = accessor;
            _stack = stack;
            _frame = frame;
            _snapshotFactory = snapshotFactory;
        }

        /// <summary>
        /// 释放作用域并恢复父级数据库上下文。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _stack.Exit(_accessor, _frame, _snapshotFactory);
            _disposed = true;
        }
    }
}