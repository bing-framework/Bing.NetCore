using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认单实体写入 SQL 生成器。
/// </summary>
public sealed class DefaultSqlEntityMutationCommandBuilder : ISqlEntityMutationCommandBuilder, ISqlCombinedInsertMutationBuilder,
    ISqlCombinedDeleteMutationBuilder, ISqlBatchUpdateRenderContextBuilder
{
    /// <summary>
    /// 当前 SQL Provider。
    /// </summary>
    private readonly ISqlProvider _provider;

    /// <summary>
    /// 当前命令可共享的服务。
    /// </summary>
    private readonly SqlBuilderServices _services;

    /// <summary>
    /// 当前命令的数据库上下文快照。
    /// </summary>
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// 当前映射解析器分区内的 Mutation Plan 和 Getter 缓存。
    /// </summary>
    private readonly SqlMutationPlanCache _planCache;

    /// <summary>仅默认 Provider 命名、子句及参数服务可复用当前生成器的 SQL 文本。</summary>
    private readonly bool _useGeneratedSql;

    /// <summary>
    /// 初始化一个 <see cref="DefaultSqlEntityMutationCommandBuilder"/> 类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">当前命令可共享的服务。</param>
    public DefaultSqlEntityMutationCommandBuilder(ISqlProvider provider, SqlBuilderServices services)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _databaseContext = _services.DatabaseContextResolver.Resolve(_services.Options);
        _planCache = SqlMutationPlanCaches.Get(_services.EntityMappingResolver, _services.MetadataOptions, _services.EntityModelMetadataProvider);
        _useGeneratedSql = SqlMutationPlan.SupportsGeneratedSql(_provider, _services);
    }

    /// <summary>
    /// 获取当前映射解析器分区内已缓存的 Mutation Plan 数量。
    /// </summary>
    internal int PlanCacheCount => _planCache.PlanCount;

    /// <summary>
    /// 获取当前映射解析器分区内已缓存的属性 Getter 数量。
    /// </summary>
    internal int GetterCacheCount => _planCache.GetterCount;

    /// <inheritdoc />
    public SqlWriteCommand Insert<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class =>
        InsertCore(entity, options, false);

    /// <inheritdoc />
    public SqlWriteCommand InsertReturning<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class =>
        InsertCore(entity, options, true);

    /// <summary>共用实体插入计划，按需追加整行返回列。</summary>
    private SqlWriteCommand InsertCore<TEntity>(TEntity entity, SqlInsertOptions options, bool returning) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Insert, options?.IncludeProperties,
            options?.ExcludeProperties);
        var mapping = plan.Mapping;
        var columns = plan.WriteColumns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可插入列。");
        var generatedSql = GetGeneratedSql(plan, returning: returning);
        if (generatedSql != null)
            return BuildGeneratedCommand(generatedSql, entity, plan, SqlOperationKind.InsertValues, returning);
        var builder = new SqlInsertBuilder(_provider, _services);
        builder.InsertClause.Into(mapping.Table);
        builder.InsertColumnsClause.AddRange(columns.Select(column => column.ColumnName));
        var parameters = new List<SqlParam>(columns.Count);
        foreach (var column in columns)
        {
            var parameter = CreateParameter(builder.MutationContext.ParameterManager, entity, column, typeof(TEntity), plan);
            parameters.Add(parameter);
        }
        builder.ValuesClause.AddRow(parameters);
        if (returning)
            ConfigureReturning(builder, plan);
        return WithGeneratedParameterBinding(builder.BuildCommand(generatedSql, returning), plan);
    }

    /// <inheritdoc />
    public SqlWriteCommand InsertCombined<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlInsertOptions options = null) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        if (entities.Count == 0)
            throw new ArgumentException("组合 Insert 实体集合不能为空。", nameof(entities));
        if (entities.Any(entity => entity == null))
            throw new ArgumentException("组合 Insert 实体集合不能包含 null。", nameof(entities));
        if (entities.Count > 1)
        {
            if (SqlProviderCapabilityResolver.HasProfile(_provider) == false)
                throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMissing, "MultiRowValues",
                    _provider.Key, $"Provider {_provider.Key} 不支持多行 Values。");
            var profile = SqlProviderCapabilityResolver.GetProfile(_provider);
            if (SqlProviderCapabilityResolver.HasCompleteProfile(_provider) == false)
                throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMismatch, "MultiRowValues",
                    _provider.Key, $"Provider {_provider.Key} 的 Mutation 能力 Profile 不完整。[ProfileMismatch]");
            if (profile.Mutation.SupportsMultiRowValues == false)
                throw SqlCapabilityFailure.Create(profile.Mutation.MultiRowValuesFailureReason ??
                    SqlCapabilityFailureReason.ProviderImplementationGap, "MultiRowValues",
                    _provider.Key, $"Provider {_provider.Key} 不支持多行 Values。");
        }
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Insert, options?.IncludeProperties,
            options?.ExcludeProperties);
        var mapping = plan.Mapping;
        var columns = plan.WriteColumns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可插入列。");
        // 默认参数服务每列生成一个参数，先拒绝超限批次，避免构造无效 SQL 和读取全部实体值。
        if (_useGeneratedSql)
            SqlMutationBuilderBase.ValidateParameterLimit(_provider, (long)entities.Count * columns.Count);
        var generatedSql = GetGeneratedSql(plan, entities.Count);
        if (generatedSql != null)
        {
            var parameters = _provider.ParameterManagerFactory.Create(_provider.Dialect);
            foreach (var entity in entities)
                foreach (var column in columns)
                    AddParameter(parameters, CreateParameter(parameters, entity, column, typeof(TEntity), plan));
            return new SqlWriteCommand(generatedSql, ((IAdvancedParameterManager)parameters).GetSqlParams().Values,
                _provider, SqlOperationKind.InsertValues, false, false,
                plan.CanUseGeneratedParameterBinding ? SqlGeneratedParameterBinding.Bind : null);
        }
        var builder = new SqlInsertBuilder(_provider, _services);
        builder.InsertClause.Into(mapping.Table);
        builder.InsertColumnsClause.AddRange(columns.Select(column => column.ColumnName));
        foreach (var entity in entities)
        {
            var parameters = new List<SqlParam>(columns.Count);
            foreach (var column in columns)
            {
                var parameter = CreateParameter(builder.MutationContext.ParameterManager, entity, column,
                    typeof(TEntity), plan);
                parameters.Add(parameter);
            }
            builder.ValuesClause.AddRow(parameters);
        }
        return WithGeneratedParameterBinding(builder.BuildCommand(generatedSql), plan);
    }

    /// <inheritdoc />
    public SqlWriteCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class =>
        UpdateCore(entity, options, false);

    /// <inheritdoc />
    public SqlWriteCommand UpdateReturning<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class =>
        UpdateCore(entity, options, true);

    /// <summary>共用实体更新计划，保留原始值和并发校验。</summary>
    private SqlWriteCommand UpdateCore<TEntity>(TEntity entity, SqlUpdateOptions options, bool returning) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Update, options?.IncludeProperties,
            options?.ExcludeProperties);
        var mapping = plan.Mapping;
        var columns = plan.WriteColumns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可更新列。");
        EnsureKeys(plan, typeof(TEntity), "更新");
        var generatedSql = RequiresDataBoundaryBuilder(plan, SqlOperationKind.Update)
            ? null : GetGeneratedSql(plan, returning: returning);
        if (generatedSql != null)
            return BuildGeneratedCommand(generatedSql, entity, plan, SqlOperationKind.Update, returning, update: options);
        var builder = new SqlUpdateBuilder(_provider, _services);
        builder.UpdateClause.UpdateTable(mapping.Table);
        foreach (var column in columns)
        {
            var parameter = CreateParameter(builder.MutationContext.ParameterManager, entity, column, typeof(TEntity), plan);
            builder.SetClause.Set(column.ColumnName, parameter);
        }
        ConfigureWhere(builder.WhereClause, plan, entity, options, builder.MutationContext.ParameterManager);
        if (returning)
            ConfigureReturning(builder, plan);
        return WithConcurrencyValidation(WithGeneratedParameterBinding(
            builder.BuildCommand(generatedSql, returning), plan), plan,
            options?.ConcurrencyConflictBehavior);
    }

    /// <inheritdoc />
    public SqlBatchUpdateRenderContext CreateUpdateRenderContext<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlUpdateOptions options = null) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        if (entities.Count == 0)
            throw new ArgumentException("批量 Update 实体集合不能为空。", nameof(entities));
        if (entities.Any(entity => entity == null))
            throw new ArgumentException("批量 Update 实体集合不能包含 null。", nameof(entities));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Update, options?.IncludeProperties,
            options?.ExcludeProperties);
        if (plan.WriteColumns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可更新列。");
        if (plan.Keys.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有主键，不能执行优化批量 Update。");
        return new SqlBatchUpdateRenderContext(_provider, _services, _databaseContext, plan.Mapping,
            plan.WriteColumns, plan.Keys, plan.ConcurrencyColumns, entities.Cast<object>().ToArray(), options);
    }

    /// <inheritdoc />
    public SqlWriteCommand Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
            return SetSoftDeleteState(entity, true, options);
        return Purge(entity, options);
    }

    /// <inheritdoc />
    public SqlWriteCommand DeleteReturning<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        return typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity))
            ? SetSoftDeleteState(entity, true, options, returning: true)
            : PurgeCore(entity, options, true);
    }

    /// <inheritdoc />
    public SqlWriteCommand Purge<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class =>
        PurgeCore(entity, options, false);

    /// <summary>共用物理删除计划，返回列读取删除前的行。</summary>
    private SqlWriteCommand PurgeCore<TEntity>(TEntity entity, SqlDeleteOptions options, bool returning) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Delete, null, null);
        EnsureKeys(plan, typeof(TEntity), "物理清除");
        var generatedSql = GetGeneratedSql(plan, returning: returning);
        if (generatedSql != null && !RequiresDataBoundaryBuilder(plan, SqlOperationKind.Delete))
            return BuildGeneratedCommand(generatedSql, entity, plan, SqlOperationKind.Delete, returning, delete: options);
        var mapping = plan.Mapping;
        var builder = new SqlDeleteBuilder(_provider, _services);
        builder.DataBoundaryOperation = SqlDataBoundaryOperation.Purge;
        builder.DeleteClause.From(mapping.Table);
        ConfigureWhere(builder.WhereClause, plan, entity, options, builder.MutationContext.ParameterManager);
        if (returning)
            ConfigureReturning(builder, plan);
        return WithConcurrencyValidation(WithGeneratedParameterBinding(
            builder.BuildCommand(generatedSql, returning), plan), plan,
            options?.ConcurrencyConflictBehavior);
    }

    /// <inheritdoc />
    public SqlWriteCommand Restore<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)) == false)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 未实现 ISoftDelete，不能恢复。");
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Update, null, null);
        EnsureKeys(plan, typeof(TEntity), "恢复");
        var stateColumn = GetSoftDeleteStateColumn(plan);
        var builder = new SqlUpdateBuilder(_provider, _services)
        {
            DataBoundaryOperation = SqlDataBoundaryOperation.Restore
        };
        builder.UpdateClause.UpdateTable(plan.Mapping.Table);
        var stateParameter = _services.ParameterFactory.Create(builder.MutationContext.ParameterManager.GenerateName(), false,
            stateColumn, _databaseContext, typeof(TEntity), SqlParameterSource.SqlBuilder);
        builder.SetClause.Set(stateColumn.ColumnName, stateParameter);
        ConfigureWhere(builder.WhereClause, plan, entity, options, builder.MutationContext.ParameterManager);
        return WithConcurrencyValidation(WithGeneratedParameterBinding(builder.BuildCommand(), plan), plan,
            options?.ConcurrencyConflictBehavior);
    }

    /// <inheritdoc />
    public SqlWriteCommand DeleteCombined<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlDeleteOptions options = null, SqlBatchDeleteStrategy strategy = SqlBatchDeleteStrategy.Auto) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        if (entities.Count == 0)
            throw new ArgumentException("组合 Delete 实体集合不能为空。", nameof(entities));
        if (entities.Any(entity => entity == null))
            throw new ArgumentException("组合 Delete 实体集合不能包含 null。", nameof(entities));
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
            throw new NotSupportedException("逻辑删除实体不支持组合物理 Delete，请逐实体调用 Delete 或显式调用 Purge。");
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Delete, null, null);
        EnsureKeys(plan, typeof(TEntity), "批量 Delete");
        var canUseInPredicate = plan.Keys.Count == 1 && plan.ConcurrencyColumns.Count == 0;
        if (strategy == SqlBatchDeleteStrategy.InPredicate && canUseInPredicate == false)
            throw new NotSupportedException("InPredicate 策略仅支持不带并发令牌的单主键实体。");
        if ((!canUseInPredicate || strategy != SqlBatchDeleteStrategy.CompositePredicate) && entities.Count > 1 &&
            _useGeneratedSql && !RequiresDataBoundaryBuilder(plan, SqlOperationKind.Delete))
        {
            // 配对删除每行包含全部键及令牌，先拒绝超限批次，再展开 SQL 或读取实体。
            SqlMutationBuilderBase.ValidateParameterLimit(_provider,
                checked(entities.Count * (plan.Keys.Count + plan.ConcurrencyColumns.Count)));
            var generatedSql = GetGeneratedSql(plan, entities.Count);
            if (generatedSql != null)
            {
                var parameters = _provider.ParameterManagerFactory.Create(_provider.Dialect);
                foreach (var entity in entities)
                {
                    for (var index = 0; index < plan.Keys.Count; index++)
                        CreateConditionParameter(parameters, plan, entity, plan.Keys[index], typeof(TEntity));
                    for (var index = 0; index < plan.ConcurrencyColumns.Count; index++)
                    {
                        var column = plan.ConcurrencyColumns[index];
                        var hasOriginalValue = TryGetOriginalValue(options, column.PropertyName, out var value);
                        CreateConditionParameter(parameters, plan, entity, column, typeof(TEntity), hasOriginalValue, value);
                    }
                }
                var validateAffectedRows = plan.ConcurrencyColumns.Count > 0 &&
                    (options?.ConcurrencyConflictBehavior ?? SqlConcurrencyConflictBehavior.Throw) == SqlConcurrencyConflictBehavior.Throw;
                return new SqlWriteCommand(generatedSql, ((IAdvancedParameterManager)parameters).GetSqlParams().Values,
                    _provider, SqlOperationKind.Delete, false, validateAffectedRows,
                    plan.CanUseGeneratedParameterBinding ? SqlGeneratedParameterBinding.Bind : null);
            }
        }
        var builder = new SqlDeleteBuilder(_provider, _services);
        builder.DeleteClause.From(plan.Mapping.Table);
        if (canUseInPredicate && strategy != SqlBatchDeleteStrategy.CompositePredicate)
            ConfigureSingleKeyInWhere(builder.WhereClause, plan, entities, builder.MutationContext.ParameterManager);
        else
            ConfigurePairedWhere(builder.WhereClause, plan, entities, options, builder.MutationContext.ParameterManager);
        return WithConcurrencyValidation(WithGeneratedParameterBinding(builder.BuildCommand(), plan), plan,
            options?.ConcurrencyConflictBehavior);
    }

    /// <summary>
    /// 生成更新逻辑删除状态的实体命令。
    /// </summary>
    /// <typeparam name="TEntity">逻辑删除实体类型。</typeparam>
    /// <param name="entity">包含主键和并发属性值的实体。</param>
    /// <param name="isDeleted">目标逻辑删除状态。</param>
    /// <param name="options">可选的 Delete 原始值和并发配置。</param>
    /// <param name="returning">是否返回更新后的完整行。</param>
    /// <returns>带参数和并发校验信息的可执行 Update 命令。</returns>
    private SqlWriteCommand SetSoftDeleteState<TEntity>(TEntity entity, bool isDeleted, SqlDeleteOptions options,
        bool returning = false)
        where TEntity : class
    {
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Update, null, null);
        EnsureKeys(plan, typeof(TEntity), isDeleted ? "逻辑删除" : "恢复");
        var stateColumn = GetSoftDeleteStateColumn(plan);
        var builder = new SqlUpdateBuilder(_provider, _services)
        {
            DataBoundaryOperation = isDeleted ? SqlDataBoundaryOperation.SoftDelete : SqlDataBoundaryOperation.Restore
        };
        builder.UpdateClause.UpdateTable(plan.Mapping.Table);
        var stateParameter = _services.ParameterFactory.Create(builder.MutationContext.ParameterManager.GenerateName(), isDeleted,
            stateColumn, _databaseContext, typeof(TEntity), SqlParameterSource.SqlBuilder);
        builder.SetClause.Set(stateColumn.ColumnName, stateParameter);
        ConfigureWhere(builder.WhereClause, plan, entity, options, builder.MutationContext.ParameterManager);
        if (returning)
            ConfigureReturning(builder, plan);
        return WithConcurrencyValidation(WithGeneratedParameterBinding(builder.BuildCommand(), plan), plan,
            options?.ConcurrencyConflictBehavior);
    }


    /// <summary>
    /// 获取逻辑删除状态列映射。
    /// </summary>
    /// <param name="plan">当前实体 Mutation 计划。</param>
    /// <returns>逻辑删除状态列。</returns>
    private static ColumnMappingMetadata GetSoftDeleteStateColumn(SqlMutationPlan plan) =>
        plan.Mapping.Columns.Values.FirstOrDefault(column =>
            string.Equals(column.PropertyName, nameof(ISoftDelete.IsDeleted), StringComparison.OrdinalIgnoreCase)) ??
        throw new InvalidOperationException($"逻辑删除实体 {plan.Mapping.EntityType.Name} 的属性 {nameof(ISoftDelete.IsDeleted)} 未映射到数据库列。");

    /// <summary>
    /// 解析实体的最终映射。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <returns>实体映射。</returns>
    private EntityMappingMetadata ResolveMapping(Type entityType)
    {
        var mapping = _services.EntityMappingResolver.Resolve(entityType, _databaseContext);
        if (mapping?.Table == null)
            throw new InvalidOperationException($"未找到实体 {entityType.Name} 的数据库表映射。");
        _services.TableReferenceValidator.Validate(mapping.Table, _provider.DatabaseType);
        return mapping;
    }

    /// <summary>
    /// 解析并缓存实体 Mutation Plan。
    /// </summary>
    /// <param name="entityType">实体类型。</param>
    /// <param name="operation">Mutation 操作类型。</param>
    /// <param name="includes">仅包含属性名集合。</param>
    /// <param name="excludes">排除属性名集合。</param>
    /// <returns>可复用的映射计划。</returns>
    private SqlMutationPlan ResolvePlan(Type entityType, SqlMutationOperation operation,
        IReadOnlyCollection<string> includes, IReadOnlyCollection<string> excludes)
    {
        var mapping = ResolveMapping(entityType);
        return _planCache.GetOrAdd(mapping, _provider.Key, operation, includes, excludes);
    }

    /// <summary>自定义命名或参数服务继续使用其原有渲染路径。</summary>
    private string GetGeneratedSql(SqlMutationPlan plan, int rowCount = 1, bool returning = false) =>
        !_useGeneratedSql ? null : returning
            ? plan.GetGeneratedReturningSql(_provider.Key)
            : plan.GetGeneratedSql(_provider.Key, rowCount);

    /// <summary>自定义数据边界继续由 Builder 应用；内置逻辑删除过滤器仅在适用实体的 Update 上需要它。</summary>
    private bool RequiresDataBoundaryBuilder(SqlMutationPlan plan, SqlOperationKind operation)
    {
        foreach (var filter in _services.Filters)
        {
            if (filter is not ISqlDataBoundaryContributor)
                continue;
            if (filter.GetType() == typeof(IsDeletedFilter) &&
                (operation == SqlOperationKind.Delete || !typeof(ISoftDelete).IsAssignableFrom(plan.Mapping.EntityType)))
                continue;
            return true;
        }
        return false;
    }

    /// <summary>完整 SQL 已生成时只创建参数和冻结命令，不再分配可变子句或格式化条件。</summary>
    private SqlWriteCommand BuildGeneratedCommand(string sql, object entity, SqlMutationPlan plan,
        SqlOperationKind operation, bool returning, SqlUpdateOptions update = null, SqlDeleteOptions delete = null)
    {
        // 保留默认参数管理器的名称规范化及方言值转换；不可直接跳过这一步绑定实体原值。
        var parameters = _provider.ParameterManagerFactory.Create(_provider.Dialect);
        if (operation != SqlOperationKind.Delete)
            foreach (var column in plan.WriteColumns)
                AddParameter(parameters, CreateParameter(parameters, entity, column, plan.Mapping.EntityType, plan));
        if (operation != SqlOperationKind.InsertValues)
        {
            foreach (var key in plan.Keys)
                CreateConditionParameter(parameters, plan, entity, key, plan.Mapping.EntityType);
            foreach (var column in plan.ConcurrencyColumns)
            {
                object value;
                var hasOriginal = operation == SqlOperationKind.Update
                    ? TryGetOriginalValue(update, column.PropertyName, out value)
                    : TryGetOriginalValue(delete, column.PropertyName, out value);
                CreateConditionParameter(parameters, plan, entity, column, plan.Mapping.EntityType, hasOriginal, value);
            }
        }
        SqlMutationBuilderBase.ValidateParameterLimit(_provider, parameters.Count);
        if (returning)
            ReturningClause.ValidateProvider(new SqlValidationContext(_provider, parameters.Count, false,
                operation switch
                {
                    SqlOperationKind.InsertValues => SqlExecutionKind.Insert,
                    SqlOperationKind.Update => SqlExecutionKind.Update,
                    _ => SqlExecutionKind.Delete
                }));
        var behavior = update?.ConcurrencyConflictBehavior ?? delete?.ConcurrencyConflictBehavior;
        var validateAffectedRows = operation != SqlOperationKind.InsertValues && plan.ConcurrencyColumns.Count > 0 &&
            (behavior ?? SqlConcurrencyConflictBehavior.Throw) == SqlConcurrencyConflictBehavior.Throw;
        return new SqlWriteCommand(sql, ((IAdvancedParameterManager)parameters).GetSqlParams().Values, _provider,
            operation, returning, validateAffectedRows,
            plan.CanUseGeneratedParameterBinding ? SqlGeneratedParameterBinding.Bind : null);
    }

    /// <summary>返回所有最终映射列，并用 CLR 属性名连接已有实体读取器。</summary>
    private static void ConfigureReturning(SqlMutationBuilderBase builder, SqlMutationPlan plan) =>
        builder.ReturningClause.AddRange(plan.Mapping.Columns.Values
            .Select(column => new SqlReturningColumn(column.ColumnName, alias: column.PropertyName)).ToArray());

    /// <summary>
    /// 验证实体 Mutation 具有主键条件。
    /// </summary>
    /// <param name="plan">已解析的实体 Mutation 计划。</param>
    /// <param name="entityType">实体类型。</param>
    /// <param name="operation">当前实体写入操作名称。</param>
    private static void EnsureKeys(SqlMutationPlan plan, Type entityType, string operation)
    {
        if (plan.Keys.Count == 0)
            throw new InvalidOperationException($"实体 {entityType.Name} 没有主键，不能执行{operation}。" );
    }

    /// <summary>
    /// 配置 Mutation 条件子句。
    /// </summary>
    /// <param name="plan">已解析的 Mutation 映射计划。</param>
    /// <param name="entity">实体当前值。</param>
    /// <param name="options">更新选项。</param>
    /// <param name="whereClause">待配置的 Mutation Where 子句。</param>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    private void ConfigureWhere(IMutationWhereClause whereClause, SqlMutationPlan plan, object entity,
        SqlUpdateOptions options, IParameterManager parameterManager)
    {
        foreach (var key in plan.Keys)
            whereClause.And(CreateCondition(parameterManager, plan, entity, key, plan.Mapping.EntityType));
        foreach (var concurrency in plan.ConcurrencyColumns)
            whereClause.And(CreateCondition(parameterManager, plan, entity, concurrency, plan.Mapping.EntityType,
                TryGetOriginalValue(options, concurrency.PropertyName, out var value), value));
    }

    /// <summary>
    /// 配置 Mutation 条件子句。
    /// </summary>
    /// <param name="whereClause">待配置的 Mutation Where 子句。</param>
    /// <param name="plan">已解析的 Mutation 映射计划。</param>
    /// <param name="entity">实体当前值。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    private void ConfigureWhere(IMutationWhereClause whereClause, SqlMutationPlan plan, object entity,
        SqlDeleteOptions options, IParameterManager parameterManager)
    {
        foreach (var key in plan.Keys)
            whereClause.And(CreateCondition(parameterManager, plan, entity, key, plan.Mapping.EntityType));
        foreach (var concurrency in plan.ConcurrencyColumns)
            whereClause.And(CreateCondition(parameterManager, plan, entity, concurrency, plan.Mapping.EntityType,
                TryGetOriginalValue(options, concurrency.PropertyName, out var value), value));
    }

    /// <summary>
    /// 配置单主键的参数化 IN 条件。
    /// </summary>
    /// <typeparam name="TEntity">待删除实体类型。</typeparam>
    /// <param name="whereClause">待追加条件的 Mutation Where 子句。</param>
    /// <param name="plan">包含唯一主键列的实体 Mutation 计划。</param>
    /// <param name="entities">待删除实体集合。</param>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    private void ConfigureSingleKeyInWhere<TEntity>(IMutationWhereClause whereClause, SqlMutationPlan plan,
        IEnumerable<TEntity> entities, IParameterManager parameterManager) where TEntity : class
    {
        var key = plan.Keys[0];
        var parameterNames = new List<string>();
        foreach (var entity in entities)
        {
            var value = GetValue(plan, entity, key);
            if (value == null)
                throw new InvalidOperationException($"实体 {plan.Mapping.EntityType.Name} 的条件列 {key.PropertyName} 不能为空。");
            var parameter = _services.ParameterFactory.Create(parameterManager.GenerateName(), value, key,
                _databaseContext, plan.Mapping.EntityType, SqlParameterSource.SqlBuilder);
            AddParameter(parameterManager, parameter);
            parameterNames.Add(SqlMutationContext.GetParameterPlaceholder(_provider, parameter.Name));
        }
        whereClause.And(new InCondition(_provider.Dialect.SafeName(key.ColumnName), parameterNames));
    }

    /// <summary>
    /// 配置复合主键或并发列的按实体配对条件。
    /// </summary>
    /// <typeparam name="TEntity">待删除实体类型。</typeparam>
    /// <param name="whereClause">待追加条件的 Mutation Where 子句。</param>
    /// <param name="plan">包含主键和并发列的实体 Mutation 计划。</param>
    /// <param name="entities">待删除实体集合。</param>
    /// <param name="options">可选的并发原始值配置。</param>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    private void ConfigurePairedWhere<TEntity>(IMutationWhereClause whereClause, SqlMutationPlan plan,
        IEnumerable<TEntity> entities, SqlDeleteOptions options, IParameterManager parameterManager) where TEntity : class
    {
        foreach (var entity in entities)
        {
            var conditions = new List<string>();
            foreach (var key in plan.Keys)
                conditions.Add(CreateCondition(parameterManager, plan, entity, key, plan.Mapping.EntityType).GetCondition());
            foreach (var concurrency in plan.ConcurrencyColumns)
            {
                var hasOriginalValue = TryGetOriginalValue(options, concurrency.PropertyName, out var value);
                conditions.Add(CreateCondition(parameterManager, plan, entity, concurrency, plan.Mapping.EntityType,
                    hasOriginalValue, value).GetCondition());
            }
            whereClause.Or(new SqlCondition($"({string.Join(" And ", conditions)})"));
        }
    }

    /// <summary>
    /// 创建列等值条件。
    /// </summary>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    /// <param name="plan">当前 Mutation 计划。</param>
    /// <param name="source">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <param name="entityType">实体类型。</param>
    /// <param name="hasOriginalValue">是否使用已配置的并发原始值。</param>
    /// <param name="originalValue">已配置的并发原始值。</param>
    /// <returns>参数化等值条件。</returns>
    private ICondition CreateCondition(IParameterManager parameterManager, SqlMutationPlan plan, object source,
        ColumnMappingMetadata column,
        Type entityType, bool hasOriginalValue = false, object originalValue = null)
    {
        var parameter = CreateConditionParameter(parameterManager, plan, source, column, entityType,
            hasOriginalValue, originalValue);
        return new SqlCondition(
            $"{_provider.Dialect.SafeName(column.ColumnName)} = {SqlMutationContext.GetParameterPlaceholder(_provider, parameter.Name)}");
    }

    /// <summary>生成 SQL 和动态条件共用非空检查、并发原值读取和参数转换。</summary>
    private SqlParam CreateConditionParameter(IParameterManager parameterManager, SqlMutationPlan plan, object source,
        ColumnMappingMetadata column, Type entityType, bool hasOriginalValue = false, object originalValue = null)
    {
        var value = hasOriginalValue ? originalValue : GetValue(plan, source, column);
        if (value == null)
            throw new InvalidOperationException($"实体 {entityType.Name} 的条件列 {column.PropertyName} 不能为空。");
        var parameter = _services.ParameterFactory.Create(parameterManager.GenerateName(), value, column,
            _databaseContext, entityType, SqlParameterSource.SqlBuilder);
        AddParameter(parameterManager, parameter);
        return parameter;
    }

    /// <summary>
    /// 为具备完整生成 Getter 的 Mutation 计划附加静态参数绑定器。
    /// </summary>
    /// <param name="command">已生成的写入命令。</param>
    /// <param name="plan">当前 Mutation 计划。</param>
    /// <returns>带有生成式参数绑定器的命令，或原命令。</returns>
    private static SqlWriteCommand WithGeneratedParameterBinding(SqlWriteCommand command, SqlMutationPlan plan) =>
        plan?.CanUseGeneratedParameterBinding == true
            ? command.WithGeneratedParameterBinder(SqlGeneratedParameterBinding.Bind)
            : command;

    /// <summary>
    /// 根据并发策略生成带受影响行数校验的命令快照。
    /// </summary>
    /// <param name="command">已生成的实体 Mutation 命令。</param>
    /// <param name="plan">实体 Mutation 计划。</param>
    /// <param name="behavior">调用方指定的并发冲突行为。</param>
    /// <returns>最终可执行命令。</returns>
    private static SqlWriteCommand WithConcurrencyValidation(SqlWriteCommand command, SqlMutationPlan plan,
        SqlConcurrencyConflictBehavior? behavior) => command.WithValidateAffectedRows(
        plan.ConcurrencyColumns.Count > 0 && (behavior ?? SqlConcurrencyConflictBehavior.Throw) ==
        SqlConcurrencyConflictBehavior.Throw);

    /// <summary>
    /// 尝试读取并发原始值。
    /// </summary>
    /// <param name="options">可选的 Update 选项。</param>
    /// <param name="propertyName">并发属性名称。</param>
    /// <param name="value">读取到的显式原始值。</param>
    /// <returns><c>true</c> 表示选项包含该属性的原始值；否则返回 <c>false</c>。</returns>
    private static bool TryGetOriginalValue(SqlUpdateOptions options, string propertyName, out object value)
    {
        if (options != null)
            return options.TryGetOriginalValue(propertyName, out value);
        value = null;
        return false;
    }

    /// <summary>
    /// 尝试读取并发原始值。
    /// </summary>
    /// <param name="options">可选的 Delete 选项。</param>
    /// <param name="propertyName">并发属性名称。</param>
    /// <param name="value">读取到的显式原始值。</param>
    /// <returns><c>true</c> 表示选项包含该属性的原始值；否则返回 <c>false</c>。</returns>
    private static bool TryGetOriginalValue(SqlDeleteOptions options, string propertyName, out object value)
    {
        if (options != null)
            return options.TryGetOriginalValue(propertyName, out value);
        value = null;
        return false;
    }

    /// <summary>
    /// 创建实体属性参数。
    /// </summary>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    /// <param name="plan">当前 Mutation 计划；为空时使用兼容 Getter 缓存。</param>
    /// <param name="entity">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <param name="entityType">实体类型。</param>
    /// <returns>SQL 参数。</returns>
    private SqlParam CreateParameter(IParameterManager parameterManager, object entity, ColumnMappingMetadata column,
        Type entityType, SqlMutationPlan plan = null) =>
        _services.ParameterFactory.Create(parameterManager.GenerateName(), GetValue(plan, entity, column), column,
            _databaseContext, entityType, SqlParameterSource.SqlBuilder);

    /// <summary>
    /// 优先读取当前 Mutation 计划登记的生成 Getter，未命中时回退到兼容 Getter 缓存。
    /// </summary>
    /// <param name="plan">当前 Mutation 计划。</param>
    /// <param name="source">实体或并发原始值对象。</param>
    /// <param name="column">待读取的列映射。</param>
    /// <returns>属性值。</returns>
    private object GetValue(SqlMutationPlan plan, object source, ColumnMappingMetadata column) =>
        plan.GetValue(source, column, _planCache);

    /// <summary>
    /// 将带元数据参数写入当前管理器。
    /// </summary>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    /// <param name="parameter">待添加参数。</param>
    private static void AddParameter(IParameterManager parameterManager, SqlParam parameter)
    {
        if (parameterManager is IAdvancedParameterManager advancedParameterManager)
            advancedParameterManager.Add(parameter);
        else
            parameterManager.Add(parameter.Name, parameter.Value);
    }

}
