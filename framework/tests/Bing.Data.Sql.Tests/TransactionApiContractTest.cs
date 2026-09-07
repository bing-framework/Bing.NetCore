using Bing.Data;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// SQL 事务 API 契约测试。
/// </summary>
public class TransactionApiContractTest
{
    /// <summary>
    /// 测试目的：事务工厂应使用显式无键、数据源键和隔离级别重载，避免 dbKey 可选参数造成 API 歧义。
    /// </summary>
    [Fact]
    public void TransactionFactory_WhenPublicApiInspected_ShouldExposeExplicitDataSourceOverloads()
    {
        var factoryType = typeof(ISqlTransactionScopeFactory);
        var beginMethods = factoryType.GetMethods().Where(method => method.Name == "Begin").ToArray();
        var beginAsyncMethods = factoryType.GetMethods().Where(method => method.Name == "BeginAsync").ToArray();

        Assert.Equal(new[] { 0, 1, 2 }, beginMethods.Select(method => method.GetParameters().Length).OrderBy(value => value));
        Assert.All(beginMethods.SelectMany(method => method.GetParameters()), parameter =>
            Assert.False(parameter.HasDefaultValue));
        Assert.Equal(new[] { 1, 2, 3 }, beginAsyncMethods.Select(method => method.GetParameters().Length).OrderBy(value => value));
        Assert.All(beginAsyncMethods.SelectMany(method => method.GetParameters()), parameter =>
        {
            if (parameter.ParameterType == typeof(string))
                Assert.Equal("dataSourceKey", parameter.Name);
        });
        Assert.All(beginAsyncMethods.SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.ParameterType == typeof(CancellationToken)), parameter =>
        {
            Assert.Equal("cancellationToken", parameter.Name);
            Assert.True(parameter.HasDefaultValue);
        });
    }

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
        // Assert
        Assert.True(contextType.IsAssignableFrom(scopeType));
        Assert.NotNull(contextType.GetProperty(nameof(ISqlTransactionContext.MappingProfile)));
        Assert.NotNull(contextType.GetProperty(nameof(ISqlTransactionContext.ReadPreference)));
        Assert.Null(contextType.GetProperty("DatabaseContext"));
        Assert.Null(contextType.GetProperty("Connection"));
        Assert.Null(contextType.GetProperty("Transaction"));
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
    /// 测试目的：独立查询描述的异步集合终结重载应显式提供取消令牌入口，且不使用可选参数。
    /// </summary>
    [Fact]
    public void QueryDescriptionAsyncTerminals_ShouldExposeOptionalCancellationToken()
    {
        // Arrange
        var methods = typeof(SqlFluentQuery).GetMethods().Where(method => method.Name is
            "ToListAsync" or "FirstAsync" or "FirstOrDefaultAsync" or "SingleAsync" or "SingleOrDefaultAsync" or
            "ScalarAsync" or "ToPageAsync" or "AsAsyncEnumerable").ToList();
        var listMethods = methods.Where(method => method.Name == "ToListAsync").ToList();
        var otherTerminalMethods = methods.Where(method => method.Name != "ToListAsync").ToList();

        // Act
        Assert.NotEmpty(methods);
        Assert.All(listMethods, method =>
        {
            Assert.DoesNotContain(method.GetParameters(), parameter => parameter.HasDefaultValue);
        });
        Assert.Contains(listMethods, method => method.GetParameters()
            .Any(parameter => parameter.ParameterType == typeof(CancellationToken)));
        Assert.All(otherTerminalMethods, method =>
        {
            var cancellationToken = method.GetParameters()
                .SingleOrDefault(parameter => parameter.ParameterType == typeof(CancellationToken));
            Assert.NotNull(cancellationToken);
            Assert.Equal("cancellationToken", cancellationToken.Name);
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
    /// 测试目的：跨 ORM 的数据库契约仍应提供只读连接访问器。
    /// </summary>
    [Fact]
    public void DatabaseContract_ShouldUseConnectionAccessor()
    {
        Assert.True(typeof(IDatabaseConnectionAccessor).IsAssignableFrom(typeof(IDatabase)));
    }
}
