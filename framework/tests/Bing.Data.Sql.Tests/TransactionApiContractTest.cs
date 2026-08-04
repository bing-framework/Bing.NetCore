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
    /// 测试目的：资源和元数据绑定 SPI 不得重新暴露为公开契约。
    /// </summary>
    [Fact]
    public void RuntimeBindingContracts_ShouldNotBePublic()
    {
        // Arrange
        var publicTypeNames = typeof(ISqlQuery).Assembly.GetExportedTypes().Select(type => type.Name);

        // Assert
        Assert.DoesNotContain("ISqlQueryExecutionResourceAccessor", publicTypeNames);
        Assert.DoesNotContain("ISqlQueryResourceBinder", publicTypeNames);
        Assert.DoesNotContain("ISqlQueryMetadataBinder", publicTypeNames);
        Assert.DoesNotContain("ISqlTransactionScopeResourceBinder", publicTypeNames);
        Assert.DoesNotContain("ISqlTransactionScopeLease", publicTypeNames);
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
    /// 测试目的：Root Query 不得重新公开异步终端执行入口，调用方必须使用独立查询描述。
    /// </summary>
    [Fact]
    public void QueryContract_ShouldNotExposeLegacyAsyncTerminals()
    {
        // Arrange
        var legacyMethods = new[]
        {
            "ExecuteQuery", "ExecuteQueryAsync", "ExecuteProcedureQuery", "ExecuteProcedureQueryAsync",
            "ExecuteScalar", "ExecuteScalarAsync", "ExecuteProcedureScalar", "ExecuteProcedureScalarAsync",
            "ExecuteSingle", "ExecuteSingleAsync", "ExecuteProcedureSingle", "ExecuteProcedureSingleAsync",
            "StreamQuery", "StreamQueryAsync", "StreamAsync", "PagerQuery", "PagerQueryAsync"
        };
        var publicMethods = typeof(ISqlQuery).GetMethods().Select(method => method.Name);

        // Act and Assert
        Assert.DoesNotContain(publicMethods, method => legacyMethods.Contains(method));
    }

    /// <summary>
    /// 测试目的：独立查询描述的异步终端均应在末尾提供可选取消令牌。
    /// </summary>
    [Fact]
    public void QueryDescriptionAsyncTerminals_ShouldExposeOptionalCancellationToken()
    {
        // Arrange
        var methods = typeof(SqlQuery<int>).GetMethods().Where(method => method.Name is
            "ToListAsync" or "FirstAsync" or "FirstOrDefaultAsync" or "SingleAsync" or "SingleOrDefaultAsync" or
            "ScalarAsync" or "ToPageAsync" or "AsAsyncEnumerable").ToList();

        // Act
        Assert.NotEmpty(methods);
        Assert.All(methods, method =>
        {
            var cancellationToken = method.GetParameters().Last();
            Assert.Equal(typeof(CancellationToken), cancellationToken.ParameterType);
            Assert.True(cancellationToken.HasDefaultValue);
        });
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
    /// 测试目的：旧 Query 异步扩展已移除，避免通过 Root Query 重新暴露终端执行。
    /// </summary>
    [Fact]
    public void QueryAsyncExtensions_ShouldNotExposeLegacyTerminals()
    {
        // Arrange
        var legacyMethods = typeof(SqlQueryExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is "ToEntityAsync" or "ToListAsync" or "ToPagerListAsync").ToList();

        // Act and Assert
        Assert.Empty(legacyMethods);
    }

    /// <summary>
    /// 测试目的：旧 Root Query 标量转换扩展已移除，标量查询应通过描述对象执行。
    /// </summary>
    [Fact]
    public void ScalarAsyncExtensions_ShouldNotExposeLegacyTerminals()
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
        Assert.Empty(scalarMethods);
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