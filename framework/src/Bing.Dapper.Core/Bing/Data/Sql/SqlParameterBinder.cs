using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// Sql 参数绑定器
/// </summary>
public interface ISqlParameterBinder
{
    /// <summary>
    /// 绑定 Sql 生成器参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    object Bind(ISqlBuilder builder);

    /// <summary>
    /// 绑定参数对象
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    object Bind(object parameter);
}

/// <summary>
/// 默认 Sql 参数绑定器
/// </summary>
public class DefaultSqlParameterBinder : ISqlParameterBinder
{
    /// <summary>
    /// 实体映射解析器
    /// </summary>
    private readonly IEntityMappingResolver _entityMappingResolver;

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    private readonly IDatabaseContextAccessor _databaseContextAccessor;

    /// <summary>
    /// Sql 参数工厂
    /// </summary>
    private readonly ISqlParameterFactory _sqlParameterFactory;

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    private readonly SqlMetadataOptions _options;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlParameterBinder"/>类型的实例
    /// </summary>
    /// <param name="entityMetadata">实体元数据解析器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="options">Sql 元数据配置</param>
    public DefaultSqlParameterBinder(IEntityMetadata entityMetadata = null,
        IEntityMappingResolver entityMappingResolver = null, IDatabaseContextAccessor databaseContextAccessor = null,
        ISqlParameterFactory sqlParameterFactory = null, SqlMetadataOptions options = null)
    {
        _options = options ?? new SqlMetadataOptions();
        _databaseContextAccessor = databaseContextAccessor;
        _entityMappingResolver = entityMappingResolver ??
            new DefaultEntityMappingResolver(entityMetadata, databaseContextAccessor, _options);
        _sqlParameterFactory = sqlParameterFactory ?? new DefaultSqlParameterFactory(
            new DefaultFieldValueConverterSelector(null, _options), databaseContextAccessor, _options);
    }

    /// <summary>
    /// 绑定 Sql 生成器参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    public object Bind(ISqlBuilder builder)
    {
        if (builder == null)
            return null;
        if (builder is ISqlPartAccessor accessor && accessor.ParameterManager is IAdvancedParameterManager advanced)
            return new MetadataDynamicParameters(advanced.GetSqlParams().Values);
        return builder.GetParams();
    }

    /// <summary>
    /// 绑定参数对象
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    public object Bind(object parameter)
    {
        if (parameter == null)
            return null;
        if (parameter is SqlMapper.IDynamicParameters)
            return parameter;
        if (parameter is ISqlParameterMap map)
            return Bind(map);
        if (parameter is IEnumerable<SqlParam> sqlParams)
            return new MetadataDynamicParameters(sqlParams);
        return parameter;
    }

    /// <summary>
    /// 绑定参数映射
    /// </summary>
    /// <param name="map">参数映射</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    protected virtual object Bind(ISqlParameterMap map)
    {
        if (map == null)
            return null;
        var parameters = map.GetItems().Select(CreateSqlParam).Where(t => t != null).ToList();
        return new MetadataDynamicParameters(parameters);
    }

    /// <summary>
    /// 创建增强 Sql 参数
    /// </summary>
    /// <param name="item">参数映射项</param>
    /// <returns>Sql 参数</returns>
    protected virtual SqlParam CreateSqlParam(SqlParameterMapItem item)
    {
        if (item == null)
            return null;
        if (item.ValueResolved == false && item.HasExplicitValue == false)
        {
            return new SqlParam(item.Name, item.Value)
            {
                EntityType = item.EntityType,
                PropertyName = item.PropertyName,
                Source = SqlParameterSource.RawSql,
                MetadataLevel = SqlParameterMetadataLevel.Weak
            };
        }
        var column = ResolveColumnMetadata(item.EntityType, item.PropertyName);
        return _sqlParameterFactory.Create(item.Name, item.Value, column, GetDatabaseContext(), item.EntityType,
            SqlParameterSource.RawSql);
    }

    /// <summary>
    /// 解析列映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="propertyOrColumnName">属性名或列名</param>
    /// <returns>列映射元数据</returns>
    protected virtual ColumnMappingMetadata ResolveColumnMetadata(Type entityType, string propertyOrColumnName)
    {
        if (entityType == null || string.IsNullOrWhiteSpace(propertyOrColumnName))
            return null;
        var mapping = _entityMappingResolver.Resolve(entityType, GetDatabaseContext());
        if (mapping?.Columns == null || mapping.Columns.Count == 0)
            return null;
        if (mapping.Columns.TryGetValue(propertyOrColumnName, out var column))
            return column;
        return mapping.Columns.Values.FirstOrDefault(t =>
            string.Equals(t.PropertyName, propertyOrColumnName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.ColumnName, propertyOrColumnName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取数据库上下文
    /// </summary>
    /// <returns>数据库上下文</returns>
    protected virtual DatabaseContext GetDatabaseContext() =>
        _databaseContextAccessor?.Current ?? _options.DefaultDatabaseContext;

    /// <summary>
    /// 元数据参数对象
    /// </summary>
    private sealed class MetadataDynamicParameters : SqlMapper.IDynamicParameters
    {
        /// <summary>
        /// 参数集合
        /// </summary>
        private readonly IReadOnlyCollection<SqlParam> _parameters;

        /// <summary>
        /// 初始化一个<see cref="MetadataDynamicParameters"/>类型的实例
        /// </summary>
        /// <param name="parameters">参数集合</param>
        public MetadataDynamicParameters(IEnumerable<SqlParam> parameters) =>
            _parameters = parameters?.Where(t => t != null).ToList() ?? new List<SqlParam>();

        /// <summary>
        /// 将参数添加到命令对象
        /// </summary>
        /// <param name="command">命令对象</param>
        /// <param name="identity">Dapper 标识</param>
        public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
        {
            if (command == null)
                return;
            foreach (var parameter in _parameters)
                AddParameter(command, parameter);
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="command">命令对象</param>
        /// <param name="parameter">Sql 参数</param>
        private static void AddParameter(IDbCommand command, SqlParam parameter)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            if (parameter.DbType.HasValue)
                dbParameter.DbType = parameter.DbType.Value;
            if (parameter.Direction.HasValue)
                dbParameter.Direction = parameter.Direction.Value;
            if (parameter.Size.HasValue)
                dbParameter.Size = parameter.Size.Value;
            if (parameter.Precision.HasValue)
                dbParameter.Precision = parameter.Precision.Value;
            if (parameter.Scale.HasValue)
                dbParameter.Scale = parameter.Scale.Value;
            command.Parameters.Add(dbParameter);
        }
    }
}