using System.Linq.Expressions;
using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 聚合表达式标记 API 契约测试。
/// </summary>
public class SqlAggregateFunctionsTest
{
    /// <summary>
    /// 定义在 CLR 中直接调用聚合标记方法时抛出的错误消息。
    /// </summary>
    private const string NotSupportedMessage = "聚合标记方法只能用于 SQL 表达式，不能在 CLR 中直接调用。";

    /// <summary>
    /// 测试目的：聚合上下文只能由查询表达式提供，调用方不能直接构造。
    /// </summary>
    [Fact]
    public void Constructor_WhenPublicApiInspected_ShouldBeInternalOnly()
    {
        var type = typeof(SqlAggregateFunctions<SampleEntity>);

        Assert.Null(type.GetConstructor(Type.EmptyTypes));
        Assert.NotNull(type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            Type.EmptyTypes, null));
    }

    /// <summary>
    /// 测试目的：聚合表达式应以强类型返回值构建，并保留显式 alias 和 Distinct 重载。
    /// </summary>
    [Fact]
    public void AggregateMethods_WhenExpressionsAreBuilt_ShouldPreserveResultTypes()
    {
        var functions = CreateFunctions<SampleEntity>();

        AssertExpression<SampleEntity, long>(entity => functions.CountAll());
        AssertExpression<SampleEntity, long>(entity => functions.CountAll("Rows"));
        AssertExpression<SampleEntity, long>(entity => functions.Count(item => item.Id));
        AssertExpression<SampleEntity, long>(entity => functions.Count(item => item.Id, "Rows"));
        AssertExpression<SampleEntity, long>(entity => functions.Count(item => item.Id, true));
        AssertExpression<SampleEntity, long>(entity => functions.Count(item => item.Id, "Rows", true));

        AssertExpression<SampleEntity, int?>(entity => functions.Sum(item => item.IntValue));
        AssertExpression<SampleEntity, int?>(entity => functions.Sum(item => item.NullableIntValue));
        AssertExpression<SampleEntity, long?>(entity => functions.Sum(item => item.LongValue));
        AssertExpression<SampleEntity, long?>(entity => functions.Sum(item => item.NullableLongValue));
        AssertExpression<SampleEntity, decimal?>(entity => functions.Sum(item => item.DecimalValue));
        AssertExpression<SampleEntity, decimal?>(entity => functions.Sum(item => item.NullableDecimalValue));
        AssertExpression<SampleEntity, double?>(entity => functions.Sum(item => item.DoubleValue));
        AssertExpression<SampleEntity, double?>(entity => functions.Sum(item => item.NullableDoubleValue));

        AssertExpression<SampleEntity, decimal?>(entity => functions.Avg(item => item.DecimalValue));
        AssertExpression<SampleEntity, decimal?>(entity => functions.Avg(item => item.NullableDecimalValue));
        AssertExpression<SampleEntity, double?>(entity => functions.Avg(item => item.DoubleValue));
        AssertExpression<SampleEntity, double?>(entity => functions.Avg(item => item.NullableDoubleValue));

        AssertExpression<SampleEntity, int?>(entity => functions.Min(item => item.IntValue));
        AssertExpression<SampleEntity, int?>(entity => functions.Min(item => item.NullableIntValue));
        AssertExpression<SampleEntity, string>(entity => functions.Min(item => item.Name));
        AssertExpression<SampleEntity, int?>(entity => functions.Max(item => item.IntValue));
        AssertExpression<SampleEntity, int?>(entity => functions.Max(item => item.NullableIntValue));
        AssertExpression<SampleEntity, string>(entity => functions.Max(item => item.Name));
    }

    /// <summary>
    /// 测试目的：公开聚合重载应使用计划约定的强类型结果和显式参数组合。
    /// </summary>
    [Fact]
    public void AggregateMethods_WhenSignaturesAreInspected_ShouldExposeTypedOverloads()
    {
        var type = typeof(SqlAggregateFunctions<SampleEntity>);
        var countAll = GetMethods(type, "CountAll");
        Assert.Equal(2, countAll.Length);
        Assert.All(countAll, method => Assert.Equal(typeof(long), method.ReturnType));
        AssertOptionVariants(countAll, new[] { "", "String" });
        AssertNoOptionalParameters(countAll);

        var count = GetSelectorMethods(type, "Count");
        Assert.Equal(4, count.Length);
        Assert.All(count, method =>
        {
            Assert.True(method.IsGenericMethodDefinition);
            Assert.Equal(typeof(long), method.ReturnType);
        });
        AssertOptionVariants(count, new[] { "", "String", "Boolean", "String,Boolean" });
        AssertNoOptionalParameters(count);

        AssertNumericOverloads("Sum",
            (typeof(int), typeof(int?)),
            (typeof(int?), typeof(int?)),
            (typeof(long), typeof(long?)),
            (typeof(long?), typeof(long?)),
            (typeof(decimal), typeof(decimal?)),
            (typeof(decimal?), typeof(decimal?)),
            (typeof(double), typeof(double?)),
            (typeof(double?), typeof(double?)));
        AssertNumericOverloads("Avg",
            (typeof(decimal), typeof(decimal?)),
            (typeof(decimal?), typeof(decimal?)),
            (typeof(double), typeof(double?)),
            (typeof(double?), typeof(double?)));

        AssertValueAggregateOverloads("Min");
        AssertValueAggregateOverloads("Max");
    }

    /// <summary>
    /// 测试目的：所有聚合标记方法在普通 CLR 调用时都应抛出统一的 NotSupportedException。
    /// </summary>
    [Fact]
    public void AggregateMethods_WhenInvokedOutsideExpression_ShouldThrowNotSupportedException()
    {
        var functions = CreateFunctions<SampleEntity>();
        var methods = typeof(SqlAggregateFunctions<SampleEntity>)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        methods = methods.Where(method => method.Name == "CountAll" || IsSelectorMethod(method)).ToArray();

        Assert.NotEmpty(methods);
        foreach (var definition in methods)
        {
            var method = definition.IsGenericMethodDefinition
                ? definition.MakeGenericMethod(typeof(int))
                : definition;
            var arguments = method.GetParameters().Select(CreateArgument).ToArray();
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(functions, arguments));
            var innerException = Assert.IsType<NotSupportedException>(exception.InnerException);

            Assert.Equal(NotSupportedMessage, innerException.Message);
        }
    }

    /// <summary>
    /// 验证聚合表达式的结果类型及方法调用形状。
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
    /// 验证数值聚合重载的选择器及返回类型。
    /// </summary>
    /// <param name="methodName">待检查的方法名称。</param>
    /// <param name="expected">预期的重载输入及返回类型集合。</param>
    private static void AssertNumericOverloads(string methodName, params (Type SelectorType, Type ReturnType)[] expected)
    {
        var methods = GetSelectorMethods(typeof(SqlAggregateFunctions<SampleEntity>), methodName);
        Assert.Equal(expected.Length * 4, methods.Length);

        foreach (var (selectorType, returnType) in expected)
        {
            var matching = methods.Where(method => GetSelectorResultType(method) == selectorType).ToArray();
            Assert.Equal(4, matching.Length);
            Assert.All(matching, method => Assert.Equal(returnType, method.ReturnType));
            AssertOptionVariants(matching, new[] { "", "String", "Boolean", "String,Boolean" });
            AssertNoOptionalParameters(matching);
        }
    }

    /// <summary>
    /// 验证值聚合重载的签名集合。
    /// </summary>
    /// <param name="methodName">待检查的方法名称。</param>
    private static void AssertValueAggregateOverloads(string methodName)
    {
        var methods = GetSelectorMethods(typeof(SqlAggregateFunctions<SampleEntity>), methodName);
        Assert.Equal(12, methods.Length);

        var genericMethods = methods.Where(method => method.IsGenericMethodDefinition).ToArray();
        Assert.Equal(8, genericMethods.Length);
        Assert.All(genericMethods, method =>
        {
            var valueType = method.GetGenericArguments()[0];
            Assert.True((valueType.GenericParameterAttributes &
                GenericParameterAttributes.NotNullableValueTypeConstraint) != 0);
            Assert.Equal(typeof(Nullable<>).MakeGenericType(valueType), method.ReturnType);

            var selectorType = GetSelectorResultType(method);
            Assert.True(selectorType == valueType ||
                selectorType.IsGenericType &&
                selectorType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                selectorType.GetGenericArguments()[0] == valueType);
        });

        var stringMethods = methods.Where(method => !method.IsGenericMethodDefinition).ToArray();
        Assert.Equal(4, stringMethods.Length);
        Assert.All(stringMethods, method =>
        {
            Assert.Equal(typeof(string), method.ReturnType);
            Assert.Equal(typeof(string), GetSelectorResultType(method));
        });

        var optionVariants = new[] { "", "String", "Boolean", "String,Boolean" };
        AssertOptionVariants(methods, optionVariants.Concat(optionVariants).Concat(optionVariants).ToArray());
        AssertNoOptionalParameters(methods);
    }

    /// <summary>
    /// 验证聚合方法的选项参数组合。
    /// </summary>
    /// <param name="methods">待检查的反射方法集合。</param>
    /// <param name="expected">预期的断言结果。</param>
    private static void AssertOptionVariants(IEnumerable<MethodInfo> methods, string[] expected)
    {
        var signatures = methods.Select(method => string.Join(",",
            method.GetParameters().Skip(method.Name == "CountAll" ? 0 : 1).Select(parameter => parameter.ParameterType.Name)))
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(signature => signature, StringComparer.Ordinal), signatures);
    }

    /// <summary>
    /// 验证聚合方法没有可选参数。
    /// </summary>
    /// <param name="methods">待检查的反射方法集合。</param>
    private static void AssertNoOptionalParameters(IEnumerable<MethodInfo> methods)
    {
        Assert.DoesNotContain(methods.SelectMany(method => method.GetParameters()),
            parameter => parameter.IsOptional);
    }

    /// <summary>
    /// 读取聚合选择器表达式的结果类型。
    /// </summary>
    /// <param name="method">待检查的反射方法。</param>
    /// <returns>表达式选择器返回值的运行时类型。</returns>
    private static Type GetSelectorResultType(MethodInfo method)
    {
        var expressionType = method.GetParameters()[0].ParameterType;
        Assert.Equal(typeof(Expression<>), expressionType.GetGenericTypeDefinition());
        var delegateType = expressionType.GetGenericArguments()[0];
        return delegateType.GetGenericArguments()[1];
    }

    /// <summary>
    /// 查找类型中声明的同名公共实例方法。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="name">需要查找的方法名称。</param>
    /// <returns>符合筛选条件的方法集合。</returns>
    private static MethodInfo[] GetMethods(Type type, string name) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == name)
            .ToArray();

    /// <summary>
    /// 查找指定聚合方法的选择器重载。
    /// </summary>
    /// <param name="type">待检查或实例化的运行时类型。</param>
    /// <param name="name">需要查找的方法名称。</param>
    /// <returns>符合筛选条件的方法集合。</returns>
    private static MethodInfo[] GetSelectorMethods(Type type, string name) =>
        GetMethods(type, name).Where(IsSelectorMethod).ToArray();

    /// <summary>
    /// 判断方法是否使用表达式选择器。
    /// </summary>
    /// <param name="method">待检查的反射方法。</param>
    /// <returns>首个参数为泛型表达式选择器时为 true，否则为 false。</returns>
    private static bool IsSelectorMethod(MethodInfo method)
    {
        var parameterType = method.GetParameters().FirstOrDefault()?.ParameterType;
        return parameterType != null && parameterType.IsGenericType &&
               parameterType.GetGenericTypeDefinition() == typeof(Expression<>);
    }

    /// <summary>
    /// 创建聚合方法反射调用所需的测试实参。
    /// </summary>
    /// <param name="parameter">需要构造测试实参的反射参数。</param>
    /// <returns>支持的参数类型对应的非 null 测试实参；其他类型抛出异常。</returns>
    private static object? CreateArgument(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsGenericType &&
            parameter.ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
        {
            var delegateType = parameter.ParameterType.GetGenericArguments()[0];
            var delegateArguments = delegateType.GetGenericArguments();
            var parameterExpression = Expression.Parameter(delegateArguments[0], "entity");
            return Expression.Lambda(delegateType, Expression.Default(delegateArguments[1]), parameterExpression);
        }

        if (parameter.ParameterType == typeof(string))
            return "Aggregate";
        if (parameter.ParameterType == typeof(bool))
            return true;

        throw new InvalidOperationException($"未处理的聚合参数类型：{parameter.ParameterType}。");
    }

    /// <summary>
    /// 创建指定实体的聚合函数标记实例。
    /// </summary>
    /// <typeparam name="TEntity">被聚合、映射或写入的实体类型。</typeparam>
    /// <returns>供表达式构造使用的聚合函数标记实例。</returns>
    private static SqlAggregateFunctions<TEntity> CreateFunctions<TEntity>() =>
        (SqlAggregateFunctions<TEntity>)Activator.CreateInstance(typeof(SqlAggregateFunctions<TEntity>), true)!;

    /// <summary>
    /// 提供聚合函数字段与结果测试使用的实体记录。
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
        /// 获取或设置聚合函数使用的可空长整数值。
        /// </summary>
        public long? NullableLongValue { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的十进制值。
        /// </summary>
        public decimal DecimalValue { get; set; }
        /// <summary>
        /// 获取或设置聚合函数使用的可空十进制值。
        /// </summary>
        public decimal? NullableDecimalValue { get; set; }
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
