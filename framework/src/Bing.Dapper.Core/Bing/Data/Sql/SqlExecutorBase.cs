using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象基类
/// </summary>
public abstract class SqlExecutorBase : SqlQueryBase, ISqlExecutor
{
    /// <summary>
    /// 未指定批量大小时使用的最大实体窗口。
    /// </summary>
    /// <remarks>
    /// 该上限仅限制内存占用；Provider 参数和 SQL 长度限制仍由既有批量规划器在窗口内收紧。
    /// </remarks>
    private const int DefaultMutationBatchWindowSize = 256;

    /// <inheritdoc />
    public ISqlBuilder CreateBuilder() => CreateIndependentSqlBuilder();

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="SqlExecutorBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    protected SqlExecutorBase(IServiceProvider serviceProvider, SqlOptions options)
        : base(serviceProvider, options)
    {
    }

    #endregion

    /// <inheritdoc />
    public List<TResult> ExecuteReturning<TResult>(SqlMutationDescription description, int? timeout = null)
    {
        EnsureWritableDataSource();
        ValidateReturningMutationDescription(description);
        return ExecuteDirect(context =>
        {
            if (ExecuteBefore() == false)
                return new List<TResult>();
            var command = PrepareCommand(description);
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = connection.Query<TResult>(command.Sql, command.DapperParameters, transaction, true, timeout)
                .ToList();
            CompleteQueryTransaction();
            ExecuteAfter(context.Message);
            return result;
        });
    }

    /// <inheritdoc />
    public async Task<List<TResult>> ExecuteReturningAsync<TResult>(SqlMutationDescription description, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        ValidateReturningMutationDescription(description);
        return await ExecuteDirectAsync(async context =>
        {
            if (ExecuteBefore() == false)
                return new List<TResult>();
            var command = PrepareCommand(description);
            var connection = GetExecutionConnection();
            var transaction = await GetQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = await ExecuteMaterializedQueryAsync<TResult>(connection,
                CreateQueryCommandDefinition(command.Sql, command.DapperParameters, transaction, timeout, buffered: true,
                    cancellationToken), cancellationToken);
            await CompleteQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            ExecuteAfter(context.Message);
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    #region ExecuteMutation(执行 Mutation 描述)

    /// <inheritdoc />
    public virtual int ExecuteMutation(SqlMutationDescription description, int? timeout = null)
    {
        EnsureWritableDataSource();
        ValidateExecutableMutationDescription(description);
        return ExecuteDirect(context =>
        {
            if (ExecuteBefore() == false)
                return 0;
            var command = PrepareCommand(description);
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = connection.Execute(command.Sql, command.DapperParameters, transaction, timeout);
            CompleteQueryTransaction();
            ExecuteAfter(context.Message);
            return result;
        });
    }

    /// <inheritdoc />
    public virtual async Task<int> ExecuteMutationAsync(SqlMutationDescription description, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        ValidateExecutableMutationDescription(description);
        return await ExecuteDirectAsync(async context =>
        {
            if (ExecuteBefore() == false)
                return 0;
            var command = PrepareCommand(description);
            var connection = GetExecutionConnection();
            var transaction = await GetQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = await connection.ExecuteAsync(new CommandDefinition(command.Sql, command.DapperParameters, transaction, timeout,
                cancellationToken: cancellationToken));
            await CompleteQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            ExecuteAfter(context.Message);
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual int Execute(SqlMutationDescription description, int? timeout = null) =>
        ExecuteMutation(description, timeout);

    /// <inheritdoc />
    public virtual Task<int> ExecuteAsync(SqlMutationDescription description, int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteMutationAsync(description, timeout, cancellationToken);

    /// <summary>
    /// 验证 Mutation 描述是否可通过非 Returning 执行入口执行。
    /// </summary>
    private void ValidateExecutableMutationDescription(SqlMutationDescription description)
    {
        if (description == null)
            throw new ArgumentNullException(nameof(description));
        if (description.HasReturning)
            throw new InvalidOperationException("包含 Returning 的 Mutation 必须通过查询结果 API 执行。");
        if (description.OperationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            throw new InvalidOperationException($"ISqlExecutor 不支持执行 {description.OperationKind} 状态的 Mutation 描述。");
        ValidateMutationDescriptionProvider(description);
    }

    /// <summary>
    /// 验证 Mutation 描述是否为包含 Returning 的 Mutation。
    /// </summary>
    private void ValidateReturningMutationDescription(SqlMutationDescription description)
    {
        if (description == null)
            throw new ArgumentNullException(nameof(description));
        if (description.OperationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            throw new InvalidOperationException($"ISqlExecutor 不支持执行 {description.OperationKind} 状态的 Mutation 描述。");
        if (description.HasReturning == false)
            throw new InvalidOperationException("Mutation 必须配置 Returning 后才能通过查询结果 API 执行。");
        if (description.ProviderProfile.Mutation.SupportsReturning == false)
            throw new InvalidOperationException($"Mutation 描述 Provider {description.ProviderKey} 未声明 Returning 能力，不能执行。");
        ValidateMutationDescriptionProvider(description);
    }

    /// <summary>
    /// 验证 Mutation 描述生成时的 Provider 与当前 Executor 一致。
    /// </summary>
    /// <param name="description">待执行的 Mutation 描述。</param>
    private void ValidateMutationDescriptionProvider(SqlMutationDescription description)
    {
        var provider = GetCurrentProvider();
        var providerKey = provider.Key?.Trim();
        if (string.Equals(description.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
        {
            if (description.HasReturning && SqlProviderCapabilityResolver.GetProfile(provider).Mutation.SupportsReturning == false)
                throw new NotSupportedException($"Provider {providerKey ?? "<未指定>"} 不支持 Mutation Returning。");
            return;
        }
        throw new InvalidOperationException($"Mutation 描述 Provider {description.ProviderKey} 与当前 Executor Provider {providerKey ?? "<未指定>"} 不一致，不能执行。");
    }

    #endregion

    #region Insert(插入实体)

    /// <inheritdoc />
    public virtual int Insert<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null)
        where TEntity : class
    {
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> InsertAsync<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int InsertBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null,
        int? timeout = null) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchInsertOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options,
            items => CreateInsertBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> InsertBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchInsertOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options,
            items => CreateInsertBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    #endregion

    #region Update(更新实体)

    /// <inheritdoc />
    public virtual int Update<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null)
        where TEntity : class
    {
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Update(entity, options);
        return ExecuteMutationCommand(command, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> UpdateAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Update(entity, options);
        return ExecuteMutationCommandAsync(command, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int UpdateBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null,
        int? timeout = null) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchUpdateOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options,
            items => CreateUpdateBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> UpdateBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchUpdateOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options,
            items => CreateUpdateBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    #endregion

    #region Delete(删除实体)

    /// <inheritdoc />
    public virtual int Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null)
        where TEntity : class
    {
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Delete(entity, options);
        return ExecuteMutationCommand(command, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> DeleteAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        var command = CreateMutationBuilder().Delete(entity, options);
        return ExecuteMutationCommandAsync(command, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int DeleteBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null,
        int? timeout = null) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        options ??= new SqlBatchDeleteOptions();
        return ExecuteMutationBatch(CreateWindowedMutationBatchCommands(entities, options,
            items => CreateDeleteBatchCommands(items, options)), options.UseTransaction, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> DeleteBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class
    {
        EnsureMutationBatchExecutionAllowed(entities);
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new SqlBatchDeleteOptions();
        return ExecuteMutationBatchAsync(CreateWindowedMutationBatchCommands(entities, options,
            items => CreateDeleteBatchCommands(items, options)), options.UseTransaction, timeout, cancellationToken);
    }

    #endregion

    /// <summary>
    /// 确保批量 Mutation 的实体输入有效且当前数据源允许写入。
    /// </summary>
    /// <typeparam name="TEntity">待写入的实体类型。</typeparam>
    /// <param name="entities">待写入的实体序列。</param>
    private void EnsureMutationBatchExecutionAllowed<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        EnsureWritableDataSource();
    }

    /// <summary>
    /// 按受控实体窗口惰性生成批量命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待处理实体序列。</param>
    /// <param name="options">当前批量选项。</param>
    /// <param name="batchFactory">基于单个窗口创建命令的工厂。</param>
    /// <returns>按需生成的批量命令序列。</returns>
    /// <remarks>
    /// 一个窗口内仍使用既有参数和 SQL 长度规划，因而不会放宽 Provider 限制；
    /// 数据库命令失败或取消后，枚举不会读取下一窗口实体。
    /// </remarks>
    private static IEnumerable<SqlMutationBatchCommand> CreateWindowedMutationBatchCommands<TEntity>(
        IEnumerable<TEntity> entities, SqlMutationBatchOptions options,
        Func<IEnumerable<TEntity>, IReadOnlyList<SqlMutationBatchCommand>> batchFactory) where TEntity : class
    {
        if (batchFactory == null)
            throw new ArgumentNullException(nameof(batchFactory));
        foreach (var window in EnumerateMutationWindows(entities, GetMutationBatchWindowSize(options)))
        {
            foreach (var batch in batchFactory(window))
                yield return batch;
        }
    }

    /// <summary>
    /// 获取单次命令生成可保留的最大实体数。
    /// </summary>
    /// <param name="options">当前批量选项。</param>
    /// <returns>调用方指定批次大小或保守默认窗口大小。</returns>
    private static int GetMutationBatchWindowSize(SqlMutationBatchOptions options)
    {
        var windowSize = options?.BatchSize ?? DefaultMutationBatchWindowSize;
        if (windowSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "批量大小必须大于零。");
        return windowSize;
    }

    /// <summary>
    /// 将输入序列按窗口大小分割，且不读取未请求的后续窗口。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待处理实体序列。</param>
    /// <param name="windowSize">单个窗口的最大实体数。</param>
    /// <returns>惰性实体窗口。</returns>
    private static IEnumerable<IReadOnlyList<TEntity>> EnumerateMutationWindows<TEntity>(IEnumerable<TEntity> entities,
        int windowSize)
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

    #region ExecuteText(执行 SQL 文本)

    /// <inheritdoc />
    public virtual int ExecuteText(string sql, object param = null, int? timeout = null) =>
        ExecuteSql(sql, param, timeout);

    /// <inheritdoc />
    public virtual Task<int> ExecuteTextAsync(string sql, object param = null, int? timeout = null,
        CancellationToken cancellationToken = default) => ExecuteSqlAsync(sql, param, timeout, cancellationToken);

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual int ExecuteSql(string sql, object param = null, int? timeout = null)
        => ExecuteSqlCore(sql, param, timeout, null);

    /// <summary>
    /// 在同一执行生命周期内执行 SQL 并在提交前验证受影响行数。
    /// </summary>
    /// <param name="sql">执行的 SQL 语句。</param>
    /// <param name="param">SQL 参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="validateResult">在事务提交前验证受影响行数的回调。</param>
    /// <returns>操作影响的行数。</returns>
    private int ExecuteSqlCore(string sql, object param, int? timeout, Action<int> validateResult)
    {
        return ExecuteDirect(context =>
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var command = PrepareCommand(sql, param);
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = connection.Execute(command.Sql, command.DapperParameters, transaction, timeout);
            validateResult?.Invoke(result);
            CompleteQueryTransaction();
            ExecuteAfter(context.Message);
            return result;
        });
    }

    #endregion

    #region ExecuteSqlAsync(执行Sql增删改操作)

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作影响的行数</returns>
    public virtual async Task<int> ExecuteSqlAsync(string sql, object param = null, int? timeout = null,
        CancellationToken cancellationToken = default)
        => await ExecuteSqlCoreAsync(sql, param, timeout, cancellationToken, null).ConfigureAwait(false);

    /// <summary>
    /// 在同一异步执行生命周期内执行 SQL 并在提交前验证受影响行数。
    /// </summary>
    /// <param name="sql">执行的 SQL 语句。</param>
    /// <param name="param">SQL 参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="validateResult">在事务提交前验证受影响行数的回调。</param>
    /// <returns>表示操作影响行数的异步操作。</returns>
    private async Task<int> ExecuteSqlCoreAsync(string sql, object param, int? timeout,
        CancellationToken cancellationToken, Action<int> validateResult)
    {
        return await ExecuteDirectAsync(async context =>
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = await GetQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            var command = PrepareCommand(sql, param);
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = await connection.ExecuteAsync(new CommandDefinition(command.Sql, command.DapperParameters, transaction, timeout,
                cancellationToken: cancellationToken));
            validateResult?.Invoke(result);
            await CompleteQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            ExecuteAfter(context.Message);
            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region ExecuteProcedure(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>包含受影响行数及本次输出参数访问器的过程执行结果。</returns>
    public virtual SqlProcedureResult<int> ExecuteProcedure(string procedure, object param = null, int? timeout = null)
    {
        EnsureWritableDataSource();
        EnsureStoredProceduresSupported();
        EnsureOutputParametersSupported(param);
        return ExecuteDirect(context =>
        {
            if (ExecuteBefore() == false)
                return new SqlProcedureResult<int>(0, null);
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var command = PrepareProcedureCommand(procedure, param);
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = connection.Execute(command.Sql, command.DapperParameters, transaction, timeout,
                GetProcedureCommandType());
            var procedureResult = new SqlProcedureResult<int>(result,
                CreateOutputParameterAccessor(command.DapperParameters));
            CompleteQueryTransaction();
            ExecuteAfter(context.Message);
            return procedureResult;
        });
    }

    #endregion

    #region ExecuteProcedureAsync(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示包含受影响行数及本次输出参数访问器的过程执行结果的异步操作。</returns>
    public virtual async Task<SqlProcedureResult<int>> ExecuteProcedureAsync(string procedure, object param = null,
        int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCancellationSupported(cancellationToken);
        EnsureWritableDataSource();
        EnsureStoredProceduresSupported();
        EnsureOutputParametersSupported(param);
        return await ExecuteDirectAsync(async context =>
        {
            if (ExecuteBefore() == false)
                return new SqlProcedureResult<int>(0, null);
            var connection = GetExecutionConnection();
            var transaction = await GetQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            var command = PrepareProcedureCommand(procedure, param);
            context.Message = CreateExecutionDiagnostics(command, connection);
            WriteTraceLog(command);
            var result = await connection.ExecuteAsync(new CommandDefinition(command.Sql, command.DapperParameters, transaction, timeout,
                GetProcedureCommandType(), cancellationToken: cancellationToken));
            var procedureResult = new SqlProcedureResult<int>(result,
                CreateOutputParameterAccessor(command.DapperParameters));
            await CompleteQueryTransactionAsync(cancellationToken).ConfigureAwait(false);
            ExecuteAfter(context.Message);
            return procedureResult;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 在统一异常聚合语义下执行同步命令入口。
    /// </summary>
    /// <typeparam name="TResult">命令结果类型。</typeparam>
    /// <param name="operation">实际命令操作。</param>
    /// <returns>命令操作的结果。</returns>
    /// <remarks>
    /// 原始操作异常始终作为主异常保留；回滚、错误诊断、业务完成 Hook 和执行租约释放失败
    /// 按生命周期顺序追加为清理异常，避免任一清理步骤覆盖诊断根因。
    /// </remarks>
    private TResult ExecuteDirect<TResult>(Func<DirectExecutionContext, TResult> operation)
    {
        var executionLease = AcquireExecutionLease();
        var context = new DirectExecutionContext();
        return SqlQueryPlanLifecycle.Execute(() => operation(context), (exception, cleanupExceptions) =>
        {
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions, RollbackQueryTransaction);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions,
                () => ExecuteError(context.Message, exception));
        }, result => ExecuteAfter(result), executionLease.Dispose);
    }

    /// <summary>
    /// 在统一异常聚合语义下执行异步命令入口。
    /// </summary>
    /// <typeparam name="TResult">命令结果类型。</typeparam>
    /// <param name="operation">实际异步命令操作。</param>
    /// <param name="cancellationToken">调用方传入的取消令牌。</param>
    /// <returns>表示命令操作结果的异步任务。</returns>
    /// <remarks>
    /// 与同步入口保持相同的主异常和清理异常排序，确保取消异常不会被清理异常覆盖。
    /// </remarks>
    private async Task<TResult> ExecuteDirectAsync<TResult>(Func<DirectExecutionContext, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        EnsureCancellationSupported(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var executionLease = AcquireExecutionLease();
        var context = new DirectExecutionContext();
        return await SqlQueryPlanLifecycle.ExecuteAsync(() => operation(context), async (exception, cleanupExceptions) =>
        {
            await SqlQueryPlanLifecycle.CaptureCleanupExceptionAsync(cleanupExceptions, RollbackQueryTransactionAsync)
                .ConfigureAwait(false);
            SqlQueryPlanLifecycle.CaptureCleanupException(cleanupExceptions,
                () => ExecuteError(context.Message, exception));
        }, result => ExecuteAfter(result), executionLease.Dispose).ConfigureAwait(false);
    }

    /// <summary>
    /// 保存一次直接命令执行过程中创建的诊断消息。
    /// </summary>
    private sealed class DirectExecutionContext
    {
        /// <summary>
        /// 当前命令的执行前诊断消息。
        /// </summary>
        public DiagnosticsMessage Message { get; set; }
    }

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">Sql 参数映射</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual int ExecuteSql<TEntity>(string sql, SqlParameterMap<TEntity> param, int? timeout = null)
        where TEntity : class => ExecuteSql(sql, (object)param, timeout);

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">Sql 参数映射</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作影响的行数</returns>
    public virtual Task<int> ExecuteSqlAsync<TEntity>(string sql, SqlParameterMap<TEntity> param, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class =>
        ExecuteSqlAsync(sql, (object)param, timeout, cancellationToken);

    /// <summary>
    /// 创建绑定当前 Executor 数据源和映射服务的实体写入 Builder。
    /// </summary>
    /// <returns>实体写入 Builder。</returns>
    private ISqlEntityMutationCommandBuilder CreateMutationBuilder() =>
        CreateMutationBuilder(ResolveMutationProvider());

    /// <summary>
    /// 使用指定 Provider 创建绑定当前 Executor 共享服务的实体写入 Builder。
    /// </summary>
    /// <param name="provider">已解析的当前 SQL Provider。</param>
    /// <returns>实体写入 Builder。</returns>
    private ISqlEntityMutationCommandBuilder CreateMutationBuilder(ISqlProvider provider)
    {
        var factory = ServiceProvider.GetService<ISqlEntityMutationCommandBuilderFactory>();
        if (factory == null)
            throw new InvalidOperationException("未注册实体写入 SQL Builder 工厂。");
        return factory.Create(provider, CreateSqlBuilderServices());
    }

    /// <summary>
    /// 解析当前 Executor 数据源对应的 SQL Provider。
    /// </summary>
    /// <returns>当前数据源的 SQL Provider。</returns>
    private ISqlProvider ResolveMutationProvider()
    {
        return GetCurrentProvider();
    }

    /// <summary>
    /// 确定批量 Insert 是否应使用组合式多行 Values 命令。
    /// </summary>
    /// <param name="options">批量插入选项。</param>
    /// <returns>应使用组合式 Insert 时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    private bool ShouldUseCombinedInsert(SqlBatchInsertOptions options)
    {
        if (options?.Strategy == SqlBatchInsertStrategy.PerEntity)
            return false;
        if (options?.Strategy == SqlBatchInsertStrategy.MultiRowValues)
            return true;
        var provider = ResolveMutationProvider();
        return SqlProviderCapabilityResolver.GetProfile(provider).Mutation.SupportsMultiRowValues;
    }

    /// <summary>
    /// 根据实体集合和当前策略生成 Insert 批次。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入实体集合。</param>
    /// <param name="options">批量插入选项。</param>
    /// <returns>待执行的 Insert 批次。</returns>
    private IReadOnlyList<SqlMutationBatchCommand> CreateInsertBatchCommands<TEntity>(IEnumerable<TEntity> entities,
        SqlBatchInsertOptions options) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        if (items.Count == 0)
            return Array.Empty<SqlMutationBatchCommand>();
        return ShouldUseCombinedInsert(options)
            ? CreateCombinedInsertBatchCommands(items, options)
            : CreateMutationBatchCommands(items, entity => CreateMutationBuilder().Insert(entity, options?.InsertOptions),
                options);
    }

    /// <summary>
    /// 根据实体集合和当前策略生成 Update 批次。
    /// </summary>
    /// <typeparam name="TEntity">待更新实体类型。</typeparam>
    /// <param name="entities">待更新实体集合。</param>
    /// <param name="options">批量 Update 策略、容量和并发选项。</param>
    /// <returns>按当前 Provider 能力和容量限制切分的 Update 命令批次。</returns>
    private IReadOnlyList<SqlMutationBatchCommand> CreateUpdateBatchCommands<TEntity>(IEnumerable<TEntity> entities,
        SqlBatchUpdateOptions options) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        if (items.Count == 0)
            return Array.Empty<SqlMutationBatchCommand>();
        options ??= new SqlBatchUpdateOptions();
        if (options.Strategy != SqlBatchUpdateStrategy.PerEntity)
        {
            var provider = ResolveMutationProvider();
            var renderer = TryResolveBatchUpdateRenderer(provider);
            if (renderer != null && CreateMutationBuilder(provider) is ISqlBatchUpdateRenderContextBuilder contextBuilder)
            {
                var context = contextBuilder.CreateUpdateRenderContext(items.Take(1).ToArray(), options.UpdateOptions);
                if (renderer.CanRender(context))
                    return CreateProviderOptimizedUpdateBatchCommands(items, options);
            }
            if (options.Strategy == SqlBatchUpdateStrategy.ProviderOptimized)
                return CreateProviderOptimizedUpdateBatchCommands(items, options);
        }
        return CreateMutationBatchCommands(items,
            entity => CreateMutationBuilder().Update(entity, options.UpdateOptions), options);
    }

    /// <summary>
    /// 使用当前 Provider 注册的优化渲染器生成 Update 批次。
    /// </summary>
    /// <typeparam name="TEntity">待更新实体类型。</typeparam>
    /// <param name="items">已物化的待更新实体集合。</param>
    /// <param name="options">批量 Update 策略、容量和并发选项。</param>
    /// <returns>每批包含一条 Provider 优化 Update 命令的批次集合。</returns>
    private IReadOnlyList<SqlMutationBatchCommand> CreateProviderOptimizedUpdateBatchCommands<TEntity>(
        IReadOnlyList<TEntity> items, SqlBatchUpdateOptions options) where TEntity : class
    {
        var provider = ResolveMutationProvider();
        var renderer = ResolveBatchUpdateRenderer(provider);
        if (CreateMutationBuilder(provider) is not ISqlBatchUpdateRenderContextBuilder contextBuilder)
            throw new NotSupportedException($"Provider {provider.Key} 的实体 Mutation Builder 未实现批量 Update 渲染上下文。");
        var firstContext = contextBuilder.CreateUpdateRenderContext(items.Take(1).ToArray(), options.UpdateOptions);
        var firstCommand = renderer.Render(firstContext);
        var validateAffectedRows = firstContext.ConcurrencyColumns.Count > 0 &&
            (options.UpdateOptions?.ConcurrencyConflictBehavior ?? SqlConcurrencyConflictBehavior.Throw) ==
            SqlConcurrencyConflictBehavior.Throw;
        var maxParameterCount = options.GetEffectiveMaxParameterCount(provider);
        var plan = new SqlMutationBatchPlanner().Plan(new SqlMutationBatchPlanContext(items.Count,
            Math.Max(1, firstCommand.Parameters.Count), maxParameterCount: maxParameterCount,
            estimatedSqlLengthPerEntity: 0, options: options));
        var batches = new List<SqlMutationBatchCommand>(plan.BatchSizes.Count);
        var offset = 0;
        foreach (var size in plan.BatchSizes)
        {
            if (options.MaxSqlLength == null)
            {
                var command = RenderProviderOptimizedUpdateCommand(items.Skip(offset).Take(size).ToArray(), options,
                    provider, renderer);
                batches.Add(new SqlMutationBatchCommand(new[] { command }, size, options.UseTransaction,
                    validateAffectedRows, "Update"));
                offset += size;
                continue;
            }
            var remaining = size;
            while (remaining > 0)
            {
                var minimumSize = 1;
                var maximumSize = remaining;
                var commandSize = 0;
                SqlMutationCommand command = null;
                while (minimumSize <= maximumSize)
                {
                    var candidateSize = minimumSize + (maximumSize - minimumSize) / 2;
                    var candidate = RenderProviderOptimizedUpdateCommand(items.Skip(offset).Take(candidateSize).ToArray(),
                        options, provider, renderer);
                    if (options.MaxSqlLength != null && candidate.Sql.Length > options.MaxSqlLength.Value)
                    {
                        maximumSize = candidateSize - 1;
                        continue;
                    }
                    command = candidate;
                    commandSize = candidateSize;
                    minimumSize = candidateSize + 1;
                }
                if (command == null)
                    throw new InvalidOperationException("当前 Provider 参数或 SQL 长度上限无法容纳一个 Mutation 实体。");
                batches.Add(new SqlMutationBatchCommand(new[] { command }, commandSize, options.UseTransaction,
                    validateAffectedRows, "Update"));
                offset += commandSize;
                remaining -= commandSize;
            }
        }
        return batches;
    }

    /// <summary>
    /// 生成单个 Provider 优化 Update 命令。
    /// </summary>
    /// <typeparam name="TEntity">待更新实体类型。</typeparam>
    /// <param name="entities">当前命令包含的实体集合。</param>
    /// <param name="options">批量 Update 选项。</param>
    /// <param name="provider">用于创建 Mutation Builder 的 SQL Provider。</param>
    /// <param name="renderer">用于渲染优化命令的唯一 Provider 渲染器。</param>
    /// <returns>由 Provider 专用语法渲染的单条 Update 命令。</returns>
    private SqlMutationCommand RenderProviderOptimizedUpdateCommand<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlBatchUpdateOptions options, ISqlProvider provider, ISqlBatchUpdateRenderer renderer) where TEntity : class
    {
        if (CreateMutationBuilder(provider) is not ISqlBatchUpdateRenderContextBuilder contextBuilder)
            throw new NotSupportedException($"Provider {provider.Key} 的实体 Mutation Builder 未实现批量 Update 渲染上下文。");
        return renderer.Render(contextBuilder.CreateUpdateRenderContext(entities, options.UpdateOptions));
    }

    /// <summary>
    /// 解析当前 Provider 唯一匹配的批量 Update 渲染器。
    /// </summary>
    /// <param name="provider">待匹配的 SQL Provider。</param>
    /// <returns>Provider Key 唯一匹配的批量 Update 渲染器。</returns>
    private ISqlBatchUpdateRenderer ResolveBatchUpdateRenderer(ISqlProvider provider)
    {
        var renderers = ServiceProvider.GetServices<ISqlBatchUpdateRenderer>()
            .Where(renderer => string.Equals(renderer.ProviderKey?.Trim(), provider.Key?.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (renderers.Length == 0)
            throw new NotSupportedException($"Provider {provider.Key} 未注册优化批量 Update 渲染器。");
        if (renderers.Length > 1)
            throw new InvalidOperationException($"Provider {provider.Key} 注册了多个优化批量 Update 渲染器。");
        return renderers[0];
    }

    /// <summary>
    /// 尝试解析当前 Provider 的唯一批量 Update 渲染器。
    /// </summary>
    /// <param name="provider">待匹配的 SQL Provider。</param>
    /// <returns>Provider Key 唯一匹配的渲染器；未注册时返回 <see langword="null"/>。</returns>
    private ISqlBatchUpdateRenderer TryResolveBatchUpdateRenderer(ISqlProvider provider)
    {
        var renderers = ServiceProvider.GetServices<ISqlBatchUpdateRenderer>()
            .Where(renderer => string.Equals(renderer.ProviderKey?.Trim(), provider.Key?.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (renderers.Length == 0)
            return null;
        if (renderers.Length > 1)
            throw new InvalidOperationException($"Provider {provider.Key} 注册了多个优化批量 Update 渲染器。");
        return renderers[0];
    }

    /// <summary>
    /// 根据实体集合和当前策略生成 Delete 批次。
    /// </summary>
    private IReadOnlyList<SqlMutationBatchCommand> CreateDeleteBatchCommands<TEntity>(IEnumerable<TEntity> entities,
        SqlBatchDeleteOptions options) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        if (items.Count == 0)
            return Array.Empty<SqlMutationBatchCommand>();
        options ??= new SqlBatchDeleteOptions();
        if (options.Strategy == SqlBatchDeleteStrategy.PerEntity)
            return CreateMutationBatchCommands(items,
                entity => CreateMutationBuilder().Delete(entity, options.DeleteOptions), options);
        var provider = ResolveMutationProvider();
        if (CreateMutationBuilder(provider) is not ISqlCombinedDeleteMutationBuilder)
        {
            if (options.Strategy == SqlBatchDeleteStrategy.Auto)
                return CreateMutationBatchCommands(items,
                    entity => CreateMutationBuilder(provider).Delete(entity, options.DeleteOptions), options);
            throw new NotSupportedException($"Provider {provider.Key} 未实现组合式 Delete 批量命令。");
        }
        return CreateCombinedDeleteBatchCommands(items, options, provider);
    }

    /// <summary>
    /// 将实体映射为按参数和 SQL 长度限制分片的组合 Delete 命令。
    /// </summary>
    /// <typeparam name="TEntity">待删除实体类型。</typeparam>
    /// <param name="items">已物化的待删除实体集合。</param>
    /// <param name="options">批量 Delete 策略、容量和并发选项。</param>
    /// <param name="provider">支持组合式 Delete 的 SQL Provider。</param>
    /// <returns>每批包含一条组合 Delete 命令的批次集合。</returns>
    private IReadOnlyList<SqlMutationBatchCommand> CreateCombinedDeleteBatchCommands<TEntity>(IReadOnlyList<TEntity> items,
        SqlBatchDeleteOptions options, ISqlProvider provider) where TEntity : class
    {
        if (items.Any(entity => entity == null))
            throw new ArgumentException("批量 Delete 实体集合不能包含 null。", nameof(items));
        var firstCommand = ((ISqlCombinedDeleteMutationBuilder)CreateMutationBuilder(provider))
            .DeleteCombined(new[] { items[0] }, options.DeleteOptions, options.Strategy);
        var maxParameterCount = options.GetEffectiveMaxParameterCount(provider);
        var plan = new SqlMutationBatchPlanner().Plan(new SqlMutationBatchPlanContext(items.Count,
            Math.Max(1, firstCommand.Parameters.Count), maxParameterCount: maxParameterCount,
            estimatedSqlLengthPerEntity: 0, options: options));
        var batches = new List<SqlMutationBatchCommand>(plan.BatchSizes.Count);
        var offset = 0;
        foreach (var size in plan.BatchSizes)
        {
            if (options.MaxSqlLength == null)
            {
                var command = ((ISqlCombinedDeleteMutationBuilder)CreateMutationBuilder(provider)).DeleteCombined(
                    items.Skip(offset).Take(size).ToArray(), options.DeleteOptions, options.Strategy);
                var validateAffectedRows = command.ValidateAffectedRows;
                command = WithoutSingleEntityAffectedRowsValidation(command);
                batches.Add(new SqlMutationBatchCommand(new[] { command }, size, options.UseTransaction,
                    validateAffectedRows, "Delete"));
                offset += size;
                continue;
            }
            var remaining = size;
            while (remaining > 0)
            {
                var minimumSize = 1;
                var maximumSize = remaining;
                var commandSize = 0;
                SqlMutationCommand command = null;
                while (minimumSize <= maximumSize)
                {
                    var candidateSize = minimumSize + (maximumSize - minimumSize) / 2;
                    var builder = (ISqlCombinedDeleteMutationBuilder)CreateMutationBuilder(provider);
                    var candidate = builder.DeleteCombined(items.Skip(offset).Take(candidateSize).ToArray(),
                        options.DeleteOptions, options.Strategy);
                    if (options.MaxSqlLength != null && candidate.Sql.Length > options.MaxSqlLength.Value)
                    {
                        maximumSize = candidateSize - 1;
                        continue;
                    }
                    command = candidate;
                    commandSize = candidateSize;
                    minimumSize = candidateSize + 1;
                }
                if (command == null)
                    throw new InvalidOperationException("当前 Provider 参数或 SQL 长度上限无法容纳一个 Mutation 实体。");
                var validateAffectedRows = command.ValidateAffectedRows;
                command = WithoutSingleEntityAffectedRowsValidation(command);
                batches.Add(new SqlMutationBatchCommand(new[] { command }, commandSize, options.UseTransaction,
                    validateAffectedRows, "Delete"));
                offset += commandSize;
                remaining -= commandSize;
            }
        }
        return batches;
    }

    /// <summary>
    /// 将组合命令的并发校验提升到批次层，避免按单实体一行规则误判。
    /// </summary>
    private static SqlMutationCommand WithoutSingleEntityAffectedRowsValidation(SqlMutationCommand command) =>
        command.ValidateAffectedRows
            ? new SqlMutationCommand(command.Sql, command.Parameters)
            : command;

    /// <summary>
    /// 将实体映射为按参数及 SQL 长度限制分组的 Mutation 命令。
    /// </summary>
    private IReadOnlyList<SqlMutationBatchCommand> CreateMutationBatchCommands<TEntity>(
        IEnumerable<TEntity> entities, Func<TEntity, SqlMutationCommand> commandFactory, SqlMutationBatchOptions options)
        where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        if (commandFactory == null)
            throw new ArgumentNullException(nameof(commandFactory));
        var commands = entities.Select(commandFactory).ToList();
        if (commands.Count == 0)
            return Array.Empty<SqlMutationBatchCommand>();
        options ??= new SqlMutationBatchOptions();
        var maxParameterCount = options.GetEffectiveMaxParameterCount(ResolveMutationProvider());
        if (commands.Any(command => maxParameterCount != null && command.Parameters.Count > maxParameterCount.Value ||
                                    options.MaxSqlLength != null && command.Sql.Length > options.MaxSqlLength.Value))
            throw new InvalidOperationException("当前 Provider 参数或 SQL 长度上限无法容纳一个 Mutation 实体。");
        var plan = new SqlMutationBatchPlanner().Plan(new SqlMutationBatchPlanContext(commands.Count,
            parametersPerEntity: 1, options: options));
        var batches = new List<SqlMutationBatchCommand>(plan.BatchSizes.Count);
        var offset = 0;
        foreach (var size in plan.BatchSizes)
        {
            batches.Add(new SqlMutationBatchCommand(commands.GetRange(offset, size), size, options.UseTransaction));
            offset += size;
        }
        return batches;
    }

    /// <summary>
    /// 将实体映射为按限制分片的多行 Insert 命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入实体集合。</param>
    /// <param name="options">批量插入选项。</param>
    /// <returns>每个元素包含一条多行 Insert 命令的批次集合。</returns>
    /// <exception cref="NotSupportedException">当前实体 Mutation Builder 不支持组合插入时抛出。</exception>
    private IReadOnlyList<SqlMutationBatchCommand> CreateCombinedInsertBatchCommands<TEntity>(
        IEnumerable<TEntity> entities, SqlBatchInsertOptions options) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        var items = entities.ToList();
        if (items.Count == 0)
            return Array.Empty<SqlMutationBatchCommand>();
        options ??= new SqlBatchInsertOptions();
        if (items.Any(entity => entity == null))
            throw new ArgumentException("批量 Insert 实体集合不能包含 null。", nameof(entities));
        var provider = ResolveMutationProvider();
        var supportsMultiRowValues =
            SqlProviderCapabilityResolver.GetProfile(provider).Mutation.SupportsMultiRowValues;
        if (supportsMultiRowValues == false)
            throw new NotSupportedException($"Provider {provider.Key} 未声明支持组合式 Insert 批量命令。");
        if (CreateMutationBuilder(provider) is not ISqlCombinedInsertMutationBuilder)
        {
            if (options?.Strategy == SqlBatchInsertStrategy.Auto)
                return CreateMutationBatchCommands(items,
                    entity => CreateMutationBuilder(provider).Insert(entity, options.InsertOptions), options);
            throw new NotSupportedException($"Provider {provider.Key} 未实现组合式 Insert 批量命令。");
        }
        var firstCommand = CreateMutationBuilder(provider).Insert(items[0], options.InsertOptions);
        var parametersPerEntity = firstCommand.Parameters.Count;
        var maxParameterCount = options.GetEffectiveMaxParameterCount(provider);
        var plan = new SqlMutationBatchPlanner().Plan(new SqlMutationBatchPlanContext(items.Count,
            Math.Max(1, parametersPerEntity), maxParameterCount: maxParameterCount,
            estimatedSqlLengthPerEntity: 0, options: options));
        var batches = new List<SqlMutationBatchCommand>(plan.BatchSizes.Count);
        var offset = 0;
        foreach (var size in plan.BatchSizes)
        {
            if (options.MaxSqlLength == null)
            {
                if (CreateMutationBuilder(provider) is not ISqlCombinedInsertMutationBuilder builder)
                    throw new NotSupportedException($"Provider {provider.Key} 未实现组合式 Insert 批量命令。");
                var command = builder.InsertCombined(items.GetRange(offset, size), options.InsertOptions);
                batches.Add(new SqlMutationBatchCommand(new[] { command }, size, options.UseTransaction));
                offset += size;
                continue;
            }
            var remaining = size;
            while (remaining > 0)
            {
                var minimumSize = 1;
                var maximumSize = remaining;
                var commandSize = 0;
                SqlMutationCommand command = null;
                while (minimumSize <= maximumSize)
                {
                    if (CreateMutationBuilder(provider) is not ISqlCombinedInsertMutationBuilder builder)
                        throw new NotSupportedException($"Provider {provider.Key} 未实现组合式 Insert 批量命令。");
                    var candidateSize = minimumSize + (maximumSize - minimumSize) / 2;
                    var candidate = builder.InsertCombined(items.GetRange(offset, candidateSize), options.InsertOptions);
                    if (options.MaxSqlLength != null && candidate.Sql.Length > options.MaxSqlLength.Value)
                    {
                        maximumSize = candidateSize - 1;
                        continue;
                    }
                    command = candidate;
                    commandSize = candidateSize;
                    minimumSize = candidateSize + 1;
                }
                if (command == null)
                    throw new InvalidOperationException("当前 Provider 参数或 SQL 长度上限无法容纳一个 Mutation 实体。");
                batches.Add(new SqlMutationBatchCommand(new[] { command }, commandSize, options.UseTransaction));
                offset += commandSize;
                remaining -= commandSize;
            }
        }
        return batches;
    }

    /// <summary>
    /// 同步执行 Mutation 批次；启用事务时复用统一事务作用域。
    /// </summary>
    /// <param name="batches">按执行顺序排列的 Mutation 命令批次。</param>
    /// <param name="useTransaction">是否要求使用事务；批次自身要求事务时也会开启事务。</param>
    /// <param name="timeout">单条命令执行超时时间，单位为秒。</param>
    /// <returns>所有命令影响行数的总和。</returns>
    /// <remarks>任一命令失败会回滚；回滚也失败时保留执行与回滚异常。</remarks>
    private int ExecuteMutationBatch(IEnumerable<SqlMutationBatchCommand> batches, bool useTransaction, int? timeout)
    {
        if (batches == null)
            return 0;
        using var enumerator = batches.GetEnumerator();
        if (enumerator.MoveNext() == false)
            return 0;
        EnsureWritableDataSource();
        var orderedBatches = EnumerateFirstAndRemaining(enumerator.Current, enumerator);
        if (useTransaction == false)
            return ExecuteMutationCommands(this, orderedBatches, timeout);
        var factory = ServiceProvider.GetService<ISqlTransactionScopeFactory>() ??
            throw new InvalidOperationException("未注册 SQL 事务作用域工厂。");
        using var scope = factory.Begin(GetDatabaseContext()?.DbKey);
        int result;
        try
        {
            result = ExecuteMutationCommands(scope.CreateExecutor(), orderedBatches, timeout);
        }
        catch (Exception executionException)
        {
            try
            {
                scope.Rollback();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(executionException, rollbackException);
            }
            throw;
        }
        scope.Commit();
        return result;
    }

    /// <summary>
    /// 异步执行 Mutation 批次；启用事务时复用统一事务作用域。
    /// </summary>
    /// <param name="batches">按执行顺序排列的 Mutation 命令批次。</param>
    /// <param name="useTransaction">是否要求使用事务；批次自身要求事务时也会开启事务。</param>
    /// <param name="timeout">单条命令执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">在开始及每条命令执行前检查的取消令牌。</param>
    /// <returns>表示异步执行并返回总影响行数的任务。</returns>
    /// <remarks>任一命令失败会回滚；回滚也失败时保留执行与回滚异常。</remarks>
    private async Task<int> ExecuteMutationBatchAsync(IEnumerable<SqlMutationBatchCommand> batches,
        bool useTransaction, int? timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (batches == null)
            return 0;
        using var enumerator = batches.GetEnumerator();
        if (enumerator.MoveNext() == false)
            return 0;
        EnsureWritableDataSource();
        var orderedBatches = EnumerateFirstAndRemaining(enumerator.Current, enumerator);
        if (useTransaction == false)
            return await ExecuteMutationCommandsAsync(this, orderedBatches, timeout, cancellationToken).ConfigureAwait(false);
        var factory = ServiceProvider.GetService<ISqlTransactionScopeFactory>() ??
            throw new InvalidOperationException("未注册 SQL 事务作用域工厂。");
        await using var scope = await factory.BeginAsync(GetDatabaseContext()?.DbKey, cancellationToken).ConfigureAwait(false);
        int result;
        try
        {
            result = await ExecuteMutationCommandsAsync(scope.CreateExecutor(), orderedBatches, timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception executionException)
        {
            try
            {
                await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(executionException, rollbackException);
            }
            throw;
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await scope.CommitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException cancellationException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(cancellationException, rollbackException);
            }
            throw;
        }
        return result;
    }

    /// <summary>
    /// 按顺序执行已生成的 Mutation 命令。
    /// </summary>
    /// <param name="executor">用于执行命令的 SQL 执行器。</param>
    /// <param name="batches">按执行顺序排列的命令批次。</param>
    /// <param name="timeout">单条命令执行超时时间，单位为秒。</param>
    /// <returns>所有命令影响行数的总和。</returns>
    private static int ExecuteMutationCommands(ISqlExecutor executor, IEnumerable<SqlMutationBatchCommand> batches,
        int? timeout)
    {
        var result = 0;
        foreach (var batch in batches)
        {
            var batchResult = 0;
            foreach (var command in batch.Commands)
            {
                var commandResult = executor.ExecuteSql(command.Sql, command.Parameters, timeout);
                ValidateAffectedRows(command, commandResult);
                batchResult = checked(batchResult + commandResult);
            }
            ValidateAffectedRows(batch, batchResult);
            result = checked(result + batchResult);
        }
        return result;
    }

    /// <summary>
    /// 按顺序异步执行已生成的 Mutation 命令。
    /// </summary>
    /// <param name="executor">用于执行命令的 SQL 执行器。</param>
    /// <param name="batches">按执行顺序排列的命令批次。</param>
    /// <param name="timeout">单条命令执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">每条命令执行前检查的取消令牌。</param>
    /// <returns>表示异步执行并返回总影响行数的任务。</returns>
    private static async Task<int> ExecuteMutationCommandsAsync(ISqlExecutor executor,
        IEnumerable<SqlMutationBatchCommand> batches, int? timeout, CancellationToken cancellationToken)
    {
        var result = 0;
        foreach (var batch in batches)
        {
            var batchResult = 0;
            foreach (var command in batch.Commands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var commandResult = await executor.ExecuteSqlAsync(command.Sql, command.Parameters, timeout,
                    cancellationToken).ConfigureAwait(false);
                ValidateAffectedRows(command, commandResult);
                batchResult = checked(batchResult + commandResult);
            }
            ValidateAffectedRows(batch, batchResult);
            result = checked(result + batchResult);
        }
        return result;
    }

    /// <summary>
    /// 将已探测的首批命令和同一枚举器中的后续命令组合为单次枚举序列。
    /// </summary>
    /// <param name="first">已探测的第一批命令。</param>
    /// <param name="enumerator">仍位于第一批后的原始枚举器。</param>
    /// <returns>按原始顺序输出的批量命令。</returns>
    private static IEnumerable<SqlMutationBatchCommand> EnumerateFirstAndRemaining(SqlMutationBatchCommand first,
        IEnumerator<SqlMutationBatchCommand> enumerator)
    {
        yield return first;
        while (enumerator.MoveNext())
            yield return enumerator.Current;
    }

    /// <summary>
    /// 验证带并发令牌的优化批量命令是否更新了全部目标实体。
    /// </summary>
    private static void ValidateAffectedRows(SqlMutationBatchCommand batch, int affectedRows)
    {
        if (batch.ValidateAffectedRows && affectedRows != batch.EntityCount)
            throw new Bing.Exceptions.ConcurrencyException(
                $"批量 {batch.OperationName} 预期影响 {batch.EntityCount} 行，实际影响 {affectedRows} 行。");
    }

    /// <summary>
    /// 执行单体 Mutation 命令并校验并发结果。
    /// </summary>
    /// <param name="command">待执行的单体 Mutation 命令。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <returns>命令实际影响的行数。</returns>
    private int ExecuteMutationCommand(SqlMutationCommand command, int? timeout)
    {
        return ExecuteSqlCore(command.Sql, command.Parameters, timeout,
            affectedRows => ValidateAffectedRows(command, affectedRows));
    }

    /// <summary>
    /// 异步执行单体 Mutation 命令并校验并发结果。
    /// </summary>
    /// <param name="command">待执行的单体 Mutation 命令。</param>
    /// <param name="timeout">命令执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">传递给数据库执行器的取消令牌。</param>
    /// <returns>表示异步执行并返回命令实际影响行数的任务。</returns>
    private async Task<int> ExecuteMutationCommandAsync(SqlMutationCommand command, int? timeout,
        CancellationToken cancellationToken)
    {
        return await ExecuteSqlCoreAsync(command.Sql, command.Parameters, timeout, cancellationToken,
            affectedRows => ValidateAffectedRows(command, affectedRows)).ConfigureAwait(false);
    }

    /// <summary>
    /// 验证单体带并发令牌的命令是否影响了一行。
    /// </summary>
    private static void ValidateAffectedRows(SqlMutationCommand command, int affectedRows)
    {
        if (command.ValidateAffectedRows && affectedRows != 1)
            throw new Bing.Exceptions.ConcurrencyException($"实体 Mutation 预期影响 1 行，实际影响 {affectedRows} 行。");
    }

    #endregion
}
