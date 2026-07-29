using System.Reflection;
using System.Text;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 默认单实体写入 SQL 生成器。
/// </summary>
public sealed class DefaultSqlMutationBuilder : ISqlMutationBuilder
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
    /// 初始化一个<see cref="DefaultSqlMutationBuilder"/>类型的实例。
    /// </summary>
    /// <param name="provider">当前 SQL Provider。</param>
    /// <param name="services">当前命令可共享的服务。</param>
    public DefaultSqlMutationBuilder(ISqlProvider provider, SqlBuilderServices services)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _databaseContext = _services.DatabaseContextResolver.Resolve(_services.Options);
    }

    /// <inheritdoc />
    public SqlMutationCommand Insert<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var mapping = ResolveMapping(typeof(TEntity));
        var columns = FilterColumns(mapping.Columns.Values.Where(column => column.CanInsert),
            options?.IncludeProperties, options?.ExcludeProperties).ToList();
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可插入列。");
        var parameters = new List<SqlParam>(columns.Count);
        var names = new List<string>(columns.Count);
        foreach (var column in columns)
        {
            var parameter = CreateParameter(parameters.Count, entity, column, typeof(TEntity));
            parameters.Add(parameter);
            names.Add(_provider.Dialect.GetParamName(parameter.Name));
        }
        var sql = new StringBuilder("Insert Into ")
            .Append(FormatTable(mapping.Table))
            .Append(" (")
            .Append(string.Join(", ", columns.Select(column => _provider.Dialect.SafeName(column.ColumnName))))
            .Append(") Values (")
            .Append(string.Join(", ", names))
            .Append(')')
            .ToString();
        return new SqlMutationCommand(sql, parameters.AsReadOnly());
    }

    /// <inheritdoc />
    public SqlMutationCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var mapping = ResolveMapping(typeof(TEntity));
        var columns = FilterColumns(mapping.Columns.Values.Where(column => column.CanUpdate),
            options?.IncludeProperties, options?.ExcludeProperties).ToList();
        if (columns.Count == 0)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 没有可更新列。");
        var parameters = new List<SqlParam>(columns.Count + mapping.Columns.Count);
        var assignments = new List<string>(columns.Count);
        foreach (var column in columns)
        {
            var parameter = CreateParameter(parameters.Count, entity, column, typeof(TEntity));
            parameters.Add(parameter);
            assignments.Add($"{_provider.Dialect.SafeName(column.ColumnName)} = {_provider.Dialect.GetParamName(parameter.Name)}");
        }
        var where = CreateWhere(mapping, entity, options?.OriginalValues ?? entity, options?.AllowAllRows == true,
            parameters);
        var sql = $"Update {FormatTable(mapping.Table)} Set {string.Join(", ", assignments)}{where}";
        return new SqlMutationCommand(sql, parameters.AsReadOnly());
    }

    /// <inheritdoc />
    public SqlMutationCommand Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));
        var mapping = ResolveMapping(typeof(TEntity));
        var parameters = new List<SqlParam>();
        var where = CreateWhere(mapping, entity, options?.OriginalValues ?? entity, options?.AllowAllRows == true,
            parameters);
        return new SqlMutationCommand($"Delete From {FormatTable(mapping.Table)}{where}", parameters.AsReadOnly());
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
    /// 筛选可写入列。
    /// </summary>
    /// <param name="columns">候选列。</param>
    /// <param name="includes">仅包含属性名集合。</param>
    /// <param name="excludes">排除属性名集合。</param>
    /// <returns>筛选后的列集合。</returns>
    private static IEnumerable<ColumnMappingMetadata> FilterColumns(IEnumerable<ColumnMappingMetadata> columns,
        IReadOnlyCollection<string> includes, IReadOnlyCollection<string> excludes)
    {
        var includeSet = CreatePropertySet(includes);
        var excludeSet = CreatePropertySet(excludes);
        return columns.Where(column =>
            (includeSet == null || includeSet.Contains(column.PropertyName)) &&
            (excludeSet == null || excludeSet.Contains(column.PropertyName) == false));
    }

    /// <summary>
    /// 创建属性名集合。
    /// </summary>
    /// <param name="properties">属性名集合。</param>
    /// <returns>忽略大小写的属性名集合；未指定时返回 <see langword="null"/>。</returns>
    private static HashSet<string> CreatePropertySet(IReadOnlyCollection<string> properties)
    {
        if (properties == null || properties.Count == 0)
            return null;
        return new HashSet<string>(properties.Where(property => string.IsNullOrWhiteSpace(property) == false),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建 Update 或 Delete 的条件子句。
    /// </summary>
    /// <param name="mapping">实体映射。</param>
    /// <param name="entity">实体当前值。</param>
    /// <param name="originalValues">并发列原始值来源。</param>
    /// <param name="allowAllRows">是否允许全表写入。</param>
    /// <param name="parameters">待追加的参数集合。</param>
    /// <returns>以空格开头的条件子句；允许全表写入时可能为空。</returns>
    private string CreateWhere(EntityMappingMetadata mapping, object entity, object originalValues, bool allowAllRows,
        ICollection<SqlParam> parameters)
    {
        var conditions = new List<string>();
        var keys = mapping.Columns.Values.Where(column => column.IsKey).ToList();
        if (keys.Count == 0 && allowAllRows == false)
            throw new InvalidOperationException($"实体 {mapping.EntityType.Name} 未定义主键，拒绝执行无条件写入。");
        foreach (var key in keys)
            conditions.Add(CreateCondition(parameters, entity, key, mapping.EntityType));
        foreach (var concurrency in mapping.Columns.Values.Where(column => column.IsConcurrencyToken))
            conditions.Add(CreateCondition(parameters, originalValues, concurrency, mapping.EntityType));
        if (conditions.Count == 0)
        {
            if (allowAllRows)
                return string.Empty;
            throw new InvalidOperationException("拒绝执行无条件写入。");
        }
        return $" Where {string.Join(" And ", conditions)}";
    }

    /// <summary>
    /// 创建列等值条件。
    /// </summary>
    /// <param name="parameters">待追加的参数集合。</param>
    /// <param name="source">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <param name="entityType">实体类型。</param>
    /// <returns>等值条件 SQL。</returns>
    private string CreateCondition(ICollection<SqlParam> parameters, object source, ColumnMappingMetadata column,
        Type entityType)
    {
        var value = GetPropertyValue(source, column);
        if (value == null)
            throw new InvalidOperationException($"实体 {entityType.Name} 的条件列 {column.PropertyName} 不能为空。");
        var parameter = _services.ParameterFactory.Create(_provider.Dialect.GenerateName(parameters.Count), value, column,
            _databaseContext, entityType, SqlParameterSource.SqlBuilder);
        parameters.Add(parameter);
        return $"{_provider.Dialect.SafeName(column.ColumnName)} = {_provider.Dialect.GetParamName(parameter.Name)}";
    }

    /// <summary>
    /// 创建实体属性参数。
    /// </summary>
    /// <param name="index">参数索引。</param>
    /// <param name="entity">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <param name="entityType">实体类型。</param>
    /// <returns>SQL 参数。</returns>
    private SqlParam CreateParameter(int index, object entity, ColumnMappingMetadata column, Type entityType) =>
        _services.ParameterFactory.Create(_provider.Dialect.GenerateName(index), GetPropertyValue(entity, column), column,
            _databaseContext, entityType, SqlParameterSource.SqlBuilder);

    /// <summary>
    /// 获取实体或原始值对象的属性值。
    /// </summary>
    /// <param name="source">属性值来源。</param>
    /// <param name="column">列映射。</param>
    /// <returns>属性值。</returns>
    private static object GetPropertyValue(object source, ColumnMappingMetadata column)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var property = source.GetType().GetProperty(column.PropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (property == null)
            throw new InvalidOperationException($"原始值对象未包含属性 {column.PropertyName}。");
        return property.GetValue(source);
    }

    /// <summary>
    /// 格式化最终表引用。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    /// <returns>按当前 Provider 转义的表名。</returns>
    private string FormatTable(SqlTableReference table) =>
        _services.ObjectNameFormatter.Format(table, _provider.Dialect, _provider.DatabaseType);
}