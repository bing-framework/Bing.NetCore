using System.ComponentModel;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
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

    /// <summary>测试目的：Root 查询只公开唯一的非泛型 Lambda From<TEntity> 入口。</summary>
    [Fact]
    public void From_WhenPublicApiInspected_ShouldExposeNonGenericLambdaEntryPoints()
    {
        var type = typeof(ISqlQuery);
        var fromMethods = type.GetMethods().Where(method => method.Name == "From" && method.IsGenericMethodDefinition)
            .ToArray();
        var fromEntity = fromMethods.Single();
        var fromTable = type.GetMethod("FromTable");
        var fromSubquery = type.GetMethods().Single(method => method.Name == "FromSubquery");

        Assert.Equal(typeof(SqlLambdaQuery), fromEntity.ReturnType);
        Assert.Equal(new[] { typeof(string), typeof(string) },
            fromEntity.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.All(fromEntity.GetParameters(), parameter => Assert.True(parameter.HasDefaultValue));
        Assert.Equal(typeof(SqlLambdaQuery), fromTable.ReturnType);
        Assert.Equal(typeof(SqlLambdaQuery), fromSubquery.ReturnType);
    }

    /// <summary>测试目的：From<TEntity>()、From<TEntity>(alias) 和双参数调用必须解析为同一非泛型返回类型。</summary>
    [Fact]
    public void From_WhenOverloadsAreResolved_ShouldUseTheSameNonGenericPath()
    {
        var fromMethods = typeof(ISqlQuery).GetMethods()
            .Where(method => method.Name == "From" && method.IsGenericMethodDefinition)
            .ToArray();
        var from = Assert.Single(fromMethods);

        Assert.Equal(typeof(SqlLambdaQuery), from.ReturnType);
        Assert.Equal(2, from.GetParameters().Length);
        Assert.All(from.GetParameters(), parameter => Assert.True(parameter.HasDefaultValue));
    }

    /// <summary>
    /// 测试目的：查询程序集只应公开非泛型 Lambda 描述，不得导出泛型或多元兼容包装器。
    /// </summary>
    [Fact]
    public void LambdaQuery_WhenPublicTypesInspected_ShouldExposeOnlyNonGenericDescription()
    {
        var lambdaTypes = typeof(ISqlQuery).Assembly.GetTypes()
            .Where(type => type.Name.StartsWith("SqlLambdaQuery", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(lambdaTypes, type => type == typeof(SqlLambdaQuery) && type.IsPublic);
        Assert.DoesNotContain(lambdaTypes, type => type.IsGenericTypeDefinition && type.IsPublic);
        Assert.DoesNotContain(typeof(ISqlQuery).Assembly.GetTypes(), type => type.Name == "SqlMultiLambdaQuery");
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
    /// 测试目的：类型化 Join 的右侧来源参数必须明确命名为 rightAlias，避免调用方误把它当作通用别名。
    /// </summary>
    [Fact]
    public void Join_WhenPublicApiInspected_ShouldNameRightSourceExplicitly()
    {
        var joinMethods = typeof(SqlLambdaQuery).GetMethods()
            .Where(method => method.Name is "Join" or "LeftJoin" or "RightJoin" or "FullJoin")
            .Where(method => method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2)
            .Where(method => method.GetParameters().First().ParameterType.IsGenericType &&
                method.GetParameters().First().ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
            .ToArray();

        Assert.NotEmpty(joinMethods);
        Assert.All(joinMethods, method => Assert.Contains(method.GetParameters(), parameter =>
            parameter.Name == "rightAlias"));
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

        Assert.Equal(typeof(SqlFluentQuery), rootType.GetMethod("Query")?.ReturnType);
        Assert.DoesNotContain(rootType.GetMethods(), method => method.Name == "Query" && method.IsGenericMethod);
        Assert.Equal(typeof(SqlTextQuery), rootType.GetMethods().Single(method => method.Name == "Sql" &&
            method.IsGenericMethod == false).ReturnType);
        Assert.Equal(typeof(SqlTextQuery), rootType.GetMethods().Single(method => method.Name == "SqlInterpolated" &&
            method.IsGenericMethod == false).ReturnType);
        Assert.Equal(typeof(SqlProcedureQuery), rootType.GetMethod("Procedure")?.ReturnType);
        Assert.DoesNotContain(rootType.GetMethods(), method => method.Name == "Procedure" && method.IsGenericMethod);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToEntity" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToList" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToPage" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToListAsync" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "ToPageAsync" && method.IsGenericMethodDefinition);
        Assert.Contains(textType.GetMethods(), method => method.Name == "AsAsyncEnumerable" && method.IsGenericMethodDefinition);
        Assert.Contains(typeof(SqlProcedureQuery).GetMethods(), method => method.Name == "ExecuteList" &&
            method.IsGenericMethodDefinition);
        Assert.Contains(typeof(SqlProcedureQuery).GetMethods(), method => method.Name == "ExecuteScalar" &&
            method.IsGenericMethodDefinition);
    }

    /// <summary>
    /// 测试目的：非泛型查询描述只保留唯一推荐的基数与集合终结语义。
    /// </summary>
    [Fact]
    public void QueryDescriptions_WhenPublicApiInspected_ShouldNotExposeDuplicateHighLevelTerminals()
    {
        var types = new[] { typeof(SqlLambdaQuery), typeof(SqlFluentQuery), typeof(SqlTextQuery),
            typeof(SqlProcedureQuery) };

        Assert.All(types, type =>
        {
            Assert.DoesNotContain(type.GetMethods(), method => method.Name == "SingleOrDefault");
            Assert.DoesNotContain(type.GetMethods(), method => method.Name == "ToDictionary");
        });
    }

    /// <summary>
    /// 测试目的：运行时 SPI 保持跨程序集可用，但不得作为普通查询用户的 IntelliSense 推荐入口。
    /// </summary>
    [Fact]
    public void RuntimeContracts_WhenPublicApiInspected_ShouldBeHiddenFromIntelliSense()
    {
        var runtimeTypes = new[]
        {
            typeof(ISqlQueryBuilderSource),
            typeof(ISqlQueryPlanExecutor),
            typeof(ISqlQueryRuntimeBindingController),
            typeof(SqlQueryPlan),
            typeof(SqlBuilderRuntimeBridge),
            typeof(SqlQueryRuntimeFactory),
            typeof(SqlQueryRuntimeBinding)
        };

        Assert.All(runtimeTypes, type => Assert.Equal(EditorBrowsableState.Never,
            type.GetCustomAttributes(typeof(EditorBrowsableAttribute), false)
                .Cast<EditorBrowsableAttribute>().Single().State));
    }

    /// <summary>
    /// 测试目的：生产程序集的友元只能指向测试或 Benchmark，避免生产实现依赖内部访问绕过 Runtime SPI。
    /// </summary>
    [Fact]
    public void RuntimeContracts_WhenFriendAssembliesInspected_ShouldContainOnlyTestConsumers()
    {
        var friends = typeof(ISqlQuery).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        Assert.NotEmpty(friends);
        Assert.All(friends, friend =>
        {
            Assert.True(friend.Contains(".Tests", StringComparison.Ordinal) ||
                friend.EndsWith(".Benchmarks", StringComparison.Ordinal));
        });
    }

    /// <summary>
    /// 测试目的：公开 SQL 查询计划只能暴露不可变描述和身份信息，不得通过属性泄露 Builder、连接或事务。
    /// </summary>
    [Fact]
    public void RuntimeContracts_WhenQueryPlanPropertiesInspected_ShouldNotExposeExecutionResources()
    {
        var publicProperties = typeof(SqlQueryPlan).GetProperties();

        Assert.DoesNotContain(publicProperties, property => typeof(ISqlBuilder).IsAssignableFrom(property.PropertyType));
        Assert.DoesNotContain(publicProperties, property => typeof(DbConnection).IsAssignableFrom(property.PropertyType));
        Assert.DoesNotContain(publicProperties, property => typeof(DbTransaction).IsAssignableFrom(property.PropertyType));
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
        public SqlFluentQuery Query() => throw new NotSupportedException();

        public SqlFluentQuery<TResult> Query<TResult>() => throw new NotSupportedException();

        public SqlTextQuery<TResult> Sql<TResult>(string sql, object parameters = null) =>
            throw new NotSupportedException();

        public SqlTextQuery Sql(string sql, object parameters = null) => throw new NotSupportedException();

        public SqlTextQuery<TResult> SqlInterpolated<TResult>(FormattableString sql) =>
            throw new NotSupportedException();

        public SqlTextQuery SqlInterpolated(FormattableString sql) => throw new NotSupportedException();

        public SqlProcedureQuery Procedure(string procedure, object parameters = null) =>
            throw new NotSupportedException();

        public SqlProcedureQuery<TResult> Procedure<TResult>(string procedure, object parameters = null) =>
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
