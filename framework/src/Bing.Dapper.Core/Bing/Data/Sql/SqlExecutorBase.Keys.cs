using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Mutations;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql;

/// <summary>
/// 提供 SQL 执行器的公共基础能力。
/// </summary>
public abstract partial class SqlExecutorBase
{
    /// <inheritdoc />
    public Task<InsertBatchResult<TKey>> InsertWithKeysAsync<TEntity, TKey>(IReadOnlyCollection<InsertRow<TEntity>> rows,
        bool requireNativeBulk = false, CancellationToken cancellationToken = default) where TEntity : class
    {
        if (rows == null) throw new ArgumentNullException(nameof(rows));
        if (rows.Count == 0 || rows.Count > 1000) throw new ArgumentOutOfRangeException(nameof(rows), "每次键映射批次必须为 1..1000 行。");
        if (requireNativeBulk) throw new NotSupportedException("该入口提供普通事务写入，不承诺原生 Bulk 返回键。");
        cancellationToken.ThrowIfCancellationRequested();
        WriteResources.EnsureWritableDataSource();
        var input = rows.ToArray();
        if (input.Any(x => x == null) || input.Select(x => x.CorrelationKey).Distinct(StringComparer.Ordinal).Count() != input.Length)
            throw new ArgumentException("输入关联标识重复或存在空行。", nameof(rows));
        var policy = ServiceProvider.GetRequiredService<IEntityKeyPolicyProvider>().Find(typeof(TEntity))
            ?? throw new InvalidOperationException("返回键入口需要显式实体策略。");
        if (policy.KeyType != typeof(TKey)) throw new ArgumentException("请求键类型与实体不一致。", nameof(TKey));
        if (ExecutionMode == Configs.SqlExecutionMode.GeneratedOnly && !policy.IsGenerated)
            throw new NotSupportedException("严格生成模式的主键策略必须使用 ForGenerated 静态访问器。");
        TryCreateStaticReader<TKey>(out _);
        var initializer = ServiceProvider.GetRequiredService<EntityKeyInitializer>();
        foreach (var row in input) initializer.Prepare(row.Entity, policy, cancellationToken);
        // 全批次预建命令：映射/能力错误必须发生在第一条 INSERT 前。
        var commands = input.Select(row => new SqlKeyInsertCommandBuilder(CreateInsertBuilder(), policy).Build(row.Entity)).ToArray();
        return ExecuteKeyCommandsAsync<TEntity, TKey>(input, commands, policy, cancellationToken);
    }

    /// <summary>
    /// 执行插入命令并回填实体主键。
    /// </summary>
    /// <typeparam name="TEntity">待插入或注册的实体类型。</typeparam>
    /// <typeparam name="TKey">实体主键的类型。</typeparam>
    /// <param name="rows">包含关联键及实体的待插入行。</param>
    /// <param name="commands">与待插入行对应的单行写命令。</param>
    /// <param name="policy">实体主键生成策略。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含关联键、生成主键、影响行数及提交状态的批量插入结果。</returns>
    /// <remarks>已有事务由其所有者提交；没有事务时创建独立事务作用域并在成功后提交。逐行读取或取得主键，并校验每行影响数和主键有效性。</remarks>
    private Task<InsertBatchResult<TKey>> ExecuteKeyCommandsAsync<TEntity, TKey>(InsertRow<TEntity>[] rows,
        SqlWriteCommand[] commands, EntityKeyPolicy policy, CancellationToken cancellationToken) where TEntity : class =>
        ExecuteDirectAsync(async (context, token) =>
        {
            if (!ExecuteBefore()) throw new InvalidOperationException("写入被执行钩子拒绝，未生成批次结果。");
            if (WriteResources.HasUnitOfWorkResource && !WriteResources.IsUnitOfWorkRootTransactional)
                throw new InvalidOperationException("关联键写入必须使用事务。");
            // 写入口不借用 PrimaryRead 的短事务；已有 UOW/显式事务由原所有者提交。
            var transaction = await WriteResources.GetWriteTransactionAsync(token).ConfigureAwait(false);
            if (transaction == null)
            {
                var factory = ServiceProvider.GetRequiredService<ISqlTransactionScopeFactory>();
                await using var scope = await factory.BeginAsync(GetDatabaseContext()?.DbKey, token).ConfigureAwait(false);
                try
                {
                    var executor = scope.CreateExecutor() as SqlExecutorBase ?? throw new NotSupportedException("事务执行器不支持生成键。");
                    var result = await executor.ExecuteKeyCommandsAsync<TEntity, TKey>(rows, commands, policy, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    await scope.CommitAsync(token).ConfigureAwait(false);
                    return new InsertBatchResult<TKey>(rows.Select(x => x.CorrelationKey), result.Keys,
                        result.AffectedRows, WriteCompletionState.Committed, result.ExecutionPath);
                }
                catch
                {
                    if (!scope.IsCompleted) await scope.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
            var connection = WriteResources.GetExecutionConnection();
            var oracleIdentity = policy.Kind == EntityKeyGenerationKind.DatabaseIdentity &&
                GetDatabaseType() == DatabaseType.Oracle;
            var keys = new Dictionary<string, TKey>(StringComparer.Ordinal);
            for (var index = 0; index < rows.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var command = commands[index];
                if (command.HasReturning) ValidateReturningMutationCommand(command);
                else ValidateExecutableMutationCommand(command);
                var prepared = PrepareCommand(command);
                context.Message = CreateExecutionDiagnostics(prepared, connection);
                WriteTraceLog(prepared);
                TKey key;
                if (command.HasReturning)
                {
                    var returned = CanExecuteStatically(prepared.DapperParameters)
                        ? (await ReadStaticRowsAsync(connection, prepared.Sql, prepared.DapperParameters, transaction, null,
                            new SqlStaticResultReader<TKey>(null, null, null), token, 2).ConfigureAwait(false)).ToArray()
                        : (await SqlDynamicCommandRunner.QueryAsync<TKey>(connection, new CommandDefinition(prepared.Sql, prepared.DapperParameters, transaction, cancellationToken: token)).ConfigureAwait(false)).ToArray();
                    if (returned.Length != 1) throw new InvalidOperationException("单行 INSERT 必须返回且只返回一个主键。");
                    key = returned[0];
                }
                else if (oracleIdentity)
                {
                    key = await SqlStaticCommandRunner.ExecuteAsync(connection, prepared.Sql, prepared.DapperParameters,
                        transaction, null, async (value, cancellation) =>
                        {
                            if (await value.ExecuteNonQueryAsync(cancellation).ConfigureAwait(false) != 1)
                                throw new InvalidOperationException("单行 INSERT 影响行数不等于一。");
                            // 构建器把输出参数附加在所有输入参数之后，驱动在同一命令上填充键值。
                            return ConvertScalar<TKey>(value.Parameters[value.Parameters.Count - 1].Value);
                        }, token).ConfigureAwait(false);
                }
                else
                {
                    var affected = CanExecuteStatically(prepared.DapperParameters)
                        ? await SqlStaticCommandRunner.ExecuteAsync(connection, prepared.Sql, prepared.DapperParameters, transaction,
                            null, (value, cancellation) => value.ExecuteNonQueryAsync(cancellation), token).ConfigureAwait(false)
                        : await SqlDynamicCommandRunner.ExecuteAsync(connection, new CommandDefinition(prepared.Sql, prepared.DapperParameters, transaction, cancellationToken: token)).ConfigureAwait(false);
                    if (affected != 1) throw new InvalidOperationException("单行 INSERT 影响行数不等于一。");
                    if (policy.Kind == EntityKeyGenerationKind.DatabaseIdentity)
                    {
                        if (GetDatabaseType() != DatabaseType.MySql) throw new NotSupportedException("缺少同连接自增返回适配。");
                        key = ExecutionMode != Configs.SqlExecutionMode.Compatible
                            ? await SqlStaticCommandRunner.ExecuteAsync(connection, "SELECT LAST_INSERT_ID()", null, transaction,
                                null, async (value, cancellation) => ConvertScalar<TKey>(await value.ExecuteScalarAsync(cancellation).ConfigureAwait(false)), token).ConfigureAwait(false)
                            : await SqlDynamicCommandRunner.ExecuteScalarAsync<TKey>(connection, new CommandDefinition("SELECT LAST_INSERT_ID()", transaction: transaction, cancellationToken: token)).ConfigureAwait(false);
                    }
                    else key = (TKey)policy.GetKey(rows[index].Entity);
                }
                if (EqualityComparer<TKey>.Default.Equals(key, default) || key is long id && id <= 0 || key is int intId && intId <= 0)
                    throw new InvalidOperationException("数据库返回无效主键。");
                policy.SetKey(rows[index].Entity, key);
                keys.Add(rows[index].CorrelationKey, key);
            }
            var batch = new InsertBatchResult<TKey>(rows.Select(x => x.CorrelationKey), keys, rows.Length,
                WriteCompletionState.Flushed, policy.Kind == EntityKeyGenerationKind.DatabaseIdentity ? "PerEntityIdentity" : "PerEntityAssigned");
            ExecuteAfter(context.Message);
            return batch;
        }, cancellationToken);

    // 延迟枚举保留既有窗口内存边界；只在显式注册策略时改变初始化行为。
    /// <summary>
    /// 准备待插入实体的主键。
    /// </summary>
    /// <typeparam name="TEntity">待插入或注册的实体类型。</typeparam>
    /// <param name="entities">待插入的实体序列。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按输入顺序枚举的实体；有主键策略时先完成主键准备。</returns>
    private IEnumerable<TEntity> PrepareInsertKeys<TEntity>(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) where TEntity : class
    {
        var policy = ServiceProvider.GetService<IEntityKeyPolicyProvider>()?.Find(typeof(TEntity));
        if (policy == null)
        {
            foreach (var entity in entities) yield return entity;
            yield break;
        }
        if (ExecutionMode == Configs.SqlExecutionMode.GeneratedOnly && !policy.IsGenerated)
            throw new NotSupportedException("严格生成模式的主键策略必须使用 ForGenerated 静态访问器。");
        var services = CreateSqlBuilderServices();
        var mapping = services.EntityMappingResolver.Resolve(typeof(TEntity), services.DatabaseContextResolver.Resolve(services.Options));
        if (!mapping.Columns.TryGetValue(policy.KeyPropertyName, out var key)) throw new InvalidOperationException("主键没有 SQL 映射。");
        policy.ValidateMapping(key.IsKey && mapping.Columns.Values.Count(x => x.IsKey) == 1 &&
            key.DatabaseGeneratedOption != System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Computed &&
            (policy.Kind != EntityKeyGenerationKind.DatabaseIdentity ||
             key.DatabaseGeneratedOption == System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity), key.IsDatabaseGenerated);
        var initializer = ServiceProvider.GetRequiredService<EntityKeyInitializer>();
        foreach (var entity in entities)
        {
            initializer.Prepare(entity, policy, cancellationToken);
            yield return entity;
        }
    }
}
