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
internal sealed class Helper
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
    internal Helper(SqlClauseContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <summary>
    /// 获取处理后的列名
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <param name="type">实体类型</param>
    /// <returns>按实体映射和 SQL 方言处理后的列名；表达式为空时返回 <see langword="null"/>。</returns>
    internal string GetColumn(Expression expression, Type type)
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
    /// <returns>按实体映射和 SQL 方言处理后的列名；表达式为空时返回 <see langword="null"/>。</returns>
    internal string GetColumn<TEntity>(Expression<Func<TEntity, object>> expression)
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
    /// <returns>按实体别名和 SQL 方言处理后的列名；列名为空时原样返回。</returns>
    internal string GetColumn(string column, Type type)
    {
        if (string.IsNullOrWhiteSpace(column))
            return column;
        return new SqlItem(column, _register.GetAlias(type)).ToSql(_dialect);
    }

    /// <summary>
    /// 获取处理后的列名
    /// </summary>
    /// <param name="column">列名</param>
    /// <returns>按 SQL 方言处理后的列名；列名为空时原样返回。</returns>
    internal string GetColumn(string column)
    {
        if (string.IsNullOrWhiteSpace(column))
            return column;
        return new SqlItem(column).ToSql(_dialect);
    }

    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="expression">表达式</param>
    /// <returns>从表达式解析出的值；表达式为空时返回 <see langword="null"/>。</returns>
    internal object GetValue(Expression expression)
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
    /// 创建查询条件并将其参数写入参数管理器。
    /// </summary>
    /// <param name="expression">列名</param>
    /// <param name="type">实体类型</param>
    /// <returns>根据表达式创建的查询条件。</returns>
    internal ICondition CreateCondition(Expression expression, Type type)
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
    /// <returns>根据表达式和值创建的查询条件。</returns>
    internal ICondition CreateCondition(Expression expression, Type type, object value, Operator @operator)
    {
        var rawColumn = _resolver.GetColumn(expression, type);
        return CreateConditionInternal(rawColumn, GetColumn(rawColumn, type), value, @operator, type,
            SqlParameterSource.Lambda);
    }

    /// <summary>
    /// 使用原始列名、格式化列名和实体类型创建查询条件。
    /// </summary>
    /// <param name="rawColumn">用于解析映射元数据的原始列名。</param>
    /// <param name="column">已格式化的列名。</param>
    /// <param name="entityType">实体类型。</param>
    /// <param name="value">条件值。</param>
    /// <param name="operator">运算符。</param>
    /// <returns>根据指定列信息创建的查询条件。</returns>
    internal ICondition CreateCondition(string rawColumn, string column, Type entityType, object value,
        Operator @operator)
    {
        return CreateConditionInternal(rawColumn, column, value, @operator, entityType, SqlParameterSource.Lambda);
    }

    /// <summary>
    /// 创建查询条件并添加参数
    /// </summary>
    /// <param name="column">列名</param>
    /// <param name="value">值</param>
    /// <param name="operator">运算符</param>
    /// <returns>根据列名和值创建的查询条件；参数管理器不可用时返回 <see langword="null"/>。</returns>
    internal ICondition CreateCondition(string column, object value, Operator @operator)
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
    /// <param name="rawColumn">用于解析映射元数据的原始列名。</param>
    /// <param name="column">已格式化并用于生成 SQL 的列名。</param>
    /// <param name="value">条件参数值；集合值由 <c>In</c>/<c>NotIn</c> 分支单独展开。</param>
    /// <param name="operator">条件运算符。</param>
    /// <param name="entityType">可选的实体类型，用于解析列映射元数据。</param>
    /// <param name="source">参数元数据来源。</param>
    /// <returns>根据指定列、值和运算符创建的查询条件。</returns>
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
    /// 验证 <c>In</c> 和 <c>NotIn</c> 条件值是否为可枚举集合。
    /// </summary>
    /// <param name="value">条件值；允许为空，字符串和字节数组不视为集合条件。</param>
    /// <exception cref="ArgumentException">值不是可枚举集合，或值为字符串、字节数组时抛出。</exception>
    private static void ValidateInConditionValue(object value)
    {
        if (value == null)
            return;
        if (value is string || value is byte[] || value is not IEnumerable)
            throw new ArgumentException("In 和 NotIn 条件值必须是非字符串、非二进制的 IEnumerable。", nameof(value));
    }

    /// <summary>
    /// 验证关系比较条件是否提供了必需的非空值。
    /// </summary>
    /// <param name="value">条件值。</param>
    /// <param name="operator">关系运算符。</param>
    /// <exception cref="ArgumentNullException">关系比较运算符对应的值为空时抛出。</exception>
    private static void ValidateComparisonConditionValue(object value, Operator @operator)
    {
        if (value != null)
            return;
        if (@operator is Operator.Greater or Operator.GreaterEqual or Operator.Less or Operator.LessEqual)
            throw new ArgumentNullException(nameof(value), "关系比较条件值不能为空。");
    }

    /// <summary>
    /// 判断当前运算和值是否应按集合 <c>In</c> 条件处理。
    /// </summary>
    /// <param name="operator">待判断的运算符。</param>
    /// <param name="value">待判断的条件值。</param>
    /// <returns>运算符为 <c>In</c>，或 <c>Contains</c> 且值为集合时返回 <see langword="true"/>。</returns>
    private bool IsInCondition(Operator @operator, object value)
    {
        if (@operator == Operator.In)
            return true;
        if (@operator == Operator.Contains && value != null && Reflection.Reflections.IsCollection(value.GetType()))
            return true;
        return false;
    }

    /// <summary>
    /// 判断当前运算是否应按集合 <c>NotIn</c> 条件处理。
    /// </summary>
    /// <param name="operator">待判断的运算符。</param>
    /// <param name="value">条件值；该判断仅用于保持与统一条件创建流程一致。</param>
    /// <returns>运算符为 <c>NotIn</c> 时返回 <see langword="true"/>。</returns>
    private bool IsNotInCondition(Operator @operator, object value)
    {
        if (@operator == Operator.NotIn)
            return true;
        return false;
    }

    /// <summary>
    /// 创建 <c>In</c> 或 <c>NotIn</c> 条件，并为集合中的每个值生成独立参数。
    /// </summary>
    /// <param name="rawColumn">用于解析映射元数据的原始列名。</param>
    /// <param name="column">已格式化并用于生成 SQL 的列名。</param>
    /// <param name="entityType">可选的实体类型，用于解析列映射元数据。</param>
    /// <param name="values">条件值集合；为空时生成不含参数的空集合条件。</param>
    /// <param name="notIn">是否生成 <c>NotIn</c> 条件。</param>
    /// <param name="source">参数元数据来源。</param>
    /// <returns>包含已生成参数名称的 <c>In</c> 或 <c>NotIn</c> 条件。</returns>
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
    /// 将参数添加到当前实例的参数管理器，并尽可能附加 SQL 参数元数据。
    /// </summary>
    /// <param name="paramName">参数名称。</param>
    /// <param name="value">参数值。</param>
    /// <param name="operator">参数关联的条件运算符。</param>
    /// <param name="entityType">可选的实体类型。</param>
    /// <param name="rawColumn">可选的原始列名。</param>
    /// <param name="source">参数元数据来源。</param>
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
    /// 尝试根据实体列映射创建并添加带完整 SQL 元数据的参数。
    /// </summary>
    /// <param name="parameterManager">目标参数管理器。</param>
    /// <param name="paramName">参数名称。</param>
    /// <param name="value">原始参数值。</param>
    /// <param name="operator">参数关联的条件运算符。</param>
    /// <param name="entityType">用于解析列映射的实体类型。</param>
    /// <param name="rawColumn">实体属性名或数据库列名。</param>
    /// <param name="source">参数元数据来源。</param>
    /// <returns>成功创建并添加增强参数时返回 <see langword="true"/>；缺少增强参数能力或映射时返回 <see langword="false"/>。</returns>
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
    /// 根据实体类型和属性名或列名解析列映射元数据。
    /// </summary>
    /// <param name="entityType">待解析映射的实体类型。</param>
    /// <param name="rawColumn">实体属性名或数据库列名。</param>
    /// <returns>找到匹配映射时返回列元数据，否则返回 <c>null</c>。</returns>
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
    /// 按执行上下文、解析器、选项和默认值的优先级获取数据库上下文。
    /// </summary>
    /// <returns>当前 SQL 条件构造使用的数据库上下文。</returns>
    private DatabaseContext GetDatabaseContext() =>
        _databaseContext ?? _databaseContextResolver?.Resolve(_sqlOptions) ?? _sqlOptions.GetDatabaseContext() ??
        _databaseContextAccessor?.Current ?? _options.DefaultDatabaseContext;

    /// <summary>
    /// 根据条件运算符转换实际提交的参数值。
    /// </summary>
    /// <param name="value">原始条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>应用 <c>Contains</c>、<c>Starts</c> 或 <c>Ends</c> 通配符后的参数值。</returns>
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
    /// 生成条件参数名称，并处理允许空值的相等性运算。
    /// </summary>
    /// <param name="value">条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>生成的参数名称；参数管理器不可用时返回空字符串，空值用于相等或不等比较时返回 <c>null</c>。</returns>
    internal string GenerateParamName(object value, Operator @operator)
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
    /// <returns>根据最小值、最大值和边界创建的范围条件。</returns>
    internal ICondition Between(string column, object min, object max, Boundary boundary)
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
    /// <returns>根据实体表达式、范围值和边界创建的范围条件。</returns>
    internal ICondition Between(Expression expression, Type type, object min, object max, Boundary boundary)
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
    /// <returns>根据格式化列名、范围值和边界创建的范围条件。</returns>
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
    /// <returns>将方括号标识符替换为当前方言标识符后的 SQL 文本。</returns>
    internal static string ResolveSql(string sql, IDialect dialect) => sql?.Replace('[', dialect.OpeningIdentifier).Replace(']', dialect.ClosingIdentifier);
}
