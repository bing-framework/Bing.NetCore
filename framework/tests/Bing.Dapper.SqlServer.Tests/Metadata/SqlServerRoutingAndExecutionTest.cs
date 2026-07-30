using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// SqlServer 路由与执行测试
/// </summary>
public class SqlServerRoutingAndExecutionTest
{
    /// <summary>
    /// 测试 - 查询工厂应使用解析后的连接字符串创建查询对象。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_ShouldUseResolvedConnectionString()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=reporting;Database=test;"
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create<InspectableSqlServerQuery>("reporting");

        // Assert
        query.CurrentOptions.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        query.CurrentOptions.DatabaseType.ShouldBe(DatabaseType.SqlServer);
        query.CurrentOptions.Connection.ShouldBeNull();
    }

    /// <summary>
    /// 测试 - 查询工厂只传 dbKey 时应通过数据源解析数据库类型和连接字符串。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_CreateWithDbKey_ShouldResolveDataSource()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=reporting;Database=test;",
            MappingProfile = "reporting-v2"
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create<InspectableSqlServerQuery>("reporting");

        // Assert
        query.CurrentOptions.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        query.CurrentOptions.DatabaseType.ShouldBe(DatabaseType.SqlServer);
        query.CurrentOptions.GetDatabaseContext().MappingProfile.ShouldBe("reporting-v2");
    }

    /// <summary>
    /// 测试 - 数据源注册应支持从 IConfiguration.GetConnectionString 读取连接字符串。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WithConfiguration_ShouldUseConnectionStringSection()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:reporting"] = "Server=config;Database=test;"
            })
            .Build();
        var services = CreateServices();
        services.AddSqlDataSource(configuration, "reporting", DatabaseType.SqlServer);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create<InspectableSqlServerQuery>("reporting");

        // Assert
        query.CurrentOptions.ConnectionString.ShouldBe("Server=config;Database=test;");
    }

    /// <summary>
    /// 测试目的：无键数据源快捷注册不应静默覆盖已注册的其他 Provider 默认数据源。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDefaultProviderDiffers_ShouldRequireNamedDataSource()
    {
        // Arrange
        var services = CreateServices();
        services.AddSqlDataSource(null, DatabaseType.SqlServer, "Server=default;Database=test;");
        services.AddSqlDataSource(null, DatabaseType.Sqlite, "Data Source=default.db");
        using var provider = services.BuildServiceProvider();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            provider.GetRequiredService<SqlMetadataOptions>());

        // Assert
        exception.Message.ShouldContain("多 Provider");
        exception.Message.ShouldContain("具名数据源");
    }

    /// <summary>
    /// 测试 - 命名连接字符串不存在时不应回退到模板连接字符串。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenNamedConnectionStringMissing_ShouldThrow()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionStringName = "ReportingConnection"
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=template;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            factory.Create<InspectableSqlServerQuery>("reporting"));

        // Assert
        exception.Message.ShouldContain("reporting");
        exception.Message.ShouldContain("ReportingConnection");
    }

    /// <summary>
    /// 测试 - 命名连接字符串未命中时不应回退到默认连接字符串。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenNamedConnectionStringNotConfigured_ShouldNotUseDefaultConnection()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionStringName = "ReportingConnection"
        };
        var services = CreateServices(metadataOptions);
        services.AddSingleton(new ConnectionStringCollection { Default = "Server=default;Database=test;" });
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=template;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            factory.Create<InspectableSqlServerQuery>("reporting"));

        // Assert
        exception.Message.ShouldContain("ReportingConnection");
        exception.Message.ShouldNotContain("Server=default");
    }

    /// <summary>
    /// 测试 - UsePrimary 应按数据源配置切换到主库连接字符串。
    /// </summary>
    [Fact]
    public void UsePrimary_WhenPrimaryDataSourceConfigured_ShouldResolvePrimaryConnectionString()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["default"] = new SqlDataSourceDescriptor
        {
            Key = "default",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=primary;Database=test;"
        };
        metadataOptions.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=reporting;Database=test;",
            PrimaryReadStrategy = PrimaryReadStrategy.PrimaryDataSource,
            PrimaryDataSourceKey = "default"
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=template;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        var query = factory.Create<InspectableSqlServerQuery>("reporting");

        // Act
        query.UsePrimary();
        var connectionString = query.InvokeResolveConnectionString();

        // Assert
        connectionString.ShouldBe("Server=primary;Database=test;");
    }

    /// <summary>
    /// 测试 - 查询工厂创建接口查询对象时不应为了获取实现类型而提前创建实例。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_CreateInterface_ShouldNotInstantiateServiceWhenResolvingImplementationType()
    {
        // Arrange
        CountedSqlServerQuery.CreatedCount = 0;
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=reporting;Database=test;"
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<ICountedSqlServerQuery, CountedSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create<ICountedSqlServerQuery>("reporting");

        // Assert
        query.ShouldBeOfType<CountedSqlServerQuery>();
        CountedSqlServerQuery.CreatedCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 工厂创建的查询对象应在连接上下文与实体映射上下文之间保持一致。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_ShouldUseSameContextForConnectionAndEntityMapping()
    {
        // Arrange
        var metadataOptions = CreateRoutingMetadataOptions();
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=default;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        var accessor = provider.GetRequiredService<IDatabaseContextAccessor>();

        // Act
        var query = factory.Create<InspectableSqlServerQuery>("reporting");
        accessor.Current = new DatabaseContext
        {
            DbKey = "default",
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "default",
                DatabaseType = DatabaseType.SqlServer,
                ConnectionString = "Server=default;Database=test;"
            }
        };
        query.From<MappedSample>("u").Where<MappedSample>(t => t.Name, "abc");

        // Assert
        query.CurrentOptions.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        query.CurrentSql.ShouldContain("[Users_Reporting]");
        query.CurrentSql.ShouldContain("[reporting_name]");
    }

    /// <summary>
    /// 测试 - 原生 SQL 参数映射应生成带完整元数据的数据库参数。
    /// </summary>
    [Fact]
    public void RawSql_WithParameterMap_ShouldCreateFullMetadataParams()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        var result = executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name Where [Id]=@id",
            new { name = "abc", id = 1 },
            map => map.Map("name", t => t.Name).Map("id", t => t.Id));

        // Assert
        result.ShouldBe(1);
        connection.LastCreatedParameters.Count.ShouldBe(2);
        var name = connection.LastCreatedParameters.Single(t => t.ParameterName == "name");
        name.Value.ShouldBe("abc");
        name.DbType.ShouldBe(DbType.String);
        name.Size.ShouldBe(20);
        var id = connection.LastCreatedParameters.Single(t => t.ParameterName == "id");
        id.Value.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 原生 SQL 参数映射显式传入 null 时应绑定 DBNull 而不是从源对象回退取值。
    /// </summary>
    [Fact]
    public void RawSql_WithParameterMapExplicitNull_ShouldBindDbNull()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        var result = executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name Where [Id]=@id",
            new { name = "source", id = 1 },
            map => map.Add("name", t => t.Name, null).Map("id", t => t.Id));

        // Assert
        result.ShouldBe(1);
        var name = connection.LastCreatedParameters.Single(t => t.ParameterName == "name");
        name.Value.ShouldBe(DBNull.Value);
        var id = connection.LastCreatedParameters.Single(t => t.ParameterName == "id");
        id.Value.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - SQL 诊断应只包含一个标准化参数快照。
    /// </summary>
    [Fact]
    public void ExecuteSql_WithParameterMap_ShouldPublishParameterMetadataDiagnostics()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(t => message = t);
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name Where [Id]=@id",
            new { id = 1 },
            map => map.Add("name", t => t.Name, null).Map("id", t => t.Id));

        // Assert
        message.ShouldNotBeNull();
        message.Parameters.ShouldNotBeNull();
        message.Parameters.OriginalParameterType.ShouldBe(typeof(SqlParameterMap<MappedSample>).FullName);
        message.Parameters.IsMetadataBound.ShouldBeTrue();
        message.Parameters.Items.Count.ShouldBe(2);
        message.Connection.ShouldNotBeNull();
        message.Connection.Database.ShouldBe("test");
        message.Connection.Source.ShouldBe(SqlConnectionSource.External);
        message.Connection.Ownership.ShouldBe(SqlResourceOwnership.External);
        message.Transaction.ShouldNotBeNull();
        var name = message.Parameters.Items.Single(t => t.Name == "name");
        name.Value.ShouldBeNull();
        name.OriginalValue.ShouldBeNull();
        name.PropertyName.ShouldBe(nameof(MappedSample.Name));
        name.Source.ShouldBe(SqlParameterSource.RawSql);
        name.MetadataLevel.ShouldBe(SqlParameterMetadataLevel.Full);
    }

    /// <summary>
    /// 测试目的：非敏感参数诊断应同时保留最终值与转换前的原始 CLR 值。
    /// </summary>
    [Fact]
    public void ExecuteSql_WithParameterMap_ShouldPublishOriginalParameterValue()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item);
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name",
            new { name = "Bing" },
            map => map.Map("name", item => item.Name));

        // Assert
        var parameter = message.Parameters.Items.Single(item => item.Name == "name");
        parameter.Value.ShouldBe("Bing");
        parameter.OriginalValue.ShouldBe("Bing");
    }

    /// <summary>
    /// 测试目的：诊断观察器接收的二进制参数快照不能与调用方输入数组共享引用。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenParameterIsBinary_ShouldPublishIndependentDiagnosticSnapshot()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item);
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);
        var payload = new byte[] { 1, 2, 3 };

        // Act
        executor.ExecuteSql<MappedSample>("Update [Users] Set [Payload]=@payload", new { payload },
            map => map.Add("payload", item => item.Payload, payload));
        payload[0] = 9;

        // Assert
        var parameter = message.Parameters.Items.Single();
        ((byte[])parameter.Value).ShouldBe(new byte[] { 1, 2, 3 });
        ((byte[])parameter.OriginalValue).ShouldBe(new byte[] { 1, 2, 3 });
    }

    /// <summary>
    /// 测试目的：诊断应记录固定映射配置，租户标识默认不输出且可由 Query 选项显式启用。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenTenantDiagnosticsIsConfigured_ShouldApplyOptInPolicy()
    {
        // Arrange
        var messages = new List<DiagnosticsMessage>();
        using var observer = new SqlDiagnosticObserver(messages.Add);
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);
        executor.Config(options =>
        {
            options.SetDatabaseContext(new DatabaseContext
            {
                DbKey = "diagnostics",
                TenantId = "tenant-a",
                MappingProfile = "profile-a",
                DataSource = new SqlDataSourceDescriptor
                {
                    Key = "diagnostics",
                    DatabaseType = DatabaseType.SqlServer
                }
            });
        });

        // Act
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "default" });
        executor.Config(options => options.IncludeTenantIdInDiagnostics = true);
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "opt-in" });

        // Assert
        messages.Count.ShouldBe(2);
        messages[0].MappingProfile.ShouldBe("profile-a");
        messages[0].TenantId.ShouldBeNull();
        messages[1].MappingProfile.ShouldBe("profile-a");
        messages[1].TenantId.ShouldBe("tenant-a");
    }

    /// <summary>
    /// 测试目的：执行路径缺失映射输入时，绑定异常应包含实际 SQL 与数据源键。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenMappedInputIsMissing_ShouldExposeSqlAndDbKey()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var exception = Should.Throw<SqlParameterBindingException>(() => executor.ExecuteSql<MappedSample>(
            "Update [Users] Set [Name]=@name",
            new { id = 1 },
            map => map.Map("name", item => item.Name)));

        // Assert
        exception.ParameterName.ShouldBe("name");
        exception.Sql.ShouldBe("Update [Users] Set [Name]=@name");
        exception.DbKey.ShouldBe("primary");
        exception.PropertyName.ShouldBe(nameof(MappedSample.Name));
        connection.LastTransaction.RollbackCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 未提供参数映射时应保持原生 SQL 的旧参数行为。
    /// </summary>
    [Fact]
    public void RawSql_WithoutParameterMap_ShouldKeepBackwardCompatibility()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        var result = executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" });

        // Assert
        result.ShouldBe(1);
        connection.LastCreatedParameters.Count.ShouldBe(1);
        var parameter = connection.LastCreatedParameters.Single();
        parameter.ParameterName.ShouldBe("name");
        parameter.Value.ShouldBe("abc");
    }

    /// <summary>
    /// 测试目的：自有连接创建必须传递已解析 SQL Provider Key，不能回退为仅按 DatabaseType 查找工厂。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenOwnedConnectionIsCreated_ShouldResolveFactoryByProviderKey()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var resolver = new CaptureConnectionResolver(connection);
        var services = CreateServices();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(resolver);
        services.AddSqlServerSqlExecutor<InspectableSqlServerExecutor, InspectableSqlServerExecutor>(options =>
            options.ConnectionString("Server=test;Database=test;"));
        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<InspectableSqlServerExecutor>();

        // Act
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" });

        // Assert
        resolver.LastProviderKey.ShouldBe(SqlServerSqlProvider.Instance.Key);
    }

    /// <summary>
    /// 测试目的：主库短事务策略执行成功后应提交内部事务并关闭内部连接。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenPrimaryReadTransactionSucceeds_ShouldCommitAndCloseOwnedResources()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var result = executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" });

        // Assert
        result.ShouldBe(1);
        connection.LastTransaction.ShouldNotBeNull();
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    /// <summary>
    /// 测试目的：主库短事务必须在 Before 诊断消息创建前就存在，以便诊断反映实际执行资源。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenPrimaryReadTransactionStarts_ShouldPublishTransactionInBeforeDiagnostics()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item);
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" });

        // Assert
        message.ShouldNotBeNull();
        message.Transaction.ShouldNotBeNull();
        message.Transaction.HasTransaction.ShouldBeTrue();
        message.Transaction.IsPrimaryReadTransaction.ShouldBeTrue();
        message.Transaction.Ownership.ShouldBe(SqlResourceOwnership.Owned);
    }

    /// <summary>
    /// 测试目的：主库短事务策略执行失败后应回滚内部事务并关闭内部连接。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenPrimaryReadTransactionFails_ShouldRollbackAndCloseOwnedResources()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnExecute = true };
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" }));

        // Assert
        connection.LastTransaction.ShouldNotBeNull();
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.RollbackCount.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    /// <summary>
    /// 测试目的：主库短事务失败并回滚后，同一执行器应清除内部状态并允许后续执行成功。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenPrimaryReadTransactionFails_ShouldAllowExecutorReuse()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnExecute = true };
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "first" }));
        var failedTransaction = connection.LastTransaction;
        connection.ThrowOnExecute = false;
        var result = executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "second" });

        // Assert
        failedTransaction.RollbackCount.ShouldBe(1);
        result.ShouldBe(1);
        connection.LastTransaction.ShouldNotBeSameAs(failedTransaction);
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试 - 独立 SQL 事务作用域提交时应提交作用域拥有的事务。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_Commit_ShouldCommitOwnedTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();

        // Act
        using var scope = scopeFactory.Begin();
        var executor = scope.CreateExecutor();
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" });
        scope.Commit();

        // Assert
        connection.LastTransaction.ShouldNotBeNull();
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试 - 独立 SQL 事务作用域未完成时 Dispose 应自动回滚。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_DisposeWhenNotCompleted_ShouldRollbackOwnedTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();

        // Act
        using (scopeFactory.Begin())
        {
        }

        // Assert
        connection.LastTransaction.ShouldNotBeNull();
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.RollbackCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：子 Query 绑定事务作用域失败时，应释放子对象且不影响作用域提交自身事务。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_CreateQuery_WhenBindingFails_ShouldDisposeChildAndKeepScopeUsable()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var invalidChild = DispatchProxy.Create<ISqlQuery, DisposeTrackingSqlQueryProxy>();
        var trackingProxy = (DisposeTrackingSqlQueryProxy)(object)invalidChild;
        var queryFactory = new BindingFailureSqlQueryFactory(provider.GetRequiredService<ISqlQueryFactory>(), invalidChild);
        var scopeFactory = new SqlTransactionScopeFactory(queryFactory,
            provider.GetRequiredService<ISqlExecutorFactory>());

        // Act
        using var scope = scopeFactory.Begin();
        var exception = Should.Throw<InvalidOperationException>(() => scope.CreateQuery());
        scope.Commit();

        // Assert
        exception.Message.ShouldContain("事务作用域资源绑定器", Case.Insensitive);
        trackingProxy.DisposeCount.ShouldBe(1);
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：事务作用域提交后，已创建的子执行器必须拒绝在已结束事务外继续执行。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenCompleted_ShouldRejectExecutionFromExistingChild()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();
        var scope = scopeFactory.Begin();
        var executor = scope.CreateExecutor();

        // Act
        scope.Commit();
        var exception = Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "after-complete" }));

        // Assert
        exception.Message.ShouldContain("事务作用域已结束");
        connection.LastTransaction.CommitCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：事务作用域资源绑定器应绑定固定事务上下文，并在租约失效后拒绝资源访问。
    /// </summary>
    [Fact]
    public void TransactionScopeResourceBinder_WhenLeaseExpires_ShouldRejectBoundResourceAccess()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<ISqlQuery>();
        var transaction = connection.BeginTransaction();
        var lease = new TestTransactionScopeLease("scope-1");
        var context = new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = new SqlDataSourceDescriptor { Key = "reporting", DatabaseType = DatabaseType.SqlServer }
        };

        // Act
        ((ISqlTransactionScopeResourceBinder)query).BindTransactionScope(context, connection, transaction, lease);
        var accessor = (ISqlQueryExecutionResourceAccessor)query;
        var boundTransaction = accessor.GetCurrentTransaction();
        var transactionId = accessor.GetCurrentTransactionId();
        lease.IsActive = false;
        var exception = Should.Throw<InvalidOperationException>(() => accessor.GetCurrentTransaction());

        // Assert
        boundTransaction.ShouldBeSameAs(transaction);
        transactionId.ShouldBe("scope-1");
        exception.Message.ShouldBe("事务作用域租约已失效。");
    }

    /// <summary>
    /// 测试目的：外部事务绑定器必须拒绝与 Query 外部连接不匹配的事务连接。
    /// </summary>
    [Fact]
    public void QueryResourceBinder_WhenTransactionConnectionDiffers_ShouldThrow()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var otherConnection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var query = provider.GetRequiredService<ISqlQuery>();
        var binder = (ISqlQueryResourceBinder)query;
        binder.BindExternalConnection(connection, SqlConnectionSource.External);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            binder.BindExternalTransaction(otherConnection.BeginTransaction(), "external-1"));

        // Assert
        exception.Message.ShouldBe("外部事务连接与 Query 连接不一致。");
    }

    /// <summary>
    /// 测试目的：Before 诊断载荷被订阅者修改后，不应污染同一操作的 After 诊断快照。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenDiagnosticObserverMutatesBeforePayload_ShouldKeepAfterPayloadIndependent()
    {
        // Arrange
        DiagnosticsMessage before = null;
        DiagnosticsMessage after = null;
        using var observer = new SqlDiagnosticObserver(message =>
        {
            if (message.Operation == SqlQueryDiagnosticListenerNames.BeforeExecute)
            {
                before = message;
                message.Connection.DbKey = "mutated";
                return;
            }
            if (message.Operation == SqlQueryDiagnosticListenerNames.AfterExecute)
                after = message;
        }, name => name == SqlQueryDiagnosticListenerNames.BeforeExecute ||
                 name == SqlQueryDiagnosticListenerNames.AfterExecute);
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "Bing" });

        // Assert
        before.ShouldNotBeNull();
        after.ShouldNotBeNull();
        before.ShouldNotBeSameAs(after);
        before.OperationId.ShouldBe(after.OperationId);
        after.Connection.DbKey.ShouldBe("primary");
        after.Transaction.TransactionId.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试 - 诊断观察器修改一维数组参数后，不应污染同一操作的 After 诊断快照。
    /// </summary>
    [Fact]
    public void Diagnostics_WhenObserverMutatesArrayValue_ShouldKeepAfterValueIndependent()
    {
        // Arrange
        DiagnosticsMessage after = null;
        using var observer = new SqlDiagnosticObserver(message =>
        {
            var value = (int[])message.Parameters.Items.Single().Value;
            if (message.Operation == SqlQueryDiagnosticListenerNames.BeforeExecute)
            {
                value[0] = 99;
                return;
            }

            if (message.Operation == SqlQueryDiagnosticListenerNames.AfterExecute)
                after = message;
        }, name => name == SqlQueryDiagnosticListenerNames.BeforeExecute ||
                 name == SqlQueryDiagnosticListenerNames.AfterExecute);
        var connection = new CaptureDbConnection();
        var executor = CreateExecutor(connection);

        // Act
        executor.PublishDiagnosticsForTest(new[] { 1, 2 });

        // Assert
        after.ShouldNotBeNull();
        ((int[])after.Parameters.Items.Single().Value).ShouldBe(new[] { 1, 2 });
    }

    /// <summary>
    /// 测试目的：异步事务作用域应提供事务标识，并在异步提交后释放其拥有的事务。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_BeginAsyncAndCommitAsync_ShouldCommitOwnedTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();

        // Act
        await using var scope = await scopeFactory.BeginAsync();
        await scope.CommitAsync();

        // Assert
        scope.TransactionId.ShouldNotBeNullOrWhiteSpace();
        connection.LastTransaction.ShouldNotBeNull();
        (connection.LastTransaction.CommitCount + connection.LastTransaction.AsyncCommitCount).ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：事务提交后重复提交应保持幂等，回滚应被拒绝，显式释放后完成操作应报告对象已释放。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenCommitted_ShouldBeIdempotentAndRejectOppositeCompletion()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();

        // Act
        scope.Commit();
        scope.Commit();

        // Assert
        Should.Throw<InvalidOperationException>(() => scope.Rollback());
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
        scope.Dispose();
        Should.Throw<ObjectDisposedException>(() => scope.Commit());
        Should.Throw<ObjectDisposedException>(() => scope.Rollback());
    }

    /// <summary>
    /// 测试目的：事务回滚后重复回滚应保持幂等，提交应被拒绝，资源只应释放一次。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenRolledBack_ShouldBeIdempotentAndRejectOppositeCompletion()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();

        // Act
        scope.Rollback();
        scope.Rollback();

        // Assert
        Should.Throw<InvalidOperationException>(() => scope.Commit());
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.RollbackCount.ShouldBe(1);
        connection.LastTransaction.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：异步事务完成路径应与同步路径具有一致的幂等和终态语义。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenCompletedAsync_ShouldBeIdempotentAndRejectInvalidCompletion()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scope = await provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();

        // Act
        await scope.CommitAsync();
        await scope.CommitAsync();

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.RollbackAsync());
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.DisposeCount.ShouldBe(1);
        await scope.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => scope.CommitAsync());
    }

    /// <summary>
    /// 测试目的：事务资源释放失败时仍应使租约失效并完成状态收口。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenTransactionDisposeFails_ShouldInvalidateLeaseAndFinalizeScope()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        var executor = scope.CreateExecutor();
        connection.LastTransaction.ThrowOnDispose = true;

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => scope.Commit());

        // Assert
        exception.Message.ShouldBe("transaction dispose failed");
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.DisposeCount.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "after" }));
        scope.Dispose();
    }

    /// <summary>
    /// 测试目的：异步事务开始和提交应优先调用 ADO.NET 原生异步成员。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenNativeAsyncMembersExist_ShouldUseThem()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();

        // Act
        await using var scope = await provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        await scope.CommitAsync();

        // Assert
        connection.AsyncBeginCount.ShouldBe(1);
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.CommitCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：原生异步回滚完成后不能再同步回退执行一次回滚。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenNativeAsyncRollbackExists_ShouldNotFallbackToSynchronousRollback()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();

        // Act
        await using var scope = await provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        await scope.RollbackAsync();

        // Assert
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试 - Scope提交失败时应尝试回滚，并保留原始提交异常。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenCommitFails_ShouldRollbackAndPreserveCommitException()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        connection.LastTransaction.ThrowOnCommit = true;

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => scope.Commit());

        // Assert
        exception.Message.ShouldBe("commit failed");
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - Scope提交和回滚同时失败时应聚合两个异常。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenCommitAndRollbackFail_ShouldAggregateExceptions()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        connection.LastTransaction.ThrowOnCommit = true;
        connection.LastTransaction.ThrowOnRollback = true;

        // Act
        var exception = Should.Throw<AggregateException>(() => scope.Commit());

        // Assert
        exception.Flatten().InnerExceptions.Select(item => item.Message)
            .ShouldBe(new[] { "commit failed", "rollback failed" }, ignoreOrder: true);
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - Scope异步提交失败时应通过原生异步回滚并保留提交异常。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenCommitAsyncFails_ShouldRollbackAsyncAndPreserveCommitException()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        await using var scope = await provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        connection.LastTransaction.ThrowOnCommit = true;

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.CommitAsync());

        // Assert
        exception.Message.ShouldBe("commit failed");
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 异步事务开始失败且 Owner Query 清理失败时，应聚合保留两个失败原因。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenBeginAsyncAndOwnerQueryCleanupFail_ShouldAggregateFailures()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnAsyncBegin = true, ThrowOnDispose = true };
        var services = CreateServices();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(new CaptureConnectionResolver(connection));
        services.AddSqlServerSqlQuery<ISqlQuery, FaultingTransactionSqlServerQuery>(options =>
            options.ConnectionString("Server=test;Database=test;"));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options =>
            options.ConnectionString("Server=test;Database=test;"));
        using var provider = services.BuildServiceProvider();

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync());

        // Assert
        exception.Flatten().InnerExceptions.Select(item => item.Message)
            .ShouldBe(new[] { "async begin failed", "connection dispose failed" }, ignoreOrder: true);
        connection.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 预先取消异步事务开始时不应打开连接或创建事务。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenBeginAsyncIsCancelled_ShouldNotOpenConnectionOrCreateTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync(cancellationToken: cancellationTokenSource.Token));

        // Assert
        connection.AsyncBeginCount.ShouldBe(0);
        connection.LastTransaction.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：事务作用域应公开实际连接、事务、数据库类型、隔离级别和完成状态。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_BeginWithIsolationLevel_ShouldExposeOwnedResourcesAndState()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();

        // Act
        using var scope = scopeFactory.Begin(null, IsolationLevel.Serializable);

        // Assert
        scope.DatabaseType.ShouldBe(DatabaseType.SqlServer);
        scope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
        scope.Connection.ShouldBeSameAs(connection);
        scope.Transaction.ShouldBeSameAs(connection.LastTransaction);
        scope.IsCompleted.ShouldBeFalse();

        // Act
        scope.Commit();

        // Assert
        scope.IsCompleted.ShouldBeTrue();
        connection.LastTransaction.CommitCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：事务开始后切换环境数据库时，子查询和执行器仍应使用开始时解析的主库上下文、连接和事务。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenAmbientContextChanges_ShouldKeepCapturedPrimaryContext()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var metadataOptions = CreateRoutingMetadataOptions();
        metadataOptions.DataSources.DataSources["default"].ConnectionString = null;
        metadataOptions.DataSources.DataSources["reporting"].ConnectionString = null;
        metadataOptions.DataSources.DataSources["reporting"].PrimaryReadStrategy = PrimaryReadStrategy.PrimaryDataSource;
        metadataOptions.DataSources.DataSources["reporting"].PrimaryDataSourceKey = "default";
        var services = CreateServices(metadataOptions);
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        var databaseScopeManager = provider.GetRequiredService<IDatabaseScopeManager>();
        var transactionScopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();
        var accessor = provider.GetRequiredService<IDatabaseContextAccessor>();

        // Act
        using (databaseScopeManager.Use("reporting"))
        using (var transactionScope = transactionScopeFactory.Begin())
        {
            accessor.Current = new DatabaseContext
            {
                DbKey = "reporting",
                DataSource = metadataOptions.DataSources.DataSources["reporting"],
                MappingProfile = "changed-after-begin"
            };
            var query = transactionScope.CreateQuery();
            var executor = transactionScope.CreateExecutor();

            // Assert
            transactionScope.DbKey.ShouldBe("default");
            transactionScope.DatabaseType.ShouldBe(DatabaseType.SqlServer);
            query.AppendSelect("Count(*)").AppendFrom("[Users]").ExecuteScalar<int>().ShouldBe(1);
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "scope" }).ShouldBe(1);
            transactionScope.Connection.ShouldBeSameAs(connection);
            transactionScope.Transaction.ShouldBeSameAs(connection.LastTransaction);
        }

        connection.LastTransaction.RollbackCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：事务 Scope 的子 Query 释放不应关闭 Scope 连接，且 Scope 完成后子 Query 必须失效。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenChildQueryIsDisposed_ShouldKeepOwnerConnectionAndInvalidateAfterCompletion()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        var query = scope.CreateQuery();

        // Act
        query.Dispose();
        scope.Rollback();

        // Assert
        connection.State.ShouldBe(ConnectionState.Open);
        Should.Throw<InvalidOperationException>(() => query.ExecuteScalar<int>());
    }

    /// <summary>
    /// 测试 - Scope处于活动状态时已释放子对象不应重新创建独立资源或脱离事务执行。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenChildIsDisposedWhileActive_ShouldRejectFurtherResourceAccessAndExecution()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var services = CreateServices();
        services.AddSqlServerSqlQuery<ISqlQuery, SqlServerSqlQuery>(options => options.Connection(connection));
        services.AddSqlServerSqlExecutor<ISqlExecutor, SqlServerSqlExecutor>(options => options.Connection(connection));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        var query = scope.CreateQuery();
        var executor = scope.CreateExecutor();

        // Act
        query.Dispose();
        executor.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => query.ExecuteScalar<int>());
        Should.Throw<ObjectDisposedException>(() => executor.ExecuteSql("Update [Users] Set [Name]=@name",
            new { name = "after-dispose" }));
        connection.State.ShouldBe(ConnectionState.Open);
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试 - 异步 Count 应使用增强参数而不是旧字典参数。
    /// </summary>
    [Fact]
    public async Task GetCountAsync_ShouldUseMetadataParameters()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var query = CreateQuery(connection);
        query.From<MappedSample>("a").Where<MappedSample>(t => t.Name, "abc");

        // Act
        var result = await query.InvokeCountAsync();

        // Assert
        result.ShouldBe(1);
        connection.LastCreatedParameters.Count.ShouldBe(1);
        var parameter = connection.LastCreatedParameters.Single();
        parameter.DbType.ShouldBe(DbType.String);
        parameter.Size.ShouldBe(20);
    }

    /// <summary>
    /// 测试目的：Trace 未启用时，同步查询只应渲染一次执行 SQL，不应生成调试 SQL。
    /// </summary>
    [Fact]
    public void ExecuteScalar_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateCountingQuery(connection, false);
        query.AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var result = query.ExecuteScalar<int>();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：Trace 未启用时，异步查询只应渲染一次执行 SQL，不应生成调试 SQL。
    /// </summary>
    [Fact]
    public async Task ExecuteScalarAsync_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateCountingQuery(connection, false);
        query.AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var result = await query.ExecuteScalarAsync<int>();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：Trace 未启用时，同步流式查询只应渲染一次执行 SQL，不应生成调试 SQL。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql()
    {
        // Arrange
        var connection = new CaptureDbConnection { ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "stream" }) };
        using var query = CreateCountingQuery(connection, false);
        query.Select("Id,Name").From("[Users]");

        // Act
        var result = query.StreamQuery<MappedSample>().ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：Trace 未启用时，异步流式查询只应渲染一次执行 SQL，不应生成调试 SQL。
    /// </summary>
    [Fact]
    public async Task StreamQueryAsync_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql()
    {
        // Arrange
        var connection = new CaptureDbConnection { ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "async-stream" }) };
        using var query = CreateCountingQuery(connection, false);
        query.Select("Id,Name").From("[Users]");

        // Act
        var result = new List<MappedSample>();
        await foreach (var item in query.StreamQueryAsync<MappedSample>())
            result.Add(item);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：Trace 未启用时，同步和异步 Count 查询都不应生成调试 SQL。
    /// </summary>
    [Fact]
    public async Task GetCount_WhenTraceIsDisabled_ShouldRenderSqlOnceWithoutDebugSql()
    {
        // Arrange
        var syncConnection = new CaptureDbConnection();
        using var syncQuery = CreateCountingQuery(syncConnection, false);
        syncQuery.From("[Users]");

        // Act
        var syncResult = syncQuery.InvokeCount();

        // Assert
        Assert.Equal(1, syncResult);
        Assert.Equal(1, syncQuery.Counters.ToSqlCallCount);
        Assert.Equal(0, syncQuery.Counters.ToDebugSqlCallCount);

        // Arrange
        var asyncConnection = new CaptureDbConnection();
        using var asyncQuery = CreateCountingQuery(asyncConnection, false);
        asyncQuery.From("[Users]");

        // Act
        var asyncResult = await asyncQuery.InvokeCountAsync();

        // Assert
        Assert.Equal(1, asyncResult);
        Assert.Equal(1, asyncQuery.Counters.ToSqlCallCount);
        Assert.Equal(0, asyncQuery.Counters.ToDebugSqlCallCount);
    }

    /// <summary>
    /// 测试目的：Trace 启用时应复用执行 SQL 生成调试 SQL，而不是再次调用 ToSql。
    /// </summary>
    [Fact]
    public void ExecuteScalar_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateCountingQuery(connection, true);
        query.AppendSelect("Count(*)").AppendFrom("[Users]").AppendWhere("[Name]=@name").AddParam("name", "trace");

        // Act
        var result = query.ExecuteScalar<int>();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(1, query.Counters.ToDebugSqlCallCount);
        Assert.Equal(query.TraceSql, query.Counters.LastDebugSqlInput);
        Assert.Equal("Select Count(*) \r\nFrom [Users] \r\nWhere [Name]='trace'", query.TraceDebugSql);
    }

    /// <summary>
    /// 测试目的：显式禁用调试日志时，即使 Trace 启用也不应生成调试 SQL。
    /// </summary>
    [Fact]
    public void ExecuteScalar_WhenDebugLogIsDisabled_ShouldNotRenderDebugSql()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateCountingQuery(connection, true);
        query.AppendSelect("Count(*)").AppendFrom("[Users]").DisableDebugLog();

        // Act
        var result = query.ExecuteScalar<int>();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：Trace 启用时，异步列表查询应复用执行 SQL 生成调试 SQL。
    /// </summary>
    [Fact]
    public async Task ExecuteQueryAsync_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "async-trace" })
        };
        using var query = CreateCountingQuery(connection, true);
        query.Select("Id,Name").From("[Users]").Where("[Name]", "async-trace");

        // Act
        var result = await query.ExecuteQueryAsync<MappedSample>();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(1, query.Counters.ToDebugSqlCallCount);
        Assert.Equal(query.TraceSql, query.Counters.LastDebugSqlInput);
        Assert.Equal("Select [Id],[Name] \r\nFrom [Users] \r\nWhere [Name]='async-trace'", query.TraceDebugSql);
    }

    /// <summary>
    /// 测试目的：Trace 启用时，同步和异步 Count 均应复用各自的执行 SQL 生成调试 SQL。
    /// </summary>
    [Fact]
    public async Task GetCount_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql()
    {
        // Arrange
        var syncConnection = new CaptureDbConnection();
        using var syncQuery = CreateCountingQuery(syncConnection, true);
        syncQuery.From("[Users]").Where("[Enabled]", true);

        // Act
        var syncResult = syncQuery.InvokeCount();

        // Assert
        Assert.Equal(1, syncResult);
        Assert.Equal(1, syncQuery.Counters.ToSqlCallCount);
        Assert.Equal(1, syncQuery.Counters.ToDebugSqlCallCount);
        Assert.Equal(syncQuery.TraceSql, syncQuery.Counters.LastDebugSqlInput);

        // Arrange
        var asyncConnection = new CaptureDbConnection();
        using var asyncQuery = CreateCountingQuery(asyncConnection, true);
        asyncQuery.From("[Users]").Where("[Enabled]", true);

        // Act
        var asyncResult = await asyncQuery.InvokeCountAsync();

        // Assert
        Assert.Equal(1, asyncResult);
        Assert.Equal(1, asyncQuery.Counters.ToSqlCallCount);
        Assert.Equal(1, asyncQuery.Counters.ToDebugSqlCallCount);
        Assert.Equal(asyncQuery.TraceSql, asyncQuery.Counters.LastDebugSqlInput);
    }

    /// <summary>
    /// 测试目的：Trace 启用时，同步和异步流式查询应复用执行 SQL 生成调试 SQL。
    /// </summary>
    [Fact]
    public async Task StreamQuery_WhenTraceIsEnabled_ShouldReuseExecutedSqlForDebugSql()
    {
        // Arrange
        var syncConnection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "sync-stream" })
        };
        using var syncQuery = CreateCountingQuery(syncConnection, true);
        syncQuery.Select("Id,Name").From("[Users]");

        // Act
        var syncResult = syncQuery.StreamQuery<MappedSample>().ToList();

        // Assert
        Assert.Single(syncResult);
        Assert.Equal(1, syncQuery.Counters.ToSqlCallCount);
        Assert.Equal(1, syncQuery.Counters.ToDebugSqlCallCount);
        Assert.Equal(syncQuery.TraceSql, syncQuery.Counters.LastDebugSqlInput);

        // Arrange
        var asyncConnection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "async-stream" })
        };
        using var asyncQuery = CreateCountingQuery(asyncConnection, true);
        asyncQuery.Select("Id,Name").From("[Users]");

        // Act
        var asyncResult = new List<MappedSample>();
        await foreach (var item in asyncQuery.StreamQueryAsync<MappedSample>())
            asyncResult.Add(item);

        // Assert
        Assert.Single(asyncResult);
        Assert.Equal(1, asyncQuery.Counters.ToSqlCallCount);
        Assert.Equal(1, asyncQuery.Counters.ToDebugSqlCallCount);
        Assert.Equal(asyncQuery.TraceSql, asyncQuery.Counters.LastDebugSqlInput);
    }

    /// <summary>
    /// 测试目的：Trace 启用时，同步和异步分页应分别复用 Count 与列表查询的执行 SQL。
    /// </summary>
    [Fact]
    public async Task PagerQuery_WhenTraceIsEnabled_ShouldReuseExecutedSqlForCountAndData()
    {
        // Arrange
        var syncConnection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "sync-page" })
        };
        using var syncQuery = CreateCountingQuery(syncConnection, true);
        syncQuery.Select("Id,Name").From("[Users]");

        // Act
        var syncResult = syncQuery.PagerQuery(() => syncQuery.ExecuteQuery<MappedSample>(), new Pager(1, 20, "Id"));

        // Assert
        Assert.Single(syncResult.Data);
        Assert.Equal(2, syncQuery.Counters.ToSqlCallCount);
        Assert.Equal(2, syncQuery.Counters.ToDebugSqlCallCount);
        Assert.Equal(syncQuery.TraceSql, syncQuery.Counters.LastDebugSqlInput);

        // Arrange
        var asyncConnection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "async-page" })
        };
        using var asyncQuery = CreateCountingQuery(asyncConnection, true);
        asyncQuery.Select("Id,Name").From("[Users]");

        // Act
        var asyncResult = await asyncQuery.PagerQueryAsync(
            token => asyncQuery.ExecuteQueryAsync<MappedSample>(cancellationToken: token), new Pager(1, 20, "Id"));

        // Assert
        Assert.Single(asyncResult.Data);
        Assert.Equal(2, asyncQuery.Counters.ToSqlCallCount);
        Assert.Equal(2, asyncQuery.Counters.ToDebugSqlCallCount);
        Assert.Equal(asyncQuery.TraceSql, asyncQuery.Counters.LastDebugSqlInput);
    }

    /// <summary>
    /// 测试目的：Trace 启用时，同步标量执行失败仍应保留单次渲染并发布错误诊断。
    /// </summary>
    [Fact]
    public void ExecuteScalar_WhenExecutionFails_ShouldRenderSqlOnceAndPublishError()
    {
        // Arrange
        DiagnosticsMessage errorMessage = null;
        using var observer = new SqlDiagnosticObserver(item => errorMessage = item,
            name => name == SqlQueryDiagnosticListenerNames.ErrorExecute);
        var connection = new CaptureDbConnection { ThrowOnScalarExecute = true };
        using var query = CreateCountingQuery(connection, true);
        query.AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.ExecuteScalar<int>());

        // Assert
        Assert.Equal("execute failed", exception.Message);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(1, query.Counters.ToDebugSqlCallCount);
        Assert.Equal(query.TraceSql, query.Counters.LastDebugSqlInput);
        Assert.Same(exception, errorMessage.Exception);
    }

    /// <summary>
    /// 测试目的：Trace 启用时，异步标量执行失败仍应保留单次渲染并发布错误诊断。
    /// </summary>
    [Fact]
    public async Task ExecuteScalarAsync_WhenExecutionFails_ShouldRenderSqlOnceAndPublishError()
    {
        // Arrange
        DiagnosticsMessage errorMessage = null;
        using var observer = new SqlDiagnosticObserver(item => errorMessage = item,
            name => name == SqlQueryDiagnosticListenerNames.ErrorExecute);
        var connection = new CaptureDbConnection { ThrowOnScalarExecute = true };
        using var query = CreateCountingQuery(connection, true);
        query.AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ExecuteScalarAsync<int>());

        // Assert
        Assert.Equal("execute failed", exception.Message);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(1, query.Counters.ToDebugSqlCallCount);
        Assert.Equal(query.TraceSql, query.Counters.LastDebugSqlInput);
        Assert.Same(exception, errorMessage.Exception);
    }

    /// <summary>
    /// 测试目的：Trace 启用时，异步流式命令执行失败仍应保留单次渲染并发布错误诊断。
    /// </summary>
    [Fact]
    public async Task StreamQueryAsync_WhenExecutionFails_ShouldRenderSqlOnceAndPublishError()
    {
        // Arrange
        DiagnosticsMessage errorMessage = null;
        using var observer = new SqlDiagnosticObserver(item => errorMessage = item,
            name => name == SqlQueryDiagnosticListenerNames.ErrorExecute);
        var connection = new CaptureDbConnection { ThrowOnExecute = true };
        using var query = CreateCountingQuery(connection, true);
        query.Select("Id,Name").From("[Users]");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in query.StreamQueryAsync<MappedSample>())
            {
            }
        });

        // Assert
        Assert.Equal("execute failed", exception.Message);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(1, query.Counters.ToDebugSqlCallCount);
        Assert.Equal(query.TraceSql, query.Counters.LastDebugSqlInput);
        Assert.Same(exception, errorMessage.Exception);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Equal(0, connection.ReaderDisposeCount);
    }

    /// <summary>
    /// 测试 - 存储过程标量执行应使用 StoredProcedure 命令类型并保留增强参数元数据。
    /// </summary>
    [Fact]
    public void ExecuteProcedureScalar_ShouldUseStoredProcedureCommandAndMetadataParameters()
    {
        // Arrange
        var connection = new CaptureDbConnection { ScalarResult = 7 };
        var query = CreateQuery(connection);
        query.From<MappedSample>("a").Where<MappedSample>(t => t.Name, "abc");

        // Act
        var result = query.ExecuteProcedureScalar<int>("usp_users_count");

        // Assert
        result.ShouldBe(7);
        connection.LastCommandText.ShouldBe("usp_users_count");
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        connection.LastCreatedParameters.Count.ShouldBe(1);
        connection.LastCreatedParameters[0].DbType.ShouldBe(DbType.String);
        connection.LastCreatedParameters[0].Size.ShouldBe(20);
    }

    /// <summary>
    /// 测试 - 存储过程单行异步执行应使用 StoredProcedure 命令并正确映射结果。
    /// </summary>
    [Fact]
    public async Task ExecuteProcedureSingleAsync_ShouldUseStoredProcedureCommandAndMapEntity()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 2, Name = "Alice" })
        };
        var query = CreateQuery(connection);

        // Act
        var result = await query.ExecuteProcedureSingleAsync<MappedSample>("usp_users_single");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(2);
        result.Name.ShouldBe("Alice");
        connection.LastCommandText.ShouldBe("usp_users_single");
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 异步列表查询应使用非缓冲CommandFlags并在连接释放前完成枚举。
    /// </summary>
    [Fact]
    public async Task ExecuteQueryAsync_WhenBufferedIsFalse_ShouldUseNonBufferedFlagsAndMaterializeRows()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");

        // Act
        var result = await query.ExecuteQueryAsync<MappedSample>(buffered: false);

        // Assert
        query.LastQueryCommandFlags.ShouldBe(CommandFlags.None);
        result.Count.ShouldBe(2);
        connection.ReaderCreateCount.ShouldBe(1);
        connection.ReaderDisposeCount.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Open);
    }

    /// <summary>
    /// 测试 - 异步存储过程列表查询应使用非缓冲CommandFlags并返回完整列表。
    /// </summary>
    [Fact]
    public async Task ExecuteProcedureQueryAsync_WhenBufferedIsFalse_ShouldUseNonBufferedFlagsAndMaterializeRows()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);

        // Act
        var result = await query.ExecuteProcedureQueryAsync<MappedSample>("usp_users_query", buffered: false);

        // Assert
        query.LastQueryCommandFlags.ShouldBe(CommandFlags.None);
        result.Count.ShouldBe(2);
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 存储过程集合执行应返回完整结果集并释放读取器。
    /// </summary>
    [Fact]
    public void ExecuteProcedureQuery_ShouldReturnRowsAndDisposeReader()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);

        // Act
        var result = query.ExecuteProcedureQuery<MappedSample>("usp_users_query");

        // Assert
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Alice");
        result[1].Name.ShouldBe("Bob");
        connection.LastCommandText.ShouldBe("usp_users_query");
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 流式查询完整枚举后应释放读取器并返回所有行。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenFullyEnumerated_ShouldReturnRowsAndDisposeReader()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");

        // Act
        var result = query.StreamQuery<MappedSample>().ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe(1);
        result[1].Name.ShouldBe("Bob");
        connection.ReaderCreateCount.ShouldBe(1);
        connection.ReaderDisposeCount.ShouldBe(1);
        connection.LastCommandType.ShouldBe(CommandType.Text);
    }

    /// <summary>
    /// 测试 - 流式查询提前终止时也应释放读取器。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenEnumerationStopsEarly_ShouldDisposeReader()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");

        // Act
        using (var enumerator = query.StreamQuery<MappedSample>().GetEnumerator())
        {
            enumerator.MoveNext().ShouldBeTrue();
            enumerator.Current.Name.ShouldBe("Alice");
        }

        // Assert
        connection.ReaderCreateCount.ShouldBe(1);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 流式查询提前终止时应完成一次成功诊断。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenEnumerationStopsEarly_ShouldPublishAfterDiagnosticsOnce()
    {
        // Arrange
        var messages = new List<DiagnosticsMessage>();
        using var observer = new SqlDiagnosticObserver(messages.Add,
            name => name == SqlQueryDiagnosticListenerNames.AfterExecute);
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");

        // Act
        using (var enumerator = query.StreamQuery<MappedSample>().GetEnumerator())
            enumerator.MoveNext().ShouldBeTrue();

        // Assert
        messages.Count.ShouldBe(1);
        messages[0].Operation.ShouldBe(SqlQueryDiagnosticListenerNames.AfterExecute);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 异步流式查询提前终止时也应释放读取器。
    /// </summary>
    [Fact]
    public async Task StreamQueryAsync_WhenEnumerationStopsEarly_ShouldDisposeReader()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");

        // Act
        await foreach (var item in query.StreamQueryAsync<MappedSample>())
        {
            item.Name.ShouldBe("Alice");
            break;
        }

        // Assert
        connection.ReaderCreateCount.ShouldBe(1);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - StreamAsync 应保留异步逐行读取的资源释放语义。
    /// </summary>
    [Fact]
    public async Task StreamAsync_WhenEnumerationStopsEarly_ShouldDisposeReader()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 1, Name = "Alice" },
                new MappedSample { Id = 2, Name = "Bob" })
        };
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");

        // Act
        await foreach (var item in query.StreamAsync<MappedSample>())
        {
            item.Name.ShouldBe("Alice");
            break;
        }

        // Assert
        connection.ReaderCreateCount.ShouldBe(1);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：异步流的行映射失败应发布错误诊断并释放读取器。
    /// </summary>
    [Fact]
    public async Task StreamAsync_WhenRowMappingFails_ShouldPublishErrorAndDisposeReader()
    {
        // Arrange
        DiagnosticsMessage errorMessage = null;
        using var observer = new SqlDiagnosticObserver(item => errorMessage = item,
            name => name == SqlQueryDiagnosticListenerNames.ErrorExecute);
        var table = new DataTable();
        table.Columns.Add(nameof(MappedSample.Id), typeof(string));
        table.Columns.Add(nameof(MappedSample.Name), typeof(string));
        table.Rows.Add("invalid-id", "Alice");
        var connection = new CaptureDbConnection { ResultSet = table };
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");

        // Act
        Exception exception = null;
        try
        {
            await foreach (var _ in query.StreamAsync<MappedSample>())
            {
            }
        }
        catch (Exception e)
        {
            exception = e;
        }

        // Assert
        exception.ShouldNotBeNull();
        errorMessage.ShouldNotBeNull();
        errorMessage.Exception.ShouldBeSameAs(exception);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - 主库短事务策略下应拒绝流式查询。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenPrimaryReadStrategyIsTransaction_ShouldThrow()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var query = CreateQuery(connection);
        query.Select("Id,Name").From("Users");
        ConfigurePrimaryReadTransaction(query);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => query.StreamQuery<MappedSample>());

        // Assert
        exception.Message.ShouldContain("PrimaryReadStrategy.Transaction");
        connection.ReaderCreateCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试 - 类型转换器解析器应解析 SqlServer 对应的 Provider 转换器。
    /// </summary>
    [Fact]
    public void TypeConverterResolver_ShouldResolveProviderConverter()
    {
        // Arrange
        var services = CreateServices();
        services.AddSqlServerSqlQuery("Server=default;Database=test;");
        using var provider = services.BuildServiceProvider();

        // Act
        var resolver = provider.GetRequiredService<ITypeConverterResolver>();
        var converter = resolver.Resolve(DatabaseType.SqlServer);

        // Assert
        converter.ShouldBeOfType<Bing.Data.Metadata.SqlServerTypeConverter>();
    }

    /// <summary>
    /// 创建服务集合
    /// </summary>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <returns>服务集合</returns>
    private static IServiceCollection CreateServices(SqlMetadataOptions metadataOptions = null)
    {
        var services = new ServiceCollection();
        if (metadataOptions != null)
            services.AddSingleton(metadataOptions);
        services.AddSqlCore();
        return services;
    }

    /// <summary>
    /// 可控事务作用域租约。
    /// </summary>
    private sealed class TestTransactionScopeLease : ISqlTransactionScopeLease
    {
        public TestTransactionScopeLease(string transactionId) => TransactionId = transactionId;

        public string TransactionId { get; }

        public bool IsActive { get; set; } = true;

        public void EnsureActive()
        {
            if (IsActive == false)
                throw new InvalidOperationException("事务作用域租约已失效。");
        }
    }

    /// <summary>
    /// 创建路由元数据配置
    /// </summary>
    /// <returns>Sql 元数据配置</returns>
    private static SqlMetadataOptions CreateRoutingMetadataOptions()
    {
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["default"] = new SqlDataSourceDescriptor
        {
            Key = "default",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=default;Database=test;"
        };
        options.DataSources.DataSources["reporting"] = new SqlDataSourceDescriptor
        {
            Key = "reporting",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=reporting;Database=test;"
        };
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MappedSample),
            DbKey = "default",
            TableName = "Users",
            Columns =
            {
                [nameof(MappedSample.Name)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(MappedSample.Name),
                    ColumnName = "Name"
                }
            }
        });
        options.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MappedSample),
            DbKey = "reporting",
            TableName = "Users_Reporting",
            Columns =
            {
                [nameof(MappedSample.Name)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(MappedSample.Name),
                    ColumnName = "reporting_name"
                }
            }
        });
        return options;
    }

    /// <summary>
    /// 创建样例结果集
    /// </summary>
    /// <param name="items">结果项</param>
    /// <returns>数据表</returns>
    private static DataTable CreateMappedSampleTable(params MappedSample[] items)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(MappedSample.Id), typeof(int));
        table.Columns.Add(nameof(MappedSample.Name), typeof(string));
        foreach (var item in items)
            table.Rows.Add(item.Id, item.Name);
        return table;
    }

    /// <summary>
    /// 创建查询对象
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>查询对象</returns>
    private static InspectableSqlServerQuery CreateQuery(CaptureDbConnection connection)
    {
        var services = CreateServices();
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.Connection(connection));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<InspectableSqlServerQuery>();
    }

    /// <summary>
    /// 创建用于验证 SQL 渲染次数的查询对象。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="traceEnabled">是否启用 Trace 日志。</param>
    /// <returns>可计数 SQL Server 查询对象。</returns>
    private static CountingSqlServerQuery CreateCountingQuery(CaptureDbConnection connection, bool traceEnabled)
    {
        var services = CreateServices();
        services.AddSingleton<ILoggerFactory>(new TraceLoggerFactory(traceEnabled));
        services.AddSqlServerSqlQuery<CountingSqlServerQuery, CountingSqlServerQuery>(options =>
            options.Connection(connection));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<CountingSqlServerQuery>();
    }

    /// <summary>
    /// 创建拥有连接的查询对象
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>查询对象</returns>
    private static InspectableSqlServerQuery CreateOwnedQuery(CaptureDbConnection connection)
    {
        var services = CreateServices();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(new CaptureConnectionResolver(connection));
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=test;Database=test;"));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<InspectableSqlServerQuery>();
    }

    /// <summary>
    /// 创建执行器
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>执行器</returns>
    private static InspectableSqlServerExecutor CreateExecutor(CaptureDbConnection connection)
    {
        var services = CreateServices();
        services.AddSqlServerSqlExecutor<InspectableSqlServerExecutor, InspectableSqlServerExecutor>(options =>
            options.Connection(connection));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<InspectableSqlServerExecutor>();
    }

    /// <summary>
    /// 创建拥有连接的执行器
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>执行器</returns>
    private static InspectableSqlServerExecutor CreateOwnedExecutor(CaptureDbConnection connection)
    {
        var services = CreateServices();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(new CaptureConnectionResolver(connection));
        services.AddSqlServerSqlExecutor<InspectableSqlServerExecutor, InspectableSqlServerExecutor>(options =>
            options.ConnectionString("Server=test;Database=test;"));
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<InspectableSqlServerExecutor>();
    }

    /// <summary>
    /// 配置主库短事务策略
    /// </summary>
    /// <param name="executor">SQL 执行器</param>
    private static void ConfigurePrimaryReadTransaction(InspectableSqlServerExecutor executor)
    {
        executor.Config(options => options.SetDatabaseContext(new DatabaseContext
        {
            ReadPreference = SqlReadPreference.Primary,
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "primary",
                DatabaseType = DatabaseType.SqlServer,
                PrimaryReadStrategy = PrimaryReadStrategy.Transaction
            }
        }));
    }

    /// <summary>
    /// 配置主库短事务策略
    /// </summary>
    /// <param name="query">SQL 查询对象</param>
    private static void ConfigurePrimaryReadTransaction(ISqlQuery query)
    {
        query.Config(options => options.SetDatabaseContext(new DatabaseContext
        {
            ReadPreference = SqlReadPreference.Primary,
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "primary",
                DatabaseType = DatabaseType.SqlServer,
                PrimaryReadStrategy = PrimaryReadStrategy.Transaction
            }
        }));
    }

    /// <summary>
    /// 返回指定连接的测试连接工厂解析器。
    /// </summary>
    private sealed class CaptureConnectionResolver : ISqlDbConnectionFactoryResolver
    {
        private readonly IDbConnection _connection;

        public CaptureConnectionResolver(IDbConnection connection) => _connection = connection;

        public string LastProviderKey { get; private set; }

        public IDbConnection Create(string providerKey, string connectionString)
        {
            LastProviderKey = providerKey;
            return _connection;
        }
    }

    /// <summary>
    /// 测试查询对象
    /// </summary>
    private sealed class InspectableSqlServerQuery : SqlServerSqlQueryBase
    {
        public InspectableSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<InspectableSqlServerQuery> options)
            : base(serviceProvider, options)
        {
        }

        public SqlOptions CurrentOptions => Options;

        public string CurrentSql => GetSql();

        public Task<int> InvokeCountAsync() => GetCountAsync();

        public string InvokeResolveConnectionString() => ResolveConnectionString();

        public CommandFlags LastQueryCommandFlags { get; private set; }

        protected override CommandDefinition CreateQueryCommandDefinition(string sql, object parameters,
            IDbTransaction transaction, int? timeout, bool buffered, CancellationToken cancellationToken = default,
            CommandType? commandType = null)
        {
            var command = base.CreateQueryCommandDefinition(sql, parameters, transaction, timeout, buffered,
                cancellationToken, commandType);
            LastQueryCommandFlags = command.Flags;
            return command;
        }
    }

    /// <summary>
    /// 用于验证 SQL 渲染次数的 SQL Server 查询对象。
    /// </summary>
    private sealed class CountingSqlServerQuery : SqlServerSqlQueryBase
    {
        /// <summary>
        /// 初始化一个<see cref="CountingSqlServerQuery"/>类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public CountingSqlServerQuery(IServiceProvider serviceProvider, SqlOptions<CountingSqlServerQuery> options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// SQL 渲染计数器。
        /// </summary>
        public SqlRenderCounters Counters { get; } = new();

        /// <summary>
        /// 捕获的原始 SQL。
        /// </summary>
        public string TraceSql { get; private set; }

        /// <summary>
        /// 捕获的调试 SQL。
        /// </summary>
        public string TraceDebugSql { get; private set; }

        /// <summary>
        /// 调用受保护的同步 Count 查询。
        /// </summary>
        /// <returns>查询结果。</returns>
        public int InvokeCount() => GetCount();

        /// <summary>
        /// 调用受保护的异步 Count 查询。
        /// </summary>
        /// <returns>查询结果。</returns>
        public Task<int> InvokeCountAsync() => GetCountAsync();

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => new CountingSqlServerBuilder(Counters);

        /// <inheritdoc />
        protected override void WriteTraceLog(string sql, IReadOnlyDictionary<string, object> parameters, string debugSql)
        {
            TraceSql = sql;
            TraceDebugSql = debugSql;
        }
    }

    /// <summary>
    /// SQL 渲染调用计数器。
    /// </summary>
    private sealed class SqlRenderCounters
    {
        /// <summary>
        /// ToSql 调用次数。
        /// </summary>
        public int ToSqlCallCount { get; set; }

        /// <summary>
        /// ToDebugSql 调用次数。
        /// </summary>
        public int ToDebugSqlCallCount { get; set; }

        /// <summary>
        /// 最后一次调试渲染的 SQL 输入。
        /// </summary>
        public string LastDebugSqlInput { get; set; }
    }

    /// <summary>
    /// 可计数 SQL Server Builder。
    /// </summary>
    private sealed class CountingSqlServerBuilder : SqlServerBuilder
    {
        private readonly SqlRenderCounters _counters;

        /// <summary>
        /// 初始化一个<see cref="CountingSqlServerBuilder"/>类型的实例。
        /// </summary>
        /// <param name="counters">SQL 渲染计数器。</param>
        public CountingSqlServerBuilder(SqlRenderCounters counters) => _counters = counters;

        /// <inheritdoc />
        public override string ToSql()
        {
            _counters.ToSqlCallCount++;
            return base.ToSql();
        }

        /// <inheritdoc />
        public override string ToDebugSql(string sql)
        {
            _counters.ToDebugSqlCallCount++;
            _counters.LastDebugSqlInput = sql;
            return base.ToDebugSql(sql);
        }

        /// <inheritdoc />
        public override ISqlBuilder Clone()
        {
            var builder = new CountingSqlServerBuilder(_counters);
            builder.Clone(this);
            return builder;
        }

        /// <inheritdoc />
        public override ISqlBuilder New() => new CountingSqlServerBuilder(_counters);
    }

    /// <summary>
    /// 固定日志级别的测试日志工厂。
    /// </summary>
    private sealed class TraceLoggerFactory : ILoggerFactory
    {
        private readonly bool _traceEnabled;

        /// <summary>
        /// 初始化一个<see cref="TraceLoggerFactory"/>类型的实例。
        /// </summary>
        /// <param name="traceEnabled">是否启用 Trace 日志。</param>
        public TraceLoggerFactory(bool traceEnabled) => _traceEnabled = traceEnabled;

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new TraceLogger(_traceEnabled);

        /// <inheritdoc />
        public void AddProvider(ILoggerProvider provider) { }

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// 固定日志级别的测试日志器。
    /// </summary>
    private sealed class TraceLogger : ILogger
    {
        private readonly bool _traceEnabled;

        /// <summary>
        /// 初始化一个<see cref="TraceLogger"/>类型的实例。
        /// </summary>
        /// <param name="traceEnabled">是否启用 Trace 日志。</param>
        public TraceLogger(bool traceEnabled) => _traceEnabled = traceEnabled;

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => EmptyScope.Instance;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => _traceEnabled && logLevel == LogLevel.Trace;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter) { }
    }

    /// <summary>
    /// 空释放作用域。
    /// </summary>
    private sealed class EmptyScope : IDisposable
    {
        /// <summary>
        /// 空释放作用域实例。
        /// </summary>
        public static EmptyScope Instance { get; } = new();

        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>
    /// 事务开始失败测试查询对象。
    /// </summary>
    private sealed class FaultingTransactionSqlServerQuery : SqlServerSqlQueryBase
    {
        /// <summary>
        /// 初始化一个<see cref="FaultingTransactionSqlServerQuery"/>类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public FaultingTransactionSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<FaultingTransactionSqlServerQuery> options)
            : base(serviceProvider, options)
        {
        }
    }

    /// <summary>
    /// 计数查询接口
    /// </summary>
    private interface ICountedSqlServerQuery : ISqlQuery
    {
    }

    /// <summary>
    /// 计数查询对象
    /// </summary>
    private sealed class CountedSqlServerQuery : SqlServerSqlQueryBase, ICountedSqlServerQuery
    {
        public static int CreatedCount { get; set; }

        public CountedSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<CountedSqlServerQuery> options)
            : base(serviceProvider, options)
        {
            CreatedCount++;
        }
    }

    /// <summary>
    /// 首次创建正常事务所有者、后续创建缺少内部资源绑定器子对象的测试查询工厂。
    /// </summary>
    private sealed class BindingFailureSqlQueryFactory : ISqlQueryFactory
    {
        private readonly ISqlQueryFactory _innerFactory;
        private readonly ISqlQuery _invalidChild;
        private bool _ownerCreated;

        /// <summary>
        /// 初始化一个<see cref="BindingFailureSqlQueryFactory"/>类型的实例。
        /// </summary>
        /// <param name="innerFactory">实际查询工厂。</param>
        /// <param name="invalidChild">不实现内部绑定器的子查询。</param>
        public BindingFailureSqlQueryFactory(ISqlQueryFactory innerFactory, ISqlQuery invalidChild)
        {
            _innerFactory = innerFactory;
            _invalidChild = invalidChild;
        }

        /// <inheritdoc />
        public TQuery Create<TQuery>() where TQuery : class, ISqlQuery => Create<TQuery>(null);

        /// <inheritdoc />
        public TQuery Create<TQuery>(string dbKey) where TQuery : class, ISqlQuery
        {
            if (_ownerCreated == false)
            {
                _ownerCreated = true;
                return _innerFactory.Create<TQuery>(dbKey);
            }
            return _invalidChild as TQuery;
        }
    }

    /// <summary>
    /// 记录 Dispose 调用的查询代理。
    /// </summary>
    private class DisposeTrackingSqlQueryProxy : DispatchProxy
    {
        /// <summary>
        /// Dispose 调用次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (targetMethod.Name == nameof(IDisposable.Dispose))
                DisposeCount++;
            return targetMethod.ReturnType == typeof(void)
                ? null
                : targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }

    /// <summary>
    /// 测试执行器
    /// </summary>
    private sealed class InspectableSqlServerExecutor : SqlServerSqlExecutorBase
    {
        public InspectableSqlServerExecutor(IServiceProvider serviceProvider,
            SqlOptions<InspectableSqlServerExecutor> options)
            : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 发布受控的 Before 和 After 诊断事件，用于验证诊断快照隔离。
        /// </summary>
        /// <param name="value">诊断参数值。</param>
        public void PublishDiagnosticsForTest(int[] value)
        {
            var message = ExecuteBefore("Select @payload", null, GetExecutionConnection(), new[]
            {
                new SqlParameterDiagnosticInfo { Name = "payload", Value = value, OriginalValue = value }
            });
            ExecuteAfter(message);
        }
    }

    /// <summary>
    /// 字符串映射测试样例
    /// </summary>
    private sealed class MappedSample
    {
        /// <summary>
        /// 标识
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [StringLength(20)]
        [Column(TypeName = "nvarchar(20)")]
        public string Name { get; set; }

        /// <summary>
        /// 二进制载荷。
        /// </summary>
        public byte[] Payload { get; set; }
    }

    /// <summary>
    /// 捕获参数的数据库连接
    /// </summary>
    private sealed class CaptureDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Open;

        public List<CaptureDbParameter> LastCreatedParameters { get; private set; } = new();

        public string LastCommandText { get; private set; }

        public CommandType LastCommandType { get; private set; } = CommandType.Text;

        public object ScalarResult { get; set; } = 1;

        public int NonQueryResult { get; set; } = 1;

        public DataTable ResultSet { get; set; } = new();

        public int ReaderCreateCount { get; private set; }

        public int ReaderDisposeCount { get; private set; }

        public bool ThrowOnExecute { get; set; }

        /// <summary>
        /// 是否在标量执行时抛出异常。
        /// </summary>
        public bool ThrowOnScalarExecute { get; set; }

        /// <summary>
        /// 是否在原生异步开始事务时抛出异常。
        /// </summary>
        public bool ThrowOnAsyncBegin { get; set; }

        /// <summary>
        /// 是否在连接释放时抛出异常。
        /// </summary>
        public bool ThrowOnDispose { get; set; }

        public CaptureDbTransaction LastTransaction { get; private set; }

        public int AsyncBeginCount { get; private set; }

        /// <summary>
        /// 连接释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        public override string ConnectionString { get; set; }

        public override string Database => "test";

        public override string DataSource => "test";

        public override string ServerVersion => "1.0";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            LastTransaction = new CaptureDbTransaction(this, isolationLevel);
            return LastTransaction;
        }

        protected override DbCommand CreateDbCommand() => new CaptureDbCommand(this);

        protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AsyncBeginCount++;
            if (ThrowOnAsyncBegin)
                throw new InvalidOperationException("async begin failed");
            LastTransaction = new CaptureDbTransaction(this, isolationLevel);
            return ValueTask.FromResult<DbTransaction>(LastTransaction);
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing == false)
                return;
            DisposeCount++;
            if (ThrowOnDispose)
                throw new InvalidOperationException("connection dispose failed");
            base.Dispose(disposing);
        }

        public void SetParameters(IEnumerable<CaptureDbParameter> parameters) =>
            LastCreatedParameters = parameters.ToList();

        public void SetCommand(string commandText, CommandType commandType, IEnumerable<CaptureDbParameter> parameters)
        {
            LastCommandText = commandText;
            LastCommandType = commandType;
            SetParameters(parameters);
        }

        public DbDataReader CreateReader()
        {
            ReaderCreateCount++;
            var table = ResultSet ?? new DataTable();
            return new CaptureDbDataReader(table.CreateDataReader(), this);
        }

        public void OnReaderDisposed() => ReaderDisposeCount++;
    }

    /// <summary>
    /// 捕获参数的数据库命令
    /// </summary>
    private sealed class CaptureDbCommand : DbCommand
    {
        private readonly CaptureDbConnection _connection;
        private readonly CaptureDbParameterCollection _parameters = new();

        public CaptureDbCommand(CaptureDbConnection connection) => _connection = connection;

        public override string CommandText { get; set; }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection DbConnection
        {
            get => _connection;
            set { }
        }

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction DbTransaction { get; set; }

        public override void Cancel() { }

        public override int ExecuteNonQuery()
        {
            if (_connection.ThrowOnExecute)
                throw new InvalidOperationException("execute failed");
            _connection.SetCommand(CommandText, CommandType, _parameters.Items);
            return _connection.NonQueryResult;
        }

        public override object ExecuteScalar()
        {
            if (_connection.ThrowOnScalarExecute)
                throw new InvalidOperationException("execute failed");
            _connection.SetCommand(CommandText, CommandType, _parameters.Items);
            return _connection.ScalarResult;
        }

        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new CaptureDbParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (_connection.ThrowOnExecute)
                throw new InvalidOperationException("execute failed");
            _connection.SetCommand(CommandText, CommandType, _parameters.Items);
            return _connection.CreateReader();
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            if (_connection.ThrowOnExecute)
                throw new InvalidOperationException("execute failed");
            _connection.SetCommand(CommandText, CommandType, _parameters.Items);
            return Task.FromResult(_connection.NonQueryResult);
        }

        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            if (_connection.ThrowOnScalarExecute)
                throw new InvalidOperationException("execute failed");
            _connection.SetCommand(CommandText, CommandType, _parameters.Items);
            return Task.FromResult(_connection.ScalarResult);
        }
    }

    /// <summary>
    /// 捕获释放行为的数据读取器
    /// </summary>
    private sealed class CaptureDbDataReader : DbDataReader
    {
        private readonly DbDataReader _reader;
        private readonly CaptureDbConnection _connection;

        public CaptureDbDataReader(DbDataReader reader, CaptureDbConnection connection)
        {
            _reader = reader;
            _connection = connection;
        }

        public override int Depth => _reader.Depth;

        public override int FieldCount => _reader.FieldCount;

        public override bool HasRows => _reader.HasRows;

        public override bool IsClosed => _reader.IsClosed;

        public override int RecordsAffected => _reader.RecordsAffected;

        public override object this[int ordinal] => _reader[ordinal];

        public override object this[string name] => _reader[name];

        public override bool GetBoolean(int ordinal) => _reader.GetBoolean(ordinal);

        public override byte GetByte(int ordinal) => _reader.GetByte(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) =>
            _reader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

        public override char GetChar(int ordinal) => _reader.GetChar(ordinal);

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) =>
            _reader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

        public override string GetDataTypeName(int ordinal) => _reader.GetDataTypeName(ordinal);

        public override DateTime GetDateTime(int ordinal) => _reader.GetDateTime(ordinal);

        public override decimal GetDecimal(int ordinal) => _reader.GetDecimal(ordinal);

        public override double GetDouble(int ordinal) => _reader.GetDouble(ordinal);

        public override IEnumerator GetEnumerator() => ((IEnumerable)_reader).GetEnumerator();

        public override Type GetFieldType(int ordinal) => _reader.GetFieldType(ordinal);

        public override float GetFloat(int ordinal) => _reader.GetFloat(ordinal);

        public override Guid GetGuid(int ordinal) => _reader.GetGuid(ordinal);

        public override short GetInt16(int ordinal) => _reader.GetInt16(ordinal);

        public override int GetInt32(int ordinal) => _reader.GetInt32(ordinal);

        public override long GetInt64(int ordinal) => _reader.GetInt64(ordinal);

        public override string GetName(int ordinal) => _reader.GetName(ordinal);

        public override int GetOrdinal(string name) => _reader.GetOrdinal(name);

        public override DataTable GetSchemaTable() => _reader.GetSchemaTable();

        public override string GetString(int ordinal) => _reader.GetString(ordinal);

        public override object GetValue(int ordinal) => _reader.GetValue(ordinal);

        public override int GetValues(object[] values) => _reader.GetValues(values);

        public override bool IsDBNull(int ordinal) => _reader.IsDBNull(ordinal);

        public override bool NextResult() => _reader.NextResult();

        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) =>
            _reader.NextResultAsync(cancellationToken);

        public override bool Read() => _reader.Read();

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) =>
            _reader.ReadAsync(cancellationToken);

        public override T GetFieldValue<T>(int ordinal) => _reader.GetFieldValue<T>(ordinal);

        public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) =>
            _reader.GetFieldValueAsync<T>(ordinal, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reader.Dispose();
                _connection.OnReaderDisposed();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 捕获参数集合
    /// </summary>
    private sealed class CaptureDbParameterCollection : DbParameterCollection
    {
        public List<CaptureDbParameter> Items { get; } = new();

        public override int Count => Items.Count;

        public override object SyncRoot { get; } = new();

        public override int Add(object value)
        {
            Items.Add((CaptureDbParameter)value);
            return Items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
                Add(value);
        }

        public override void Clear() => Items.Clear();

        public override bool Contains(object value) => Items.Contains((CaptureDbParameter)value);

        public override bool Contains(string value) => Items.Any(t => t.ParameterName == value);

        public override void CopyTo(Array array, int index) => Items.ToArray().CopyTo(array, index);

        public override IEnumerator GetEnumerator() => Items.GetEnumerator();

        public override int IndexOf(object value) => Items.IndexOf((CaptureDbParameter)value);

        public override int IndexOf(string parameterName) => Items.FindIndex(t => t.ParameterName == parameterName);

        public override void Insert(int index, object value) => Items.Insert(index, (CaptureDbParameter)value);

        public override void Remove(object value) => Items.Remove((CaptureDbParameter)value);

        public override void RemoveAt(int index) => Items.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
                Items.RemoveAt(index);
        }

        protected override DbParameter GetParameter(int index) => Items[index];

        protected override DbParameter GetParameter(string parameterName) =>
            Items.FirstOrDefault(t => t.ParameterName == parameterName);

        protected override void SetParameter(int index, DbParameter value) => Items[index] = (CaptureDbParameter)value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                Items.Add((CaptureDbParameter)value);
                return;
            }

            Items[index] = (CaptureDbParameter)value;
        }
    }

    /// <summary>
    /// 捕获数据库参数
    /// </summary>
    private sealed class CaptureDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public override bool IsNullable { get; set; }

        public override string ParameterName { get; set; }

        public override string SourceColumn { get; set; }

        public override object Value { get; set; }

        public override bool SourceColumnNullMapping { get; set; }

        public override int Size { get; set; }

        public override byte Precision { get; set; }

        public override byte Scale { get; set; }

        public override void ResetDbType() { }
    }

    /// <summary>
    /// 捕获数据库事务
    /// </summary>
    private sealed class CaptureDbTransaction : DbTransaction
    {
        private readonly CaptureDbConnection _connection;

        private readonly IsolationLevel _isolationLevel;

        public CaptureDbTransaction(CaptureDbConnection connection, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            _connection = connection;
            _isolationLevel = isolationLevel;
        }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public int AsyncCommitCount { get; private set; }

        public int AsyncRollbackCount { get; private set; }

        public int DisposeCount { get; private set; }

        public bool ThrowOnDispose { get; set; }

        public bool ThrowOnCommit { get; set; }

        public bool ThrowOnRollback { get; set; }

        public override IsolationLevel IsolationLevel => _isolationLevel;

        protected override DbConnection DbConnection => _connection;

        public override void Commit()
        {
            CommitCount++;
            if (ThrowOnCommit)
                throw new InvalidOperationException("commit failed");
        }

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            AsyncCommitCount++;
            if (ThrowOnCommit)
                return Task.FromException(new InvalidOperationException("commit failed"));
            return Task.CompletedTask;
        }

        public override void Rollback()
        {
            RollbackCount++;
            if (ThrowOnRollback)
                throw new InvalidOperationException("rollback failed");
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            AsyncRollbackCount++;
            if (ThrowOnRollback)
                return Task.FromException(new InvalidOperationException("rollback failed"));
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing == false)
                return;
            DisposeCount++;
            if (ThrowOnDispose)
                throw new InvalidOperationException("transaction dispose failed");
        }
    }

    /// <summary>
    /// Sql 诊断观察器
    /// </summary>
    private sealed class SqlDiagnosticObserver : IObserver<DiagnosticListener>,
        IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly Action<DiagnosticsMessage> _onMessage;
        private readonly Func<string, bool> _eventFilter;
        private readonly IDisposable _allSubscription;
        private IDisposable _listenerSubscription;

        public SqlDiagnosticObserver(Action<DiagnosticsMessage> onMessage, Func<string, bool> eventFilter = null)
        {
            _onMessage = onMessage;
            _eventFilter = eventFilter ?? (name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
            _allSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name != SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                return;
            _listenerSubscription = value.Subscribe(this);
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (_eventFilter(value.Key) && value.Value is DiagnosticsMessage message)
                _onMessage(message);
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void Dispose()
        {
            _listenerSubscription?.Dispose();
            _allSubscription.Dispose();
        }
    }
}