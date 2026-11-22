using System.Linq.Expressions;
using System.Reflection;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 静态 SqlFn 与聚合上下文直接成员 API 合同测试。
/// </summary>
public sealed class SqlFnTest
{
    /// <summary>
    /// 定义在 CLR 中直接调用 SQL 标记方法时抛出的错误消息。
    /// </summary>
    private const string NotSupportedMessage = "聚合标记方法只能用于 SQL 表达式，不能在 CLR 中直接调用。";

    /// <summary>
    /// 验证直接成员聚合表达式保留内置结果类型。
    /// </summary>
    [Fact]
    public void DirectMembers_WhenBuiltInExpressions_ShouldPreserveResultTypes()
    {
        var functions = CreateFunctions<SampleEntity>();

        AssertExpression<SampleEntity, long>(entity => SqlFn.Count(entity.IntValue));
        AssertExpression<SampleEntity, long>(entity => SqlFn.Count(entity.Name));
        AssertExpression<SampleEntity, long>(entity => SqlFn.Count(entity.IntValue, "Rows"));
        AssertExpression<SampleEntity, long>(entity => SqlFn.Count(entity.IntValue, true));
        AssertExpression<SampleEntity, long>(entity => SqlFn.Count(entity.IntValue, "Rows", true));
        AssertExpression<SampleEntity, long>(entity => SqlFn.CountAll());
        AssertExpression<SampleEntity, long>(entity => SqlFn.CountAll("Rows"));

        AssertExpression<SampleEntity, long>(entity => functions.Count(entity.IntValue));
        AssertExpression<SampleEntity, long>(entity => functions.Count(entity.Name));
        AssertExpression<SampleEntity, long>(entity => functions.Count(entity.IntValue, "Rows", true));
        AssertExpression<SampleEntity, long>(entity => functions.Count(item => item.IntValue));
        AssertExpression<SampleEntity, long>(entity => functions.CountAll());

        AssertExpression<SampleEntity, int?>(entity => SqlFn.Sum(entity.IntValue));
        AssertExpression<SampleEntity, int?>(entity => SqlFn.Sum(entity.NullableIntValue));
        AssertExpression<SampleEntity, long?>(entity => SqlFn.Sum(entity.LongValue));
        AssertExpression<SampleEntity, decimal?>(entity => SqlFn.Sum(entity.DecimalValue));
        AssertExpression<SampleEntity, double?>(entity => SqlFn.Sum(entity.DoubleValue));
        AssertExpression<SampleEntity, decimal?>(entity => SqlFn.Avg(entity.DecimalValue));
        AssertExpression<SampleEntity, double?>(entity => SqlFn.Avg(entity.NullableDoubleValue));
        AssertExpression<SampleEntity, int?>(entity => functions.Sum(entity.IntValue));
        AssertExpression<SampleEntity, int?>(entity => functions.Sum(entity.NullableIntValue));
        AssertExpression<SampleEntity, decimal?>(entity => functions.Avg(entity.DecimalValue));

        AssertExpression<SampleEntity, int?>(entity => SqlFn.Min(entity.IntValue));
        AssertExpression<SampleEntity, int?>(entity => SqlFn.Min(entity.NullableIntValue));
        AssertExpression<SampleEntity, string>(entity => SqlFn.Min(entity.Name));
        AssertExpression<SampleEntity, int?>(entity => SqlFn.Max(entity.IntValue));
        AssertExpression<SampleEntity, string>(entity => functions.Max(entity.Name));
    }

    /// <summary>
    /// 验证直接计数与强类型表达式共存时兼容旧选择器重载。
    /// </summary>
    [Fact]
    public void TypedExpressions_WhenUsedAlongsideDirectCount_ShouldKeepLegacySelectorOverload()
    {
        var functions = CreateFunctions<SampleEntity>();
        Expression<Func<SampleEntity, int>> selector = entity => entity.IntValue;
        Expression<Func<SampleEntity, int>> nullSelector = null!;
        Expression<Func<SampleEntity, int?>> nullSumSelector = null!;

        Expression<Func<SampleEntity, long>> typedCall = entity => functions.Count(selector);
        Expression<Func<SampleEntity, long>> nullCall = entity => functions.Count(nullSelector);
        Expression<Func<SampleEntity, int?>> nullSumCall = entity => functions.Sum(nullSumSelector);
        Expression<Func<SampleEntity, long>> explicitGenericCall = entity => functions.Count<int>(entity.IntValue);

        Assert.Equal(typeof(int), Assert.IsAssignableFrom<MethodCallExpression>(typedCall.Body).Method.GetGenericArguments()[0]);
        Assert.Equal(typeof(int), Assert.IsAssignableFrom<MethodCallExpression>(nullCall.Body).Method.GetGenericArguments()[0]);
        Assert.IsAssignableFrom<MethodCallExpression>(nullSumCall.Body);
        Assert.Equal(typeof(int), Assert.IsAssignableFrom<MethodCallExpression>(explicitGenericCall.Body).Method.GetGenericArguments()[0]);
    }

    /// <summary>
    /// 验证匿名对象、DTO 和数组形状中的聚合表达式可编译。
    /// </summary>
    [Fact]
    public void AggregateExpressions_WhenUsedInAnonymousDtoAndArrayShapes_ShouldCompile()
    {
        var functions = CreateFunctions<SampleEntity>();
        Expression<Func<SampleEntity, object>> anonymous = entity => new
        {
            Count = SqlFn.Count(entity.Id),
            Total = SqlFn.Sum(entity.IntValue)
        };
        Expression<Func<SampleEntity, AggregateProjection>> dto = entity => new AggregateProjection
        {
            Count = functions.Count(entity.Id),
            Total = functions.Sum(entity.IntValue)
        };
        Expression<Func<SampleEntity, object[]>> array = entity => new object[]
        {
            SqlFn.Count(entity.Id),
            functions.CountAll(),
            SqlFn.Sum(entity.IntValue)
        };

        Assert.IsAssignableFrom<NewExpression>(StripConvert(anonymous.Body));
        Assert.IsType<MemberInitExpression>(dto.Body);
        Assert.IsAssignableFrom<NewArrayExpression>(array.Body);

        var anonymousSelect = new Func<SqlLambdaQuery, SqlLambdaQuery>(query => query
            .From<SampleEntity>()
            .Select<SampleEntity>(entity => new
            {
                Count = SqlFn.Count(entity.Id),
                Total = SqlFn.Sum(entity.IntValue)
            }));
        var dtoSelect = new Func<SqlLambdaQuery, SqlLambdaQuery>(query => query
            .From<SampleEntity>()
            .Select<SampleEntity>(entity => new AggregateProjection
            {
                Count = SqlFn.Count(entity.Id),
                Total = SqlFn.Sum(entity.IntValue)
            }));
        var arraySelect = new Func<SqlLambdaQuery, SqlLambdaQuery>(query => query
            .From<SampleEntity>()
            .Select<SampleEntity>(entity => new object[]
            {
                SqlFn.Count(entity.Id),
                functions.CountAll(),
                SqlFn.Sum(entity.IntValue)
            }));
        Assert.NotNull(anonymousSelect);
        Assert.NotNull(dtoSelect);
        Assert.NotNull(arraySelect);
    }

    /// <summary>
    /// 验证聚合签名完整提供显式别名与去重选项组合。
    /// </summary>
    [Fact]
    public void AggregateSignatures_WhenInspected_ShouldExposeExplicitAliasAndDistinctMatrix()
    {
        AssertCountAll(typeof(SqlFn), 2);
        AssertCountAll(typeof(SqlAggregateFunctions<SampleEntity>), 2);
        AssertDirectCount(typeof(SqlFn));
        AssertDirectCount(typeof(SqlAggregateFunctions<SampleEntity>));

        AssertNumericOverloads(typeof(SqlFn), "Sum",
            (typeof(int), typeof(int?)), (typeof(int?), typeof(int?)),
            (typeof(long), typeof(long?)), (typeof(long?), typeof(long?)),
            (typeof(decimal), typeof(decimal?)), (typeof(decimal?), typeof(decimal?)),
            (typeof(double), typeof(double?)), (typeof(double?), typeof(double?)));
        AssertNumericOverloads(typeof(SqlAggregateFunctions<SampleEntity>), "Sum",
            (typeof(int), typeof(int?)), (typeof(int?), typeof(int?)),
            (typeof(long), typeof(long?)), (typeof(long?), typeof(long?)),
            (typeof(decimal), typeof(decimal?)), (typeof(decimal?), typeof(decimal?)),
            (typeof(double), typeof(double?)), (typeof(double?), typeof(double?)));
        AssertNumericOverloads(typeof(SqlFn), "Avg",
            (typeof(decimal), typeof(decimal?)), (typeof(decimal?), typeof(decimal?)),
            (typeof(double), typeof(double?)), (typeof(double?), typeof(double?)));
        AssertNumericOverloads(typeof(SqlAggregateFunctions<SampleEntity>), "Avg",
            (typeof(decimal), typeof(decimal?)), (typeof(decimal?), typeof(decimal?)),
            (typeof(double), typeof(double?)), (typeof(double?), typeof(double?)));
        AssertValueOverloads(typeof(SqlFn), "Min");
        AssertValueOverloads(typeof(SqlFn), "Max");
        AssertValueOverloads(typeof(SqlAggregateFunctions<SampleEntity>), "Min");
        AssertValueOverloads(typeof(SqlAggregateFunctions<SampleEntity>), "Max");
    }

    /// <summary>
    /// 验证表达式之外直接调用聚合标记时抛出不支持异常。
    /// </summary>
    [Fact]
    public void Markers_WhenInvokedOutsideExpression_ShouldThrowNotSupportedException()
    {
        var functions = CreateFunctions<SampleEntity>();
        AssertMarkersThrow(typeof(SqlFn), null);
        AssertMarkersThrow(typeof(SqlAggregateFunctions<SampleEntity>), functions);
    }

    /// <summary>
    /// 验证函数表达式的结果类型及方法调用形状。
    /// </summary>
    /// <typeparam name="TEntity">被聚合、映射或写入的实体类型。</typeparam>
    /// <typeparam name="TResult">表达式或异步操作的结果类型。</typeparam>
    /// <param name="expression">待验证或处理的表达式。</param>
    private static void AssertExpression<TEntity, TResult>(Expression<Func<TEntity, TResult>> expression)
    {
        Assert.Equal(typeof(TResult), expression.Body.Type);
        Assert.IsAssignableFrom<MethodCallExpression>(expression.Body);
    }

    /// <summary>
    /// 移除表达式外层的类型转换节点。
    /// </summary>
    /// <param name="expression">待验证或处理的表达式。</param>
    /// <returns>移除外层转换后的表达式。</returns>
    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression unary &&
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            expression = unary.Operand;
        return expression;
    }

    /// <summary>
    /// 验证总数聚合标记的重载数量及返回类型。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="expectedCount">预期的数量。</param>
    private static void AssertCountAll(Type type, int expectedCount)
    {
        var methods = GetDirectMethods(type, "CountAll");
        Assert.Equal(expectedCount, methods.Length);
        Assert.All(methods, method => Assert.Equal(typeof(long), method.ReturnType));
        var signatures = methods.Select(method => string.Join(",", method.GetParameters()
                .Select(parameter => parameter.ParameterType == typeof(string) ? "String" : parameter.ParameterType.Name)))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "", "String" }.OrderBy(signature => signature, StringComparer.Ordinal), signatures);
        AssertNoOptionalParameters(methods);
    }

    /// <summary>
    /// 验证直接计数标记的重载签名。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    private static void AssertDirectCount(Type type)
    {
        var methods = GetDirectMethods(type, "Count");
        Assert.Equal(4, methods.Length);
        Assert.All(methods, method =>
        {
            Assert.True(method.IsGenericMethodDefinition);
            Assert.Equal(typeof(long), method.ReturnType);
        });
        AssertOptionVariants(methods, 1);
        AssertNoOptionalParameters(methods);
    }

    /// <summary>
    /// 验证数值函数重载的输入及返回类型。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="name">注册或构造的测试服务、操作或场景名称。</param>
    /// <param name="expected">预期的重载输入及返回类型集合。</param>
    private static void AssertNumericOverloads(Type type, string name,
        params (Type InputType, Type ReturnType)[] expected)
    {
        var methods = GetDirectMethods(type, name);
        Assert.Equal(expected.Length * 4, methods.Length);
        foreach (var (inputType, returnType) in expected)
        {
            var matching = methods.Where(method => method.GetParameters()[0].ParameterType == inputType).ToArray();
            Assert.Equal(4, matching.Length);
            Assert.All(matching, method => Assert.Equal(returnType, method.ReturnType));
            AssertOptionVariants(matching, 1);
        }
        AssertNoOptionalParameters(methods);
    }

    /// <summary>
    /// 验证值函数的重载签名集合。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="name">注册或构造的测试服务、操作或场景名称。</param>
    private static void AssertValueOverloads(Type type, string name)
    {
        var methods = GetDirectMethods(type, name);
        Assert.Equal(12, methods.Length);
        var genericMethods = methods.Where(method => method.IsGenericMethodDefinition).ToArray();
        Assert.Equal(8, genericMethods.Length);
        Assert.All(genericMethods, method =>
        {
            Assert.True((method.GetGenericArguments()[0].GenericParameterAttributes &
                         GenericParameterAttributes.NotNullableValueTypeConstraint) != 0);
            Assert.Equal(typeof(Nullable<>), method.ReturnType.GetGenericTypeDefinition());
        });
        foreach (var group in genericMethods.GroupBy(method => method.GetParameters()[0].ParameterType.ToString()))
        {
            Assert.Equal(4, group.Count());
            AssertOptionVariants(group, 1);
        }

        var stringMethods = methods.Where(method => !method.IsGenericMethodDefinition).ToArray();
        Assert.Equal(4, stringMethods.Length);
        Assert.All(stringMethods, method => Assert.Equal(typeof(string), method.ReturnType));
        AssertOptionVariants(stringMethods, 1);
        AssertNoOptionalParameters(methods);
    }

    /// <summary>
    /// 验证函数方法的选项参数组合。
    /// </summary>
    /// <param name="methods">待检查的反射方法集合。</param>
    /// <param name="skip">检查选项前跳过的参数个数。</param>
    private static void AssertOptionVariants(IEnumerable<MethodInfo> methods, int skip)
    {
        var expected = new[] { "", "String", "Boolean", "String,Boolean" };
        var signatures = methods.Select(method => string.Join(",",
                method.GetParameters().Skip(skip).Select(parameter =>
                    parameter.ParameterType == typeof(string) ? "String" :
                    parameter.ParameterType == typeof(bool) ? "Boolean" : parameter.ParameterType.Name)))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(signature => signature, StringComparer.Ordinal), signatures);
    }

    /// <summary>
    /// 验证函数方法没有可选参数。
    /// </summary>
    /// <param name="methods">待检查的反射方法集合。</param>
    private static void AssertNoOptionalParameters(IEnumerable<MethodInfo> methods) =>
        Assert.DoesNotContain(methods.SelectMany(method => method.GetParameters()), parameter => parameter.IsOptional);

    /// <summary>
    /// 验证 SQL 函数标记直接调用时抛出异常。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="target">反射调用使用的目标实例。</param>
    private static void AssertMarkersThrow(Type type, object target)
    {
        var names = new[] { "CountAll", "Count", "Sum", "Avg", "Min", "Max" };
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                      BindingFlags.DeclaredOnly)
            .Where(method => names.Contains(method.Name))
            .Where(method => method.GetParameters().Length == 0 ||
                             !IsExpressionSelector(method.GetParameters()[0].ParameterType))
            .ToArray();

        Assert.NotEmpty(methods);
        foreach (var definition in methods)
        {
            var method = definition.IsGenericMethodDefinition
                ? definition.MakeGenericMethod(typeof(int))
                : definition;
            var args = method.GetParameters().Select(CreateArgument).ToArray();
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(target, args));
            Assert.Equal(NotSupportedMessage, Assert.IsType<NotSupportedException>(exception.InnerException).Message);
        }
    }

    /// <summary>
    /// 创建 SQL 函数反射调用所需的测试实参。
    /// </summary>
    /// <param name="parameter">需要构造测试实参的反射参数。</param>
    /// <returns>字符串、布尔值或值类型默认实例；其他引用类型返回 null。</returns>
    private static object CreateArgument(ParameterInfo parameter)
    {
        if (parameter.ParameterType == typeof(string))
            return parameter.Name == "columnAlias" ? "Aggregate" : "value";
        if (parameter.ParameterType == typeof(bool))
            return true;
        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }

    /// <summary>
    /// 判断类型是否为表达式选择器。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <returns>类型为泛型表达式选择器时为 true，否则为 false。</returns>
    private static bool IsExpressionSelector(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Expression<>);

    /// <summary>
    /// 查找类型中声明的同名公共方法。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="name">需要查找的方法名称。</param>
    /// <returns>符合筛选条件的方法集合。</returns>
    private static MethodInfo[] GetDirectMethods(Type type, string name) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == name)
            .Where(method => method.GetParameters().Length == 0 ||
                             !IsExpressionSelector(method.GetParameters()[0].ParameterType))
            .ToArray();

    /// <summary>
    /// 创建指定实体的聚合函数标记实例。
    /// </summary>
    /// <typeparam name="TEntity">被聚合、映射或写入的实体类型。</typeparam>
    /// <returns>供表达式构造使用的聚合函数标记实例。</returns>
    private static SqlAggregateFunctions<TEntity> CreateFunctions<TEntity>() =>
        (SqlAggregateFunctions<TEntity>)Activator.CreateInstance(typeof(SqlAggregateFunctions<TEntity>), true)!;

    /// <summary>
    /// 承载SQL 函数聚合与投影查询结果的投影数据模型。
    /// </summary>
    private sealed class AggregateProjection
    {
        /// <summary>
        /// 获取或设置函数聚合后的计数。
        /// </summary>
        public long Count { get; set; }
        /// <summary>
        /// 获取或设置函数聚合后的总值。
        /// </summary>
        public int? Total { get; set; }
    }

    /// <summary>
    /// 提供SQL 函数聚合与投影测试使用的实体记录。
    /// </summary>
    private sealed class SampleEntity
    {
        /// <summary>
        /// 获取或设置样例实体标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的整数值。
        /// </summary>
        public int IntValue { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的可空整数值。
        /// </summary>
        public int? NullableIntValue { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的长整数值。
        /// </summary>
        public long LongValue { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的十进制值。
        /// </summary>
        public decimal DecimalValue { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的双精度值。
        /// </summary>
        public double DoubleValue { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的可空双精度值。
        /// </summary>
        public double? NullableDoubleValue { get; set; }
        /// <summary>
        /// 获取或设置样例实体名称。
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}

