using Bing.Data;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 事务 API 契约测试。
/// </summary>
public class TransactionApiContractTest
{
    /// <summary>
    /// 测试 - 公开事务作用域应继承只读事务上下文。
    /// </summary>
    [Fact]
    public void TransactionScope_ShouldImplementReadonlyTransactionContext()
    {
        // Arrange
        var contextType = typeof(ISqlTransactionContext);
        var scopeType = typeof(ISqlTransactionScope);

        // Act
        var databaseContextProperty = contextType.GetProperty(nameof(ISqlTransactionContext.DatabaseContext));

        // Assert
        Assert.True(contextType.IsAssignableFrom(scopeType));
        Assert.NotNull(databaseContextProperty);
        Assert.False(databaseContextProperty.CanWrite);
    }

    /// <summary>
    /// 测试 - Query 执行资源仅通过公开窄 SPI 提供给执行实现。
    /// </summary>
    [Fact]
    public void ExecutionResourceContracts_ShouldBePublicNarrowSpis()
    {
        // Arrange
        var accessor = typeof(ISqlQueryExecutionResourceAccessor);
        var binder = typeof(ISqlQueryResourceBinder);
        var metadataBinder = typeof(ISqlQueryMetadataBinder);
        var scopeBinder = typeof(ISqlTransactionScopeResourceBinder);

        // Assert
        Assert.True(accessor.IsPublic);
        Assert.True(binder.IsPublic);
        Assert.True(metadataBinder.IsPublic);
        Assert.True(scopeBinder.IsPublic);
    }

    /// <summary>
    /// 测试目的：SQL Item 必须仅通过语义工厂公开创建，避免布尔参数表达原始或解析状态。
    /// </summary>
    [Fact]
    public void SqlItemFactories_ShouldReplacePublicBooleanConstructors()
    {
        // Arrange
        var itemTypes = new[] { typeof(SqlItem), typeof(ColumnItem), typeof(JoinItem) };

        // Act
        var rawFactory = typeof(SqlItem).GetMethod(nameof(SqlItem.Raw));
        var tableFactory = typeof(JoinItem).GetMethod(nameof(JoinItem.CreateTable));
        var atomicFactory = typeof(JoinItem).GetMethod(nameof(JoinItem.CreateAtomicTable));

        // Assert
        Assert.All(itemTypes, type => Assert.Empty(type.GetConstructors()));
        Assert.NotNull(rawFactory);
        Assert.NotNull(tableFactory);
        Assert.NotNull(atomicFactory);
    }

    /// <summary>
    /// 测试目的：旧连接、事务、外部上下文和数据库工厂契约必须完全移除。
    /// </summary>
    [Fact]
    public void LegacyContracts_ShouldNotExist()
    {
        // Arrange
        var assembly = typeof(ISqlTransactionScope).Assembly;

        // Act
        var legacyTypes = new[]
        {
            "Bing.Data.Sql.Database.IDbConnectionManager",
            "Bing.Data.Sql.Database.IDbTransactionManager",
            "Bing.Data.Sql.ISqlQueryExternalContext",
            "Bing.Data.IDatabaseFactory"
        };

        // Assert
        Assert.All(legacyTypes, typeName => Assert.Null(assembly.GetType(typeName)));
    }

    /// <summary>
    /// 测试目的：Query 公共契约不得暴露连接或事务生命周期入口。
    /// </summary>
    [Fact]
    public void QueryContract_ShouldNotExposeConnectionOrTransactionLifecycle()
    {
        // Arrange
        var queryType = typeof(ISqlQuery);
        var forbiddenMethods = new[]
        {
            "GetConnection", "SetConnection", "GetTransaction", "SetTransaction", "BeginTransaction",
            "CommitTransaction", "RollbackTransaction"
        };

        // Act
        var methods = queryType.GetMethods().Select(method => method.Name).ToList();

        // Assert
        Assert.DoesNotContain(methods, method => forbiddenMethods.Contains(method));
    }

    /// <summary>
    /// 测试目的：所有公开的异步列表、多映射、标量、单实体及存储过程查询入口都应在末尾提供可选取消令牌。
    /// </summary>
    [Fact]
    public void QueryAsyncContracts_ShouldExposeOptionalCancellationToken()
    {
        // Arrange
        var cancellationAwareMethods = typeof(ISqlQuery).GetMethods().Where(method => method.Name is
            nameof(ISqlQuery.ExecuteQueryAsync) or nameof(ISqlQuery.ExecuteProcedureQueryAsync) or
            nameof(ISqlQuery.ExecuteScalarAsync) or nameof(ISqlQuery.ExecuteProcedureScalarAsync) or
            nameof(ISqlQuery.ExecuteSingleAsync) or nameof(ISqlQuery.ExecuteProcedureSingleAsync)).ToList();

        // Act and Assert
        Assert.NotEmpty(cancellationAwareMethods);
        Assert.All(cancellationAwareMethods, method =>
        {
            var parameter = method.GetParameters().Last();
            Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
            Assert.True(parameter.HasDefaultValue);
        });
    }

    /// <summary>
    /// 测试目的：分页查询只应暴露令牌感知重载，避免旧委托入口丢失取消语义。
    /// </summary>
    [Fact]
    public void PagerQueryAsyncContracts_ShouldOnlyExposeCancellationAwareCallback()
    {
        // Arrange
        var methods = typeof(ISqlQuery).GetMethods().Where(method => method.Name == nameof(ISqlQuery.PagerQueryAsync))
            .ToList();
        var cancellationAwareMethod = Assert.Single(methods);

        // Act
        var cancellationToken = cancellationAwareMethod.GetParameters().Last();

        // Assert
        Assert.Equal(2, cancellationAwareMethod.GetParameters()[0].ParameterType.GetGenericArguments().Length);
        Assert.Equal(typeof(CancellationToken), cancellationToken.ParameterType);
        Assert.True(cancellationToken.HasDefaultValue);
    }

    /// <summary>
    /// 测试目的：多结果集异步读取只应保留令牌感知重载，避免旧无令牌入口丢失取消语义。
    /// </summary>
    [Fact]
    public void MultipleQueryResultAsyncContracts_ShouldOnlyExposeCancellationAwareRead()
    {
        // Arrange
        var methods = typeof(ISqlMultipleQueryResult).GetMethods().Where(method => method.Name == "ReadAsync").ToList();
        var cancellationAwareMethods = methods.Where(method => method.GetParameters().Length == 1).ToList();

        // Act and Assert
        Assert.Equal(2, cancellationAwareMethods.Count);
        Assert.All(cancellationAwareMethods, method =>
        {
            var parameter = method.GetParameters().Single();
            Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
            Assert.True(parameter.HasDefaultValue);
        });
    }

    /// <summary>
    /// 测试目的：查询 Fluent 异步扩展和分页扩展均应在末尾提供可选取消令牌。
    /// </summary>
    [Fact]
    public void QueryAsyncExtensions_ShouldExposeOptionalCancellationToken()
    {
        // Arrange
        var cancellationAwareMethods = typeof(SqlQueryExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "ToEntityAsync" or "ToListAsync" or "ToPagerListAsync")
            .ToList();

        // Act and Assert
        Assert.NotEmpty(cancellationAwareMethods);
        Assert.All(cancellationAwareMethods, method =>
        {
            var parameter = method.GetParameters().Last();
            Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
            Assert.True(parameter.HasDefaultValue);
        });
    }

    /// <summary>
    /// 测试目的：所有异步标量转换扩展都应在末尾提供可选取消令牌。
    /// </summary>
    [Fact]
    public void ScalarAsyncExtensions_ShouldExposeOptionalCancellationToken()
    {
        // Arrange
        var scalarMethods = typeof(SqlQueryExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "ToStringAsync" or "ToIntAsync" or "ToIntOrNullAsync" or
                "ToLongAsync" or "ToLongOrNullAsync" or "ToGuidAsync" or "ToGuidOrNullAsync" or "ToBoolAsync" or
                "ToBoolOrNullAsync" or "ToFloatAsync" or "ToFloatOrNullAsync" or "ToDoubleAsync" or
                "ToDoubleOrNullAsync" or "ToDecimalAsync" or "ToDecimalOrNullAsync" or "ToDateTimeAsync" or
                "ToDateTimeOrNullAsync")
            .ToList();

        // Act and Assert
        Assert.Equal(17, scalarMethods.Count);
        Assert.All(scalarMethods, method =>
        {
            var parameter = method.GetParameters().Last();
            Assert.Equal(typeof(CancellationToken), parameter.ParameterType);
            Assert.True(parameter.HasDefaultValue);
        });
    }

    /// <summary>
    /// 测试目的：7.0 不再公开含义重复的旧 Query 扩展，调用方必须使用命名明确的替代 API。
    /// </summary>
    [Fact]
    public void QueryExtensions_ShouldNotExposeRemovedLegacyMethods()
    {
        // Arrange
        var extensionMethods = typeof(SqlQueryExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);
        var removedMethodNames = new[] { "To", "ToAsync", "ToScalar", "ToScalarAsync" };

        // Act
        var remainingLegacyMethods = extensionMethods.Where(method => removedMethodNames.Contains(method.Name)).ToList();

        // Assert
        Assert.Empty(remainingLegacyMethods);
    }

    /// <summary>
    /// 测试目的：跨 ORM 的数据库契约仍应提供只读连接访问器。
    /// </summary>
    [Fact]
    public void DatabaseContract_ShouldUseConnectionAccessor()
    {
        Assert.True(typeof(IDatabaseConnectionAccessor).IsAssignableFrom(typeof(IDatabase)));
    }
}