using System.ComponentModel;
using Bing.Data;
using Bing.Data.Sql.Database;

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
    /// 测试 - 旧事务和连接管理器应标记为废弃。
    /// </summary>
    [Fact]
    public void LegacyManagers_ShouldBeMarkedObsoleteAndHidden()
    {
        // Arrange
    #pragma warning disable CS0618
        var managerTypes = new[] { typeof(IDbConnectionManager), typeof(IDbTransactionManager) };
    #pragma warning restore CS0618

        // Act
        var attributes = managerTypes.Select(type => new
        {
            Obsolete = type.GetCustomAttribute<ObsoleteAttribute>(),
            EditorBrowsable = type.GetCustomAttribute<EditorBrowsableAttribute>()
        }).ToList();

        // Assert
        Assert.All(attributes, attribute =>
        {
            Assert.NotNull(attribute.Obsolete);
            Assert.NotNull(attribute.EditorBrowsable);
            Assert.Equal(EditorBrowsableState.Never, attribute.EditorBrowsable.State);
        });
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

    #pragma warning disable CS0618
    /// <summary>
    /// 测试 - 历史外部上下文的资源绑定成员应标记为废弃并隐藏。
    /// </summary>
    [Fact]
    public void QueryExternalContext_LegacyResourceBindingMembers_ShouldBeMarkedObsoleteAndHidden()
    {
        // Arrange
        var methods = new[]
        {
            typeof(ISqlQueryExternalContext).GetMethod(nameof(ISqlQueryExternalContext.SetOwnedConnection)),
            typeof(ISqlQueryExternalContext).GetMethod(nameof(ISqlQueryExternalContext.SetExternalTransactionResolver)),
            typeof(ISqlQueryExternalContext).GetMethod(nameof(ISqlQueryExternalContext.SetConnectionSource))
        };

        // Act
        var attributes = methods.Select(method => new
        {
            Obsolete = method.GetCustomAttribute<ObsoleteAttribute>(),
            EditorBrowsable = method.GetCustomAttribute<EditorBrowsableAttribute>()
        }).ToList();

        // Assert
        Assert.All(attributes, attribute =>
        {
            Assert.NotNull(attribute.Obsolete);
            Assert.NotNull(attribute.EditorBrowsable);
            Assert.Equal(EditorBrowsableState.Never, attribute.EditorBrowsable.State);
        });
    }

    /// <summary>
    /// 测试 - 历史外部上下文应作为隐藏的兼容接口保留。
    /// </summary>
    [Fact]
    public void QueryExternalContext_ShouldBeMarkedObsoleteAndHidden()
    {
        // Arrange
        var contextType = typeof(ISqlQueryExternalContext);

        // Act
        var obsolete = contextType.GetCustomAttribute<ObsoleteAttribute>();
        var editorBrowsable = contextType.GetCustomAttribute<EditorBrowsableAttribute>();

        // Assert
        Assert.NotNull(obsolete);
        Assert.NotNull(editorBrowsable);
        Assert.Equal(EditorBrowsableState.Never, editorBrowsable.State);
    }
    #pragma warning restore CS0618

    /// <summary>
    /// 测试 - 历史数据库接口应继承只读连接访问器并保留兼容标记。
    /// </summary>
    [Fact]
    public void DatabaseCompatibilityContract_ShouldUseConnectionAccessor()
    {
        // Arrange
    #pragma warning disable CS0618
        var databaseType = typeof(IDatabase);
        var factoryType = typeof(IDatabaseFactory);
    #pragma warning restore CS0618

        // Act
        var factoryObsolete = factoryType.GetCustomAttribute<ObsoleteAttribute>();
        var factoryEditorBrowsable = factoryType.GetCustomAttribute<EditorBrowsableAttribute>();

        // Assert
        Assert.True(typeof(IDatabaseConnectionAccessor).IsAssignableFrom(databaseType));
        Assert.NotNull(factoryObsolete);
        Assert.NotNull(factoryEditorBrowsable);
        Assert.Equal(EditorBrowsableState.Never, factoryEditorBrowsable.State);
    }
}