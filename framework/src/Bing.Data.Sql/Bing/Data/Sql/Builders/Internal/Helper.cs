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
    /// 子句运行上下文。
    /// </summary>
    private readonly SqlClauseContext _context;

    /// <summary>
    /// SQL 方言。
    /// </summary>
    private IDialect _dialect => _context.Dialect;

    /// <summary>
    /// 实体解析器。
    /// </summary>
    private IEntityResolver _resolver => _context.EntityResolver;

    /// <summary>
    /// 实体别名注册器。
    /// </summary>
    private IEntityAliasRegister _register => _context.AliasRegister;

    /// <summary>
    /// 参数管理器。
    /// </summary>
    private IParameterManager _parameterManager => _context.ParameterManager;

    /// <summary>
    /// 实体映射解析器。
    /// </summary>
    private IEntityMappingResolver _entityMappingResolver => _context.Services.EntityMappingResolver;

    /// <summary>
    /// 数据库上下文访问器。
    /// </summary>
    private IDatabaseContextAccessor _databaseContextAccessor => _context.Services.DatabaseContextAccessor;

    /// <summary>
    /// SQL 参数工厂。
    /// </summary>
    private ISqlParameterFactory _sqlParameterFactory => _context.Services.ParameterFactory;

    /// <summary>
    /// SQL 元数据配置。
    /// </summary>
    private SqlMetadataOptions _options => _context.Services.MetadataOptions;

    /// <summary>
    /// SQL 配置。
    /// </summary>
    private SqlOptions _sqlOptions => _context.Services.Options;

    /// <summary>
    /// SQL 数据库上下文解析器。
    /// </summary>
    private ISqlDatabaseContextResolver _databaseContextResolver => _context.Services.DatabaseContextResolver;

    /// <summary>
    /// Builder 生命周期内固定的数据库上下文。
    /// </summary>
    private DatabaseContext _databaseContext => _context.ExecutionContext.DatabaseContext;

    /// <summary>
    /// 使用已绑定的子句运行上下文初始化辅助操作。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public Helper(SqlClauseContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

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
        SqlConditionFactory.ValidateSupported(@operator);
        if (IsInCondition(@operator, value))
        {
            ValidateInConditionValue(value);
            return CreateInCondition(rawColumn, column, entityType, value as IEnumerable, source: source);
        }
        if (IsNotInCondition(@operator, value))
        {
            ValidateInConditionValue(value);
            return CreateInCondition(rawColumn, column, entityType, value as IEnumerable, true, source);
        }
        ValidateComparisonConditionValue(value, @operator);
        var paramName = GenerateParamName(value, @operator);
        var condition = SqlConditionFactory.Create(column, paramName, @operator);
        AddParameter(paramName, value, @operator, entityType, rawColumn, source);
        return condition;
    }

    /// <summary>
    /// 验证 In 和 NotIn 条件值。
    /// </summary>
    /// <param name="value">条件值。</param>
    private static void ValidateInConditionValue(object value)
    {
        if (value == null)
            return;
        if (value is string || value is byte[] || value is not IEnumerable)
            throw new ArgumentException("In 和 NotIn 条件值必须是非字符串、非二进制的 IEnumerable。", nameof(value));
    }

    /// <summary>
    /// 验证关系比较条件值。
    /// </summary>
    /// <param name="value">条件值。</param>
    /// <param name="operator">运算符。</param>
    private static void ValidateComparisonConditionValue(object value, Operator @operator)
    {
        if (value != null)
            return;
        if (@operator is Operator.Greater or Operator.GreaterEqual or Operator.Less or Operator.LessEqual)
            throw new ArgumentNullException(nameof(value), "关系比较条件值不能为空。");
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
            return notIn ? new NotInCondition(column, new List<string>()) : new InCondition(column, new List<string>());
        var items = values.Cast<object>().ToList();
        var validation = _parameterManager.Clone();
        var paramNames = new List<string>();
        foreach (var value in items)
        {
            var name = validation.GenerateName();
            paramNames.Add(name);
            AddParameter(validation, name, value, null, entityType, rawColumn, source);
        }
        for (var index = 0; index < items.Count; index++)
        {
            var name = paramNames[index];
            var value = items[index];
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
        SqlParameterSource source) => AddParameter(_parameterManager, paramName, value, @operator, entityType,
        rawColumn, source);

    /// <summary>
    /// 向指定参数管理器添加参数。
    /// </summary>
    /// <param name="parameterManager">目标参数管理器。</param>
    /// <param name="paramName">参数名。</param>
    /// <param name="value">参数值。</param>
    /// <param name="operator">参数关联的条件运算符。</param>
    /// <param name="entityType">实体类型。</param>
    /// <param name="rawColumn">原始列名。</param>
    /// <param name="source">参数来源。</param>
    private void AddParameter(IParameterManager parameterManager, string paramName, object value, Operator? @operator,
        Type entityType, string rawColumn, SqlParameterSource source)
    {
        if (string.IsNullOrWhiteSpace(paramName))
            return;
        if (TryAddAdvancedParameter(parameterManager, paramName, value, @operator, entityType, rawColumn, source))
            return;
        parameterManager.Add(paramName, value, @operator);
    }

    /// <summary>
    /// 尝试添加增强参数
    /// </summary>
    /// <param name="parameterManager">目标参数管理器。</param>
    /// <param name="paramName">参数名</param>
    /// <param name="value">参数值</param>
    /// <param name="operator">运算符</param>
    /// <param name="entityType">实体类型</param>
    /// <param name="rawColumn">原始列名</param>
    /// <param name="source">参数来源</param>
    /// <returns>是否添加成功</returns>
    private bool TryAddAdvancedParameter(IParameterManager parameterManager, string paramName, object value,
        Operator? @operator, Type entityType, string rawColumn, SqlParameterSource source)
    {
        if (parameterManager is not IAdvancedParameterManager advancedParameterManager)
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
        _databaseContext ?? _databaseContextResolver?.Resolve(_sqlOptions) ?? _sqlOptions.GetDatabaseContext() ??
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
        var validation = _parameterManager.Clone();
        if (string.IsNullOrWhiteSpace(min.SafeString()) == false)
        {
            minParamName = validation.GenerateName();
            AddParameter(validation, minParamName, min, null, entityType, rawColumn, source);
        }
        if (string.IsNullOrWhiteSpace(max.SafeString()) == false)
        {
            maxParamName = validation.GenerateName();
            AddParameter(validation, maxParamName, max, null, entityType, rawColumn, source);
        }
        if (minParamName != null)
            AddParameter(minParamName, min, null, entityType, rawColumn, source);
        if (maxParamName != null)
            AddParameter(maxParamName, max, null, entityType, rawColumn, source);
        return new SegmentCondition(column, minParamName, maxParamName, boundary);
    }

    /// <summary>
    /// 解析Sql
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="dialect">Sql方言</param>
    public static string ResolveSql(string sql, IDialect dialect) => sql?.Replace('[', dialect.OpeningIdentifier).Replace(']', dialect.ClosingIdentifier);
}
