using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 执行对象实体 Mutation 入口。
/// </summary>
public abstract partial class SqlExecutorBase
{
    /// <inheritdoc />
    public virtual int Insert<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null) where TEntity : class
    {
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> InsertAsync<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int InsertBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null, int? timeout = null) where TEntity : class => ExecuteInsertBatch(entities, options, timeout);

    /// <inheritdoc />
    public virtual Task<int> InsertBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class => ExecuteInsertBatchAsync(entities, options, timeout, cancellationToken);

    /// <inheritdoc />
    public virtual int Update<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null) where TEntity : class
    {
        EnsureWritableDataSource();
        return ExecuteMutationCommand(CreateMutationBuilder().Update(entity, options), timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> UpdateAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        return ExecuteMutationCommandAsync(CreateMutationBuilder().Update(entity, options), timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int UpdateBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null, int? timeout = null) where TEntity : class => ExecuteUpdateBatch(entities, options, timeout);

    /// <inheritdoc />
    public virtual Task<int> UpdateBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class => ExecuteUpdateBatchAsync(entities, options, timeout, cancellationToken);

    /// <inheritdoc />
    public virtual int Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null) where TEntity : class
    {
        EnsureWritableDataSource();
        return ExecuteMutationCommand(CreateMutationBuilder().Delete(entity, options), timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> DeleteAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        return ExecuteMutationCommandAsync(CreateMutationBuilder().Delete(entity, options), timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int Purge<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null) where TEntity : class
    {
        EnsureWritableDataSource();
        return ExecuteMutationCommand(CreateMutationBuilder().Purge(entity, options), timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> PurgeAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        return ExecuteMutationCommandAsync(CreateMutationBuilder().Purge(entity, options), timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int Restore<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null) where TEntity : class
    {
        EnsureWritableDataSource();
        return ExecuteMutationCommand(CreateMutationBuilder().Restore(entity, options), timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> RestoreAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        return ExecuteMutationCommandAsync(CreateMutationBuilder().Restore(entity, options), timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int DeleteBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null, int? timeout = null) where TEntity : class => ExecuteDeleteBatch(entities, options, timeout);

    /// <inheritdoc />
    public virtual Task<int> DeleteBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null, int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class => ExecuteDeleteBatchAsync(entities, options, timeout, cancellationToken);

    /// <summary>
    /// 执行实体批量 Insert。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入实体集合。</param>
    /// <param name="options">批量 Insert 选项。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <returns>实际受影响的行数。</returns>
    private int ExecuteInsertBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options, int? timeout) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchInsertOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options, items => CreateInsertBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    /// <summary>
    /// 异步执行实体批量 Insert。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入实体集合。</param>
    /// <param name="options">批量 Insert 选项。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示实际受影响行数的异步操作。</returns>
    private Task<int> ExecuteInsertBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options, int? timeout, CancellationToken cancellationToken) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchInsertOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options, items => CreateInsertBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    /// <summary>
    /// 执行实体批量 Update。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待更新实体集合。</param>
    /// <param name="options">批量 Update 选项。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <returns>实际受影响的行数。</returns>
    private int ExecuteUpdateBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options, int? timeout) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchUpdateOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options, items => CreateUpdateBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    /// <summary>
    /// 异步执行实体批量 Update。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待更新实体集合。</param>
    /// <param name="options">批量 Update 选项。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示实际受影响行数的异步操作。</returns>
    private Task<int> ExecuteUpdateBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options, int? timeout, CancellationToken cancellationToken) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchUpdateOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options, items => CreateUpdateBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    /// <summary>
    /// 执行实体批量 Delete。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待删除实体集合。</param>
    /// <param name="options">批量 Delete 选项。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <returns>实际受影响的行数。</returns>
    private int ExecuteDeleteBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options, int? timeout) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchDeleteOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options, items => CreateDeleteBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    /// <summary>
    /// 异步执行实体批量 Delete。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待删除实体集合。</param>
    /// <param name="options">批量 Delete 选项。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示实际受影响行数的异步操作。</returns>
    private Task<int> ExecuteDeleteBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options, int? timeout, CancellationToken cancellationToken) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchDeleteOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options, items => CreateDeleteBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    /// <summary>
    /// 验证批量 Mutation 输入和当前数据源是否允许写入。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待处理实体集合。</param>
    private void EnsureMutationBatchExecutionAllowed<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        EnsureWritableDataSource();
    }

    /// <summary>
    /// 按窗口物化实体并生成批量 Mutation 命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待处理实体集合。</param>
    /// <param name="options">批量 Mutation 选项。</param>
    /// <param name="batchFactory">根据单个窗口创建命令批次的委托。</param>
    /// <returns>按原始顺序延迟输出的 Mutation 命令批次。</returns>
    private static IEnumerable<SqlMutationBatchCommand> CreateWindowedMutationBatchCommands<TEntity>(IEnumerable<TEntity> entities, SqlMutationBatchOptions options, Func<IEnumerable<TEntity>, IReadOnlyList<SqlMutationBatchCommand>> batchFactory) where TEntity : class
    {
        if (batchFactory == null)
            throw new ArgumentNullException(nameof(batchFactory));
        foreach (var window in EnumerateMutationWindows(entities, GetMutationBatchWindowSize(options)))
            foreach (var batch in batchFactory(window))
                yield return batch;
    }

    /// <summary>
    /// 获取批量 Mutation 的实体窗口大小。
    /// </summary>
    /// <param name="options">批量 Mutation 选项。</param>
    /// <returns>用于分段枚举实体的窗口大小。</returns>
    private static int GetMutationBatchWindowSize(SqlMutationBatchOptions options)
    {
        var windowSize = options?.BatchSize ?? DefaultMutationBatchWindowSize;
        if (windowSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "批量大小必须大于零。");
        return windowSize;
    }

    /// <summary>
    /// 将实体集合按指定窗口大小分段枚举。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待分段枚举的实体集合。</param>
    /// <param name="windowSize">每个窗口的最大实体数量。</param>
    /// <returns>按原始顺序排列的实体窗口序列。</returns>
    private static IEnumerable<IReadOnlyList<TEntity>> EnumerateMutationWindows<TEntity>(IEnumerable<TEntity> entities, int windowSize)
    {
        using var enumerator = entities.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var window = new List<TEntity>(windowSize) { enumerator.Current };
            while (window.Count < windowSize && enumerator.MoveNext())
                window.Add(enumerator.Current);
            yield return window;
        }
    }
}