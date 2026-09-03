namespace Bing.Data.Sql;

/// <summary>
/// Dapper SQL 事务作用域执行租约契约。
/// </summary>
internal interface ISqlTransactionScopeLease
{
    /// <summary>
    /// 事务作用域标识。
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// 当前事务最近一次异步事务操作的执行模式。
    /// </summary>
    string ExecutionMode { get; }

    /// <summary>
    /// 更新当前事务最近一次异步事务操作的执行模式。
    /// </summary>
    /// <param name="mode">实际执行模式。</param>
    void SetExecutionMode(SqlTransactionExecutionMode mode);

    /// <summary>
    /// 确保作用域仍处于活动状态。
    /// </summary>
    void EnsureActive();

    /// <summary>
    /// 获取当前事务作用域的一次执行租约。
    /// </summary>
    /// <returns>操作结束时必须释放的执行租约。</returns>
    IDisposable AcquireExecutionLease();
}

/// <summary>
/// Dapper SQL 事务作用域执行租约。
/// </summary>
internal sealed class SqlTransactionScopeLease : ISqlTransactionScopeLease
{
    /// <summary>
    /// 保护活动状态与执行计数的同步锁。
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    /// 指示事务作用域是否仍可开始新执行。
    /// </summary>
    private int _isActive = 1;

    /// <summary>
    /// 当前已开始但尚未结束的执行数量。
    /// </summary>
    private int _activeExecutionCount;

    /// <summary>
    /// 当前事务最近一次异步事务操作的执行模式。
    /// </summary>
    private string _executionMode;

    /// <summary>
    /// 初始化一个 <see cref="SqlTransactionScopeLease"/> 类型的实例。
    /// </summary>
    /// <param name="transactionId">事务作用域标识。</param>
    /// <param name="mode">实际异步执行模式。</param>
    public SqlTransactionScopeLease(string transactionId, SqlTransactionExecutionMode mode)
    {
        TransactionId = transactionId;
        _executionMode = ToDiagnosticValue(mode);
    }

    /// <inheritdoc />
    public string TransactionId { get; }

    /// <inheritdoc />
    public string ExecutionMode => Volatile.Read(ref _executionMode);

    /// <inheritdoc />
    public void SetExecutionMode(SqlTransactionExecutionMode mode) =>
        Volatile.Write(ref _executionMode, ToDiagnosticValue(mode));

    /// <summary>
    /// 将内部事务执行模式转换为稳定诊断值。
    /// </summary>
    /// <param name="mode">内部执行模式。</param>
    /// <returns>诊断使用的执行模式文本。</returns>
    private static string ToDiagnosticValue(SqlTransactionExecutionMode mode) => mode switch
    {
        SqlTransactionExecutionMode.NativeAsync => "NativeAsync",
        SqlTransactionExecutionMode.SynchronousFallback => "SynchronousFallback",
        _ => null
    };

    /// <summary>
    /// 使租约失效。
    /// </summary>
    public void InvalidateWhenNoActiveExecution()
    {
        lock (_syncRoot)
        {
            if (_activeExecutionCount != 0)
                throw new InvalidOperationException("当前 SQL Query 或 Executor 正在执行，不能释放 Root 对象。");
            Volatile.Write(ref _isActive, 0);
        }
    }

    /// <inheritdoc />
    public void EnsureActive()
    {
        if (Volatile.Read(ref _isActive) == 0)
            throw new InvalidOperationException("SQL 事务作用域已结束，不能继续使用其创建的 Query 或 Executor。");
    }

    /// <inheritdoc />
    public IDisposable AcquireExecutionLease()
    {
        lock (_syncRoot)
        {
            EnsureActive();
            _activeExecutionCount++;
            return new ExecutionLease(this);
        }
    }

    /// <summary>
    /// 归还一次事务作用域执行租约。
    /// </summary>
    private void ReleaseExecutionLease()
    {
        lock (_syncRoot)
            _activeExecutionCount--;
    }

    /// <summary>
    /// 事务作用域执行租约。
    /// </summary>
    private sealed class ExecutionLease : IDisposable
    {
        /// <summary>
        /// 所属事务作用域租约。
        /// </summary>
        private SqlTransactionScopeLease _owner;

        /// <summary>
        /// 初始化一个<see cref="ExecutionLease"/>类型的实例。
        /// </summary>
        /// <param name="owner">所属事务作用域租约。</param>
        public ExecutionLease(SqlTransactionScopeLease owner) => _owner = owner;

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseExecutionLease();
        }
    }
}