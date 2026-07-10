using System.Collections;
using System.Linq.Expressions;
using Bing.Data;
using Bing.Data.Queries;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Expressions;
using Bing.Extensions;
using Enum = Bing.Helpers.Enum;

namespace Bing.Data.Sql.Builders.Internal;

/// <summary>
/// Sql生成器辅助操作
/// </summary>
public class Helper
{
    /// <summary>
    /// Sql方言
    /// </summary>
    private readonly IDialect _dialect;

    /// <summary>
    /// 实体解析器
    /// </summary>
    private readonly IEntityResolver _resolver;

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    private readonly IEntityAliasRegister _register;

    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

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
    /// Sql 配置
    /// </summary>
    private readonly SqlOptions _sqlOptions;

    /// <summary>
    /// SQL 数据库上下文解析器
    /// </summary>
    private readonly ISqlDatabaseContextResolver _databaseContextResolver;

    /// <summary>
    /// 初始化一个<see cref="Helper"/>类型的实例
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="resolver">实体解析器</param>
    /// <param name="register">实体别名注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="options">Sql 元数据配置</param>
    /// <param name="sqlOptions">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    public Helper(IDialect dialect, IEntityResolver resolver, IEntityAliasRegister register,
        IParameterManager parameterManager, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions options = null, SqlOptions sqlOptions = null,
        ISqlDatabaseContextResolver databaseContextResolver = null)
    {
        _dialect = dialect;
        _resolver = resolver;
        _register = register;
        _parameterManager = parameterManager;
        _entityMappingResolver = entityMappingResolver;
        _databaseContextAccessor = databaseContextAccessor;
        _sqlParameterFactory = sqlParameterFactory;
        _options = options ?? new SqlMetadataOptions();
        _sqlOptions = sqlOptions;
        _databaseContextResolver = databaseContextResolver ?? new DefaultSqlDatabaseContextResolver(databaseContextAccessor,
            _options);
    }

    /// <summary>
    /// 获取处理后的列名
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="type">实体类型</param>
    public string GetColumn(Expression expression, Type type)
    {
        if (expression == null)
            return null;
        return GetColumn(_resolver.GetColumn(expression, type), type);
    }

    /// <summary>
    /// 获取处理后的列名
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="expression">列名表达式</param>
    public string GetColumn<TEntity>(Expression<Func<TEntity, object>> expression)
    {
        if (expression == null)
            return null;
        return GetColumn(_resolver.GetColumn(expression), typeof(TEntity));
    }

    /// <summary>
    /// 获取处理后的列名
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="type">实体类型</param>
    public string GetColumn(string column, Type type)
    {
        if (string.IsNullOrWhiteSpace(column))
            return column;
        return new SqlItem(column, _register.GetAlias(type)).ToSql(_dialect);
    }

    /// <summary>
    /// 获取处理后的列名
    /// </summary>
    /// <param name="column">列名</param>
    public string GetColumn(string column)
    {
        if (string.IsNullOrWhiteSpace(column))
            return column;
        return new SqlItem(column).ToSql(_dialect);
    }

    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="expression">表达式</param>
    public object GetValue(Expression expression)
    {
        if (expression == null)
            return null;
        var result = Lambdas.GetValue(expression);
        if (result == null)
            return null;
        var type = result.GetType();
        if (type.IsEnum)
            return Enum.GetValue(type, result);
        return result;
    }

    /// <summary>
    /// 创建查询条件并添加参数
    /// </summary>
    /// <param name="expression">列名</param>
    /// <param name="type">实体类型</param>
    public ICondition CreateCondition(Expression expression, Type type)
    {
        var rawColumn = _resolver.GetColumn(expression, type);
        return CreateConditionInternal(rawColumn, GetColumn(rawColumn, type), GetValue(expression),
            Lambdas.GetOperator(expression).SafeValue(), type, SqlParameterSource.Lambda);
    }

    /// <summary>
    /// 创建查询条件并添加参数
    /// </summary>
    /// <param name="expression">列名表达式</param>
    /// <param name="type">实体类型</param>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
    public ICondition CreateCondition(Expression expression, Type type, object value, Operator @operator)
    {
        var rawColumn = _resolver.GetColumn(expression, type);
        return CreateConditionInternal(rawColumn, GetColumn(rawColumn, type), value, @operator, type,
            SqlParameterSource.Lambda);
    }

    /// <summary>
    /// 创建查询条件并添加参数
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public ICondition CreateCondition(string column, object value, Operator @operator)
    {
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentNullException(nameof(column));
        if (_parameterManager == null)
            return null;
        return CreateConditionInternal(column, GetColumn(column), value, @operator, null, SqlParameterSource.SqlBuilder);
    }

    /// <summary>
    /// 创建查询条件并添加参数
    /// </summary>
    /// <param name="rawColumn">原始列名</param>
    /// <param name="column">格式化后的列名</param>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="source">参数来源</param>
    private ICondition CreateConditionInternal(string rawColumn, string column, object value, Operator @operator,
        Type entityType, SqlParameterSource source)
    {
        if (IsInCondition(@operator, value))
            return CreateInCondition(rawColumn, column, entityType, value as IEnumerable, source: source);
        if (IsNotInCondition(@operator, value))
            return CreateInCondition(rawColumn, column, entityType, value as IEnumerable, true, source);
        var paramName = GenerateParamName(value, @operator);
        AddParameter(paramName, value, @operator, entityType, rawColumn, source);
        return SqlConditionFactory.Create(column, paramName, @operator);
    }

    /// <summary>
    /// 是否In条件
    /// </summary>
    /// <param name="operator">运算符</param>
    /// <param name="value">值</param>
    private bool IsInCondition(Operator @operator, object value)
    {
        if (@operator == Operator.In)
            return true;
        if (@operator == Operator.Contains && value != null && Reflection.Reflections.IsCollection(value.GetType()))
            return true;
        return false;
    }

    /// <summary>
    /// 是否Not In条件
    /// </summary>
    /// <param name="operator">运算符</param>
    /// <param name="value">值</param>
    private bool IsNotInCondition(Operator @operator, object value)
    {
        if (@operator == Operator.NotIn)
            return true;
        return false;
    }

    /// <summary>
    /// 创建In条件
    /// </summary>
    /// <param name="rawColumn">原始列名</param>
    /// <param name="column">列名</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="values">值列表</param>
    /// <param name="notIn">是否Not In条件</param>
    /// <param name="source">参数来源</param>
    private ICondition CreateInCondition(string rawColumn, string column, Type entityType, IEnumerable values,
        bool notIn = false, SqlParameterSource source = SqlParameterSource.Unknown)
    {
        if (values == null)
            return NullCondition.Instance;
        var paramNames = new List<string>();
        foreach (var value in values)
        {
            var name = _parameterManager.GenerateName();
            paramNames.Add(name);
            AddParameter(name, value, null, entityType, rawColumn, source);
        }
        if (notIn)
            return new NotInCondition(column, paramNames);
        return new InCondition(column, paramNames);
    }

    /// <summary>
    /// 添加参数
    /// </summary>
    /// <param name="paramName">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="rawColumn">原始列名</param>
    /// <param name="source">参数来源</param>
    private void AddParameter(string paramName, object value, Operator? @operator, Type entityType, string rawColumn,
        SqlParameterSource source)
    {
        if (string.IsNullOrWhiteSpace(paramName))
            return;
        if (TryAddAdvancedParameter(paramName, value, @operator, entityType, rawColumn, source))
            return;
        _parameterManager.Add(paramName, value, @operator);
    }

    /// <summary>
    /// 尝试添加增强参数
    /// </summary>
    /// <param name="paramName">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="rawColumn">原始列名</param>
    /// <param name="source">参数来源</param>
    /// <returns>是否添加成功</returns>
    private bool TryAddAdvancedParameter(string paramName, object value, Operator? @operator, Type entityType,
        string rawColumn, SqlParameterSource source)
    {
        if (_parameterManager is not IAdvancedParameterManager advancedParameterManager)
            return false;
        if (_sqlParameterFactory == null || _entityMappingResolver == null)
            return false;
        if (entityType == null || string.IsNullOrWhiteSpace(rawColumn))
            return false;
        var columnMetadata = ResolveColumnMetadata(entityType, rawColumn);
        if (columnMetadata == null)
        {
            if (_options.StrictMetadata)
                throw new InvalidOperationException($"未能解析实体 {entityType.Name} 的列映射元数据: {rawColumn}");
            return false;
        }
        var parameter = _sqlParameterFactory.Create(paramName, GetParameterValue(value, @operator), columnMetadata,
            GetDatabaseContext(), entityType, source);
        advancedParameterManager.Add(parameter);
        return true;
    }

    /// <summary>
    /// 解析列映射元数据
    /// </summary>
    /// <param name="entityType">实体类型</param>
    /// <param name="rawColumn">原始列名</param>
    /// <returns>列映射元数据</returns>
    private ColumnMappingMetadata ResolveColumnMetadata(Type entityType, string rawColumn)
    {
        var mapping = _entityMappingResolver.Resolve(entityType, GetDatabaseContext());
        if (mapping?.Columns == null || mapping.Columns.Count == 0)
            return null;
        if (mapping.Columns.TryGetValue(rawColumn, out var metadata))
            return metadata;
        return mapping.Columns.Values.FirstOrDefault(t =>
            string.Equals(t.PropertyName, rawColumn, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.ColumnName, rawColumn, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取数据库上下文
    /// </summary>
    /// <returns>数据库上下文</returns>
    private DatabaseContext GetDatabaseContext() =>
        _databaseContextResolver?.Resolve(_sqlOptions) ?? _sqlOptions.GetDatabaseContext() ??
        _databaseContextAccessor?.Current ?? _options.DefaultDatabaseContext;

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="value">原始值</param>
    /// <param name="operator">运算符</param>
    /// <returns>参数值</returns>
    private object GetParameterValue(object value, Operator? @operator)
    {
        if (string.IsNullOrWhiteSpace(value.SafeString()))
            return value;
        switch (@operator)
        {
            case Operator.Contains:
                return $"%{value}%";
            case Operator.Starts:
                return $"{value}%";
            case Operator.Ends:
                return $"%{value}";
            default:
                return value;
        }
    }

    /// <summary>
    /// 获取参数名
    /// </summary>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    public string GenerateParamName(object value, Operator @operator)
    {
        if (_parameterManager == null)
            return string.Empty;
        var result = _parameterManager.GenerateName();
        if (value != null)
            return result;
        if (@operator == Operator.Equal || @operator == Operator.NotEqual)
            return null;
        return result;
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public ICondition Between(string column, object min, object max, Boundary boundary)
    {
        return BetweenInternal(column, GetColumn(column), null, min, max, boundary, SqlParameterSource.SqlBuilder);
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="expression">列名表达式</param>
    /// <param name="type">实体类型</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    public ICondition Between(Expression expression, Type type, object min, object max, Boundary boundary)
    {
        var rawColumn = _resolver.GetColumn(expression, type);
        return BetweenInternal(rawColumn, GetColumn(rawColumn, type), type, min, max, boundary,
            SqlParameterSource.Lambda);
    }

    /// <summary>
    /// 添加范围查询条件
    /// </summary>
    /// <param name="rawColumn">原始列名</param>
    /// <param name="column">格式化后的列名</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <param name="boundary">包含边界</param>
    /// <param name="source">参数来源</param>
    private ICondition BetweenInternal(string rawColumn, string column, Type entityType, object min, object max,
        Boundary boundary, SqlParameterSource source)
    {
        string minParamName = null;
        string maxParamName = null;
        if (string.IsNullOrWhiteSpace(min.SafeString()) == false)
        {
            minParamName = _parameterManager.GenerateName();
            AddParameter(minParamName, min, null, entityType, rawColumn, source);
        }
        if (string.IsNullOrWhiteSpace(max.SafeString()) == false)
        {
            maxParamName = _parameterManager.GenerateName();
            AddParameter(maxParamName, max, null, entityType, rawColumn, source);
        }
        return new SegmentCondition(column, minParamName, maxParamName, boundary);
    }

    /// <summary>
    /// 解析Sql
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="dialect">Sql方言</param>
    public static string ResolveSql(string sql, IDialect dialect) => sql?.Replace('[', dialect.OpeningIdentifier).Replace(']', dialect.ClosingIdentifier);
}
