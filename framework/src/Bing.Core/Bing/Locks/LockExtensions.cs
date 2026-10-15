namespace Bing.Locks;

/// <summary>
/// 业务锁(<see cref="ILock"/>) 扩展
/// </summary>
public static class LockExtensions
{
    /// <summary>
    /// 保存当前异步上下文中仍持有的便捷锁租约。
    /// </summary>
    /// <remarks>
    /// 执行上下文切换时复制容器，避免由同一父执行上下文派生的并发任务共享可变集合。
    /// </remarks>
    private static readonly System.Threading.AsyncLocal<OwnerContext> Owners =
        new(OnOwnerContextChanged);

    /// <summary>
    /// 保存当前异步流中的租约映射。
    /// </summary>
    private sealed class OwnerContext
    {
        /// <summary>
        /// 保护租约映射。
        /// </summary>
        internal readonly object SyncRoot = new object();

        /// <summary>
        /// 保存锁实例和键对应的持有者。
        /// </summary>
        internal readonly Dictionary<LockOwnerKey, string> Values;

        /// <summary>
        /// 初始化一个 <see cref="OwnerContext" /> 类型的实例。
        /// </summary>
        /// <remarks>
        /// 初始化空的租约映射。
        /// </remarks>
        internal OwnerContext() => Values = new Dictionary<LockOwnerKey, string>();

        /// <summary>
        /// 初始化一个 <see cref="OwnerContext" /> 类型的实例。
        /// </summary>
        /// <remarks>
        /// 复制已有的租约映射。
        /// </remarks>
        internal OwnerContext(OwnerContext source)
        {
            lock (source.SyncRoot)
                Values = new Dictionary<LockOwnerKey, string>(source.Values);
        }
    }

    /// <summary>
    /// 在异步执行上下文切换到子流时隔离租约映射。
    /// </summary>
    private static void OnOwnerContextChanged(
        System.Threading.AsyncLocalValueChangedArgs<OwnerContext> args)
    {
        if (!args.ThreadContextChanged || args.CurrentValue == null ||
            !ReferenceEquals(args.CurrentValue, args.PreviousValue))
            return;
        Owners.Value = new OwnerContext(args.CurrentValue);
    }

    /// <summary>
    /// 以锁实例引用和锁键组成便捷入口的租约索引。
    /// </summary>
    private readonly struct LockOwnerKey : IEquatable<LockOwnerKey>
    {
        /// <summary>
        /// 初始化一个 <see cref="LockOwnerKey" /> 类型的实例。
        /// </summary>
        /// <remarks>
        /// 初始化锁租约索引。
        /// </remarks>
        public LockOwnerKey(ILock @lock, string key)
        {
            Lock = @lock;
            Key = key;
        }

        /// <summary>
        /// 锁实例。
        /// </summary>
        private ILock Lock { get; }

        /// <summary>
        /// 锁键。
        /// </summary>
        private string Key { get; }

        /// <inheritdoc />
        public bool Equals(LockOwnerKey other) => ReferenceEquals(Lock, other.Lock) &&
            string.Equals(Key, other.Key, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is LockOwnerKey other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Lock) * 397) ^
            StringComparer.Ordinal.GetHashCode(Key);
    }

    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">等待超时时间。</param>
    /// <returns>true:成功锁定; false:之前已被锁定</returns>
    public static bool LockTak(this ILock @lock, string key, TimeSpan expiration)
    {
        var owner = CreateOwner();
        if (!@lock.LockTake(key, owner, expiration))
            return false;
        SetOwner(GetOrCreateOwnerContext(), @lock, key, owner);
        return true;
    }

    /// <summary>
    /// 获取一个锁（需要自己释放）
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">等待超时时间。</param>
    /// <returns>true:成功锁定; false:之前已被锁定</returns>
    public static Task<bool> LockTakAsync(this ILock @lock, string key, TimeSpan expiration)
    {
        return LockTakAsyncCore(@lock, key, expiration, GetOrCreateOwnerContext());
    }

    /// <summary>
    /// 异步获取锁并记录租约持有者。
    /// </summary>
    private static async Task<bool> LockTakAsyncCore(ILock @lock, string key, TimeSpan expiration,
        OwnerContext context)
    {
        var owner = CreateOwner();
        if (!await @lock.LockTakeAsync(key, owner, expiration).ConfigureAwait(false))
            return false;
        SetOwner(context, @lock, key, owner);
        return true;
    }

    /// <summary>
    /// 释放一个锁
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <returns>true:释放成功; false:释放失败</returns>
    public static bool LockRelease(this ILock @lock, string key)
    {
        var context = Owners.Value;
        if (!TryGetOwner(context, @lock, key, out var owner))
            return false;
        var released = @lock.LockRelease(key, owner);
        RemoveOwner(context, @lock, key);
        return released;
    }

    /// <summary>
    /// 释放一个锁
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <returns>true:释放成功; false:释放失败</returns>
    public static async Task<bool> LockReleaseAsync(this ILock @lock, string key)
    {
        var context = Owners.Value;
        if (!TryGetOwner(context, @lock, key, out var owner))
            return false;
        var released = await @lock.LockReleaseAsync(key, owner).ConfigureAwait(false);
        RemoveOwner(context, @lock, key);
        return released;
    }

    /// <summary>
    /// 通过独立租约提供程序获取锁租约。
    /// </summary>
    /// <param name="lock">业务锁。</param>
    /// <param name="key">锁键。</param>
    /// <param name="waitTimeout">最长等待时间。</param>
    /// <param name="leaseDuration">分布式锁租期；本地锁应传入 <see langword="null"/> 或无限时长。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功时返回唯一租约。</returns>
    public static Task<ILockLease> TryAcquireLeaseAsync(this ILock @lock, string key, TimeSpan waitTimeout,
        TimeSpan? leaseDuration = null, CancellationToken cancellationToken = default)
    {
        if (@lock is ILockLeaseProvider provider)
            return provider.TryAcquireLeaseAsync(key, waitTimeout, leaseDuration, cancellationToken);
        throw new NotSupportedException($"锁实现 {@lock?.GetType().FullName ?? "null"} 未提供租约能力。");
    }

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    public static void ExecuteWithLock(this ILock @lock, string key, TimeSpan expiration, Action executeAction) =>
        @lock.ExecuteWithLock(key, CreateOwner(), expiration, executeAction);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    public static async Task ExecuteWithLockAsync(this ILock @lock, string key, TimeSpan expiration, Func<Task> executeAction) => 
        await @lock.ExecuteWithLockAsync(key, CreateOwner(), expiration, executeAction);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <typeparam name="T">返回对象类型</typeparam>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>锁执行结果；未获取到锁或执行方法为空时返回默认值。</returns>
    public static T ExecuteWithLock<T>(this ILock @lock, string key, TimeSpan expiration, Func<T> executeAction, T defaultValue = default) => 
        @lock.ExecuteWithLock(key, CreateOwner(), expiration, executeAction, defaultValue);

    /// <summary>
    /// 使用锁执行一个方法
    /// </summary>
    /// <typeparam name="T">返回对象类型</typeparam>
    /// <param name="lock">业务锁</param>
    /// <param name="key">锁定标识</param>
    /// <param name="expiration">锁定时间间隔</param>
    /// <param name="executeAction">执行的方法</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>表示锁执行结果的异步操作；未获取到锁或执行方法为空时返回默认值。</returns>
    public static async Task<T> ExecuteWithLockAsync<T>(this ILock @lock, string key, TimeSpan expiration, Func<Task<T>> executeAction, T defaultValue = default) =>
        await @lock.ExecuteWithLockAsync(key, CreateOwner(), expiration, executeAction, defaultValue);

    /// <summary>
    /// 为一次自动释放的执行调用生成唯一持有者。
    /// </summary>
    /// <returns>不会与其他执行调用复用的持有者值。</returns>
    private static string CreateOwner() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// 记录当前异步流中的租约持有者。
    /// </summary>
    private static OwnerContext GetOrCreateOwnerContext()
    {
        if (Owners.Value == null)
            Owners.Value = new OwnerContext();
        return Owners.Value;
    }

    /// <summary>
    /// 记录当前异步流中的租约持有者。
    /// </summary>
    private static void SetOwner(OwnerContext context, ILock @lock, string key, string owner)
    {
        if (context == null)
            return;
        lock (context.SyncRoot)
            context.Values[new LockOwnerKey(@lock, key)] = owner;
    }

    /// <summary>
    /// 读取当前异步上下文中的租约持有者。
    /// </summary>
    private static bool TryGetOwner(OwnerContext context, ILock @lock, string key, out string owner)
    {
        owner = null;
        if (context == null)
            return false;
        lock (context.SyncRoot)
            return context.Values.TryGetValue(new LockOwnerKey(@lock, key), out owner);
    }

    /// <summary>
    /// 移除当前异步上下文中的租约持有者。
    /// </summary>
    private static void RemoveOwner(OwnerContext context, ILock @lock, string key)
    {
        if (context == null)
            return;
        bool empty;
        lock (context.SyncRoot)
        {
            context.Values.Remove(new LockOwnerKey(@lock, key));
            empty = context.Values.Count == 0;
        }
        if (empty && ReferenceEquals(Owners.Value, context))
            Owners.Value = null;
    }
}
