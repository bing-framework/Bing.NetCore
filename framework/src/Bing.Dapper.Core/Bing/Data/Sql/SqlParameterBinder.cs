using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;

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
/// 支持数据库上下文的 Sql 参数绑定器
/// </summary>
public interface ISqlParameterContextBinder : ISqlParameterBinder
{
    /// <summary>
    /// 绑定 Sql 生成器参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    object Bind(ISqlBuilder builder, SqlOptions options);

    /// <summary>
    /// 绑定 Sql 生成器参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="context">当前执行的参数绑定上下文</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    object Bind(ISqlBuilder builder, SqlOptions options, SqlParameterBindingContext context);

    /// <summary>
    /// 绑定参数对象
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    object Bind(object parameter, SqlOptions options);

    /// <summary>
    /// 绑定参数对象
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="context">当前执行的参数绑定上下文</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    object Bind(object parameter, SqlOptions options, SqlParameterBindingContext context);

    /// <summary>
    /// 获取 Sql 增强参数集合
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Sql 增强参数集合</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options);

    /// <summary>
    /// 获取 Sql 增强参数集合
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="context">当前执行的参数绑定上下文</param>
    /// <returns>Sql 增强参数集合</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options,
        SqlParameterBindingContext context);

    /// <summary>
    /// 获取 Sql 增强参数集合
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Sql 增强参数集合</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options);

    /// <summary>
    /// 获取 Sql 增强参数集合
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="context">当前执行的参数绑定上下文</param>
    /// <returns>Sql 增强参数集合</returns>
    IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options,
        SqlParameterBindingContext context);
}

/// <summary>
/// Dapper 参数绑定器。
/// </summary>
/// <remarks>
/// 保留 <see cref="ISqlParameterBinder"/> 作为通用 SQL 兼容入口；Dapper 调用方可逐步迁移到此契约。
/// </remarks>
public interface IDapperParameterBinder : ISqlParameterContextBinder
{
}

/// <summary>
/// Dapper 增强参数集访问器。
/// </summary>
public interface IDapperParameterSet
{
    /// <summary>
    /// 获取增强参数集合。
    /// </summary>
    IReadOnlyCollection<SqlParam> Parameters { get; }
}

/// <summary>
/// Dapper 增强参数集访问器
/// </summary>
/// <remarks>兼容旧命名，新增代码应使用 <see cref="IDapperParameterSet"/>。</remarks>
public interface IDapperSqlParameterSet : IDapperParameterSet
{
}

/// <summary>
/// 默认 Sql 参数绑定器
/// </summary>
public class DefaultSqlParameterBinder : IDapperParameterBinder
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
    /// SQL 数据库上下文解析器
    /// </summary>
    private readonly ISqlDatabaseContextResolver _databaseContextResolver;

    /// <summary>
    /// SQL 参数解析器
    /// </summary>
    private readonly ISqlParameterResolver _parameterResolver;

    /// <summary>
    /// 数据库参数定制器集合
    /// </summary>
    private readonly IReadOnlyCollection<ISqlDbParameterCustomizer> _dbParameterCustomizers;

    /// <summary>
    /// 初始化一个<see cref="DefaultSqlParameterBinder"/>类型的实例
    /// </summary>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="parameterResolver">SQL 参数解析器</param>
    /// <param name="dbParameterCustomizers">数据库参数定制器集合</param>
    public DefaultSqlParameterBinder(IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null,
        ISqlParameterFactory sqlParameterFactory = null, SqlMetadataOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null, ISqlParameterResolver parameterResolver = null,
        IEnumerable<ISqlDbParameterCustomizer> dbParameterCustomizers = null)
    {
        _options = options ?? new SqlMetadataOptions();
        _databaseContextAccessor = databaseContextAccessor;
        _databaseContextResolver = databaseContextResolver ?? new DefaultSqlDatabaseContextResolver(databaseContextAccessor,
            _options);
        _entityMappingResolver = entityMappingResolver ?? new DefaultEntityMappingResolver(
            databaseContextAccessor: databaseContextAccessor, options: _options);
        _sqlParameterFactory = sqlParameterFactory ?? new DefaultSqlParameterFactory(
            new DefaultFieldValueConverterSelector(null, _options), databaseContextAccessor, _options);
        _parameterResolver = parameterResolver ?? new DefaultSqlParameterResolver();
        _dbParameterCustomizers = dbParameterCustomizers?.Where(t => t != null).ToList() ??
                      new List<ISqlDbParameterCustomizer>();
    }

    /// <summary>
    /// 绑定 Sql 生成器参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    public object Bind(ISqlBuilder builder) => Bind(builder, null);

    /// <summary>
    /// 绑定 Sql 生成器参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    public object Bind(ISqlBuilder builder, SqlOptions options)
        => Bind(builder, options, null);

    /// <inheritdoc />
    public object Bind(ISqlBuilder builder, SqlOptions options, SqlParameterBindingContext context)
    {
        if (builder == null)
            return null;
        var parameters = GetSqlParams(builder, options, context);
        if (parameters.Count > 0)
            return new MetadataDynamicParameters(parameters, _dbParameterCustomizers);
        return builder.GetParams();
    }

    /// <summary>
    /// 绑定参数对象
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    public object Bind(object parameter) => Bind(parameter, null);

    /// <summary>
    /// 绑定参数对象
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    public object Bind(object parameter, SqlOptions options)
        => Bind(parameter, options, null);

    /// <inheritdoc />
    public object Bind(object parameter, SqlOptions options, SqlParameterBindingContext context)
    {
        if (parameter == null)
            return null;
        if (parameter is SqlMapper.IDynamicParameters)
            return parameter;
        if (parameter is ISqlParameterMap or IEnumerable<SqlParam>)
            return new MetadataDynamicParameters(GetSqlParams(parameter, options, context), _dbParameterCustomizers);
        return parameter;
    }

    /// <summary>
    /// 获取 Sql 增强参数集合
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Sql 增强参数集合</returns>
    public IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options)
        => GetSqlParams(builder, options, null);

    /// <inheritdoc />
    public IReadOnlyCollection<SqlParam> GetSqlParams(ISqlBuilder builder, SqlOptions options,
        SqlParameterBindingContext context)
    {
        if (builder is ISqlPartAccessor accessor && accessor.ParameterManager is IAdvancedParameterManager advanced)
            return advanced.GetSqlParams().Values.Where(t => t != null).ToList();
        return new List<SqlParam>();
    }

    /// <summary>
    /// 获取 Sql 增强参数集合
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Sql 增强参数集合</returns>
    public IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options)
        => GetSqlParams(parameter, options, null);

    /// <inheritdoc />
    public IReadOnlyCollection<SqlParam> GetSqlParams(object parameter, SqlOptions options,
        SqlParameterBindingContext context)
    {
        if (parameter is ISqlParameterMap or IEnumerable<SqlParam>)
        {
            var result = _parameterResolver.Resolve(CreateBindingContext(parameter, options, context));
            return result.Items.Select(t => CreateSqlParam(t, options)).Where(t => t != null).ToList();
        }
        return new List<SqlParam>();
    }

    /// <summary>
    /// 创建包含当前执行信息的参数绑定上下文
    /// </summary>
    /// <param name="parameter">参数对象</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="context">调用方提供的执行上下文</param>
    /// <returns>参数绑定上下文</returns>
    private SqlParameterBindingContext CreateBindingContext(object parameter, SqlOptions options,
        SqlParameterBindingContext context)
    {
        var databaseContext = GetDatabaseContext(options);
        return new SqlParameterBindingContext
        {
            Sql = context?.Sql,
            DbKey = context?.DbKey ?? databaseContext?.DataSource?.Key ?? databaseContext?.DbKey,
            DatabaseType = context?.DatabaseType ?? databaseContext?.DataSource?.DatabaseType ??
                           options?.DatabaseType ?? default,
            EntityType = context?.EntityType ?? (parameter as ISqlParameterMap)?.GetItems().FirstOrDefault()?.EntityType,
            Source = parameter
        };
    }

    /// <summary>
    /// 绑定参数映射
    /// </summary>
    /// <param name="map">参数映射</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    protected virtual object Bind(ISqlParameterMap map) => Bind(map, null);

    /// <summary>
    /// 绑定参数映射
    /// </summary>
    /// <param name="map">参数映射</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Dapper 可识别的参数对象</returns>
    protected virtual object Bind(ISqlParameterMap map, SqlOptions options)
    {
        if (map == null)
            return null;
        return new MetadataDynamicParameters(GetSqlParams(map, options), _dbParameterCustomizers);
    }

    /// <summary>
    /// 创建增强 Sql 参数
    /// </summary>
    /// <param name="item">参数映射项</param>
    /// <returns>Sql 参数</returns>
    protected virtual SqlParam CreateSqlParam(SqlParameterMapItem item) => CreateSqlParam(item, null);

    /// <summary>
    /// 创建增强 Sql 参数
    /// </summary>
    /// <param name="item">参数映射项</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>Sql 参数</returns>
    protected virtual SqlParam CreateSqlParam(SqlParameterMapItem item, SqlOptions options)
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
        var column = ResolveColumnMetadata(item.EntityType, item.PropertyName, options);
        return _sqlParameterFactory.Create(item.Name, item.Value, column, GetDatabaseContext(options), item.EntityType,
            SqlParameterSource.RawSql);
    }

    /// <summary>
    /// 根据统一绑定项创建增强 Sql 参数
    /// </summary>
    /// <param name="item">参数绑定项</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>增强 Sql 参数</returns>
    protected virtual SqlParam CreateSqlParam(SqlParameterBindingItem item, SqlOptions options)
    {
        if (item == null)
            return null;
        var metadata = item.Metadata;
        var column = ResolveColumnMetadata(metadata?.EntityType, metadata?.PropertyName, options);
        var converted = _sqlParameterFactory.Create(item.Name, item.Value, column, GetDatabaseContext(options),
            metadata?.EntityType, metadata?.Source ?? SqlParameterSource.RawSql);
        return new SqlParam(item.Name, converted.Value, metadata?.DbType ?? converted.DbType,
            metadata?.Direction ?? converted.Direction, metadata?.Size ?? converted.Size,
            metadata?.Precision ?? converted.Precision, metadata?.Scale ?? converted.Scale)
        {
            OriginalValue = item.OriginalValue,
            EntityType = metadata?.EntityType ?? converted.EntityType,
            PropertyName = metadata?.PropertyName ?? converted.PropertyName,
            ColumnName = metadata?.ColumnName ?? converted.ColumnName,
            DatabaseType = metadata?.DatabaseType ?? converted.DatabaseType,
            ProviderTypeName = metadata?.ProviderTypeName ?? converted.ProviderTypeName,
            Source = metadata?.Source ?? converted.Source,
            MetadataLevel = metadata?.MetadataLevel > converted.MetadataLevel
                ? metadata.MetadataLevel
                : converted.MetadataLevel,
            StorageKind = metadata?.StorageKind ?? converted.StorageKind,
            ConverterKind = metadata?.ConverterKind ?? converted.ConverterKind,
            CustomConverterName = metadata?.CustomConverterName ?? converted.CustomConverterName
        };
    }

    /// <summary>
    /// 解析列映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="propertyOrColumnName">属性名或列名</param>
    /// <returns>列映射元数据</returns>
    protected virtual ColumnMappingMetadata ResolveColumnMetadata(Type entityType, string propertyOrColumnName) =>
        ResolveColumnMetadata(entityType, propertyOrColumnName, null);

    /// <summary>
    /// 解析列映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="propertyOrColumnName">属性名或列名</param>
    /// <param name="options">Sql 配置</param>
    /// <returns>列映射元数据</returns>
    protected virtual ColumnMappingMetadata ResolveColumnMetadata(Type entityType, string propertyOrColumnName,
        SqlOptions options)
    {
        if (entityType == null || string.IsNullOrWhiteSpace(propertyOrColumnName))
            return null;
        var mapping = _entityMappingResolver.Resolve(entityType, GetDatabaseContext(options));
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
        GetDatabaseContext(null);

    /// <summary>
    /// 获取数据库上下文
    /// </summary>
    /// <param name="options">Sql 配置</param>
    /// <returns>数据库上下文</returns>
    protected virtual DatabaseContext GetDatabaseContext(SqlOptions options) =>
        _databaseContextResolver?.Resolve(options) ?? options.GetDatabaseContext() ?? _databaseContextAccessor?.Current ??
        _options.DefaultDatabaseContext;

    /// <summary>
    /// 元数据参数对象
    /// </summary>
    private sealed class MetadataDynamicParameters : SqlMapper.IDynamicParameters, ISqlOutputParameterAccessor,
        IDapperSqlParameterSet
    {
        /// <summary>
        /// 参数集合
        /// </summary>
        private readonly IReadOnlyCollection<SqlParam> _parameters;

        /// <summary>
        /// 数据库参数定制器集合
        /// </summary>
        private readonly IReadOnlyCollection<ISqlDbParameterCustomizer> _customizers;

        /// <summary>
        /// 实际创建的数据库参数
        /// </summary>
        private readonly IDictionary<string, IDbDataParameter> _dbParameters =
            new Dictionary<string, IDbDataParameter>(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IReadOnlyCollection<SqlParam> Parameters => _parameters;

        /// <summary>
        /// 初始化一个<see cref="MetadataDynamicParameters"/>类型的实例
        /// </summary>
        /// <param name="parameters">参数集合</param>
        /// <param name="customizers">数据库参数定制器集合</param>
        public MetadataDynamicParameters(IEnumerable<SqlParam> parameters,
            IReadOnlyCollection<ISqlDbParameterCustomizer> customizers = null)
        {
            _parameters = parameters?.Where(t => t != null).ToList() ?? new List<SqlParam>();
            _customizers = customizers ?? Array.Empty<ISqlDbParameterCustomizer>();
        }

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
            {
                var dbParameter = AddParameter(command, parameter, _customizers);
                _dbParameters[NormalizeName(parameter.Name)] = dbParameter;
            }
        }

        /// <summary>
        /// 添加参数
        /// </summary>
        /// <param name="command">命令对象</param>
        /// <param name="parameter">Sql 参数</param>
        /// <param name="customizers">数据库参数定制器集合</param>
        private static IDbDataParameter AddParameter(IDbCommand command, SqlParam parameter,
            IEnumerable<ISqlDbParameterCustomizer> customizers)
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
            var customizer = customizers?.FirstOrDefault(t => t.CanHandle(parameter.DatabaseType ?? default));
            customizer?.Configure(dbParameter, parameter);
            command.Parameters.Add(dbParameter);
            return dbParameter;
        }

        /// <inheritdoc />
        public object GetValue(string name)
        {
            if (_dbParameters.TryGetValue(NormalizeName(name), out var parameter) == false)
                throw new KeyNotFoundException($"未找到输出参数 '{name}'。");
            return parameter.Value == DBNull.Value ? null : parameter.Value;
        }

        /// <inheritdoc />
        public T GetValue<T>(string name)
        {
            if (TryGetValue<T>(name, out var value))
                return value;
            throw new InvalidCastException($"输出参数 '{name}' 无法转换为 {typeof(T).FullName}。");
        }

        /// <inheritdoc />
        public bool TryGetValue<T>(string name, out T value)
        {
            var rawValue = GetValue(name);
            if (rawValue == null)
            {
                value = default;
                return true;
            }
            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            try
            {
                value = targetType.IsEnum
                    ? (T)Enum.ToObject(targetType, rawValue)
                    : (T)Convert.ChangeType(rawValue, targetType);
                return true;
            }
            catch (Exception) when (rawValue is IConvertible)
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// 规范化参数名称
        /// </summary>
        /// <param name="name">参数名称</param>
        /// <returns>标准参数名称</returns>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            name = name.Trim();
            return name[0] is '@' or ':' or '?' ? name.Substring(1) : name;
        }
    }
}