using System.Linq.Expressions;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 独立 SQL 查询公开契约单元测试。
/// </summary>
public class SqlQueryApiContractTest
{
    /// <summary>
    /// 测试目的：Root 查询只负责资源和描述创建，不得公开 Fluent 操作集合或 Builder 逃逸入口。
    /// </summary>
    [Fact]
    public void RootQuery_WhenPublicApiInspected_ShouldNotExposeFluentOperationOrBuilder()
    {
        Assert.False(typeof(ISqlQueryOperation).IsAssignableFrom(typeof(ISqlQuery)));
        Assert.Null(typeof(ISqlQuery).GetProperty("SqlBuilder"));
        Assert.Null(typeof(ISqlQuery).GetMethod("GetBuilder"));
        Assert.Null(typeof(ISqlQuery).GetMethod("Config"));
    }

    /// <summary>
    /// 测试目的：Root 查询应同时保留已发布一元泛型兼容入口和两参数非泛型主入口，且不能混淆两者的静态返回类型。
    /// </summary>
    [Fact]
    public void From_WhenPublicApiInspected_ShouldExposeNonGenericLambdaEntryPoints()
    {
        var type = typeof(ISqlQuery);
        var fromMethods = type.GetMethods().Where(method => method.Name == "From" && method.IsGenericMethodDefinition)
            .ToArray();
        var fromEntity = fromMethods.Single(method => method.ReturnType == typeof(SqlLambdaQuery));
        var fromLegacyEntity = fromMethods.Single(method => method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(SqlLambdaQuery<>));
        var fromTable = type.GetMethod("FromTable");
        var fromSubquery = type.GetMethods().Single(method => method.Name == "FromSubquery");

        Assert.Equal(typeof(SqlLambdaQuery), fromEntity.ReturnType);
        Assert.Equal(new[] { typeof(string), typeof(string) },
            fromEntity.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(SqlLambdaQuery), fromTable.ReturnType);
        Assert.Equal(typeof(SqlLambdaQuery), fromSubquery.ReturnType);
        Assert.Single(fromLegacyEntity.GetParameters());
        Assert.Equal(typeof(string), fromLegacyEntity.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(SqlLambdaQuery<>), fromLegacyEntity.ReturnType.GetGenericTypeDefinition());
        Assert.All(fromMethods, method => Assert.Single(method.GetGenericArguments()));
    }

    /// <summary>
    /// 测试目的：零参数或单参数 From<TEntity> 必须保持 Shipped 泛型兼容语义，两参数调用才进入非泛型描述主路径。
    /// </summary>
    [Fact]
    public void From_WhenOverloadsAreResolved_ShouldKeepCompatibilityAndMainPathDistinct()
    {
        var fromMethods = typeof(ISqlQuery).GetMethods()
            .Where(method => method.Name == "From" && method.IsGenericMethodDefinition)
            .ToArray();
        var mainPath = fromMethods.Single(method => method.ReturnType == typeof(SqlLambdaQuery));
        var compatibilityPath = fromMethods.Single(method => method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(SqlLambdaQuery<>));

        Assert.Equal(2, mainPath.GetParameters().Length);
        Assert.DoesNotContain(mainPath.GetParameters(), parameter => parameter.HasDefaultValue);
        Assert.Single(compatibilityPath.GetParameters());
        Assert.True(compatibilityPath.GetParameters()[0].HasDefaultValue);
        Assert.Equal(typeof(SqlLambdaQuery<>), compatibilityPath.ReturnType.GetGenericTypeDefinition());
    }

    /// <summary>
    /// 测试目的：查询程序集只应公开非泛型主描述和一元泛型兼容描述，不得公开高元数 Lambda 类型。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicTypesInspected_ShouldExposeOnlyNonGenericDescription()
    {
        var lambdaTypes = typeof(ISqlQuery).Assembly.GetTypes()
            .Where(type => type.Name.StartsWith("SqlLambdaQuery", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(lambdaTypes, type => type == typeof(SqlLambdaQuery) && type.IsPublic);
        Assert.Contains(lambdaTypes, type => type == typeof(SqlLambdaQuery<>) && type.IsPublic);
        Assert.DoesNotContain(lambdaTypes, type => type.IsGenericTypeDefinition && type.IsPublic &&
            type != typeof(SqlLambdaQuery<>));
    }

    /// <summary>
    /// 测试目的：非泛型 Lambda 描述只接受一元或二元表达式，不得恢复高元数表达式和后置 On。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicApiInspected_ShouldUseMethodLevelUnaryOrBinaryExpressions()
    {
        var methods = typeof(SqlLambdaQuery).GetMethods();
        var expressionDelegates = methods
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Expression<>))
            .Select(type => type.GetGenericArguments()[0])
            .ToArray();
        var expressionInputCounts = expressionDelegates.Select(GetInputCount).ToArray();

        Assert.DoesNotContain(methods, method => method.Name is "On" or "As");
        Assert.NotEmpty(expressionInputCounts);
        Assert.All(expressionInputCounts, inputCount => Assert.InRange(inputCount, 0, 2));
        Assert.All(new[] { typeof(Func<,,,,,>), typeof(Func<,,,,,,>) }, delegateType =>
            Assert.True(GetInputCount(delegateType) > 2));
        Assert.Contains(methods, method => method.Name == "Where" && method.IsGenericMethodDefinition);
        Assert.Contains(methods, method => method.Name == "Join" && method.IsGenericMethodDefinition &&
            method.GetGenericArguments().Length == 2);

        int GetInputCount(Type delegateType) => delegateType.GetMethod("Invoke")?.GetParameters().Length ??
            throw new InvalidOperationException($"表达式委托 {delegateType} 缺少 Invoke 方法。");
    }

    /// <summary>
    /// 测试目的：查询描述仍应公开结果类型由终结方法决定的同步和异步物化入口。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicApiInspected_ShouldExposeResultTerminals()
    {
        var type = typeof(SqlLambdaQuery);
        Assert.Contains(type.GetMethods(), method => method.Name == "ToList" && method.IsGenericMethodDefinition);
        Assert.Contains(type.GetMethods(), method => method.Name == "ToPage" && method.IsGenericMethodDefinition);
        Assert.Contains(type.GetMethods(), method => method.Name == "ToListAsync" && method.IsGenericMethodDefinition);
        Assert.Null(type.GetMethod("As"));
    }

    /// <summary>
    /// 测试目的：Raw 主入口应为非泛型描述，结果类型由同步或异步终结方法选择，并保留多映射终结能力。
    /// </summary>
    [Fact]
    public void RawQuery_WhenPublicApiInspected_ShouldSelectResultAtTerminal()
    {
        var rootType = typeof(ISqlQuery);
        var textType = typeof(SqlTextQuery);

        Assert.Equal(typeof(SqlTextQuery), rootType.GetMethods().Single(method => method.Name == "Sql" &&
            method.IsGenericMethod == false).ReturnType);
        Assert.Equal(typeof(SqlTextQuery), rootType.GetMethods().Single(method => method.Name == "SqlInterpolated" &&
            method.IsGenericMethod == false).ReturnType);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToEntity" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToList" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToDictionary" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToPage" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToListAsync" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToPageAsync" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "AsAsyncEnumerable" && method.IsGenericMethodDefinition);
    }

    /// <summary>
    /// 测试目的：未实现运行时绑定控制器的查询对象必须明确拒绝资源绑定。
    /// </summary>
    [Fact]
    public void RuntimeBinding_WhenQueryDoesNotImplementController_ShouldThrowInvalidOperationException()
    {
        using var query = new UnsupportedRuntimeBindingQuery();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SqlQueryRuntimeBinding.BindDatabaseContext(query, new DatabaseContext()));

        Assert.Equal("当前 SQL 查询对象不支持框架运行时资源绑定。", exception.Message);
    }

    private sealed class UnsupportedRuntimeBindingQuery : ISqlQuery
    {
        public SqlFluentQuery<TResult> Query<TResult>() => throw new NotSupportedException();

        public SqlTextQuery<TResult> Sql<TResult>(string sql, object parameters = null) =>
            throw new NotSupportedException();

        public SqlTextQuery Sql(string sql, object parameters = null) => throw new NotSupportedException();

        public SqlTextQuery<TResult> SqlInterpolated<TResult>(FormattableString sql) =>
            throw new NotSupportedException();

        public SqlTextQuery SqlInterpolated(FormattableString sql) => throw new NotSupportedException();

        public SqlProcedureQuery<TResult> Procedure<TResult>(string procedure, object parameters = null) =>
            throw new NotSupportedException();

        public SqlLambdaQuery<TEntity> From<TEntity>(string alias = null) where TEntity : class =>
            throw new NotSupportedException();

        public SqlLambdaQuery From<TEntity>(string alias = null, string schema = null) where TEntity : class =>
            throw new NotSupportedException();

        public SqlLambdaQuery FromTable(string table, string alias = null, string schema = null) =>
            throw new NotSupportedException();

        public SqlLambdaQuery FromSubquery<TProjection>(SqlSubquery<TProjection> subquery)
            where TProjection : class => throw new NotSupportedException();

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
