using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认单实体写入 SQL 生成器。
/// </summary>
public sealed class DefaultSqlMutationBuilder : ISqlMutationBuilder, ISqlCombinedInsertMutationBuilder
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
    /// 初始化一个<see cref="DefaultSqlMutationBuilder"/>类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">当前命令可共享的服务。</param>
    public DefaultSqlMutationBuilder(ISqlProvider provider, SqlBuilderServices services)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _databaseContext = _services.DatabaseContextResolver.Resolve(_services.Options);
        _planCache = SqlMutationPlanCaches.Get(_services.EntityMappingResolver);
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
    public SqlMutationCommand Insert<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class
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
        return new SqlMutationCommand(builder.ToSql(), builder.GetParameters());
    }

    /// <inheritdoc />
    public SqlMutationCommand InsertCombined<TEntity>(IReadOnlyCollection<TEntity> entities,
        SqlInsertOptions options = null) where TEntity : class
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));
        if (entities.Count == 0)
            throw new ArgumentException("组合 Insert 实体集合不能为空。", nameof(entities));
        if (entities.Any(entity => entity == null))
            throw new ArgumentException("组合 Insert 实体集合不能包含 null。", nameof(entities));
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
        return new SqlMutationCommand(builder.ToSql(), builder.GetParameters());
    }

    /// <inheritdoc />
    public SqlMutationCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Update, options?.IncludeProperties,
            options?.ExcludeProperties);
        var mapping = plan.Mapping;
        var columns = plan.WriteColumns;
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可更新列。");
        var builder = new SqlUpdateBuilder(_provider, _services);
        builder.UpdateClause.UpdateTable(mapping.Table);
        foreach (var column in columns)
        {
            var parameter = CreateParameter(builder.MutationContext.ParameterManager, entity, column, typeof(TEntity));
            builder.SetClause.Set(column.ColumnName, parameter);
        }
        ConfigureWhere(builder.WhereClause, plan, entity, options?.OriginalValues ?? entity,
            builder.MutationContext.ParameterManager);
        builder.SetAllowAllRows(options?.AllowAllRows == true);
        return new SqlMutationCommand(builder.ToSql(), builder.GetParameters());
    }

    /// <inheritdoc />
    public SqlMutationCommand Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var plan = ResolvePlan(typeof(TEntity), SqlMutationOperation.Delete, null, null);
        var mapping = plan.Mapping;
        var builder = new SqlDeleteBuilder(_provider, _services);
        builder.DeleteClause.From(mapping.Table);
        ConfigureWhere(builder.WhereClause, plan, entity, options?.OriginalValues ?? entity,
            builder.MutationContext.ParameterManager);
        builder.SetAllowAllRows(options?.AllowAllRows == true);
        return new SqlMutationCommand(builder.ToSql(), builder.GetParameters());
    }

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
    /// 将实体主键和并发令牌配置为 Update 或 Delete 的条件子句。
    /// </summary>
    /// <param name="plan">已解析的 Mutation 映射计划。</param>
    /// <param name="entity">实体当前值。</param>
    /// <param name="originalValues">并发列原始值来源。</param>
    /// <param name="whereClause">待配置的 Mutation Where 子句。</param>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    private void ConfigureWhere(IMutationWhereClause whereClause, SqlMutationPlan plan, object entity,
        object originalValues, IParameterManager parameterManager)
    {
        foreach (var key in plan.Keys)
            whereClause.And(CreateCondition(parameterManager, entity, key, plan.Mapping.EntityType));
        foreach (var concurrency in plan.ConcurrencyColumns)
            whereClause.And(CreateCondition(parameterManager, originalValues, concurrency, plan.Mapping.EntityType));
    }

    /// <summary>
    /// 创建列等值条件。
    /// </summary>
    /// <param name="parameterManager">当前命令参数管理器。</param>
    /// <param name="source">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <param name="entityType">实体类型。</param>
    /// <returns>参数化等值条件。</returns>
    private ICondition CreateCondition(IParameterManager parameterManager, object source, ColumnMappingMetadata column,
        Type entityType)
    {
        var value = _planCache.GetValue(source, column);
        if (value == null)
            throw new InvalidOperationException($"实体 {entityType.Name} 的条件列 {column.PropertyName} 不能为空。");
        var parameter = _services.ParameterFactory.Create(parameterManager.GenerateName(), value, column,
            _databaseContext, entityType, SqlParameterSource.SqlBuilder);
        AddParameter(parameterManager, parameter);
        return new SqlCondition($"{_provider.Dialect.SafeName(column.ColumnName)} = {_provider.Dialect.GetParamName(parameter.Name)}");
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