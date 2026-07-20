using Bing.Data;

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
    /// 测试 - Query执行资源接口不应作为普通业务API公开。
    /// </summary>
    [Fact]
    public void ExecutionResourceContracts_ShouldNotBePublic()
    {
        // Arrange
        var assembly = typeof(ISqlTransactionScope).Assembly;

        // Act
        var accessor = assembly.GetType("Bing.Data.Sql.ISqlExecutionResourceAccessor");
        var binder = assembly.GetType("Bing.Data.Sql.ISqlExecutionResourceBinder");
        var metadataBinder = assembly.GetType("Bing.Data.Sql.ISqlQueryMetadataBinder");

        // Assert
        Assert.NotNull(accessor);
        Assert.NotNull(binder);
        Assert.NotNull(metadataBinder);
        Assert.False(accessor.IsPublic);
        Assert.False(binder.IsPublic);
        Assert.False(metadataBinder.IsPublic);
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
    /// 测试目的：跨 ORM 的数据库契约仍应提供只读连接访问器。
    /// </summary>
    [Fact]
    public void DatabaseContract_ShouldUseConnectionAccessor()
    {
        Assert.True(typeof(IDatabaseConnectionAccessor).IsAssignableFrom(typeof(IDatabase)));
    }
}