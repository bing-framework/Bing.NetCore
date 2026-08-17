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

    private int ExecuteInsertBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options, int? timeout) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchInsertOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options, items => CreateInsertBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    private Task<int> ExecuteInsertBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options, int? timeout, CancellationToken cancellationToken) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchInsertOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options, items => CreateInsertBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    private int ExecuteUpdateBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options, int? timeout) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchUpdateOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options, items => CreateUpdateBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    private Task<int> ExecuteUpdateBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options, int? timeout, CancellationToken cancellationToken) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchUpdateOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options, items => CreateUpdateBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    private int ExecuteDeleteBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options, int? timeout) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchDeleteOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options, items => CreateDeleteBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    private Task<int> ExecuteDeleteBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options, int? timeout, CancellationToken cancellationToken) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchDeleteOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options, items => CreateDeleteBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    private void EnsureMutationBatchExecutionAllowed<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        EnsureWritableDataSource();
    }

    private static IEnumerable<SqlMutationBatchCommand> CreateWindowedMutationBatchCommands<TEntity>(IEnumerable<TEntity> entities, SqlMutationBatchOptions options, Func<IEnumerable<TEntity>, IReadOnlyList<SqlMutationBatchCommand>> batchFactory) where TEntity : class
    {
        if (batchFactory == null)
            throw new ArgumentNullException(nameof(batchFactory));
        foreach (var window in EnumerateMutationWindows(entities, GetMutationBatchWindowSize(options)))
            foreach (var batch in batchFactory(window))
                yield return batch;
    }

    private static int GetMutationBatchWindowSize(SqlMutationBatchOptions options)
    {
        var windowSize = options?.BatchSize ?? DefaultMutationBatchWindowSize;
        if (windowSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "批量大小必须大于零。");
        return windowSize;
    }

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