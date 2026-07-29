using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象基类
/// </summary>
public abstract class SqlExecutorBase : SqlQueryBase, ISqlExecutor
{
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

    #region Insert(插入实体)

    /// <inheritdoc />
    public virtual int Insert<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> InsertAsync<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Insert(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int InsertBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null,
        int? timeout = null) where TEntity : class => ExecuteMutationBatch(CreateInsertBatchCommands(entities, options),
        options?.UseTransaction ?? true, timeout);

    /// <inheritdoc />
    public virtual Task<int> InsertBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class =>
        ExecuteMutationBatchAsync(CreateInsertBatchCommands(entities, options), options?.UseTransaction ?? true, timeout,
            cancellationToken);

    #endregion

    #region Update(更新实体)

    /// <inheritdoc />
    public virtual int Update<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Update(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> UpdateAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Update(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int UpdateBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null,
        int? timeout = null) where TEntity : class => ExecuteMutationBatch(
        CreateMutationBatchCommands(entities, entity => CreateMutationBuilder().Update(entity, options?.UpdateOptions), options),
        options?.UseTransaction ?? true, timeout);

    /// <inheritdoc />
    public virtual Task<int> UpdateBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class => ExecuteMutationBatchAsync(
        CreateMutationBatchCommands(entities, entity => CreateMutationBuilder().Update(entity, options?.UpdateOptions), options),
        options?.UseTransaction ?? true, timeout, cancellationToken);

    #endregion

    #region Delete(删除实体)

    /// <inheritdoc />
    public virtual int Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Delete(entity, options);
        return ExecuteSql(command.Sql, command.Parameters, timeout);
    }

    /// <inheritdoc />
    public virtual Task<int> DeleteAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var command = CreateMutationBuilder().Delete(entity, options);
        return ExecuteSqlAsync(command.Sql, command.Parameters, timeout, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int DeleteBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null,
        int? timeout = null) where TEntity : class => ExecuteMutationBatch(
        CreateMutationBatchCommands(entities, entity => CreateMutationBuilder().Delete(entity, options?.DeleteOptions), options),
        options?.UseTransaction ?? true, timeout);

    /// <inheritdoc />
    public virtual Task<int> DeleteBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class => ExecuteMutationBatchAsync(
        CreateMutationBatchCommands(entities, entity => CreateMutationBuilder().Delete(entity, options?.DeleteOptions), options),
        options?.UseTransaction ?? true, timeout, cancellationToken);

    #endregion

    #region ExecuteSql(执行Sql增删改操作)

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    public virtual int ExecuteSql(string sql, object param = null, int? timeout = null)
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, sql);
            var parameterMetadata = GetSqlParameterDiagnostics(param, sql);
            message = ExecuteBefore(sql, param, connection, parameterMetadata);
            result = connection.Execute(sql, dbParameters, transaction, timeout);
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
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
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, sql);
            var parameterMetadata = GetSqlParameterDiagnostics(param, sql);
            message = ExecuteBefore(sql, param, connection, parameterMetadata);
            result = await connection.ExecuteAsync(new CommandDefinition(sql, dbParameters, transaction, timeout,
                cancellationToken: cancellationToken));
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
    }

    #endregion

    #region ExecuteProcedure(执行存储过程增删改操作)

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>受影响行数</returns>
    public virtual int ExecuteProcedure(string procedure, object param = null, int? timeout = null)
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, procedure);
            var parameterMetadata = GetSqlParameterDiagnostics(param, procedure);
            message = ExecuteBefore(procedure, param, connection, parameterMetadata);
            result = connection.Execute(procedure, dbParameters, transaction, timeout, GetProcedureCommandType());
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
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
    /// <returns>受影响行数</returns>
    public virtual async Task<int> ExecuteProcedureAsync(string procedure, object param = null, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        using var executionLease = AcquireExecutionLease();
        var result = 0;
        DiagnosticsMessage message = default;
        try
        {
            if (ExecuteBefore() == false)
                return 0;
            var connection = GetExecutionConnection();
            var transaction = GetQueryTransaction();
            var dbParameters = GetDbParameters(param, procedure);
            var parameterMetadata = GetSqlParameterDiagnostics(param, procedure);
            message = ExecuteBefore(procedure, param, connection, parameterMetadata);
            result = await connection.ExecuteAsync(new CommandDefinition(procedure, dbParameters, transaction, timeout,
                GetProcedureCommandType(), cancellationToken: cancellationToken));
            CompleteQueryTransaction();
            ExecuteAfter(message);
            return result;
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
        finally
        {
            ExecuteAfter(result);
        }
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
    private ISqlMutationBuilder CreateMutationBuilder() =>
        CreateMutationBuilder(ResolveMutationProvider());

    /// <summary>
    /// 使用指定 Provider 创建绑定当前 Executor 共享服务的实体写入 Builder。
    /// </summary>
    /// <param name="provider">已解析的当前 SQL Provider。</param>
    /// <returns>实体写入 Builder。</returns>
    private ISqlMutationBuilder CreateMutationBuilder(ISqlProvider provider)
    {
        var factory = ServiceProvider.GetService<ISqlMutationBuilderFactory>();
        if (factory == null)
            throw new InvalidOperationException("未注册实体写入 SQL Builder 工厂。");
        return factory.CreateEntity(provider, CreateSqlBuilderServices());
    }

    /// <summary>
    /// 解析当前 Executor 数据源对应的 SQL Provider。
    /// </summary>
    /// <returns>当前数据源的 SQL Provider。</returns>
    private ISqlProvider ResolveMutationProvider()
    {
        var databaseType = GetDatabaseType();
        var provider = ServiceProvider.GetServices<ISqlProvider>()
            .FirstOrDefault(item => item.DatabaseType == databaseType);
        if (provider == null)
            throw new InvalidOperationException($"未注册数据库类型 {databaseType} 的 SQL Provider。");
        return provider;
    }

    /// <summary>
    /// 确定批量 Insert 是否应使用组合式多行 Values 命令。
    /// </summary>
    /// <param name="options">批量插入选项。</param>
    /// <returns>应使用组合式 Insert 时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
    private bool ShouldUseCombinedInsert(SqlBatchInsertOptions options)
    {
        if (options?.Strategy == SqlBatchStrategy.PerEntity)
            return false;
        if (options?.Strategy == SqlBatchStrategy.Combined)
            return true;
        var provider = ResolveMutationProvider();
        return provider is ISqlProviderCapabilityProvider { Capabilities.SupportsMultiRowValues: true };
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
        if (options.Strategy == SqlBatchStrategy.Combined)
            throw new NotSupportedException("当前 Provider 尚未实现合并式 Mutation 批量命令。");
        var parametersPerEntity = commands.Max(command => command.Parameters.Count);
        var estimatedSqlLengthPerEntity = commands.Max(command => command.Sql.Length);
        var maxParameterCount = options.GetEffectiveMaxParameterCount(ResolveMutationProvider());
        var plan = new SqlMutationBatchPlanner().Plan(new SqlMutationBatchPlanContext(commands.Count,
            Math.Max(1, parametersPerEntity), maxParameterCount: maxParameterCount,
            estimatedSqlLengthPerEntity: estimatedSqlLengthPerEntity, maxSqlLength: options.MaxSqlLength, options: options));
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
        if (provider is not ISqlProviderCapabilityProvider { Capabilities.SupportsMultiRowValues: true })
            throw new NotSupportedException($"Provider {provider.Key} 未声明支持组合式 Insert 批量命令。");
        if (CreateMutationBuilder(provider) is not ISqlCombinedInsertMutationBuilder)
        {
            if (options?.Strategy == SqlBatchStrategy.Auto)
                return CreateMutationBatchCommands(items,
                    entity => CreateMutationBuilder(provider).Insert(entity, options.InsertOptions), options);
            throw new NotSupportedException($"Provider {provider.Key} 未实现组合式 Insert 批量命令。");
        }
        var commands = items.Select(entity => CreateMutationBuilder(provider).Insert(entity, options.InsertOptions)).ToList();
        var parametersPerEntity = commands.Max(command => command.Parameters.Count);
        var maxParameterCount = options.GetEffectiveMaxParameterCount(provider);
        var plan = new SqlMutationBatchPlanner().Plan(new SqlMutationBatchPlanContext(items.Count,
            Math.Max(1, parametersPerEntity), maxParameterCount: maxParameterCount,
            estimatedSqlLengthPerEntity: 0, options: options));
        var batches = new List<SqlMutationBatchCommand>(plan.BatchSizes.Count);
        var offset = 0;
        foreach (var size in plan.BatchSizes)
        {
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
    private int ExecuteMutationBatch(IReadOnlyList<SqlMutationBatchCommand> batches, bool useTransaction, int? timeout)
    {
        if (batches == null || batches.Count == 0)
            return 0;
        if (useTransaction == false)
            return ExecuteMutationCommands(this, batches, timeout);
        var factory = ServiceProvider.GetService<ISqlTransactionScopeFactory>() ??
            throw new InvalidOperationException("未注册 SQL 事务作用域工厂。");
        using var scope = factory.Begin(GetDatabaseContext()?.DbKey);
        int result;
        try
        {
            result = ExecuteMutationCommands(scope.CreateExecutor(), batches, timeout);
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
    private async Task<int> ExecuteMutationBatchAsync(IReadOnlyList<SqlMutationBatchCommand> batches,
        bool useTransaction, int? timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (batches == null || batches.Count == 0)
            return 0;
        if (useTransaction == false)
            return await ExecuteMutationCommandsAsync(this, batches, timeout, cancellationToken).ConfigureAwait(false);
        var factory = ServiceProvider.GetService<ISqlTransactionScopeFactory>() ??
            throw new InvalidOperationException("未注册 SQL 事务作用域工厂。");
        await using var scope = await factory.BeginAsync(GetDatabaseContext()?.DbKey, cancellationToken).ConfigureAwait(false);
        int result;
        try
        {
            result = await ExecuteMutationCommandsAsync(scope.CreateExecutor(), batches, timeout, cancellationToken)
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
        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 按顺序执行已生成的 Mutation 命令。
    /// </summary>
    private static int ExecuteMutationCommands(ISqlExecutor executor, IEnumerable<SqlMutationBatchCommand> batches,
        int? timeout)
    {
        var result = 0;
        foreach (var command in batches.SelectMany(batch => batch.Commands))
            result = checked(result + executor.ExecuteSql(command.Sql, command.Parameters, timeout));
        return result;
    }

    /// <summary>
    /// 按顺序异步执行已生成的 Mutation 命令。
    /// </summary>
    private static async Task<int> ExecuteMutationCommandsAsync(ISqlExecutor executor,
        IEnumerable<SqlMutationBatchCommand> batches, int? timeout, CancellationToken cancellationToken)
    {
        var result = 0;
        foreach (var command in batches.SelectMany(batch => batch.Commands))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = checked(result + await executor.ExecuteSqlAsync(command.Sql, command.Parameters, timeout, cancellationToken)
                .ConfigureAwait(false));
        }
        return result;
    }

    #endregion
}
