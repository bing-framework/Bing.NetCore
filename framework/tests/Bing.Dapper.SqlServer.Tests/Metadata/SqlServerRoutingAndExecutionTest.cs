using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    /// 测试 - 外部事务执行失败时，执行器不应回滚或关闭外部事务连接。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenExternalTransactionFails_ShouldNotRollbackExternalTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnExecute = true };
        var transaction = new CaptureDbTransaction(connection);
        var executor = CreateExecutor(connection);
        executor.SetTransaction(transaction);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "abc" }));

        // Assert
        exception.Message.ShouldBe("execute failed");
        transaction.RollbackCount.ShouldBe(0);
        transaction.CommitCount.ShouldBe(0);
        connection.State.ShouldBe(ConnectionState.Open);
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
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
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
        services.AddDatabase<TestDatabase>();
        return services;
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
    /// 创建拥有连接的查询对象
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>查询对象</returns>
    private static InspectableSqlServerQuery CreateOwnedQuery(CaptureDbConnection connection)
    {
        var services = CreateServices();
        services.AddSqlServerSqlQuery<InspectableSqlServerQuery, InspectableSqlServerQuery>(options =>
            options.ConnectionString("Server=test;Database=test;"));
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqlOptions<InspectableSqlServerQuery>>();
        return new InspectableSqlServerQuery(provider, options, new ConnectionDatabase(connection));
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
        services.AddSqlServerSqlExecutor<InspectableSqlServerExecutor, InspectableSqlServerExecutor>(options =>
            options.ConnectionString("Server=test;Database=test;"));
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqlOptions<InspectableSqlServerExecutor>>();
        return new InspectableSqlServerExecutor(provider, options, new ConnectionDatabase(connection));
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
    /// 测试数据库
    /// </summary>
    private sealed class TestDatabase : IDatabase
    {
        public IDbConnection GetConnection() => null;
    }

    /// <summary>
    /// 返回指定连接的测试数据库
    /// </summary>
    private sealed class ConnectionDatabase : IDatabase
    {
        private readonly IDbConnection _connection;

        public ConnectionDatabase(IDbConnection connection) => _connection = connection;

        public IDbConnection GetConnection() => _connection;
    }

    /// <summary>
    /// 测试查询对象
    /// </summary>
    private sealed class InspectableSqlServerQuery : SqlServerSqlQueryBase
    {
        public InspectableSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<InspectableSqlServerQuery> options, IDatabase database = null)
            : base(serviceProvider, options, database)
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
            SqlOptions<CountedSqlServerQuery> options, IDatabase database = null)
            : base(serviceProvider, options, database)
        {
            CreatedCount++;
        }
    }

    /// <summary>
    /// 测试执行器
    /// </summary>
    private sealed class InspectableSqlServerExecutor : SqlServerSqlExecutorBase
    {
        public InspectableSqlServerExecutor(IServiceProvider serviceProvider,
            SqlOptions<InspectableSqlServerExecutor> options, IDatabase database = null)
            : base(serviceProvider, options, database)
        {
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

        public CaptureDbTransaction LastTransaction { get; private set; }

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
            LastTransaction = new CaptureDbTransaction(this, isolationLevel);
            return ValueTask.FromResult<DbTransaction>(LastTransaction);
        }

        public override Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

        public override IsolationLevel IsolationLevel => _isolationLevel;

        protected override DbConnection DbConnection => _connection;

        public override void Commit() => CommitCount++;

        public override void Rollback() => RollbackCount++;
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