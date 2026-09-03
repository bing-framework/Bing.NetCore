using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Mutations.Builders;
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

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlEntityMutationCommandBuilder"/>类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">当前命令可共享的服务。</param>
    public DefaultSqlEntityMutationCommandBuilder(ISqlProvider provider, SqlBuilderServices services)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _databaseContext = _services.DatabaseContextResolver.Resolve(_services.Options);
        _planCache = SqlMutationPlanCaches.Get(_services.EntityMappingResolver, _services.MetadataOptions);
    }

    /// <summary>
    /// 当前映射解析器分区内已缓存的 Mutation Plan 数量。
    /// </summary>
    internal int PlanCacheCount => _planCache.PlanCount;

    /// <summary>
    /// 当前映射解析器分区内已缓存的属性 Getter 数量。
    /// </summary>
    internal int GetterCacheCount => _planCache.GetterCount;

    /// <inheritdoc />
    public SqlWriteCommand Insert<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Insert, options?.IncludeProperties,
            options?.ExcludeProperties);
        var mapping = plan.Mapping;
        var columns = plan.WriteColumns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可插入列。");
        var builder = new SqlInsertBuilder(_provider, _services);
        builder.InsertClause.Into(mapping.Table);
        builder.InsertColumnsClause.AddRange(columns.Select(column => column.ColumnName));
        var parameters = new List<SqlParam>(columns.Count);
        foreach (var column in columns)
        {
            var parameter = CreateParameter(builder.MutationContext.ParameterManager, entity, column, typeof(TEntity));
            parameters.Add(parameter);
        }
        builder.ValuesClause.AddRow(parameters);
        return builder.BuildCommand();
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
        var builder = new SqlInsertBuilder(_provider, _services);
        builder.InsertClause.Into(mapping.Table);
        builder.InsertColumnsClause.AddRange(columns.Select(column => column.ColumnName));
        foreach (var entity in entities)
        {
            var parameters = new List<SqlParam>(columns.Count);
            foreach (var column in columns)
            {
                var parameter = CreateParameter(builder.MutationContext.ParameterManager, entity, column,
                    typeof(TEntity));
                parameters.Add(parameter);
            }
            builder.ValuesClause.AddRow(parameters);
        }
        return builder.BuildCommand();
    }

    /// <inheritdoc />
    public SqlWriteCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class
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
        var builder = new SqlUpdateBuilder(_provider, _services);
        builder.UpdateClause.UpdateTable(mapping.Table);
        foreach (var column in columns)
        {
            var parameter = CreateParameter(builder.MutationContext.ParameterManager, entity, column, typeof(TEntity));
            builder.SetClause.Set(column.ColumnName, parameter);
        }
        ConfigureWhere(builder.WhereClause, plan, entity, options, builder.MutationContext.ParameterManager);
        return WithConcurrencyValidation(builder.BuildCommand(), plan, options?.ConcurrencyConflictBehavior);
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
    public SqlWriteCommand Purge<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Delete, null, null);
        EnsureKeys(plan, typeof(TEntity), "物理清除");
        var mapping = plan.Mapping;
        var builder = new SqlDeleteBuilder(_provider, _services);
        builder.DataBoundaryOperation = SqlDataBoundaryOperation.Purge;
        builder.DeleteClause.From(mapping.Table);
        ConfigureWhere(builder.WhereClause, plan, entity, options, builder.MutationContext.ParameterManager);
        return WithConcurrencyValidation(builder.BuildCommand(), plan, options?.ConcurrencyConflictBehavior);
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
        return WithConcurrencyValidation(builder.BuildCommand(), plan, options?.ConcurrencyConflictBehavior);
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
        var builder = new SqlDeleteBuilder(_provider, _services);
        builder.DeleteClause.From(plan.Mapping.Table);
        var canUseInPredicate = plan.Keys.Count == 1 && plan.ConcurrencyColumns.Count == 0;
        if (strategy == SqlBatchDeleteStrategy.InPredicate && canUseInPredicate == false)
            throw new NotSupportedException("InPredicate 策略仅支持不带并发令牌的单主键实体。");
        if (canUseInPredicate && strategy != SqlBatchDeleteStrategy.CompositePredicate)
            ConfigureSingleKeyInWhere(builder.WhereClause, plan, entities, builder.MutationContext.ParameterManager);
        else
            ConfigurePairedWhere(builder.WhereClause, plan, entities, options, builder.MutationContext.ParameterManager);
        return WithConcurrencyValidation(builder.BuildCommand(), plan, options?.ConcurrencyConflictBehavior);
    }

    /// <summary>
    /// 生成更新逻辑删除状态的实体命令。
    /// </summary>
    /// <typeparam name="TEntity">逻辑删除实体类型。</typeparam>
    /// <param name="entity">包含主键和并发属性值的实体。</param>
    /// <param name="isDeleted">目标逻辑删除状态。</param>
    /// <param name="options">可选的 Delete 原始值和并发配置。</param>
    /// <returns>带参数和并发校验信息的可执行 Update 命令。</returns>
    private SqlWriteCommand SetSoftDeleteState<TEntity>(TEntity entity, bool isDeleted, SqlDeleteOptions options)
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
        return WithConcurrencyValidation(builder.BuildCommand(), plan, options?.ConcurrencyConflictBehavior);
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
    /// 将实体主键和并发令牌配置为 Update 或 Delete 的条件子句。
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
            whereClause.And(CreateCondition(parameterManager, entity, key, plan.Mapping.EntityType));
        foreach (var concurrency in plan.ConcurrencyColumns)
            whereClause.And(CreateCondition(parameterManager, entity, concurrency, plan.Mapping.EntityType,
                TryGetOriginalValue(options, concurrency.PropertyName, out var value), value));
    }

    /// <summary>
    /// 将实体主键和并发令牌配置为 Delete 条件子句。
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
            whereClause.And(CreateCondition(parameterManager, entity, key, plan.Mapping.EntityType));
        foreach (var concurrency in plan.ConcurrencyColumns)
            whereClause.And(CreateCondition(parameterManager, entity, concurrency, plan.Mapping.EntityType,
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
            var value = _planCache.GetValue(entity, key);
            if (value == null)
                throw new InvalidOperationException($"实体 {plan.Mapping.EntityType.Name} 的条件列 {key.PropertyName} 不能为空。");
            var parameter = _services.ParameterFactory.Create(parameterManager.GenerateName(), value, key,
                _databaseContext, plan.Mapping.EntityType, SqlParameterSource.SqlBuilder);
            AddParameter(parameterManager, parameter);
            parameterNames.Add(_provider.Dialect.GetParamName(parameter.Name));
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
                conditions.Add(CreateCondition(parameterManager, entity, key, plan.Mapping.EntityType).GetCondition());
            foreach (var concurrency in plan.ConcurrencyColumns)
            {
                var hasOriginalValue = TryGetOriginalValue(options, concurrency.PropertyName, out var value);
                conditions.Add(CreateCondition(parameterManager, entity, concurrency, plan.Mapping.EntityType,
                    hasOriginalValue, value).GetCondition());
            }
            whereClause.Or(new SqlCondition($"({string.Join(" And ", conditions)})"));
        }
    }

    /// <summary>
    /// 创建列等值条件。
    /// </summary>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    /// <param name="source">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <param name="entityType">实体类型。</param>
    /// <param name="hasOriginalValue">是否使用已配置的并发原始值。</param>
    /// <param name="originalValue">已配置的并发原始值。</param>
    /// <returns>参数化等值条件。</returns>
    private ICondition CreateCondition(IParameterManager parameterManager, object source, ColumnMappingMetadata column,
        Type entityType, bool hasOriginalValue = false, object originalValue = null)
    {
        var value = hasOriginalValue ? originalValue : _planCache.GetValue(source, column);
        if (value == null)
            throw new InvalidOperationException($"实体 {entityType.Name} 的条件列 {column.PropertyName} 不能为空。");
        var parameter = _services.ParameterFactory.Create(parameterManager.GenerateName(), value, column,
            _databaseContext, entityType, SqlParameterSource.SqlBuilder);
        AddParameter(parameterManager, parameter);
        return new SqlCondition($"{_provider.Dialect.SafeName(column.ColumnName)} = {_provider.Dialect.GetParamName(parameter.Name)}");
    }

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
    /// 尝试读取更新选项中的并发原始值。
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
    /// 尝试读取删除选项中的并发原始值。
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
    /// <param name="entity">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <param name="entityType">实体类型。</param>
    /// <returns>SQL 参数。</returns>
    private SqlParam CreateParameter(IParameterManager parameterManager, object entity, ColumnMappingMetadata column,
        Type entityType) =>
        _services.ParameterFactory.Create(parameterManager.GenerateName(), _planCache.GetValue(entity, column), column,
            _databaseContext, entityType, SqlParameterSource.SqlBuilder);

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