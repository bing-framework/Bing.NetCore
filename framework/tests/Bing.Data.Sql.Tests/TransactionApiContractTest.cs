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
    /// 测试目的：跨 ORM 的数据库契约仍应提供只读连接访问器。
    /// </summary>
    [Fact]
    public void DatabaseContract_ShouldUseConnectionAccessor()
    {
        Assert.True(typeof(IDatabaseConnectionAccessor).IsAssignableFrom(typeof(IDatabase)));
    }
}