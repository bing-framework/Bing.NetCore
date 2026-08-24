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
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Builders.Multiple;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Dapper.Sqlite;
using Bing.Tracing;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>
/// SqlServer 路由与执行测试
/// </summary>
public class SqlServerRoutingAndExecutionTest
{
    /// <summary>
    /// 测试目的：支持 Full Join 的 SQL Server 应为多表类型化连接生成完整方言 SQL，并按参数位置绑定同类型来源。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenTypedFullJoinConfigured_ShouldRenderCompleteSql()
    {
        // Arrange
        var services = CreateServices(CreateRoutingMetadataOptions());
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();

        // Act
        var query = rootQuery.From<MappedSample>("owner").From<MappedSample>("reviewer")
            .FullJoin<MappedSample, MappedSample>((owner, audit) => owner.Id == audit.Id, "audit", "owner")
            .Select<MappedSample>(owner => new object[] { owner.Id })
            .AppendSelect<MappedSample>(reviewer => new object[] { reviewer.Id }, "reviewer")
            .AppendSelect<MappedSample>(audit => new object[] { audit.Id }, "audit");

        // Assert
        query.ToSql().ShouldBe("Select [owner].[Id],[reviewer].[Id],[audit].[Id] \r\nFrom [Users] As [owner], [Users] As [reviewer] \r\nFull Join [Users] As [audit] On [owner].[Id]=[audit].[Id]");
    }

    /// <summary>
    /// 测试目的：支持 Full Join 的 SQL Server 应允许单表实体查询进入双表类型化 Full Join 链。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenSingleSourceTypedFullJoinConfigured_ShouldRenderCompleteSql()
    {
        // Arrange
        var services = CreateServices(CreateRoutingMetadataOptions());
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();

        // Act
        var query = rootQuery.From<MappedSample>("owner")
            .FullJoin<MappedSample, MappedSample>((owner, audit) => owner.Id == audit.Id, "audit", "owner")
            .Select<MappedSample>(owner => new object[] { owner.Id })
            .AppendSelect<MappedSample>(audit => new object[] { audit.Id }, "audit");

        // Assert
        query.ToSql().ShouldBe("Select [owner].[Id],[audit].[Id] \r\nFrom [Users] As [owner] \r\nFull Join [Users] As [audit] On [owner].[Id]=[audit].[Id]");
    }

    /// <summary>
    /// 测试目的：多表类型化 Cross Join 不应添加连接参数，后置 On 表面由 API 契约测试禁止。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenTypedCrossJoinConfigured_ShouldNotAddParameter()
    {
        // Arrange
        var services = CreateServices(CreateRoutingMetadataOptions());
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();
        var query = rootQuery.From<MappedSample>()
            .CrossJoin<MappedSample>("audit");

        // Assert
        var builder = ((ISqlQueryBuilderAccessor)query).GetSqlBuilder();
        Assert.Empty(builder.GetParams());
        Assert.Equal(
            "Select [Users].[Id],[Users].[Name],[Users].[Payload] \r\nFrom [Users] \r\nCross Join [Users] As [audit]",
            query.ToSql());
    }

    /// <summary>
    /// 测试目的：执行带逻辑删除过滤的实体查询时，SQL 与 Dapper 参数必须来自同一渲染快照。
    /// </summary>
    [Fact]
    public void ToList_WhenSoftDeleteFilterAddsParameter_ShouldBindRenderedSnapshotParameter()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" })
        };
        using var query = CreateQuery(connection);
        var description = query.From<SoftDeleteMappedSample>();
        var builder = ((ISqlQueryBuilderAccessor)description).GetSqlBuilder();

        // Act
        var result = description.ToList<SoftDeleteMappedSample>();

        // Assert
        result.Count.ShouldBe(1);
        connection.LastCommandText.ShouldBe(
            "Select [SoftDeleteMappedSample].[Id],[SoftDeleteMappedSample].[Name],[SoftDeleteMappedSample].[IsDeleted] \r\nFrom [SoftDeleteMappedSample] \r\nWhere [SoftDeleteMappedSample].[IsDeleted]=@_p_0");
        connection.LastCreatedParameters.Count.ShouldBe(1);
        var parameter = connection.LastCreatedParameters.Single();
        parameter.ParameterName.ShouldBe("@_p_0");
        parameter.Value.ShouldBe(false);
        builder.GetParams().ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：无行限制的类型化派生表不应保留内部排序，避免 SQL Server 派生表的无效 Order By 语法。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenTypedSubqueryHasOrderWithoutLimit_ShouldRemoveInnerOrderBy()
    {
        // Arrange
        var services = CreateServices(CreateRoutingMetadataOptions());
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();
        var summary = rootQuery.From<MappedSample>("users")
            .OrderBy<MappedSample>(item => new object[] { item.Id })
            .SelectSubquery<MappedSample, DerivedMappedSample>(item => new DerivedMappedSample { OwnerId = item.Id }, "summary");

        // Act
        var query = rootQuery.FromSubquery(summary)
            .Select<DerivedMappedSample>(item => new object[] { item.OwnerId });

        // Assert
        query.ToSql().ShouldBe("Select [summary].[OwnerId] \r\nFrom (Select [users].[Id] As [OwnerId] \r\nFrom [Users] As [users]) As [summary]");
    }

    /// <summary>
    /// 测试目的：含 Skip 和 Take 的类型化派生表必须保留内部排序与分页，以维持行限制语义。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenTypedSubqueryHasOrderAndLimit_ShouldKeepInnerOrderByAndPaging()
    {
        // Arrange
        var metadataOptions = CreateRoutingMetadataOptions();
        metadataOptions.DataSources.DataSources["default"].QueryCapabilities = new SqlQueryCapabilities
        {
            Pagination = SqlQueryCapabilityState.Supported
        };
        var services = CreateServices(metadataOptions);
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();
        var summary = rootQuery.From<MappedSample>("users")
            .OrderBy<MappedSample>(item => new object[] { item.Id })
            .Skip(5)
            .Take(10)
            .SelectSubquery<MappedSample, DerivedMappedSample>(item => new DerivedMappedSample { OwnerId = item.Id }, "summary");

        // Act
        var query = rootQuery.FromSubquery(summary)
            .Select<DerivedMappedSample>(item => new object[] { item.OwnerId });

        // Assert
        query.ToSql().ShouldBe("Select [summary].[OwnerId] \r\nFrom (Select [users].[Id] As [OwnerId] \r\nFrom [Users] As [users] \r\nOrder By [users].[Id] \r\nOffset @_p_0 Rows Fetch Next @_p_1 Rows Only) As [summary]");
    }

    /// <summary>
    /// 测试目的：支持 Full Join 的 SQL Server 应以 DTO 成员别名绑定类型化派生表，并保留内部筛选参数。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenDtoSubqueryFullJoined_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var services = CreateServices(CreateRoutingMetadataOptions());
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();
        var summary = rootQuery.From<MappedSample>("owner").From<MappedSample>("reviewer")
            .Where<MappedSample, MappedSample>((owner, reviewer) => owner.Id > 10)
            .SelectSubquery<MappedSample, MappedSample, DerivedMappedSample>(
                (owner, reviewer) => new DerivedMappedSample { OwnerId = owner.Id }, "summary");

        // Act
        var query = rootQuery.From<MappedSample>("owner").From<MappedSample>("reviewer")
            .FullJoin<MappedSample, DerivedMappedSample>(summary,
                (owner, derived) => owner.Id == derived.OwnerId, "owner")
            .Select<MappedSample>(owner => new object[] { owner.Id })
            .AppendSelect<MappedSample>(reviewer => new object[] { reviewer.Id }, "reviewer")
            .AppendSelect<DerivedMappedSample>(derived => new object[] { derived.OwnerId }, "summary");

        // Assert
        query.ToSql().ShouldBe("Select [owner].[Id],[reviewer].[Id],[summary].[OwnerId] \r\nFrom [Users] As [owner], [Users] As [reviewer] \r\nFull Join (Select [owner].[Id] As [OwnerId] \r\nFrom [Users] As [owner], [Users] As [reviewer] \r\nWhere [owner].[Id]>@_p_0) As [summary] On [owner].[Id]=[summary].[OwnerId]");
    }

    /// <summary>
    /// 测试目的：支持 Full Join 的 SQL Server 应允许单表实体来源连接类型化派生表，并保留派生表参数。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenSingleSourceDtoSubqueryFullJoined_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var services = CreateServices(CreateRoutingMetadataOptions());
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();
        var summary = rootQuery.From<MappedSample>("users")
            .Where<MappedSample>(item => item.Id > 10)
            .SelectSubquery<MappedSample, DerivedMappedSample>(item => new DerivedMappedSample { OwnerId = item.Id }, "summary");

        // Act
        var query = rootQuery.From<MappedSample>("sample")
            .FullJoin<MappedSample, DerivedMappedSample>(summary,
                (sample, derived) => sample.Id == derived.OwnerId, "sample")
            .Select<MappedSample>(sample => new object[] { sample.Id })
            .AppendSelect<DerivedMappedSample>(derived => new object[] { derived.OwnerId }, "summary");

        // Assert
        query.ToSql().ShouldBe("Select [sample].[Id],[summary].[OwnerId] \r\nFrom [Users] As [sample] \r\nFull Join (Select [users].[Id] As [OwnerId] \r\nFrom [Users] As [users] \r\nWhere [users].[Id]>@_p_0) As [summary] On [sample].[Id]=[summary].[OwnerId]");
    }

    /// <summary>
    /// 测试目的：支持 Full Join 的 SQL Server 应允许两个类型化派生表以派生根入口组合，并隔离同名参数。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenDtoSubqueryRootFullJoined_ShouldRenderCompleteSqlAndParameters()
    {
        // Arrange
        var services = CreateServices(CreateRoutingMetadataOptions());
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var rootQuery = provider.GetRequiredService<ISqlQueryFactory>().Create();
        var owner = rootQuery.From<MappedSample>("users")
            .Where<MappedSample>(item => item.Id > 10)
            .SelectSubquery<MappedSample, DerivedMappedSample>(item => new DerivedMappedSample { OwnerId = item.Id }, "owner");
        var audit = rootQuery.From<MappedSample>("users")
            .Where<MappedSample>(item => item.Id > 20)
            .SelectSubquery<MappedSample, DerivedMappedSample>(item => new DerivedMappedSample { OwnerId = item.Id }, "audit");

        // Act
        var query = rootQuery.FromSubquery(owner)
            .FullJoin<DerivedMappedSample, DerivedMappedSample>(audit,
                (left, right) => left.OwnerId == right.OwnerId, "owner")
            .Select<DerivedMappedSample>()
            .AppendSelect<DerivedMappedSample>(right => new object[] { right.OwnerId }, "audit");

        // Assert
        query.ToSql().ShouldBe("Select [owner].[OwnerId],[audit].[OwnerId] \r\nFrom (Select [users].[Id] As [OwnerId] \r\nFrom [Users] As [users] \r\nWhere [users].[Id]>@_p_0) As [owner] \r\nFull Join (Select [users].[Id] As [OwnerId] \r\nFrom [Users] As [users] \r\nWhere [users].[Id]>@_p_1) As [audit] On [owner].[OwnerId]=[audit].[OwnerId]");
    }

    /// <summary>
    /// 测试目的：类型化 DTO 派生表不能跨 Provider 组合，拒绝时不得修改外层 SQL 状态。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenDtoSubqueryUsesDifferentProvider_ShouldRejectWithoutChangingOuterQuery()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["sqlserver"] = new SqlDataSourceDescriptor
        {
            Key = "sqlserver",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=sqlserver;Database=test;"
        };
        metadataOptions.DataSources.DataSources["sqlite"] = new SqlDataSourceDescriptor
        {
            Key = "sqlite",
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = "Data Source=:memory:"
        };
        foreach (var dbKey in new[] { "sqlserver", "sqlite" })
            metadataOptions.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MappedSample),
                DbKey = dbKey,
                TableName = "Users"
            });
        var services = CreateServices(metadataOptions);
        services.AddSqlServerProvider();
        services.AddSqliteProvider();
        using var provider = services.BuildServiceProvider();
        using var sqlServerQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlserver");
        using var sqliteQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = sqliteQuery.From<MappedSample>("left").From<MappedSample>("right")
            .SelectSubquery<MappedSample, MappedSample, DerivedMappedSample>(
                (left, right) => new DerivedMappedSample { OwnerId = left.Id }, "summary");
        var outer = sqlServerQuery.From<MappedSample>("owner").From<MappedSample>("reviewer");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => outer.Join<MappedSample, DerivedMappedSample>(
            subquery, (owner, derived) => owner.Id == derived.OwnerId, "owner"));

        // Assert
        exception.Message.ShouldBe("类型化派生表 Provider bing.sqlite 与当前 Provider bing.sqlserver 不兼容。");
        outer.ToSql().ShouldBe("Select [owner].[Id],[owner].[Name],[owner].[Payload] \r\nFrom [Users] As [owner], [Users] As [reviewer]");
    }

    /// <summary>
    /// 测试目的：类型化 DTO 派生表作为根来源时，跨 Provider 组合应在独立 Builder 创建阶段拒绝，且不访问连接。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_WhenDtoSubqueryRootUsesDifferentProvider_ShouldRejectBeforeConnectionAccess()
    {
        // Arrange
        var metadataOptions = new SqlMetadataOptions();
        metadataOptions.DataSources.DataSources["sqlserver"] = new SqlDataSourceDescriptor
        {
            Key = "sqlserver",
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=sqlserver;Database=test;"
        };
        metadataOptions.DataSources.DataSources["sqlite"] = new SqlDataSourceDescriptor
        {
            Key = "sqlite",
            DatabaseType = DatabaseType.Sqlite,
            ConnectionString = "Data Source=:memory:"
        };
        foreach (var dbKey in new[] { "sqlserver", "sqlite" })
            metadataOptions.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(MappedSample),
                DbKey = dbKey,
                TableName = "Users"
            });
        var services = CreateServices(metadataOptions);
        services.AddSqlServerProvider();
        services.AddSqliteProvider();
        using var provider = services.BuildServiceProvider();
        using var sqlServerQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlserver");
        using var sqliteQuery = provider.GetRequiredService<ISqlQueryFactory>().Create("sqlite");
        var subquery = sqliteQuery.From<MappedSample>("users")
            .SelectSubquery<MappedSample, DerivedMappedSample>(
                item => new DerivedMappedSample { OwnerId = item.Id }, "summary");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => sqlServerQuery.FromSubquery(subquery));

        // Assert
        exception.Message.ShouldBe("类型化派生表 Provider bing.sqlite 与当前 Provider bing.sqlserver 不兼容。");
    }

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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = Assert.IsType<SqlServerSqlQuery>(factory.Create("reporting"));

        // Assert
        query.Options.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        query.Options.DatabaseType.ShouldBe(DatabaseType.SqlServer);
        query.Options.Connection.ShouldBeNull();
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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = Assert.IsType<SqlServerSqlQuery>(factory.Create("reporting"));

        // Assert
        query.Options.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        query.Options.DatabaseType.ShouldBe(DatabaseType.SqlServer);
        query.Options.GetDatabaseContext().MappingProfile.ShouldBe("reporting-v2");
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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = Assert.IsType<SqlServerSqlQuery>(factory.Create("reporting"));

        // Assert
        query.Options.ConnectionString.ShouldBe("Server=config;Database=test;");
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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            factory.Create("reporting"));

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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            factory.Create("reporting"));

        // Assert
        exception.Message.ShouldContain("ReportingConnection");
        exception.Message.ShouldNotContain("Server=default");
    }

    /// <summary>
    /// 测试 - 工厂应按当前作用域的主库读取偏好切换到主库连接字符串。
    /// </summary>
    [Fact]
    public void Create_WhenPrimaryReadPreferenceIsConfigured_ShouldResolvePrimaryConnectionString()
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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        using var scope = provider.GetRequiredService<IDatabaseScopeManager>().Use(new DatabaseScopeOptions
        {
            DbKey = "reporting",
            ReadPreference = SqlReadPreference.Primary
        });
        var query = Assert.IsType<SqlServerSqlQuery>(factory.Create("reporting"));

        // Act
        var connectionString = query.GetExecutionConnection().ConnectionString;

        // Assert
        connectionString.ShouldBe("Server=primary;Database=test;");
    }

    /// <summary>
    /// 测试目的：在已绑定只读数据源的作用域中切换主库读取偏好时，工厂必须同时切换连接、映射与 Provider 上下文。
    /// </summary>
    [Fact]
    public void Create_WhenCurrentContextSwitchesToPrimaryRead_ShouldUsePrimaryContextAndMapping()
    {
        // Arrange
        var metadataOptions = CreateRoutingMetadataOptions();
        metadataOptions.DataSources.DataSources["reporting"].PrimaryReadStrategy =
            PrimaryReadStrategy.PrimaryDataSource;
        metadataOptions.DataSources.DataSources["reporting"].PrimaryDataSourceKey = "default";
        var services = CreateServices(metadataOptions);
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        using var databaseScope = provider.GetRequiredService<IDatabaseScopeManager>().Use("reporting");
        using var readPreferenceScope = provider.GetRequiredService<IReadPreferenceScopeManager>()
            .Use(SqlReadPreference.Primary);

        // Act
        using var query = Assert.IsType<SqlServerSqlQuery>(factory.Create());
        var description = query.From<MappedSample>("u").Where<MappedSample, string>(item => item.Name, "primary");

        // Assert
        query.Options.ConnectionString.ShouldBe("Server=default;Database=test;");
        description.ToSql().ShouldBe(
            "Select [u].[Id],[u].[Name],[u].[Payload] \r\nFrom [Users] As [u] \r\nWhere [u].[Name]=@_p_0");
    }

    /// <summary>
    /// 测试 - 固定查询工厂应按 Provider 路由创建官方查询实现。
    /// </summary>
    [Fact]
    public void SqlQueryFactory_Create_ShouldUseOfficialProviderImplementation()
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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();

        // Act
        var query = factory.Create("reporting");

        // Assert
        query.ShouldBeOfType<SqlServerSqlQuery>();
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
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISqlQueryFactory>();
        var accessor = provider.GetRequiredService<IDatabaseContextAccessor>();

        // Act
        var query = Assert.IsType<SqlServerSqlQuery>(factory.Create("reporting"));
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
        var description = query.From<MappedSample>("u").Where<MappedSample, string>(t => t.Name, "abc");

        // Assert
        query.Options.ConnectionString.ShouldBe("Server=reporting;Database=test;");
        description.ToSql().ShouldContain("[Users_Reporting]");
        description.ToSql().ShouldContain("[reporting_name]");
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
        executor.ConfigureDatabaseContext(new DatabaseContext
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

        // Act
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "default" });
        executor.EnableTenantDiagnostics();
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "opt-in" });

        // Assert
        messages.Count.ShouldBe(2);
        messages[0].MappingProfile.ShouldBe("profile-a");
        messages[0].TenantId.ShouldBeNull();
        messages[1].MappingProfile.ShouldBe("profile-a");
        messages[1].TenantId.ShouldBe("tenant-a");
    }

    /// <summary>
    /// 测试目的：Executor 每次创建 Mutation Builder 都应获得独立状态，避免 Root 对象保留共享可变 SQL。
    /// </summary>
    [Fact]
    public void CreateBuilder_WhenCalledMultipleTimes_ShouldReturnIndependentBuilders()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var executor = CreateExecutor(connection);

        // Act
        var first = executor.CreateWriteBuilder().InsertInto("Users").Columns("Name").Values("first");
        var second = executor.CreateWriteBuilder().InsertInto("Users").Columns("Name").Values("second");

        // Assert
        first.ShouldNotBeSameAs(second);
        first.ToSql().ShouldBe("Insert Into [Users] ([Name]) Values (@_p_0)");
        second.ToSql().ShouldBe("Insert Into [Users] ([Name]) Values (@_p_0)");
        first.GetParams()["@_p_0"].ShouldBe("first");
        second.GetParams()["@_p_0"].ShouldBe("second");
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
        var services = new ServiceCollection();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(resolver);
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var executor = CreateSqlServerTestRoot<InspectableSqlServerExecutor>(provider,
            options => options.ConnectionString("Server=test;Database=test;"));

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
    /// 测试目的：普通异步命令的主库短事务必须使用原生异步开始和提交成员，不能回退到同步事务 API。
    /// </summary>
    [Fact]
    public async Task ExecuteSqlAsync_WhenPrimaryReadTransactionSucceeds_ShouldUseNativeAsyncTransactionMembers()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var result = await executor.ExecuteSqlAsync("Update [Users] Set [Name]=@name", new { name = "async" });

        // Assert
        result.ShouldBe(1);
        connection.AsyncBeginCount.ShouldBe(1);
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：命令完成后、事务提交前发生取消时，主库短事务必须回滚，不能依赖 Provider 忽略取消令牌的提交实现。
    /// </summary>
    [Fact]
    public async Task ScalarAsync_WhenCancelledAfterCommand_ShouldRollbackBeforeTransactionCommit()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var connection = new CaptureDbConnection { OnScalarExecuted = cancellationTokenSource.Cancel };
        using var query = CreateOwnedQuery(connection);
        ConfigurePrimaryReadTransaction(query);
        var description = query.Query<int>().Select("Count(*)").From("[Users]");

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => description.ScalarAsync(
            cancellationToken: cancellationTokenSource.Token));

        // Assert
        connection.LastTransaction.AsyncCommitCount.ShouldBe(0);
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：预取消必须先于分页参数校验执行，不能先因缺少分页参数而失败。
    /// </summary>
    [Fact]
    public async Task ToPageAsync_WhenCancellationRequested_ShouldCancelBeforePagingPlanValidation()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateQuery(connection);
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => description.ToPageAsync(
            cancellationToken: cancellationTokenSource.Token));

        // Assert
        connection.ExecutedCommandTexts.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：普通异步命令失败时，主库短事务必须使用原生异步回滚成员且保留原始执行异常。
    /// </summary>
    [Fact]
    public async Task ExecuteSqlAsync_WhenPrimaryReadTransactionFails_ShouldUseNativeAsyncRollback()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnExecute = true };
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteSqlAsync("Update [Users] Set [Name]=@name", new { name = "async-failure" }));

        // Assert
        exception.Message.ShouldBe("execute failed");
        connection.AsyncBeginCount.ShouldBe(1);
        connection.LastTransaction.AsyncCommitCount.ShouldBe(0);
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
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
    /// 测试目的：内部事务提交后的连接关闭失败不能阻断事务释放，关闭异常仍应反馈给调用方。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenCommitConnectionCloseFails_ShouldReleaseOwnedTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnClose = true };
        using var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "success" }));

        // Assert
        exception.Message.ShouldBe("connection close failed");
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.DisposeCount.ShouldBe(1);
        connection.CloseCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：Root Executor 异步释放自有连接时，应调用连接的原生异步释放成员。
    /// </summary>
    [Fact]
    public async Task DisposeAsync_WhenExecutorOwnsConnection_ShouldUseNativeAsyncConnectionDisposal()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "async-dispose" });

        // Act
        await ((IAsyncDisposable)executor).DisposeAsync();

        // Assert
        connection.AsyncDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：执行失败后的回滚清理中即使连接关闭失败，也必须释放内部事务并保留主执行异常。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenRollbackConnectionCloseFails_ShouldReleaseOwnedTransactionAndPreserveExecutionException()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnExecute = true, ThrowOnClose = true };
        using var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var exception = Should.Throw<AggregateException>(() =>
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "failure" }));

        // Assert
        exception.Flatten().InnerExceptions.Select(item => item.Message)
            .ShouldBe(new[] { "execute failed", "connection close failed" });
        connection.LastTransaction.RollbackCount.ShouldBe(1);
        connection.LastTransaction.DisposeCount.ShouldBe(1);
        connection.CloseCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：普通同步 Executor 的执行、回滚、错误钩子和完成钩子同时失败时，
    /// 原始执行异常必须排在聚合异常首位，后续清理异常按生命周期顺序保留。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenOperationAndCleanupFail_ShouldPreserveLifecycleExceptionOrder()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ThrowOnExecute = true,
            ThrowOnTransactionRollback = true
        };
        var executor = CreateLifecycleExecutor(connection);
        executor.ThrowOnErrorHook = true;
        executor.ThrowOnCompletionHook = true;
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var exception = Assert.Throws<AggregateException>(() =>
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "failure" }));

        // Assert
        Assert.Equal(new[] { "execute failed", "rollback failed", "error hook failed", "completion hook failed" },
            exception.Flatten().InnerExceptions.Select(item => item.Message));
        Assert.Equal(1, connection.LastTransaction.RollbackCount);
        Assert.Equal(1, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：普通异步 Executor 的异常聚合规则必须与同步入口一致，
    /// 取消或命令失败均不得被回滚或 Hook 异常覆盖。
    /// </summary>
    [Fact]
    public async Task ExecuteSqlAsync_WhenOperationAndCleanupFail_ShouldPreserveLifecycleExceptionOrder()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ThrowOnExecute = true,
            ThrowOnTransactionRollback = true
        };
        var executor = CreateLifecycleExecutor(connection);
        executor.ThrowOnErrorHook = true;
        executor.ThrowOnCompletionHook = true;
        ConfigurePrimaryReadTransaction(executor);

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            executor.ExecuteSqlAsync("Update [Users] Set [Name]=@name", new { name = "failure" }));

        // Assert
        Assert.Equal(new[] { "execute failed", "rollback failed", "error hook failed", "completion hook failed" },
            exception.Flatten().InnerExceptions.Select(item => item.Message));
        Assert.Equal(1, connection.LastTransaction.AsyncRollbackCount);
        Assert.Equal(0, connection.LastTransaction.RollbackCount);
        Assert.Equal(1, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：多结果集命令在创建读取器失败时，执行、回滚、错误 Hook 和完成 Hook 异常
    /// 必须按生命周期顺序聚合，不能由清理步骤覆盖原始数据库异常。
    /// </summary>
    [Fact]
    public void ExecuteMultiple_WhenOperationAndCleanupFail_ShouldPreserveLifecycleExceptionOrder()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ThrowOnExecute = true,
            ThrowOnTransactionRollback = true
        };
        var executor = CreateLifecycleMultipleExecutor(connection);
        executor.ThrowOnErrorHook = true;
        executor.ThrowOnCompletionHook = true;
        ConfigurePrimaryReadTransaction(executor);
        var command = new SqlMultipleQueryCommand("Select 1", Array.Empty<SqlParam>());

        // Act
        var exception = Assert.Throws<AggregateException>(() => executor.Execute(command));

        // Assert
        Assert.Equal(new[] { "execute failed", "rollback failed", "error hook failed", "completion hook failed" },
            exception.Flatten().InnerExceptions.Select(item => item.Message));
        Assert.Equal(1, connection.LastTransaction.RollbackCount);
        Assert.Equal(1, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：多结果集异步命令失败时，异常聚合规则必须与同步入口一致。
    /// </summary>
    [Fact]
    public async Task ExecuteMultipleAsync_WhenOperationAndCleanupFail_ShouldPreserveLifecycleExceptionOrder()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ThrowOnExecute = true,
            ThrowOnTransactionRollback = true
        };
        var executor = CreateLifecycleMultipleExecutor(connection);
        executor.ThrowOnErrorHook = true;
        executor.ThrowOnCompletionHook = true;
        ConfigurePrimaryReadTransaction(executor);
        var command = new SqlMultipleQueryCommand("Select 1", Array.Empty<SqlParam>());

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() => executor.ExecuteAsync(command));

        // Assert
        Assert.Equal(new[] { "execute failed", "rollback failed", "error hook failed", "completion hook failed" },
            exception.Flatten().InnerExceptions.Select(item => item.Message));
        Assert.Equal(1, connection.LastTransaction.AsyncRollbackCount);
        Assert.Equal(0, connection.LastTransaction.RollbackCount);
        Assert.Equal(1, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：多结果集在执行前被跳过且完成 Hook 失败时，只能执行一次完成清理，
    /// 不得将完成 Hook 异常重新识别为数据库执行异常。
    /// </summary>
    [Fact]
    public void ExecuteMultiple_WhenBeforeHookSkipsAndCompletionFails_ShouldCompleteOnlyOnce()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateLifecycleMultipleExecutor(connection);
        executor.SkipBeforeExecution = true;
        executor.ThrowOnCompletionHook = true;
        var command = new SqlMultipleQueryCommand("Select 1", Array.Empty<SqlParam>());

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => executor.Execute(command));

        // Assert
        Assert.Equal("completion hook failed", exception.Message);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Equal(0, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：异步多结果集在执行前被跳过且完成 Hook 失败时，也只能执行一次完成清理，
    /// 不得额外调用错误 Hook 或重复释放执行状态。
    /// </summary>
    [Fact]
    public async Task ExecuteMultipleAsync_WhenBeforeHookSkipsAndCompletionFails_ShouldCompleteOnlyOnce()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateLifecycleMultipleExecutor(connection);
        executor.SkipBeforeExecution = true;
        executor.ThrowOnCompletionHook = true;
        var command = new SqlMultipleQueryCommand("Select 1", Array.Empty<SqlParam>());

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(command));

        // Assert
        Assert.Equal("completion hook failed", exception.Message);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Equal(0, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：Returning 查询在执行前 Hook 跳过时必须返回空集合，且不得创建命令或触发错误 Hook。
    /// </summary>
    [Fact]
    public void ExecuteReturningQuery_WhenBeforeHookSkips_ShouldReturnEmptyList()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateLifecycleExecutor(connection);
        executor.SkipBeforeExecution = true;
        ISqlBuilder builder = new SqlServerBuilder();
        builder.Update(new SqlTableReference { TableName = "Users" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id");
        var command = builder.ToSqlWriteCommand();

        // Act
        var rows = executor.ExecuteReturning<int>(command);

        // Assert
        Assert.Empty(rows);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Equal(0, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：异步 Returning 查询在执行前 Hook 跳过时必须与同步入口返回相同的空集合语义。
    /// </summary>
    [Fact]
    public async Task ExecuteReturningQueryAsync_WhenBeforeHookSkips_ShouldReturnEmptyList()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateLifecycleExecutor(connection);
        executor.SkipBeforeExecution = true;
        ISqlBuilder builder = new SqlServerBuilder();
        builder.Update(new SqlTableReference { TableName = "Users" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id");
        var command = builder.ToSqlWriteCommand();

        // Act
        var rows = await executor.ExecuteReturningAsync<int>(command);

        // Assert
        Assert.NotNull(rows);
        Assert.Empty(rows);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Equal(0, executor.ErrorHookCount);
        Assert.Equal(1, executor.CompletionHookCount);
    }

    /// <summary>
    /// 测试目的：只读数据源上的带 Returning 结构化查询计划必须在创建读取器前拒绝写入。
    /// </summary>
    [Fact]
    public void QueryPlanReturning_WhenDataSourceIsReadOnly_ShouldRejectBeforeReaderCreation()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateQuery(connection);
        ConfigureReadOnlyDataSource(query);
        var plan = CreateReturningQueryPlan();

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => ((ISqlQueryPlanExecutor)query).ToList<int>(plan,
            timeout: null));

        // Assert
        exception.Message.ShouldContain("reporting");
        connection.ReaderCreateCount.ShouldBe(0);
        connection.ExecutedCommandTexts.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：异步带 Returning 结构化查询计划必须与同步入口一致，在创建读取器前拒绝只读数据源写入。
    /// </summary>
    [Fact]
    public async Task QueryPlanReturningAsync_WhenDataSourceIsReadOnly_ShouldRejectBeforeReaderCreation()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateQuery(connection);
        ConfigureReadOnlyDataSource(query);
        var plan = CreateReturningQueryPlan();

        // Act
        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => ((ISqlQueryPlanExecutor)query)
            .ToListAsync<int>(plan, timeout: null, cancellationToken: CancellationToken.None));

        // Assert
        exception.Message.ShouldContain("reporting");
        connection.ReaderCreateCount.ShouldBe(0);
        connection.ExecutedCommandTexts.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：由 SQLite Builder 冻结的 Returning 写入命令必须在 SQL Server Executor 打开连接前被拒绝。
    /// </summary>
    [Fact]
    public void ExecuteReturningQuery_WhenWriteCommandProviderMismatches_ShouldRejectBeforeConnectionAccess()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        ISqlBuilder builder = new SqliteBuilder();
        builder.Update(new SqlTableReference { TableName = "Users" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id");
        var command = builder.ToSqlWriteCommand();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => executor.ExecuteReturning<int>(command));

        // Assert
        Assert.Equal("写入命令 Provider bing.sqlite 与当前 Executor Provider bing.sqlserver 不一致，不能执行。",
            exception.Message);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Null(connection.LastTransaction);
        Assert.Equal(0, connection.DisposeCount);
    }

    /// <summary>
    /// 测试目的：异步 Returning 查询也必须在开始异步事务或创建读取器前拒绝跨 Provider 的写入命令。
    /// </summary>
    [Fact]
    public async Task ExecuteReturningQueryAsync_WhenWriteCommandProviderMismatches_ShouldRejectBeforeConnectionAccess()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var executor = CreateOwnedExecutor(connection);
        ISqlBuilder builder = new SqliteBuilder();
        builder.Update(new SqlTableReference { TableName = "Users" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id");
        var command = builder.ToSqlWriteCommand();

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteReturningAsync<int>(command));

        // Assert
        Assert.Equal("写入命令 Provider bing.sqlite 与当前 Executor Provider bing.sqlserver 不一致，不能执行。",
            exception.Message);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Null(connection.LastTransaction);
        Assert.Equal(0, connection.DisposeCount);
    }

    /// <summary>
    /// 测试目的：多结果集提前释放 Reader 且回滚失败时，Reader 释放异常必须位于聚合异常首位，
    /// 回滚异常不得覆盖该主异常。
    /// </summary>
    [Fact]
    public void ExecuteMultiple_WhenReaderDisposeAndRollbackFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ThrowOnReaderDispose = true,
            ThrowOnTransactionRollback = true
        };
        var executor = CreateLifecycleMultipleExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);
        var command = new SqlMultipleQueryCommand("Select Id,Name From [Users]", Array.Empty<SqlParam>());

        // Act
        var result = executor.Execute(command);
        var exception = Assert.Throws<AggregateException>(result.Dispose);

        // Assert
        Assert.Equal(new[] { "reader dispose failed", "rollback failed" }, exception.Flatten().InnerExceptions
            .Select(item => item.Message));
        Assert.Equal(1, connection.ReaderDisposeCount);
        Assert.Equal(1, connection.LastTransaction.RollbackCount);
    }

    /// <summary>
    /// 测试目的：多结果集异步命令完成后发生取消时，执行器必须释放已获取的读取器并回滚短事务，不能返回可提交的结果对象。
    /// </summary>
    [Fact]
    public async Task ExecuteMultipleAsync_WhenCancelledAfterCommand_ShouldDisposeReaderAndRollback()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var connection = new CaptureDbConnection { OnReaderCreated = cancellationTokenSource.Cancel };
        var executor = CreateLifecycleMultipleExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);
        var command = new SqlMultipleQueryCommand("Select Id,Name From [Users]", Array.Empty<SqlParam>());

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(command,
            cancellationToken: cancellationTokenSource.Token));

        // Assert
        connection.ReaderDisposeCount.ShouldBe(1);
        connection.LastTransaction.AsyncCommitCount.ShouldBe(0);
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：异步多结果集读取在开始前取消且 Reader 释放失败时，取消异常必须位于聚合异常首位，
    /// Reader 释放异常不得被完成回调或执行租约释放覆盖。
    /// </summary>
    [Fact]
    public async Task ExecuteMultipleAsync_WhenCancelledAndReaderDisposeFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var connection = new CaptureDbConnection { ThrowOnReaderDispose = true };
        var executor = CreateLifecycleMultipleExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);
        var command = new SqlMultipleQueryCommand("Select Id,Name From [Users]", Array.Empty<SqlParam>());
        cancellationTokenSource.Cancel();

        // Act
        var result = await executor.ExecuteAsync(command);
        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            result.ReadAsync<MappedSample>(cancellationTokenSource.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception.Flatten().InnerExceptions.First());
        Assert.Equal("reader dispose failed", exception.Flatten().InnerExceptions.Last().Message);
        Assert.Equal(1, connection.ReaderDisposeCount);
        Assert.Equal(1, connection.LastTransaction.AsyncRollbackCount);
        Assert.Equal(0, connection.LastTransaction.RollbackCount);
    }

    /// <summary>
    /// 测试目的：异步多结果集完整消费并异步释放后，主库短事务必须使用原生异步开始和提交成员。
    /// </summary>
    [Fact]
    public async Task ExecuteMultipleAsync_WhenConsumedAndDisposedAsync_ShouldUseNativeAsyncTransactionMembers()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" })
        };
        var executor = CreateLifecycleMultipleExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);
        var command = new SqlMultipleQueryCommand("Select Id,Name From [Users]", Array.Empty<SqlParam>());

        // Act
        var result = await executor.ExecuteAsync(command);
        var rows = await result.ReadAsync<MappedSample>();
        await result.DisposeAsync();

        // Assert
        rows.ShouldHaveSingleItem().Name.ShouldBe("Alice");
        connection.AsyncBeginCount.ShouldBe(1);
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(0);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：多结果集已完整读取后即使调用方令牌迟到取消，也必须提交短事务并完成释放。
    /// </summary>
    [Fact]
    public async Task ExecuteMultipleAsync_WhenCancelledAfterAllResultsConsumed_ShouldCommitAndReleaseResources()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" })
        };
        var executor = CreateLifecycleMultipleExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);
        var command = new SqlMultipleQueryCommand("Select Id,Name From [Users]", Array.Empty<SqlParam>());

        // Act
        var result = await executor.ExecuteAsync(command, cancellationToken: cancellationTokenSource.Token);
        var rows = await result.ReadAsync<MappedSample>();
        cancellationTokenSource.Cancel();
        await result.DisposeAsync();

        // Assert
        rows.ShouldHaveSingleItem().Name.ShouldBe("Alice");
        connection.ReaderDisposeCount.ShouldBe(1);
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：同一多结果集读取器在异步读取期间必须拒绝第二次读取和释放，防止结果集错序或读取器被提前关闭。
    /// </summary>
    [Fact]
    public async Task ExecuteMultipleAsync_WhenReadIsInProgress_ShouldRejectConcurrentReadAndDispose()
    {
        // Arrange
        var readStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRead = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" }),
            OnReaderReadAsync = async () =>
            {
                if (Interlocked.Increment(ref readCount) == 1)
                {
                    readStarted.TrySetResult(null);
                    await allowRead.Task;
                }
            }
        };
        var executor = CreateLifecycleMultipleExecutor(connection);
        var command = new SqlMultipleQueryCommand("Select Id,Name From [Users]", Array.Empty<SqlParam>());
        var result = await executor.ExecuteAsync(command);

        // Act
        var readTask = result.ReadAsync<MappedSample>();
        await readStarted.Task;
        var concurrentReadException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            result.ReadAsync<MappedSample>());
        var disposeException = await Assert.ThrowsAsync<InvalidOperationException>(() => result.DisposeAsync().AsTask());
        allowRead.TrySetResult(null);
        var rows = await readTask;
        await result.DisposeAsync();

        // Assert
        concurrentReadException.Message.ShouldBe("当前多结果集正在读取或释放，不能并发访问。");
        disposeException.Message.ShouldBe("当前多结果集正在读取或释放，不能并发访问。");
        rows.ShouldHaveSingleItem().Name.ShouldBe("Alice");
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：完整消费多结果集后事务提交失败时，应发布错误诊断而不是成功诊断，
    /// 使观测系统可以准确识别资源完成阶段的失败。
    /// </summary>
    [Fact]
    public void ExecuteMultiple_WhenCommitFails_ShouldPublishErrorDiagnostics()
    {
        // Arrange
        var messages = new List<DiagnosticsMessage>();
        using var observer = new SqlDiagnosticObserver(messages.Add, name => name is
            SqlQueryDiagnosticListenerNames.AfterExecute or SqlQueryDiagnosticListenerNames.ErrorExecute);
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" }),
            ThrowOnTransactionCommit = true
        };
        var executor = CreateLifecycleMultipleExecutor(connection);
        ConfigurePrimaryReadTransaction((ISqlQuery)executor);
        var command = new SqlMultipleQueryCommand("Select Id,Name From [Users]", Array.Empty<SqlParam>());

        // Act
        var result = executor.Execute(command);
        result.Read<MappedSample>();
        var exception = Assert.Throws<InvalidOperationException>(result.Dispose);

        // Assert
        Assert.Equal("commit failed", exception.Message);
        Assert.Equal(new[] { SqlQueryDiagnosticListenerNames.ErrorExecute }, messages.Select(item => item.Operation));
    }

    /// <summary>
    /// 测试目的：单体更新的并发校验必须在事务提交前发生，零受影响行应回滚内部事务。
    /// </summary>
    [Fact]
    public void Update_WhenConcurrencyValidationFails_ShouldRollbackInsteadOfCommit()
    {
        // Arrange
        var connection = new CaptureDbConnection { NonQueryResult = 0 };
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        Should.Throw<Bing.Exceptions.ConcurrencyException>(() => executor.Update(new ConcurrencySample
        {
            Id = 1,
            Name = "updated",
            Version = 2
        }));

        // Assert
        connection.LastTransaction.RollbackCount.ShouldBe(1);
        connection.LastTransaction.CommitCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：异步单体更新的并发校验必须在事务提交前发生，零受影响行应回滚内部事务。
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WhenConcurrencyValidationFails_ShouldRollbackInsteadOfCommit()
    {
        // Arrange
        var connection = new CaptureDbConnection { NonQueryResult = 0 };
        var executor = CreateOwnedExecutor(connection);
        ConfigurePrimaryReadTransaction(executor);

        // Act
        await Should.ThrowAsync<Bing.Exceptions.ConcurrencyException>(() => executor.UpdateAsync(new ConcurrencySample
        {
            Id = 1,
            Name = "updated",
            Version = 2
        }));

        // Assert
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
        connection.LastTransaction.CommitCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试 - 独立 SQL 事务作用域提交时应提交作用域拥有的事务。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_Commit_ShouldCommitOwnedTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        exception.Message.ShouldContain("仅支持 Dapper SQL 查询实现", Case.Insensitive);
        trackingProxy.DisposeCount.ShouldBe(1);
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：子 Executor 绑定事务作用域失败时，应释放已创建子对象且不影响作用域提交自身事务。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_CreateExecutor_WhenBindingFails_ShouldDisposeChildAndKeepScopeUsable()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
        var invalidChild = DispatchProxy.Create<ISqlExecutor, DisposeTrackingSqlExecutorProxy>();
        var trackingProxy = (DisposeTrackingSqlExecutorProxy)(object)invalidChild;
        var executorFactory = new BindingFailureSqlExecutorFactory(invalidChild);
        var scopeFactory = new SqlTransactionScopeFactory(provider.GetRequiredService<ISqlQueryFactory>(), executorFactory);

        // Act
        using var scope = scopeFactory.Begin();
        var exception = Should.Throw<InvalidOperationException>(() => scope.CreateExecutor());
        scope.Commit();

        // Assert
        exception.Message.ShouldContain("仅支持 Dapper SQL 查询实现", Case.Insensitive);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
    /// 测试目的：事务作用域完成后，已创建的子查询应拒绝继续创建或执行独立查询描述。
    /// </summary>
    [Fact]
    public void TransactionScope_WhenLeaseExpires_ShouldRejectChildQueryExecution()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
        var scopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();
        using var scope = scopeFactory.Begin();
        var query = scope.CreateQuery();

        // Act
        scope.Commit();
        var exception = Should.Throw<InvalidOperationException>(() => query.Sql("Select 1"));

        // Assert
        exception.Message.ShouldContain("事务作用域已结束");
        connection.LastTransaction.CommitCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：公共查询对象不应暴露外部资源绑定 SPI。
    /// </summary>
    [Fact]
    public void SqlQuery_WhenCreated_ShouldNotImplementResourceBinder()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
        var query = provider.GetRequiredService<ISqlQuery>();
        // Assert
        Assert.DoesNotContain("ISqlQueryResourceBinder", query.GetType().GetInterfaces().Select(item => item.Name));
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
        before.QueryContextId.ShouldBe(after.QueryContextId);
        before.ExecutionId.ShouldBe(after.ExecutionId);
        after.Connection.DbKey.ShouldBe("primary");
        after.Transaction.TransactionId.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// 测试目的：存在 Activity 时，诊断 TraceId 和 SpanId 必须优先使用当前 Activity，不能被 Core 回退值覆盖。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenActivityExists_ShouldPreferActivityTraceAndSpan()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item,
            name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
        var connection = new CaptureDbConnection();
        using var executor = CreateExecutor(connection);
        using var activity = new Activity("sql-diagnostics").SetIdFormat(ActivityIdFormat.W3C).Start();

        // Act
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "activity" });

        // Assert
        message.ShouldNotBeNull();
        message.TraceId.ShouldBe(activity.TraceId.ToString());
        message.SpanId.ShouldBe(activity.SpanId.ToString());
    }

    /// <summary>
    /// 测试目的：仅注入 Logger 时也应创建结构化执行 Scope，不依赖 Trace 级别或 DiagnosticListener 订阅。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenOnlyLoggerIsConfigured_ShouldBeginStructuredScope()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var loggerFactory = new TraceLoggerFactory(false);
        using var query = CreateTraceQuery(connection, loggerFactory);

        // Act
        query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").Scalar();

        // Assert
        var scope = Assert.Single(loggerFactory.Scopes);
        Assert.False(string.IsNullOrWhiteSpace(scope["QueryContextId"] as string));
        Assert.False(string.IsNullOrWhiteSpace(scope["ExecutionId"] as string));
        Assert.Equal("Data", scope["Phase"]);
    }

    /// <summary>
    /// 测试目的：DiagnosticListener、Activity、Logger Scope 和 Error 事件必须共享同一 QueryContext、ExecutionId、Parent 和 Phase。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenAllDiagnosticsAreEnabled_ShouldShareExecutionIdentityOnError()
    {
        // Arrange
        var messages = new List<DiagnosticsMessage>();
        using var observer = new SqlDiagnosticObserver(messages.Add,
            name => name == SqlQueryDiagnosticListenerNames.BeforeExecute ||
                name == SqlQueryDiagnosticListenerNames.ErrorExecute);
        var loggerFactory = new TraceLoggerFactory(false);
        var connection = new CaptureDbConnection { ThrowOnScalarExecute = true };
        using var query = CreateTraceQuery(connection, loggerFactory);
        using var activity = new Activity("sql-diagnostics-combined")
            .SetIdFormat(ActivityIdFormat.W3C).Start();

        // Act
        Assert.Throws<InvalidOperationException>(() => query.Query<int>()
            .AppendSelect("Count(*)").AppendFrom("[Users]").Scalar());

        // Assert
        var before = Assert.Single(messages.Where(item => item.Operation ==
            SqlQueryDiagnosticListenerNames.BeforeExecute));
        var error = Assert.Single(messages.Where(item => item.Operation ==
            SqlQueryDiagnosticListenerNames.ErrorExecute));
        var scope = Assert.Single(loggerFactory.Scopes);
        Assert.Equal(before.QueryContextId, error.QueryContextId);
        Assert.Equal(before.QueryContextId, scope["QueryContextId"]);
        Assert.Equal(before.ParentQueryContextId, error.ParentQueryContextId);
        Assert.Equal(before.ParentQueryContextId, scope["ParentQueryContextId"]);
        Assert.Equal(before.ExecutionId, error.ExecutionId);
        Assert.Equal(before.ExecutionId, scope["ExecutionId"]);
        Assert.Equal(before.Phase, error.Phase);
        Assert.Equal(before.Phase, scope["Phase"]);
        Assert.Equal(before.QueryContextId, activity.GetTagItem("bing.sql.query_context_id"));
        Assert.Equal(before.ExecutionId, activity.GetTagItem("bing.sql.execution_id"));
        Assert.Equal(before.Phase, activity.GetTagItem("bing.sql.phase"));
    }

    /// <summary>
    /// 测试目的：未启用 DiagnosticListener、Activity 和 Logger 时，不应创建执行前诊断消息。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenAllDiagnosticsAreDisabled_ShouldNotCreateExecutionMessage()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateSqlServerTestRoot<CountingDiagnosticsSqlServerQuery>(
            CreateSqlServerTestProvider(), options => options.Connection(connection));

        // Act
        query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").Scalar();

        // Assert
        Assert.Equal(0, query.BeforeCount);
    }

    /// <summary>
    /// 测试目的：没有 Activity 时，诊断 TraceId、SpanId 和 CorrelationId 应使用 Core 关联标识提供程序。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenActivityIsMissing_ShouldUseCorrelationProvider()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item,
            name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
        var connection = new CaptureDbConnection();
        var provider = CreateSqlServerTestProvider(new FixedCorrelationIdProvider("correlation-id"));
        using var executor = CreateSqlServerTestRoot<InspectableSqlServerExecutor>(provider,
            options => options.Connection(connection));

        // Act
        executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "correlation" });

        // Assert
        message.ShouldNotBeNull();
        message.TraceId.ShouldBe("correlation-id");
        message.SpanId.ShouldBe("correlation-id");
        message.CorrelationId.ShouldBe("correlation-id");
    }

    /// <summary>
    /// 测试目的：没有 Activity 和 Correlation Provider 时，诊断应回退到 TraceIdContext 的链路标识。
    /// </summary>
    [Fact]
    public void ExecuteSql_WhenActivityAndCorrelationProviderAreMissing_ShouldUseTraceContext()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item,
            name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
        var connection = new CaptureDbConnection();
        using var executor = CreateExecutor(connection);
        var previous = TraceIdContext.Current;
        TraceIdContext.Current = new TraceIdContext("trace-context", "root", "parent", "child");

        try
        {
            // Act
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "trace-context" });
        }
        finally
        {
            TraceIdContext.Current = previous;
        }

        // Assert
        message.ShouldNotBeNull();
        message.TraceId.ShouldBe("trace-context");
        message.SpanId.ShouldBe("child");
        message.CorrelationId.ShouldBe("trace-context");
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
    /// 测试目的：提交开始后必须原子阻止相反完成操作和新的子对象创建，且只允许一次底层提交。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenCommitIsInProgress_ShouldRejectConcurrentRollbackAndChildCreation()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        using var commitStarted = new ManualResetEventSlim();
        using var allowCommit = new ManualResetEventSlim();
        connection.LastTransaction.OnCommit = () =>
        {
            commitStarted.Set();
            allowCommit.Wait();
        };

        // Act
        var commitTask = Task.Run(scope.Commit);
        commitStarted.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
        var duplicateCommitException = Should.Throw<InvalidOperationException>(() => scope.Commit());
        var rollbackException = Should.Throw<InvalidOperationException>(() => scope.Rollback());
        var createException = Should.Throw<InvalidOperationException>(() => scope.CreateExecutor());
        allowCommit.Set();
        await commitTask;

        // Assert
        duplicateCommitException.Message.ShouldContain("正在完成");
        rollbackException.Message.ShouldContain("正在完成");
        createException.Message.ShouldContain("已结束");
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
        scope.Commit();
        connection.LastTransaction.CommitCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：异步提交尚未完成时，同步和异步释放都必须拒绝并发执行，避免调用方误判资源已释放。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenCommitAsyncIsInProgress_ShouldRejectConcurrentDispose()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
        await using var scope = await provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        var commitStarted = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCommit = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.LastTransaction.OnCommitAsync = async () =>
        {
            commitStarted.TrySetResult(null);
            await allowCommit.Task;
        };

        // Act
        var commitTask = scope.CommitAsync();
        await commitStarted.Task;
        var disposeException = Should.Throw<InvalidOperationException>(scope.Dispose);
        var disposeAsyncException = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());
        allowCommit.TrySetResult(null);
        await commitTask;

        // Assert
        disposeException.Message.ShouldContain("正在完成");
        disposeAsyncException.Message.ShouldContain("正在完成");
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.AsyncRollbackCount.ShouldBe(0);
        connection.LastTransaction.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：事务回滚后重复回滚应保持幂等，提交应被拒绝，资源只应释放一次。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenRolledBack_ShouldBeIdempotentAndRejectOppositeCompletion()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);

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
        using var provider = CreateSqlServerScopeProvider(connection);

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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerOwnedScopeProvider(connection);

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync());

        // Assert
        exception.Flatten().InnerExceptions.Select(item => item.Message)
            .ShouldBe(new[] { "async begin failed", "connection dispose failed" }, ignoreOrder: true);
        connection.DisposeCount.ShouldBe(1);
        connection.AsyncDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：同步事务开始失败且 Owner Query 清理失败时，应按开始异常、清理异常的顺序聚合保留两个失败原因。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenBeginAndOwnerQueryCleanupFail_ShouldAggregateFailures()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnBegin = true, ThrowOnDispose = true };
        using var provider = CreateSqlServerOwnedScopeProvider(connection);

        // Act
        var exception = Assert.Throws<AggregateException>(() =>
            provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin());

        // Assert
        exception.Flatten().InnerExceptions.Select(item => item.Message)
            .ShouldBe(new[] { "begin failed", "connection dispose failed" });
        connection.DisposeCount.ShouldBe(1);
        connection.AsyncDisposeCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试 - 预先取消异步事务开始时不应打开连接或创建事务。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenBeginAsyncIsCancelled_ShouldNotOpenConnectionOrCreateTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
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
        using var provider = CreateSqlServerScopeProvider(connection);
        var scopeFactory = provider.GetRequiredService<ISqlTransactionScopeFactory>();

        // Act
        using var scope = scopeFactory.Begin(null, IsolationLevel.Serializable);

        // Assert
        scope.DatabaseType.ShouldBe(DatabaseType.SqlServer);
        scope.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
        var runtime = (ISqlTransactionScopeRuntime)scope;
        runtime.Connection.ShouldBeSameAs(connection);
        runtime.Transaction.ShouldBeSameAs(connection.LastTransaction);
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
        using var provider = CreateSqlServerScopeProvider(connection, metadataOptions);
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
            query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").Scalar().ShouldBe(1);
            executor.ExecuteSql("Update [Users] Set [Name]=@name", new { name = "scope" }).ShouldBe(1);
            var runtime = (ISqlTransactionScopeRuntime)transactionScope;
            runtime.Connection.ShouldBeSameAs(connection);
            runtime.Transaction.ShouldBeSameAs(connection.LastTransaction);
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
        using var provider = CreateSqlServerScopeProvider(connection);
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        var query = scope.CreateQuery();

        // Act
        query.Dispose();
        scope.Rollback();

        // Assert
        connection.State.ShouldBe(ConnectionState.Open);
        Should.Throw<InvalidOperationException>(() => query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").Scalar());
    }

    /// <summary>
    /// 测试目的：子查询流式读取持有执行租约时，事务 Scope 不得提交或提前释放事务资源。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenChildStreamIsActive_ShouldRejectCommitBeforeResourceSideEffects()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" })
        };
        using var provider = CreateSqlServerScopeProvider(connection);
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        var query = scope.CreateQuery();

        // Act
        using (var enumerator = query.Query<MappedSample>().Select("Id,Name").From("Users").AsEnumerable().GetEnumerator())
        {
            enumerator.MoveNext().ShouldBeTrue();
            var exception = Should.Throw<InvalidOperationException>(() => scope.Commit());

            // Assert
            exception.Message.ShouldBe("当前 SQL Query 或 Executor 正在执行，不能释放 Root 对象。");
            connection.LastTransaction.CommitCount.ShouldBe(0);
            connection.LastTransaction.RollbackCount.ShouldBe(0);
        }

        scope.Commit();

        // Assert
        connection.LastTransaction.CommitCount.ShouldBe(1);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：子查询流式读取持有执行租约时，Scope 同步释放必须拒绝且不得提前回滚、失效租约或释放资源。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenChildStreamIsActive_ShouldRejectDisposeBeforeResourceSideEffects()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" })
        };
        using var provider = CreateSqlServerScopeProvider(connection);
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        var query = scope.CreateQuery();

        // Act
        using (var enumerator = query.Query<MappedSample>().Select("Id,Name").From("Users").AsEnumerable().GetEnumerator())
        {
            enumerator.MoveNext().ShouldBeTrue();
            var exception = Should.Throw<InvalidOperationException>(scope.Dispose);

            // Assert
            exception.Message.ShouldBe("当前 SQL Query 或 Executor 正在执行，不能释放 Root 对象。");
            scope.IsCompleted.ShouldBeFalse();
            connection.LastTransaction.CommitCount.ShouldBe(0);
            connection.LastTransaction.RollbackCount.ShouldBe(0);
            connection.LastTransaction.DisposeCount.ShouldBe(0);
        }

        scope.Commit();
        connection.LastTransaction.CommitCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：子查询异步流式读取持有执行租约时，Scope 异步释放必须拒绝且不改变事务状态；释放读取器后可正常异步提交。
    /// </summary>
    [Fact]
    public async Task SqlTransactionScope_WhenChildAsyncStreamIsActive_ShouldRejectDisposeAsyncBeforeResourceSideEffects()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" })
        };
        using var provider = CreateSqlServerScopeProvider(connection);
        await using var scope = await provider.GetRequiredService<ISqlTransactionScopeFactory>().BeginAsync();
        var query = scope.CreateQuery();

        // Act
        await using (var enumerator = query.Query<MappedSample>().Select("Id,Name").From("Users")
                         .AsAsyncEnumerable().GetAsyncEnumerator())
        {
            (await enumerator.MoveNextAsync()).ShouldBeTrue();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());

            // Assert
            exception.Message.ShouldBe("当前 SQL Query 或 Executor 正在执行，不能释放 Root 对象。");
            scope.IsCompleted.ShouldBeFalse();
            connection.LastTransaction.AsyncCommitCount.ShouldBe(0);
            connection.LastTransaction.AsyncRollbackCount.ShouldBe(0);
            connection.LastTransaction.DisposeCount.ShouldBe(0);
        }

        await scope.CommitAsync();
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试 - Scope处于活动状态时已释放子对象不应重新创建独立资源或脱离事务执行。
    /// </summary>
    [Fact]
    public void SqlTransactionScope_WhenChildIsDisposedWhileActive_ShouldRejectFurtherResourceAccessAndExecution()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var provider = CreateSqlServerScopeProvider(connection);
        using var scope = provider.GetRequiredService<ISqlTransactionScopeFactory>().Begin();
        var query = scope.CreateQuery();
        var executor = scope.CreateExecutor();

        // Act
        query.Dispose();
        executor.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").Scalar());
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
        var description = query.From<MappedSample>()
            .Where<MappedSample, string>(t => t.Name, "abc")
            .Aggregate<MappedSample>(SqlAggregateFunction.Count, t => t.Id);

        // Act
        var result = await description.ScalarAsync<int>();

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
        var description = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var result = description.Scalar();

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
        var description = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var result = await description.ScalarAsync();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：独立查询描述的异步终结必须使用原生异步短事务开始和提交成员。
    /// </summary>
    [Fact]
    public async Task ExecuteScalarAsync_WhenPrimaryReadTransactionSucceeds_ShouldUseNativeAsyncTransactionMembers()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateOwnedQuery(connection);
        ConfigurePrimaryReadTransaction(query);
        var description = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var result = await description.ScalarAsync();

        // Assert
        result.ShouldBe(1);
        connection.AsyncBeginCount.ShouldBe(1);
        connection.LastTransaction.AsyncCommitCount.ShouldBe(1);
        connection.LastTransaction.CommitCount.ShouldBe(0);
        connection.LastTransaction.RollbackCount.ShouldBe(0);
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var result = description.AsEnumerable().ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：同步流取得后应冻结 Fluent 查询描述，后续追加条件必须立即拒绝。
    /// </summary>
    [Fact]
    public void AsEnumerable_WhenDescriptionChangesAfterStreamCreation_ShouldUseInitialBuilderSnapshot()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "stream" })
        };
        using var query = CreateOwnedQuery(connection);
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]").Where("[Enabled]", true);
        var stream = description.AsEnumerable();
        var exception = Assert.Throws<InvalidOperationException>(() => description.Where("[Name]",
            "changed-after-stream-creation"));

        // Act
        var result = stream.ToList();

        // Assert
        Assert.Equal("查询已冻结，不能继续修改查询描述。", exception.Message);
        Assert.Single(result);
        Assert.Equal("Select [Id],[Name] \r\nFrom [Users] \r\nWhere [Enabled]=@_p_0", connection.LastCommandText);
        Assert.Single(connection.LastCreatedParameters);
        Assert.Equal("@_p_0", connection.LastCreatedParameters[0].ParameterName);
        Assert.Equal(true, connection.LastCreatedParameters[0].Value);
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var result = new List<MappedSample>();
        await foreach (var item in description.AsAsyncEnumerable())
            result.Add(item);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(0, query.Counters.ToDebugSqlCallCount);
        Assert.Null(query.TraceSql);
    }

    /// <summary>
    /// 测试目的：异步流取得后应冻结 Fluent 查询描述，后续追加条件必须立即拒绝。
    /// </summary>
    [Fact]
    public async Task AsAsyncEnumerable_WhenDescriptionChangesAfterStreamCreation_ShouldUseInitialBuilderSnapshot()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "async-stream" })
        };
        using var query = CreateOwnedQuery(connection);
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]").Where("[Enabled]", true);
        var stream = description.AsAsyncEnumerable();
        var exception = Assert.Throws<InvalidOperationException>(() => description.Where("[Name]",
            "changed-after-stream-creation"));

        // Act
        var result = new List<MappedSample>();
        await foreach (var item in stream)
            result.Add(item);

        // Assert
        Assert.Equal("查询已冻结，不能继续修改查询描述。", exception.Message);
        Assert.Single(result);
        Assert.Equal("Select [Id],[Name] \r\nFrom [Users] \r\nWhere [Enabled]=@_p_0", connection.LastCommandText);
        Assert.Single(connection.LastCreatedParameters);
        Assert.Equal("@_p_0", connection.LastCreatedParameters[0].ParameterName);
        Assert.Equal(true, connection.LastCreatedParameters[0].Value);
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
        var syncDescription = syncQuery.Query<int>().Select("Count(*)").From("[Users]");

        // Act
        var syncResult = syncDescription.Scalar();

        // Assert
        Assert.Equal(1, syncResult);
        Assert.Equal(1, syncQuery.Counters.ToSqlCallCount);
        Assert.Equal(0, syncQuery.Counters.ToDebugSqlCallCount);

        // Arrange
        var asyncConnection = new CaptureDbConnection();
        using var asyncQuery = CreateCountingQuery(asyncConnection, false);
        var asyncDescription = asyncQuery.Query<int>().Select("Count(*)").From("[Users]");

        // Act
        var asyncResult = await asyncDescription.ScalarAsync();

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
        var description = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]")
            .AppendWhere("[Name]=@name").AddParam("name", "trace");

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(1, query.Counters.ToDebugSqlCallCount);
        Assert.Equal(query.TraceSql, query.Counters.LastDebugSqlInput);
        Assert.Equal("Select Count(*) \r\nFrom [Users] \r\nWhere [Name]='trace'", query.TraceDebugSql);
    }

    /// <summary>
    /// 测试目的：Trace 日志应遮蔽敏感参数，避免调试 SQL 或参数清单泄露令牌值。
    /// </summary>
    [Fact]
    public void ExecuteScalar_WhenTraceContainsSensitiveParameter_ShouldRedactDebugSqlAndParameterLog()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        var loggerFactory = new TraceLoggerFactory(true);
        using var query = CreateTraceQuery(connection, loggerFactory);
        var description = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").AppendWhere("[ApiToken]=@ApiToken")
            .AddParam("ApiToken", "super-secret-token");

        // Act
        var result = description.Scalar();

        // Assert
        Assert.Equal(1, result);
        var log = Assert.Single(loggerFactory.Messages);
        Assert.Contains("Where [ApiToken]='<redacted>'", log);
        Assert.Contains("@ApiToken : '<redacted>' : <redacted>", log);
        Assert.DoesNotContain("super-secret-token", log);
    }

    /// <summary>
    /// 测试目的：原生文本查询的字典参数诊断应按真实参数名识别并脱敏敏感值。
    /// </summary>
    [Fact]
    public void SqlTextQuery_WhenDictionaryContainsSensitiveParameter_ShouldRedactDiagnosticValue()
    {
        // Arrange
        DiagnosticsMessage before = null;
        using var observer = new SqlDiagnosticObserver(message =>
        {
            if (message.Operation == SqlQueryDiagnosticListenerNames.BeforeExecute)
                before = message;
        }, name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
        var connection = new CaptureDbConnection { ScalarResult = 1 };
        using var query = CreateQuery(connection);

        // Act
        var result = query.Sql("Select @ApiToken", new Dictionary<string, object>
        {
            ["ApiToken"] = "super-secret-token"
        }).Scalar<int>();

        // Assert
        Assert.Equal(1, result);
        Assert.NotNull(before);
        var parameter = Assert.Single(before.Parameters.Items);
        Assert.Equal("ApiToken", parameter.Name);
        Assert.True(parameter.IsSensitive);
        Assert.Null(parameter.Value);
        Assert.Null(parameter.OriginalValue);
    }

    /// <summary>
    /// 测试目的：常见凭据别名也必须在原生参数诊断中脱敏，普通业务参数仍应保留其值。
    /// </summary>
    [Fact]
    public void SqlTextQuery_WhenDictionaryContainsCredentialAliases_ShouldRedactDiagnosticValues()
    {
        // Arrange
        DiagnosticsMessage before = null;
        using var observer = new SqlDiagnosticObserver(message =>
        {
            if (message.Operation == SqlQueryDiagnosticListenerNames.BeforeExecute)
                before = message;
        }, name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
        var connection = new CaptureDbConnection { ScalarResult = 1 };
        using var query = CreateQuery(connection);

        // Act
        var result = query.Sql("Select @pwd, @ClientCredential, @Authorization, @Signature, @Name",
            new Dictionary<string, object>
            {
                ["pwd"] = "database-password",
                ["ClientCredential"] = "client-credential",
                ["Authorization"] = "Bearer access-token",
                ["Signature"] = "request-signature",
                ["Name"] = "Bing"
            }).Scalar<int>();

        // Assert
        result.ShouldBe(1);
        before.ShouldNotBeNull();
        foreach (var name in new[] { "pwd", "ClientCredential", "Authorization", "Signature" })
        {
            var parameter = before.Parameters.Items.Single(item => item.Name == name);
            parameter.IsSensitive.ShouldBeTrue();
            parameter.Value.ShouldBeNull();
            parameter.OriginalValue.ShouldBeNull();
        }
        var nonSensitive = before.Parameters.Items.Single(item => item.Name == "Name");
        nonSensitive.IsSensitive.ShouldBeFalse();
        nonSensitive.Value.ShouldBe("Bing");
        nonSensitive.OriginalValue.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：独立查询描述不得继承或清空 Root Query 已预配置的 Builder 状态。
    /// </summary>
    [Fact]
    public void SqlQueryDescription_WhenRootBuilderIsPreconfigured_ShouldKeepRootStateIsolated()
    {
        // Arrange
        var connection = new CaptureDbConnection { ScalarResult = 1 };
        using var query = CreateQuery(connection);
        query.ConfigureRootSql();
        var rootSql = query.RootSql;
        var rootParameters = query.RootParameters;

        // Act
        var result = query.Query<int>().Select("Count(*)").From("[Users]").Scalar();

        // Assert
        Assert.Equal(1, result);
        Assert.Equal(rootSql, query.RootSql);
        Assert.Equal(rootParameters, query.RootParameters);
        Assert.Contains("[RootUsers]", query.RootSql);
        Assert.DoesNotContain("[Users]", query.RootSql);
    }

    /// <summary>
    /// 测试目的：同步和异步流式查询的前置钩子失败时，均应执行统一的完成清理钩子。
    /// </summary>
    [Fact]
    public async Task QueryPlanStream_WhenBeforeHookThrows_ShouldRunCompletionHook()
    {
        // Arrange
        using var provider = CreateSqlServerTestProvider();
        using var syncQuery = CreateSqlServerTestRoot<ThrowingBeforeSqlServerQuery>(provider,
            options => options.Connection(new CaptureDbConnection()));

        // Act
        var syncException = Should.Throw<InvalidOperationException>(() =>
            syncQuery.Sql("Select 1").AsEnumerable<int>().ToList());
        using var asyncQuery = CreateSqlServerTestRoot<ThrowingBeforeSqlServerQuery>(provider,
            options => options.Connection(new CaptureDbConnection()));
        var asyncException = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in asyncQuery.Sql("Select 1").AsAsyncEnumerable<int>())
            {
            }
        });

        // Assert
        syncException.Message.ShouldBe("before failed");
        asyncException.Message.ShouldBe("before failed");
        syncQuery.AfterCount.ShouldBe(1);
        asyncQuery.AfterCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：自动分页的数据查询失败时，应回滚 Count 阶段创建的主库短事务，
    /// 并恢复调用方 Pager 的未知总数状态，避免后续执行错误跳过 Count。
    /// </summary>
    [Fact]
    public void ToPage_WhenDataExecutionFails_ShouldRollbackTransactionAndRestorePager()
    {
        // Arrange
        var connection = new CaptureDbConnection { ThrowOnExecute = true };
        using var query = CreateOwnedQuery(connection);
        ConfigurePrimaryReadTransaction(query);
        var pager = new Pager(1, 10, "Id");
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act and Assert
        var exception = Assert.Throws<InvalidOperationException>(() => description.ToPage(pager));

        // Assert
        Assert.Equal("execute failed", exception.Message);
        Assert.Equal(1, connection.LastTransaction.RollbackCount);
        Assert.Equal(0, pager.TotalCount);
        Assert.False(pager.IsTotalCountKnown);
    }

    /// <summary>
    /// 测试目的：Count 完成后、Data 执行前发生取消时，应回滚主库短事务，
    /// 不得让执行租约释放掩盖未完成事务。
    /// </summary>
    [Fact]
    public async Task ToPageAsync_WhenCancelledAfterCount_ShouldRollbackTransactionAndRestorePager()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var connection = new CaptureDbConnection { OnScalarExecuted = cancellationTokenSource.Cancel };
        using var query = CreateOwnedQuery(connection);
        ConfigurePrimaryReadTransaction(query);
        var pager = new Pager(1, 10, "Id");
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act and Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            description.ToPageAsync(pager, cancellationToken: cancellationTokenSource.Token));

        // Assert
        Assert.Equal(1, connection.LastTransaction.AsyncRollbackCount);
        Assert.Equal(0, connection.LastTransaction.RollbackCount);
        Assert.Equal(0, connection.ReaderCreateCount);
        Assert.Equal(0, pager.TotalCount);
        Assert.False(pager.IsTotalCountKnown);
    }

    /// <summary>
    /// 测试目的：Data 阶段被执行前 Hook 跳过时，分页执行仍应完成 Count 阶段启动的主库短事务，
    /// 防止 Root Query 持有遗留事务。
    /// </summary>
    [Fact]
    public void ToPage_WhenDataExecutionIsSkipped_ShouldCompleteCountTransaction()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateSecondPlanSkippingQuery(connection);
        ConfigurePrimaryReadTransaction(query);
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var page = description.ToPage(new Pager(1, 10, "Id"));

        // Assert
        Assert.Equal(1, page.TotalCount);
        Assert.Empty(page.Data);
        Assert.Equal(1, connection.LastTransaction.CommitCount);
        Assert.Equal(0, connection.LastTransaction.RollbackCount);
    }

    /// <summary>
    /// 测试目的：分页 Count 执行期间修改已冻结查询描述时必须拒绝，防止 Count 与 Data 形成隐式分叉。
    /// </summary>
    [Fact]
    public void ToPage_WhenCountCallbackMutatesDescription_ShouldRejectMutation()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateOwnedQuery(connection);
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]").Where("[Enabled]", true);
        connection.OnScalarExecuted = () => description.Where("[Name]", "changed-after-count");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => description.ToPage(new Pager(1, 10, "Id")));

        // Assert
        Assert.Equal("查询已冻结，不能继续修改查询描述。", exception.Message);
        Assert.Single(connection.ExecutedCommandTexts);
    }

    /// <summary>
    /// 测试目的：分页的 Count 完成后即使全局逻辑删除过滤器被禁用，数据页仍必须保留分页开始时冻结的数据边界。
    /// </summary>
    [Fact]
    public void ToPage_WhenFilterChangesAfterCount_ShouldUseInitialFilterSnapshot()
    {
        // Arrange
        var dataFilter = new DataFilter();
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "active" })
        };
        var services = new ServiceCollection();
        services.AddSingleton<IDataFilter>(dataFilter);
        services.AddSqlServerProvider();
        using var provider = services.BuildServiceProvider();
        using var query = CreateSqlServerTestRoot<InspectableSqlServerQuery>(provider,
            options =>
            {
                options.Connection(connection);
                options.QueryCapabilities = new SqlQueryCapabilities
                {
                    Pagination = SqlQueryCapabilityState.Supported
                };
            });
        var description = query.From<SoftDeleteMappedSample>();
        IDisposable disabledScope = null;
        connection.OnScalarExecuted = () => disabledScope = dataFilter.Disable<IsDeletedFilter>();

        // Act
        var result = description.ToPage<SoftDeleteMappedSample>(new Pager(1, 10, "Id"));
        disabledScope?.Dispose();

        // Assert
        Assert.Single(result.Data);
        Assert.Equal(2, connection.ExecutedCommandTexts.Count);
        Assert.Equal(
            "Select Count(*) \r\nFrom [SoftDeleteMappedSample] \r\nWhere [SoftDeleteMappedSample].[IsDeleted]=@_p_0",
            connection.ExecutedCommandTexts[0]);
        Assert.Equal(
            "Select [SoftDeleteMappedSample].[Id],[SoftDeleteMappedSample].[Name],[SoftDeleteMappedSample].[IsDeleted] \r\nFrom [SoftDeleteMappedSample] \r\nWhere [SoftDeleteMappedSample].[IsDeleted]=@_p_0 \r\nOrder By [Id] \r\nOffset @_p_1 Rows Fetch Next @_p_2 Rows Only",
            connection.ExecutedCommandTexts[1]);
    }

    /// <summary>
    /// 测试目的：Count 成功后的回调修改调用方 Pager 时，数据页和返回值仍应使用调用开始时的分页输入，
    /// 且仅将成功计算的总数回写到调用方。
    /// </summary>
    [Fact]
    public void ToPage_WhenCountCallbackMutatesPager_ShouldUseInitialPagerSnapshot()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateOwnedQuery(connection);
        var pager = new Pager(1, 10, "Id");
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");
        connection.OnScalarExecuted = () =>
        {
            pager.Order = "Name Desc";
            pager.PageSize = 1;
        };

        // Act
        var result = description.ToPage(pager);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Id", result.Order);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(1, pager.TotalCount);
        Assert.Equal("Name Desc", pager.Order);
        Assert.Equal(1, pager.PageSize);
        Assert.Contains("Order By [Id]", connection.LastCommandText);
        Assert.DoesNotContain("Order By [Name] Desc", connection.LastCommandText);
    }

    /// <summary>
    /// 测试目的：异步分页 Count 执行期间修改已冻结查询描述时必须拒绝。
    /// </summary>
    [Fact]
    public async Task ToPageAsync_WhenCountCallbackMutatesDescription_ShouldRejectMutation()
    {
        // Arrange
        var connection = new CaptureDbConnection();
        using var query = CreateOwnedQuery(connection);
        var pager = new Pager(1, 10, "Id");
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]").Where("[Enabled]", true);
        connection.OnScalarExecuted = () =>
        {
            description.Where("[Name]", "changed-after-count");
            pager.Order = "Name Desc";
        };

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => description.ToPageAsync(pager));

        // Assert
        Assert.Equal("查询已冻结，不能继续修改查询描述。", exception.Message);
        Assert.Single(connection.ExecutedCommandTexts);
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]").Where("[Name]", "async-trace");

        // Act
        var result = await description.ToListAsync();

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
        var syncDescription = syncQuery.Query<int>().Select("Count(*)").From("[Users]").Where("[Enabled]", true);

        // Act
        var syncResult = syncDescription.Scalar();

        // Assert
        Assert.Equal(1, syncResult);
        Assert.Equal(1, syncQuery.Counters.ToSqlCallCount);
        Assert.Equal(1, syncQuery.Counters.ToDebugSqlCallCount);
        Assert.Equal(syncQuery.TraceSql, syncQuery.Counters.LastDebugSqlInput);

        // Arrange
        var asyncConnection = new CaptureDbConnection();
        using var asyncQuery = CreateCountingQuery(asyncConnection, true);
        var asyncDescription = asyncQuery.Query<int>().Select("Count(*)").From("[Users]").Where("[Enabled]", true);

        // Act
        var asyncResult = await asyncDescription.ScalarAsync();

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
        var syncDescription = syncQuery.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var syncResult = syncDescription.AsEnumerable().ToList();

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
        var asyncDescription = asyncQuery.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var asyncResult = new List<MappedSample>();
        await foreach (var item in asyncDescription.AsAsyncEnumerable())
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
        var syncDescription = syncQuery.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var syncResult = syncDescription.ToPage(new Pager(1, 20, "Id"));

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
        var asyncDescription = asyncQuery.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var asyncResult = await asyncDescription.ToPageAsync(new Pager(1, 20, "Id"));

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
        var description = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => description.Scalar());

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
        var description = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => description.ScalarAsync());

        // Assert
        Assert.Equal("execute failed", exception.Message);
        Assert.Equal(1, query.Counters.ToSqlCallCount);
        Assert.Equal(1, query.Counters.ToDebugSqlCallCount);
        Assert.Equal(query.TraceSql, query.Counters.LastDebugSqlInput);
        Assert.Same(exception, errorMessage.Exception);
    }

    /// <summary>
    /// 测试目的：查询执行失败后，回滚失败不得覆盖原始执行异常，应按主异常在前的顺序聚合。
    /// </summary>
    [Fact]
    public void QueryPlan_WhenOperationAndRollbackFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ThrowOnScalarExecute = true,
            ThrowOnTransactionRollback = true
        };
        using var query = CreateOwnedQuery(connection);
        ConfigurePrimaryReadTransaction(query);

        // Act
        var exception = Assert.Throws<AggregateException>(() => query.Sql("Select Count(*) From [Users]").Scalar<int>());

        // Assert
        Assert.Equal(new[] { "execute failed", "rollback failed" }, exception.Flatten().InnerExceptions
            .Select(item => item.Message));
        Assert.Equal(1, connection.LastTransaction.RollbackCount);
    }

    /// <summary>
    /// 测试目的：异步查询执行失败后，回滚失败不得覆盖原始执行异常，应按主异常在前的顺序聚合。
    /// </summary>
    [Fact]
    public async Task QueryPlanAsync_WhenOperationAndRollbackFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ThrowOnScalarExecute = true,
            ThrowOnTransactionRollback = true
        };
        using var query = CreateOwnedQuery(connection);
        ConfigurePrimaryReadTransaction(query);

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            query.Sql("Select Count(*) From [Users]").ScalarAsync<int>());

        // Assert
        Assert.Equal(new[] { "execute failed", "rollback failed" }, exception.Flatten().InnerExceptions
            .Select(item => item.Message));
        Assert.Equal(1, connection.LastTransaction.AsyncRollbackCount);
        Assert.Equal(0, connection.LastTransaction.RollbackCount);
    }

    /// <summary>
    /// 测试目的：诊断错误钩子失败不得覆盖原始执行异常，应作为清理异常保留。
    /// </summary>
    [Fact]
    public void QueryPlan_WhenOperationAndErrorHookFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        using var provider = CreateSqlServerTestProvider();
        using var query = CreateSqlServerTestRoot<ThrowingErrorSqlServerQuery>(provider,
            options => options.Connection(new CaptureDbConnection { ThrowOnScalarExecute = true }));

        // Act
        var exception = Assert.Throws<AggregateException>(() => query.Sql("Select Count(*) From [Users]").Scalar<int>());

        // Assert
        Assert.Equal(new[] { "execute failed", "error hook failed" }, exception.Flatten().InnerExceptions
            .Select(item => item.Message));
    }

    /// <summary>
    /// 测试目的：查询完成钩子失败不得覆盖原始执行异常，应作为清理异常保留。
    /// </summary>
    [Fact]
    public void QueryPlan_WhenOperationAndCompletionHookFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        using var provider = CreateSqlServerTestProvider();
        using var query = CreateSqlServerTestRoot<ThrowingCompletionSqlServerQuery>(provider,
            options => options.Connection(new CaptureDbConnection { ThrowOnScalarExecute = true }));

        // Act
        var exception = Assert.Throws<AggregateException>(() => query.Sql("Select Count(*) From [Users]").Scalar<int>());

        // Assert
        Assert.Equal(new[] { "execute failed", "completion hook failed" }, exception.Flatten().InnerExceptions
            .Select(item => item.Message));
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("[Users]");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in description.AsAsyncEnumerable())
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
        // Act
        var result = query.Procedure<int>("usp_users_count", new { name = "abc" }).ExecuteScalar();

        // Assert
        result.Result.ShouldBe(7);
        connection.LastCommandText.ShouldBe("usp_users_count");
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        connection.LastCreatedParameters.Count.ShouldBe(1);
        connection.LastCreatedParameters[0].Value.ShouldBe("abc");
    }

    /// <summary>
    /// 测试目的：过程描述必须冻结嵌套字典、集合和数组，调用方后续修改不得污染本次过程执行输入。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenNestedParameterContainersChange_ShouldKeepIndependentSnapshots()
    {
        // Arrange
        using var query = CreateQuery(new CaptureDbConnection());
        var payload = new byte[] { 1, 2 };
        var identifiers = new List<int> { 3, 4 };
        var parameters = new Dictionary<string, object>
        {
            ["filter"] = new Dictionary<string, object>
            {
                ["Payload"] = payload,
                ["Identifiers"] = identifiers
            }
        };

        // Act
        var description = query.Procedure<int>("usp_report", parameters);
        payload[0] = 9;
        identifiers[0] = 8;
        ((IDictionary<string, object>)parameters["filter"])["Payload"] = new byte[] { 7 };

        // Assert
        var filter = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(description.Parameters)["filter"]);
        Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<byte[]>(filter["Payload"]));
        Assert.Equal(new object[] { 3, 4 }, Assert.IsType<object[]>(filter["Identifiers"]));
    }

    /// <summary>
    /// 测试目的：直接过程执行必须在同一结果对象中返回受影响行数和本次输出参数，
    /// 不得通过 Executor 的最近一次可变状态读取输出值。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenOutputParameterConfigured_ShouldReturnExecutionResult()
    {
        // Arrange
        var connection = new CaptureDbConnection { NonQueryResult = 3 };
        connection.OutputParameterValues["result"] = 7;
        using var executor = CreateExecutor(connection);
        var parameters = new SqlParameterCollection().AddOutput("result", DbType.Int32);

        // Act
        var result = executor.ExecuteProcedure("usp_update", parameters);

        // Assert
        result.Result.ShouldBe(3);
        result.OutputParameters.ShouldNotBeNull();
        result.OutputParameters.GetValue<int>("result").ShouldBe(7);
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
    }

    /// <summary>
    /// 测试目的：原生 Dapper DynamicParameters 包含输出参数时，框架也必须在过程结果中返回本次执行的值快照，
    /// 不得静默丢失输出参数。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenDynamicParametersContainOutput_ShouldReturnSnapshot()
    {
        // Arrange
        var connection = new CaptureDbConnection { NonQueryResult = 3 };
        connection.OutputParameterValues["result"] = 7;
        using var executor = CreateExecutor(connection);
        var parameters = new DynamicParameters();
        parameters.Add("result", dbType: DbType.Int32, direction: ParameterDirection.Output);

        // Act
        var result = executor.ExecuteProcedure("usp_update", parameters);

        // Assert
        result.Result.ShouldBe(3);
        result.OutputParameters.ShouldNotBeNull();
        result.OutputParameters.GetValue<int>("result").ShouldBe(7);
    }

    /// <summary>
    /// 测试目的：原生 Dapper 参数快照只能公开 Output、InputOutput 和 ReturnValue，普通 Input 参数不得混入过程结果。
    /// </summary>
    [Fact]
    public void ExecuteProcedure_WhenDynamicParametersContainMixedDirections_ShouldExposeOnlyOutputDirections()
    {
        // Arrange
        var connection = new CaptureDbConnection { NonQueryResult = 3 };
        connection.OutputParameterValues["result"] = 7;
        connection.OutputParameterValues["state"] = 2;
        connection.OutputParameterValues["return_code"] = 9;
        using var executor = CreateExecutor(connection);
        var parameters = new DynamicParameters();
        parameters.Add("input", "request", DbType.String, ParameterDirection.Input);
        parameters.Add("result", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("state", 1, DbType.Int32, ParameterDirection.InputOutput);
        parameters.Add("return_code", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

        // Act
        var result = executor.ExecuteProcedure("usp_update", parameters);

        // Assert
        result.Result.ShouldBe(3);
        result.OutputParameters.GetValue<int>("@result").ShouldBe(7);
        result.OutputParameters.GetValue<int>(":state").ShouldBe(2);
        result.OutputParameters.GetValue<int>("?return_code").ShouldBe(9);
        result.OutputParameters.TryGetValue<string>("input", out _).ShouldBeFalse();
        Should.Throw<KeyNotFoundException>(() => result.OutputParameters.GetValue<string>("input"));
    }

    /// <summary>
    /// 测试目的：异步过程执行也只能公开本次执行的输出方向参数，且快照不依赖命令释放后的原始参数对象。
    /// </summary>
    [Fact]
    public async Task ExecuteProcedureAsync_WhenDynamicParametersContainMixedDirections_ShouldExposeOnlyOutputDirections()
    {
        // Arrange
        var connection = new CaptureDbConnection { NonQueryResult = 3 };
        connection.OutputParameterValues["result"] = 7;
        connection.OutputParameterValues["state"] = 2;
        using var executor = CreateExecutor(connection);
        var parameters = new DynamicParameters();
        parameters.Add("input", "request", DbType.String, ParameterDirection.Input);
        parameters.Add("result", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("state", 1, DbType.Int32, ParameterDirection.InputOutput);

        // Act
        var result = await executor.ExecuteProcedureAsync("usp_update", parameters);

        // Assert
        result.Result.ShouldBe(3);
        result.OutputParameters.GetValue<int>("result").ShouldBe(7);
        result.OutputParameters.GetValue<int>("state").ShouldBe(2);
        result.OutputParameters.TryGetValue<string>("input", out _).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：存储过程描述应通过独立计划执行并使用 StoredProcedure 命令类型，不得退化为文本 SQL。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenExecuted_ShouldUseStoredProcedureCommandAndMapRows()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 3, Name = "Charlie" })
        };
        var query = CreateQuery(connection);

        // Act
        var result = query.Procedure<MappedSample>("usp_users_query", new { Name = "Charlie" }).ExecuteList();

        // Assert
        result.Result.Count.ShouldBe(1);
        result.Result[0].Name.ShouldBe("Charlie");
        connection.LastCommandText.ShouldBe("usp_users_query");
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        connection.LastCreatedParameters.Count.ShouldBe(1);
        connection.LastCreatedParameters[0].Value.ShouldBe("Charlie");
    }

    /// <summary>
    /// 测试目的：异步过程描述应将 StoredProcedure 命令类型传递给 CommandDefinition，并保留调用方输出参数对象身份。
    /// </summary>
    [Fact]
    public async Task ProcedureDescription_WhenExecutedAsync_ShouldUseStoredProcedureCommandAndKeepParameterIdentity()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(
                new MappedSample { Id = 4, Name = "Delta" })
        };
        var query = CreateQuery(connection);
        var parameters = new DynamicParameters();
        parameters.Add("name", "Delta");
        parameters.Add("code", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
        var description = query.Procedure<MappedSample>("usp_users_query", parameters);

        // Act
        var result = await description.ExecuteSingleAsync();

        // Assert
        result.Result.Name.ShouldBe("Delta");
        description.Parameters.ShouldBeSameAs(parameters);
        connection.LastCommandText.ShouldBe("usp_users_query");
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
    }

    /// <summary>
    /// 测试目的：过程描述的 QueryPlan 完成后应立即复制原生 Dapper 参数输出，只公开实际输出方向。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenDynamicParametersContainMixedDirections_ShouldExposeOnlyOutputDirections()
    {
        // Arrange
        var connection = new CaptureDbConnection { ScalarResult = 1 };
        connection.OutputParameterValues["result"] = 7;
        connection.OutputParameterValues["state"] = 2;
        using var query = CreateQuery(connection);
        var parameters = new DynamicParameters();
        parameters.Add("input", "request", DbType.String, ParameterDirection.Input);
        parameters.Add("result", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("state", 1, DbType.Int32, ParameterDirection.InputOutput);
        var description = query.Procedure<int>("usp_output", parameters);

        // Act
        var result = description.ExecuteScalar();

        // Assert
        result.Result.ShouldBe(1);
        result.OutputParameters.GetValue<int>("result").ShouldBe(7);
        result.OutputParameters.GetValue<int>("state").ShouldBe(2);
        result.OutputParameters.TryGetValue<string>("input", out _).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：过程查询每次终结执行都应返回独立的输出参数访问器，
    /// 同步和异步结果不得依赖 Root Query 的最近一次共享状态。
    /// </summary>
    [Fact]
    public async Task ProcedureDescription_WhenOutputParametersConfigured_ShouldExposeFinalValues()
    {
        // Arrange
        var syncConnection = new CaptureDbConnection { ScalarResult = 1 };
        syncConnection.OutputParameterValues["result"] = 7;
        syncConnection.OutputParameterValues["state"] = 2;
        var asyncConnection = new CaptureDbConnection { ScalarResult = 1 };
        asyncConnection.OutputParameterValues["result"] = 9;
        using var syncQuery = CreateQuery(syncConnection);
        using var asyncQuery = CreateQuery(asyncConnection);
        var syncDescription = syncQuery.Procedure<int>("usp_sync", new SqlParameterCollection()
            .AddOutput("result", DbType.Int32)
            .Add(new SqlParam("state", 1, DbType.Int32, ParameterDirection.InputOutput)));
        var asyncDescription = asyncQuery.Procedure<int>("usp_async", new SqlParameterCollection()
            .AddOutput("result", DbType.Int32));

        // Act
        var syncResult = syncDescription.ExecuteScalar();
        var asyncResult = await asyncDescription.ExecuteScalarAsync();

        // Assert
        Assert.Equal(1, syncResult.Result);
        Assert.NotNull(syncResult.OutputParameters);
        Assert.Equal(7, syncResult.OutputParameters.GetValue<int>("result"));
        Assert.Equal(2, syncResult.OutputParameters.GetValue<int>("state"));
        Assert.Equal(1, asyncResult.Result);
        Assert.NotNull(asyncResult.OutputParameters);
        Assert.Equal(9, asyncResult.OutputParameters.GetValue<int>("result"));
    }

    /// <summary>
    /// 测试目的：同一个过程描述连续执行时，前一次结果必须保留自己的输出参数访问器，
    /// 后一次执行不得覆盖已返回结果。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenExecutedSequentially_ShouldRetainEachOutputParameterAccessor()
    {
        // Arrange
        var connection = new CaptureDbConnection { ScalarResult = 1 };
        using var query = CreateQuery(connection);
        var description = query.Procedure<int>("usp_output", new SqlParameterCollection()
            .AddOutput("result", DbType.Int32));

        // Act
        connection.OutputParameterValues["result"] = 7;
        var first = description.ExecuteScalar();
        connection.OutputParameterValues["result"] = 9;
        var second = description.ExecuteScalar();

        // Assert
        Assert.Equal(7, first.OutputParameters.GetValue<int>("result"));
        Assert.Equal(9, second.OutputParameters.GetValue<int>("result"));
        Assert.NotSame(first.OutputParameters, second.OutputParameters);
    }

    /// <summary>
    /// 测试目的：过程结果必须保留执行结束时的输出值快照，并支持 Guid、DateTimeOffset、TimeSpan 与 byte[] 转换，
    /// 后续修改驱动参数值不得影响已返回结果。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenOutputValuesAreMutable_ShouldReturnIndependentTypedSnapshot()
    {
        // Arrange
        var identifier = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 8, 5, 12, 30, 0, TimeSpan.FromHours(8));
        var duration = TimeSpan.FromMinutes(5);
        var payload = new byte[] { 1, 2, 3 };
        var numbers = new[] { 4, 5, 6 };
        var connection = new CaptureDbConnection { ScalarResult = 1 };
        connection.OutputParameterValues["id"] = identifier.ToString();
        connection.OutputParameterValues["timestamp"] = timestamp.ToString("O");
        connection.OutputParameterValues["duration"] = duration.ToString();
        connection.OutputParameterValues["payload"] = payload;
        connection.OutputParameterValues["numbers"] = numbers;
        using var query = CreateQuery(connection);
        var description = query.Procedure<int>("usp_output", new SqlParameterCollection()
            .AddOutput("id", DbType.String)
            .AddOutput("timestamp", DbType.String)
            .AddOutput("duration", DbType.String)
            .AddOutput("payload", DbType.Binary)
            .AddOutput("numbers", DbType.Object));

        // Act
        var result = description.ExecuteScalar();
        payload[0] = 9;
        numbers[0] = 8;

        // Assert
        result.OutputParameters.GetValue<Guid>("id").ShouldBe(identifier);
        result.OutputParameters.GetValue<DateTimeOffset>("timestamp").ShouldBe(timestamp);
        result.OutputParameters.GetValue<TimeSpan>("duration").ShouldBe(duration);
        result.OutputParameters.GetValue<byte[]>("payload").ShouldBe(new byte[] { 1, 2, 3 });
        result.OutputParameters.GetValue<int[]>("numbers").ShouldBe(new[] { 4, 5, 6 });
    }

    /// <summary>
    /// 测试目的：存储过程描述应通过专用列表终结入口执行，不得依赖文本查询的多映射入口。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenListExecuted_ShouldUseStoredProcedureCommand()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 5, Name = "Echo" })
        };
        var query = CreateQuery(connection);

        // Act
        var result = query.Procedure<MappedSample>("usp_users_with_role").ExecuteList();

        // Assert
        result.Result.Count.ShouldBe(1);
        result.Result[0].Name.ShouldBe("Echo");
        connection.LastCommandText.ShouldBe("usp_users_with_role");
        connection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
    }

    /// <summary>
    /// 测试目的：存储过程描述的 First、FirstOrDefault 和 Single 应分别保持首行、空结果默认值和严格单行语义。
    /// </summary>
    [Fact]
    public void ProcedureDescription_WhenCardinalityTerminalsUsed_ShouldPreserveExpectedSemantics()
    {
        // Arrange
        var firstConnection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "First" },
                new MappedSample { Id = 2, Name = "Second" })
        };
        var emptyConnection = new CaptureDbConnection { ResultSet = CreateMappedSampleTable() };
        var multipleConnection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "First" },
                new MappedSample { Id = 2, Name = "Second" })
        };

        // Act
        var first = CreateQuery(firstConnection).Procedure<MappedSample>("usp_users").ExecuteFirst();
        var empty = CreateQuery(emptyConnection).Procedure<MappedSample>("usp_users").ExecuteFirstOrDefault();
        var exception = Should.Throw<InvalidOperationException>(() =>
            CreateQuery(multipleConnection).Procedure<MappedSample>("usp_users").ExecuteSingle());

        // Assert
        first.Result.Name.ShouldBe("First");
        empty.Result.ShouldBeNull();
        exception.Message.ShouldNotBeNullOrWhiteSpace();
        firstConnection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        emptyConnection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
        multipleConnection.LastCommandType.ShouldBe(CommandType.StoredProcedure);
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
        var result = await query.Procedure<MappedSample>("usp_users_single").ExecuteFirstAsync();

        // Assert
        result.Result.ShouldNotBeNull();
        result.Result.Id.ShouldBe(2);
        result.Result.Name.ShouldBe("Alice");
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        var result = await description.ToListAsync();

        // Assert
        query.LastQueryCommandFlags.ShouldBe(CommandFlags.Buffered);
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
        var result = await query.Procedure<MappedSample>("usp_users_query").ExecuteListAsync();

        // Assert
        query.LastQueryCommandFlags.ShouldBe(CommandFlags.Buffered);
        result.Result.Count.ShouldBe(2);
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
        var result = query.Procedure<MappedSample>("usp_users_query").ExecuteList();

        // Assert
        result.Result.Count.ShouldBe(2);
        result.Result[0].Name.ShouldBe("Alice");
        result.Result[1].Name.ShouldBe("Bob");
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        var result = description.AsEnumerable().ToList();

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
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        using (var enumerator = description.AsEnumerable().GetEnumerator())
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        using (var enumerator = description.AsEnumerable().GetEnumerator())
            enumerator.MoveNext().ShouldBeTrue();

        // Assert
        messages.Count.ShouldBe(1);
        messages[0].Operation.ShouldBe(SqlQueryDiagnosticListenerNames.AfterExecute);
        connection.ReaderDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 测试目的：仅通过订阅谓词启用完成事件时，查询仍应创建快照并发布完成诊断。
    /// </summary>
    [Fact]
    public void ExecuteScalar_WhenOnlyAfterDiagnosticEventIsSubscribed_ShouldPublishAfterDiagnostics()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item,
            name => name == SqlQueryDiagnosticListenerNames.AfterExecute, subscribeWithEventFilter: true);
        var connection = new CaptureDbConnection { ScalarResult = 1 };
        using var query = CreateQuery(connection);

        // Act
        var result = query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").Scalar();

        // Assert
        result.ShouldBe(1);
        message.ShouldNotBeNull();
        message.Operation.ShouldBe(SqlQueryDiagnosticListenerNames.AfterExecute);
        message.Sql.ShouldBe("Select Count(*) \r\nFrom [Users]");
    }

    /// <summary>
    /// 测试目的：仅通过订阅谓词启用异常事件时，失败查询仍应创建快照并发布错误诊断。
    /// </summary>
    [Fact]
    public void ExecuteScalar_WhenOnlyErrorDiagnosticEventIsSubscribed_ShouldPublishErrorDiagnostics()
    {
        // Arrange
        DiagnosticsMessage message = null;
        using var observer = new SqlDiagnosticObserver(item => message = item,
            name => name == SqlQueryDiagnosticListenerNames.ErrorExecute, subscribeWithEventFilter: true);
        var connection = new CaptureDbConnection { ThrowOnScalarExecute = true };
        using var query = CreateQuery(connection);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            query.Query<int>().AppendSelect("Count(*)").AppendFrom("[Users]").Scalar());

        // Assert
        exception.Message.ShouldBe("execute failed");
        message.ShouldNotBeNull();
        message.Operation.ShouldBe(SqlQueryDiagnosticListenerNames.ErrorExecute);
        message.Exception.ShouldBeSameAs(exception);
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        await foreach (var item in description.AsAsyncEnumerable())
        {
            item.Name.ShouldBe("Alice");
            break;
        }

        // Assert
        connection.ReaderCreateCount.ShouldBe(1);
        connection.ReaderDisposeCount.ShouldBe(1);
        connection.AsyncReaderDisposeCount.ShouldBe(1);
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        await foreach (var item in description.AsAsyncEnumerable())
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
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        Exception exception = null;
        try
        {
            await foreach (var _ in description.AsAsyncEnumerable())
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
    /// 测试目的：Dapper 在初始化行解析器时失败，也必须释放已经创建的异步读取器并发布原始错误诊断。
    /// </summary>
    [Fact]
    public async Task StreamAsync_WhenRowParserInitializationFails_ShouldPublishErrorAndDisposeReader()
    {
        // Arrange
        DiagnosticsMessage errorMessage = null;
        using var observer = new SqlDiagnosticObserver(item => errorMessage = item,
            name => name == SqlQueryDiagnosticListenerNames.ErrorExecute);
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" }),
            ThrowOnReaderParserInitialization = true
        };
        var query = CreateQuery(connection);
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in description.AsAsyncEnumerable())
            {
            }
        });

        // Assert
        Assert.Equal("row parser initialization failed", exception.Message);
        Assert.Same(exception, errorMessage.Exception);
        Assert.Equal(1, connection.ReaderDisposeCount);
        Assert.Equal(1, connection.AsyncReaderDisposeCount);
    }

    /// <summary>
    /// 测试目的：同步流式读取失败时，错误诊断钩子异常不得覆盖原始读取异常，二者应按顺序聚合。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenReadAndErrorHookFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        var table = new DataTable();
        table.Columns.Add(nameof(MappedSample.Id), typeof(string));
        table.Columns.Add(nameof(MappedSample.Name), typeof(string));
        table.Rows.Add("invalid-id", "Alice");
        using var provider = CreateSqlServerTestProvider();
        using var query = CreateSqlServerTestRoot<ThrowingErrorSqlServerQuery>(provider,
            options => options.Connection(new CaptureDbConnection { ResultSet = table }));

        // Act
        var exception = Assert.Throws<AggregateException>(() => query.Query<MappedSample>()
            .Select("Id,Name").From("Users").AsEnumerable().ToList());

        // Assert
        Assert.Equal("error hook failed", exception.Flatten().InnerExceptions.Last().Message);
        Assert.NotEqual("error hook failed", exception.Flatten().InnerExceptions.First().Message);
    }

    /// <summary>
    /// 测试目的：异步流式读取失败时，错误诊断钩子异常不得覆盖原始读取异常，二者应按顺序聚合。
    /// </summary>
    [Fact]
    public async Task StreamQueryAsync_WhenReadAndErrorHookFail_ShouldPreserveBothExceptions()
    {
        // Arrange
        var table = new DataTable();
        table.Columns.Add(nameof(MappedSample.Id), typeof(string));
        table.Columns.Add(nameof(MappedSample.Name), typeof(string));
        table.Rows.Add("invalid-id", "Alice");
        using var provider = CreateSqlServerTestProvider();
        using var query = CreateSqlServerTestRoot<ThrowingErrorSqlServerQuery>(provider,
            options => options.Connection(new CaptureDbConnection { ResultSet = table }));

        // Act
        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
        {
            await foreach (var _ in query.Query<MappedSample>().Select("Id,Name").From("Users").AsAsyncEnumerable())
            {
            }
        });

        // Assert
        Assert.Equal("error hook failed", exception.Flatten().InnerExceptions.Last().Message);
        Assert.NotEqual("error hook failed", exception.Flatten().InnerExceptions.First().Message);
    }

    /// <summary>
    /// 测试目的：同步流提前停止时，完成钩子失败应被传播且仅调用一次。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenEnumerationStopsEarlyAndCompletionHookFails_ShouldRunHookOnce()
    {
        // Arrange
        using var provider = CreateSqlServerTestProvider();
        using var query = CreateSqlServerTestRoot<ThrowingCompletionSqlServerQuery>(provider,
            options => options.Connection(new CaptureDbConnection
            {
                ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" })
            }));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            using var enumerator = query.Query<MappedSample>().Select("Id,Name").From("Users").AsEnumerable()
                .GetEnumerator();
            enumerator.MoveNext();
        });

        // Assert
        Assert.Equal("completion hook failed", exception.Message);
        Assert.Equal(1, query.AfterCount);
    }

    /// <summary>
    /// 测试目的：读取器释放失败应走错误清理路径，并在抛出后归还执行租约。
    /// </summary>
    [Fact]
    public void StreamQuery_WhenReaderDisposeFails_ShouldReleaseExecutionLease()
    {
        // Arrange
        var connection = new CaptureDbConnection
        {
            ResultSet = CreateMappedSampleTable(new MappedSample { Id = 1, Name = "Alice" }),
            ThrowOnReaderDispose = true
        };
        using var query = CreateQuery(connection);
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => description.AsEnumerable().ToList());
        var readerDisposeCount = connection.ReaderDisposeCount;
        connection.ThrowOnReaderDispose = false;
        var result = query.Sql("Select Count(*) From [Users]").Scalar<int>();

        // Assert
        Assert.Equal("reader dispose failed", exception.Message);
        Assert.True(readerDisposeCount >= 1);
        Assert.Equal(1, result);
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
        ConfigurePrimaryReadTransaction(query);
        var description = query.Query<MappedSample>().Select("Id,Name").From("Users");

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => description.AsEnumerable().ToList());

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
        var services = new ServiceCollection();
        services.AddSqlServerProvider();
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
    /// 创建存储过程多映射样例结果集。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="userName">用户名称。</param>
    /// <param name="roleId">角色标识。</param>
    /// <param name="roleName">角色名称。</param>
    /// <returns>包含两个 Dapper 映射段的数据表。</returns>
    private static DataTable CreateProcedureSplitTable(int userId, string userName, int roleId, string roleName)
    {
        var table = new DataTable();
        table.Columns.Add(nameof(MappedSample.Id), typeof(int));
        table.Columns.Add(nameof(MappedSample.Name), typeof(string));
        table.Columns.Add(nameof(ProcedureSplitSample.SplitId), typeof(int));
        table.Columns.Add(nameof(ProcedureSplitSample.SplitName), typeof(string));
        table.Rows.Add(userId, userName, roleId, roleName);
        return table;
    }

    /// <summary>
    /// 创建查询对象
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>查询对象</returns>
    private static InspectableSqlServerQuery CreateQuery(CaptureDbConnection connection)
    {
        var provider = CreateSqlServerTestProvider();
        return CreateSqlServerTestRoot<InspectableSqlServerQuery>(provider, options => options.Connection(connection));
    }

    /// <summary>
    /// 创建用于验证真实 Trace 日志的查询对象。
    /// </summary>
    /// <param name="connection">测试连接。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    /// <returns>未重写日志写入行为的查询对象。</returns>
    private static InspectableSqlServerQuery CreateTraceQuery(CaptureDbConnection connection, ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSqlServerProvider();
        var provider = services.BuildServiceProvider();
        return CreateSqlServerTestRoot<InspectableSqlServerQuery>(provider, options => options.Connection(connection));
    }

    /// <summary>
    /// 创建用于验证 SQL 渲染次数的查询对象。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="traceEnabled">是否启用 Trace 日志。</param>
    /// <returns>可计数 SQL Server 查询对象。</returns>
    private static CountingSqlServerQuery CreateCountingQuery(CaptureDbConnection connection, bool traceEnabled)
        => CreateCountingQuery(connection, new TraceLoggerFactory(traceEnabled));

    /// <summary>
    /// 创建指定日志工厂的可计数 SQL Server 查询对象。
    /// </summary>
    /// <param name="connection">测试连接。</param>
    /// <param name="loggerFactory">日志工厂。</param>
    /// <returns>可计数 SQL Server 查询对象。</returns>
    private static CountingSqlServerQuery CreateCountingQuery(CaptureDbConnection connection, ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory);
        services.AddSqlServerProvider();
        var provider = services.BuildServiceProvider();
        return CreateSqlServerTestRoot<CountingSqlServerQuery>(provider, options => options.Connection(connection));
    }

    /// <summary>
    /// 创建在第二个独立查询计划前跳过执行的查询对象。
    /// </summary>
    /// <param name="connection">测试连接。</param>
    /// <returns>用于验证分页短事务完成行为的查询对象。</returns>
    private static SecondPlanSkippingSqlServerQuery CreateSecondPlanSkippingQuery(CaptureDbConnection connection)
    {
        var provider = CreateSqlServerTestProvider();
        return CreateSqlServerTestRoot<SecondPlanSkippingSqlServerQuery>(provider,
            options => options.Connection(connection));
    }

    /// <summary>
    /// 创建拥有连接的查询对象
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>查询对象</returns>
    private static InspectableSqlServerQuery CreateOwnedQuery(CaptureDbConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(new CaptureConnectionResolver(connection));
        services.AddSqlServerProvider();
        var provider = services.BuildServiceProvider();
        return CreateSqlServerTestRoot<InspectableSqlServerQuery>(provider, options =>
        {
            options.ConnectionString("Server=test;Database=test;");
            options.QueryCapabilities = new SqlQueryCapabilities
            {
                Pagination = SqlQueryCapabilityState.Supported
            };
        });
    }

    /// <summary>
    /// 创建执行器
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>执行器</returns>
    private static InspectableSqlServerExecutor CreateExecutor(CaptureDbConnection connection)
    {
        var provider = CreateSqlServerTestProvider();
        return CreateSqlServerTestRoot<InspectableSqlServerExecutor>(provider,
            options => options.Connection(connection));
    }

    /// <summary>
    /// 创建拥有连接的执行器
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <returns>执行器</returns>
    private static InspectableSqlServerExecutor CreateOwnedExecutor(CaptureDbConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(new CaptureConnectionResolver(connection));
        services.AddSqlServerProvider();
        var provider = services.BuildServiceProvider();
        return CreateSqlServerTestRoot<InspectableSqlServerExecutor>(provider,
            options => options.ConnectionString("Server=test;Database=test;"));
    }

    /// <summary>
    /// 创建可控制生命周期 Hook 的 SQL Server 执行器。
    /// </summary>
    /// <param name="connection">测试数据库连接。</param>
    /// <returns>用于验证异常聚合的执行器。</returns>
    private static LifecycleSqlServerExecutor CreateLifecycleExecutor(CaptureDbConnection connection)
    {
        var provider = CreateSqlServerTestProvider();
        return CreateSqlServerTestRoot<LifecycleSqlServerExecutor>(provider,
            options => options.Connection(connection));
    }

    /// <summary>
    /// 创建可控制生命周期 Hook 的 SQL Server 多结果集执行器。
    /// </summary>
    /// <param name="connection">测试数据库连接。</param>
    /// <returns>用于验证多结果集异常聚合的执行器。</returns>
    private static LifecycleSqlServerMultipleQueryExecutor CreateLifecycleMultipleExecutor(CaptureDbConnection connection)
    {
        var provider = CreateSqlServerTestProvider();
        var options = new SqlOptions<LifecycleSqlServerMultipleQueryExecutor>();
        options.DatabaseType = DatabaseType.SqlServer;
        options.Connection(connection);
        return new LifecycleSqlServerMultipleQueryExecutor(provider, options);
    }

    /// <summary>
    /// 创建包含官方 SQL Server Provider 能力的测试服务提供程序。
    /// </summary>
    private static ServiceProvider CreateSqlServerTestProvider(ICorrelationIdProvider correlationIdProvider = null)
    {
        var services = new ServiceCollection();
        if (correlationIdProvider != null)
            services.AddSingleton(correlationIdProvider);
        services.AddSqlServerProvider();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建使用外部 Fake ADO 连接的官方 SQL Server Provider 服务图。
    /// </summary>
    private static ServiceProvider CreateSqlServerScopeProvider(IDbConnection connection,
        SqlMetadataOptions metadataOptions = null)
    {
        var services = new ServiceCollection();
        if (metadataOptions != null)
            services.AddSingleton(metadataOptions);
        services.AddSqlServerProvider();
        services.AddSqlDataSource("default", DatabaseType.SqlServer);
        services.AddSingleton(CreateSqlServerOptions<SqlServerSqlQuery>(connection));
        services.AddSingleton(CreateSqlServerOptions<SqlServerSqlExecutor>(connection));
        services.AddSingleton(CreateSqlServerOptions<SqlServerSqlMultipleQueryExecutor>(connection));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建通过连接解析器提供所有权连接的官方 SQL Server Provider 服务图。
    /// </summary>
    private static ServiceProvider CreateSqlServerOwnedScopeProvider(CaptureDbConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISqlDbConnectionFactoryResolver>(new CaptureConnectionResolver(connection));
        services.AddSqlServerProvider();
        services.AddSqlDataSource("default", DatabaseType.SqlServer, "Server=test;Database=test;");
        services.AddSingleton(CreateSqlServerOptions<SqlServerSqlQuery>("Server=test;Database=test;"));
        services.AddSingleton(CreateSqlServerOptions<SqlServerSqlExecutor>("Server=test;Database=test;"));
        services.AddSingleton(CreateSqlServerOptions<SqlServerSqlMultipleQueryExecutor>("Server=test;Database=test;"));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建使用外部连接的 SQL Server 配置模板。
    /// </summary>
    private static SqlOptions<T> CreateSqlServerOptions<T>(IDbConnection connection)
        where T : class
    {
        var options = new SqlOptions<T> { DatabaseType = DatabaseType.SqlServer };
        options.Connection(connection);
        return options;
    }

    /// <summary>
    /// 创建使用连接字符串的 SQL Server 配置模板。
    /// </summary>
    private static SqlOptions<T> CreateSqlServerOptions<T>(string connectionString)
        where T : class
    {
        var options = new SqlOptions<T> { DatabaseType = DatabaseType.SqlServer };
        options.ConnectionString(connectionString);
        return options;
    }

    /// <summary>
    /// 直接构造测试派生 Root 对象，避免测试实现参与 Provider 运行时映射。
    /// </summary>
    private static T CreateSqlServerTestRoot<T>(IServiceProvider serviceProvider, Action<SqlOptions<T>> configure)
        where T : class
    {
        var options = new SqlOptions<T> { DatabaseType = DatabaseType.SqlServer };
        configure?.Invoke(options);
        return (T)Activator.CreateInstance(typeof(T), serviceProvider, options);
    }

    /// <summary>
    /// 配置主库短事务策略
    /// </summary>
    /// <param name="query">SQL 查询对象</param>
    private static void ConfigurePrimaryReadTransaction(ISqlQuery query)
    {
        SqlQueryRuntimeBinding.BindDatabaseContext(query, new DatabaseContext
        {
            ReadPreference = SqlReadPreference.Primary,
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "primary",
                DatabaseType = DatabaseType.SqlServer,
                PrimaryReadStrategy = PrimaryReadStrategy.Transaction
            }
        });
    }

    /// <summary>
    /// 将 Query 绑定到只读数据源，用于验证结构化写入计划的执行边界。
    /// </summary>
    /// <param name="query">待绑定的查询对象。</param>
    private static void ConfigureReadOnlyDataSource(ISqlQuery query)
    {
        SqlQueryRuntimeBinding.BindDatabaseContext(query, new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "reporting",
                DatabaseType = DatabaseType.SqlServer,
                IsReadOnly = true
            }
        });
    }

    /// <summary>
    /// 创建通过查询结果 API 执行的带 Returning 更新计划。
    /// </summary>
    /// <returns>待验证的独立更新查询计划。</returns>
    private static SqlQueryPlan CreateReturningQueryPlan()
    {
        ISqlBuilder builder = new SqlServerBuilder();
        builder.Update(new SqlTableReference { TableName = "Users" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id");
        return SqlQueryPlan.Create(builder);
    }

    /// <summary>
    /// 测试目的：首次使用 Query 前可以绑定固定数据库上下文，供事务和 ORM 适配层完成创建期路由。
    /// </summary>
    [Fact]
    public void BindDatabaseContext_WhenQueryIsUninitialized_ShouldUseBoundContext()
    {
        // Arrange
        using var query = CreateOwnedQuery(new CaptureDbConnection());
        var context = new DatabaseContext
        {
            DbKey = "reporting",
            DataSource = new SqlDataSourceDescriptor
            {
                Key = "reporting",
                DatabaseType = DatabaseType.SqlServer,
                ConnectionString = "Server=reporting;Database=test;"
            }
        };

        // Act
        SqlQueryRuntimeBinding.BindDatabaseContext(query, context);

        // Assert
        query.CurrentOptions.GetDatabaseContext().DbKey.ShouldBe("reporting");
        query.CurrentOptions.GetDatabaseContext().DataSource.ConnectionString.ShouldBe("Server=reporting;Database=test;");
    }

    /// <summary>
    /// 测试目的：独立查询描述已固定 Provider 后不得重绑数据库上下文，避免方言和连接分叉。
    /// </summary>
    [Fact]
    public void BindDatabaseContext_WhenProviderWasInitialized_ShouldRejectRebinding()
    {
        // Arrange
        using var query = CreateOwnedQuery(new CaptureDbConnection());
        _ = query.Query<int>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => SqlQueryRuntimeBinding.BindDatabaseContext(query,
            new DatabaseContext
            {
                DbKey = "reporting",
                DataSource = new SqlDataSourceDescriptor
                {
                    Key = "reporting",
                    DatabaseType = DatabaseType.SqlServer,
                    ConnectionString = "Server=reporting;Database=test;"
                }
            }));

        // Assert
        exception.Message.ShouldBe("SQL Query 已初始化 Provider、Builder 或连接，不能修改数据库上下文。");
    }

    /// <summary>
    /// 测试目的：根 Builder 已创建后不得替换映射解析器，避免后绑定静默不生效。
    /// </summary>
    [Fact]
    public void BindEntityMappingResolver_WhenRootBuilderWasInitialized_ShouldRejectRebinding()
    {
        // Arrange
        using var query = CreateOwnedQuery(new CaptureDbConnection());
        query.ConfigureRootSql();
        _ = query.RootSql;
        var resolver = new Mock<IEntityMappingResolver>();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() =>
            SqlQueryRuntimeBinding.BindEntityMappingResolver(query, resolver.Object));

        // Assert
        exception.Message.ShouldBe("SQL Query 已创建 Builder，不能修改实体映射解析器。");
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

        public string InvokeResolveConnectionString() => ResolveConnectionString();

        public string RootSql => SqlBuilder.ToSql();

        public IReadOnlyDictionary<string, object> RootParameters => SqlBuilder.GetParams();

    public void ConfigureDatabaseContext(DatabaseContext context) => Options.SetDatabaseContext(context);

        public void ConfigureRootSql() => SqlBuilder.Select("RootId").From("RootUsers").Where("RootId", 7);

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
    /// 统计执行前诊断创建次数的测试查询对象。
    /// </summary>
    private sealed class CountingDiagnosticsSqlServerQuery : SqlServerSqlQueryBase
    {
        public CountingDiagnosticsSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<CountingDiagnosticsSqlServerQuery> options) : base(serviceProvider, options)
        {
        }

        public int BeforeCount { get; private set; }

        protected override DiagnosticsMessage ExecuteBefore(string sql, object parameter,
            IDbConnection connection, IReadOnlyCollection<SqlParameterDiagnosticInfo> parameterMetadata = null)
        {
            BeforeCount++;
            return base.ExecuteBefore(sql, parameter, connection, parameterMetadata);
        }
    }

    /// <summary>
    /// 前置执行钩子固定失败的测试查询对象。
    /// </summary>
    private sealed class ThrowingBeforeSqlServerQuery : SqlServerSqlQueryBase
    {
        /// <summary>
        /// 初始化一个<see cref="ThrowingBeforeSqlServerQuery"/>类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public ThrowingBeforeSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<ThrowingBeforeSqlServerQuery> options) : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 完成清理钩子调用次数。
        /// </summary>
        public int AfterCount { get; private set; }

        /// <inheritdoc />
        protected override bool ExecuteBefore() => throw new InvalidOperationException("before failed");

        /// <inheritdoc />
        protected override void ExecuteAfter(object result) => AfterCount++;
    }

    /// <summary>
    /// 错误诊断钩子固定失败的测试查询对象。
    /// </summary>
    private sealed class ThrowingErrorSqlServerQuery : SqlServerSqlQueryBase
    {
        /// <summary>
        /// 初始化一个<see cref="ThrowingErrorSqlServerQuery"/>类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public ThrowingErrorSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<ThrowingErrorSqlServerQuery> options) : base(serviceProvider, options)
        {
        }

        /// <inheritdoc />
        protected override void ExecuteError(DiagnosticsMessage message, Exception exception) =>
            throw new InvalidOperationException("error hook failed");
    }

    /// <summary>
    /// 完成钩子固定失败的测试查询对象。
    /// </summary>
    private sealed class ThrowingCompletionSqlServerQuery : SqlServerSqlQueryBase
    {
        /// <summary>
        /// 初始化一个<see cref="ThrowingCompletionSqlServerQuery"/>类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public ThrowingCompletionSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<ThrowingCompletionSqlServerQuery> options) : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 完成钩子调用次数。
        /// </summary>
        public int AfterCount { get; private set; }

        /// <inheritdoc />
        protected override void ExecuteAfter(object result)
        {
            AfterCount++;
            throw new InvalidOperationException("completion hook failed");
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

        /// <inheritdoc />
        protected override ISqlBuilder CreateSqlBuilder() => new CountingSqlServerBuilder(Counters);

        /// <inheritdoc />
        protected override ISqlBuilder CreateIndependentSqlBuilder() => new CountingSqlServerBuilder(Counters);

        /// <inheritdoc />
        protected override void WriteTraceLog(string sql, IReadOnlyDictionary<string, object> parameters, string debugSql)
        {
            TraceSql = sql;
            TraceDebugSql = debugSql;
        }
    }

    /// <summary>
    /// 在第二个独立查询计划前返回 false 的测试查询对象。
    /// </summary>
    private sealed class SecondPlanSkippingSqlServerQuery : SqlServerSqlQueryBase
    {
        private int _executeBeforeCount;

        /// <summary>
        /// 初始化一个<see cref="SecondPlanSkippingSqlServerQuery"/>类型的实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public SecondPlanSkippingSqlServerQuery(IServiceProvider serviceProvider,
            SqlOptions<SecondPlanSkippingSqlServerQuery> options) : base(serviceProvider, options)
        {
        }

        /// <inheritdoc />
        protected override bool ExecuteBefore() => Interlocked.Increment(ref _executeBeforeCount) == 1;
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
        public CountingSqlServerBuilder(SqlRenderCounters counters)
            : base(new SqlBuilderServices(options: new SqlOptions
            {
                QueryCapabilities = new SqlQueryCapabilities
                {
                    Pagination = SqlQueryCapabilityState.Supported
                }
            })) => _counters = counters;

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
        private readonly TraceLogger _logger;

        /// <summary>
        /// 初始化一个<see cref="TraceLoggerFactory"/>类型的实例。
        /// </summary>
        /// <param name="traceEnabled">是否启用 Trace 日志。</param>
        public TraceLoggerFactory(bool traceEnabled)
        {
            _logger = new TraceLogger(traceEnabled);
        }

        /// <summary>
        /// 已格式化的 Trace 日志文本。
        /// </summary>
        public IReadOnlyList<string> Messages => _logger.Messages;

        /// <summary>
        /// 已捕获的结构化日志 Scope。
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Scopes => _logger.Scopes;

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => _logger;

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
        private readonly List<string> _messages = new();

        /// <summary>
        /// 初始化一个<see cref="TraceLogger"/>类型的实例。
        /// </summary>
        /// <param name="traceEnabled">是否启用 Trace 日志。</param>
        public TraceLogger(bool traceEnabled) => _traceEnabled = traceEnabled;

        /// <summary>
        /// 已格式化的 Trace 日志文本。
        /// </summary>
        public IReadOnlyList<string> Messages => _messages;

        private readonly List<IReadOnlyDictionary<string, object>> _scopes = new();

        /// <summary>
        /// 已捕获的结构化日志 Scope。
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<string, object>> Scopes => _scopes;

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            if (state is IEnumerable<KeyValuePair<string, object>> fields)
                _scopes.Add(fields.ToDictionary(item => item.Key, item => item.Value));
            return EmptyScope.Instance;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => _traceEnabled && logLevel == LogLevel.Trace;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
                _messages.Add(formatter(state, exception));
        }
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
    /// 固定 Core 关联标识的测试提供程序。
    /// </summary>
    private sealed class FixedCorrelationIdProvider : ICorrelationIdProvider
    {
        private readonly string _correlationId;

        public FixedCorrelationIdProvider(string correlationId) => _correlationId = correlationId;

        public string Get() => _correlationId;

        public IDisposable Change(string correlationId) => EmptyScope.Instance;
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
        public ISqlQuery Create(string dbKey = null)
        {
            if (_ownerCreated == false)
            {
                _ownerCreated = true;
                return _innerFactory.Create(dbKey);
            }
            return _invalidChild;
        }
    }

    /// <summary>
    /// 始终创建缺少内部资源绑定器子执行器的测试执行器工厂。
    /// </summary>
    private sealed class BindingFailureSqlExecutorFactory : ISqlExecutorFactory
    {
        private readonly ISqlExecutor _invalidChild;

        /// <summary>
        /// 初始化一个<see cref="BindingFailureSqlExecutorFactory"/>类型的实例。
        /// </summary>
        /// <param name="invalidChild">不实现内部绑定器的子执行器。</param>
        public BindingFailureSqlExecutorFactory(ISqlExecutor invalidChild) => _invalidChild = invalidChild;

        /// <inheritdoc />
        public ISqlExecutor Create(string dbKey = null) => _invalidChild;
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
    /// 记录 Dispose 调用的执行器代理。
    /// </summary>
    private class DisposeTrackingSqlExecutorProxy : DisposeTrackingSqlQueryProxy
    {
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

        public void ConfigureDatabaseContext(DatabaseContext context) => Options.SetDatabaseContext(context);

        public void EnableTenantDiagnostics() => Options.IncludeTenantIdInDiagnostics = true;
    }

    /// <summary>
    /// 为直接 Executor 生命周期测试提供可控错误与完成 Hook 的实现。
    /// </summary>
    private sealed class LifecycleSqlServerExecutor : SqlServerSqlExecutorBase
    {
        /// <summary>
        /// 初始化生命周期测试执行器。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">SQL 配置。</param>
        public LifecycleSqlServerExecutor(IServiceProvider serviceProvider,
            SqlOptions<LifecycleSqlServerExecutor> options) : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 是否在错误诊断 Hook 中抛出受控异常。
        /// </summary>
        public bool ThrowOnErrorHook { get; set; }

        /// <summary>
        /// 是否在业务完成 Hook 中抛出受控异常。
        /// </summary>
        public bool ThrowOnCompletionHook { get; set; }

        /// <summary>
        /// 是否在执行前 Hook 中跳过当前命令。
        /// </summary>
        public bool SkipBeforeExecution { get; set; }

        /// <summary>
        /// 错误诊断 Hook 调用次数。
        /// </summary>
        public int ErrorHookCount { get; private set; }

        /// <summary>
        /// 业务完成 Hook 调用次数。
        /// </summary>
        public int CompletionHookCount { get; private set; }

        /// <inheritdoc />
        protected override bool ExecuteBefore() => SkipBeforeExecution == false && base.ExecuteBefore();

        /// <inheritdoc />
        protected override void ExecuteError(DiagnosticsMessage message, Exception exception)
        {
            ErrorHookCount++;
            if (ThrowOnErrorHook)
                throw new InvalidOperationException("error hook failed");
            base.ExecuteError(message, exception);
        }

        /// <inheritdoc />
        protected override void ExecuteAfter(object result)
        {
            CompletionHookCount++;
            if (ThrowOnCompletionHook)
                throw new InvalidOperationException("completion hook failed");
            base.ExecuteAfter(result);
        }
    }

    /// <summary>
    /// 用于验证多结果集执行器生命周期异常顺序的 SQL Server 测试实现。
    /// </summary>
    private sealed class LifecycleSqlServerMultipleQueryExecutor : SqlServerSqlMultipleQueryExecutorBase
    {
        /// <summary>
        /// 初始化测试执行器。
        /// </summary>
        /// <param name="serviceProvider">服务提供程序。</param>
        /// <param name="options">执行器配置。</param>
        public LifecycleSqlServerMultipleQueryExecutor(IServiceProvider serviceProvider,
            SqlOptions<LifecycleSqlServerMultipleQueryExecutor> options) : base(serviceProvider, options)
        {
        }

        /// <summary>
        /// 指示错误 Hook 是否抛出异常。
        /// </summary>
        public bool ThrowOnErrorHook { get; set; }

        /// <summary>
        /// 指示业务完成 Hook 是否抛出异常。
        /// </summary>
        public bool ThrowOnCompletionHook { get; set; }

        /// <summary>
        /// 是否在执行前 Hook 中跳过当前命令。
        /// </summary>
        public bool SkipBeforeExecution { get; set; }

        /// <summary>
        /// 错误 Hook 调用次数。
        /// </summary>
        public int ErrorHookCount { get; private set; }

        /// <summary>
        /// 业务完成 Hook 调用次数。
        /// </summary>
        public int CompletionHookCount { get; private set; }

        /// <inheritdoc />
        protected override bool ExecuteBefore() => SkipBeforeExecution == false && base.ExecuteBefore();

        /// <inheritdoc />
        protected override void ExecuteError(DiagnosticsMessage message, Exception exception)
        {
            ErrorHookCount++;
            if (ThrowOnErrorHook)
                throw new InvalidOperationException("error hook failed");
            base.ExecuteError(message, exception);
        }

        /// <inheritdoc />
        protected override void ExecuteAfter(object result)
        {
            CompletionHookCount++;
            if (ThrowOnCompletionHook)
                throw new InvalidOperationException("completion hook failed");
            base.ExecuteAfter(result);
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
    /// 逻辑删除查询测试样例。
    /// </summary>
    private sealed class SoftDeleteMappedSample : ISoftDelete
    {
        /// <summary>
        /// 标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否已删除。
        /// </summary>
        public bool IsDeleted { get; set; }
    }

    /// <summary>
    /// 类型化派生表映射样例。
    /// </summary>
    private sealed class DerivedMappedSample
    {
        /// <summary>
        /// 所有者标识。
        /// </summary>
        public int OwnerId { get; set; }
    }

    /// <summary>
    /// 存储过程第二映射段样例。
    /// </summary>
    private sealed class ProcedureSplitSample
    {
        /// <summary>
        /// 角色标识。
        /// </summary>
        public int SplitId { get; set; }

        /// <summary>
        /// 角色名称。
        /// </summary>
        public string SplitName { get; set; }
    }

    /// <summary>
    /// 存储过程多映射结果。
    /// </summary>
    private sealed class ProcedureJoinResult
    {
        /// <summary>
        /// 用户名称。
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 角色名称。
        /// </summary>
        public string RoleName { get; set; }
    }

    /// <summary>
    /// 用于并发 Mutation 测试的实体。
    /// </summary>
    private sealed class ConcurrencySample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 更新后的名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 并发令牌。
        /// </summary>
        [ConcurrencyCheck]
        public int Version { get; set; }
    }

    /// <summary>
    /// 捕获参数的数据库连接
    /// </summary>
    private sealed class CaptureDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Open;

        public List<CaptureDbParameter> LastCreatedParameters { get; private set; } = new();

        public string LastCommandText { get; private set; }

        /// <summary>
        /// 当前连接按执行顺序捕获的命令文本。
        /// </summary>
        public List<string> ExecutedCommandTexts { get; } = new();

        public CommandType LastCommandType { get; private set; } = CommandType.Text;

        public object ScalarResult { get; set; } = 1;

        public int NonQueryResult { get; set; } = 1;

        public DataTable ResultSet { get; set; } = new();

        /// <summary>
        /// 命令完成时由测试驱动写入的输出参数值。
        /// </summary>
        public IDictionary<string, object> OutputParameterValues { get; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public int ReaderCreateCount { get; private set; }

        public int ReaderDisposeCount { get; private set; }

        public int AsyncReaderDisposeCount { get; private set; }

        public bool ThrowOnExecute { get; set; }

        /// <summary>
        /// 是否在标量执行时抛出异常。
        /// </summary>
        public bool ThrowOnScalarExecute { get; set; }

        /// <summary>
        /// 标量命令成功执行后的回调。
        /// </summary>
        public Action OnScalarExecuted { get; set; }

        /// <summary>
        /// 数据读取器创建后的测试回调。
        /// </summary>
        public Action OnReaderCreated { get; set; }

        /// <summary>
        /// 数据读取器每次异步读取前的测试回调。
        /// </summary>
        public Func<Task> OnReaderReadAsync { get; set; }

        /// <summary>
        /// 是否在读取器释放时抛出异常。
        /// </summary>
        public bool ThrowOnReaderDispose { get; set; }

        /// <summary>
        /// 是否在 Dapper 初始化行解析器读取架构信息时抛出异常。
        /// </summary>
        public bool ThrowOnReaderParserInitialization { get; set; }

        /// <summary>
        /// 是否让本连接创建的事务在回滚时抛出异常。
        /// </summary>
        public bool ThrowOnTransactionRollback { get; set; }

        /// <summary>
        /// 是否让本连接创建的事务在提交时抛出异常。
        /// </summary>
        public bool ThrowOnTransactionCommit { get; set; }

        /// <summary>
        /// 是否在同步开始事务时抛出异常。
        /// </summary>
        public bool ThrowOnBegin { get; set; }

        /// <summary>
        /// 是否在原生异步开始事务时抛出异常。
        /// </summary>
        public bool ThrowOnAsyncBegin { get; set; }

        /// <summary>
        /// 是否在连接释放时抛出异常。
        /// </summary>
        public bool ThrowOnDispose { get; set; }

        /// <summary>
        /// 是否在连接关闭时抛出异常。
        /// </summary>
        public bool ThrowOnClose { get; set; }

        /// <summary>
        /// 连接关闭次数。
        /// </summary>
        public int CloseCount { get; private set; }

        public CaptureDbTransaction LastTransaction { get; private set; }

        public int AsyncBeginCount { get; private set; }

        /// <summary>
        /// 连接释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <summary>
        /// 原生异步释放次数。
        /// </summary>
        public int AsyncDisposeCount { get; private set; }

        public override string ConnectionString { get; set; }

        public override string Database => "test";

        public override string DataSource => "test";

        public override string ServerVersion => "1.0";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }

        public override void Close()
        {
            CloseCount++;
            _state = ConnectionState.Closed;
            if (ThrowOnClose)
                throw new InvalidOperationException("connection close failed");
        }

        public override void Open() => _state = ConnectionState.Open;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            if (ThrowOnBegin)
                throw new InvalidOperationException("begin failed");
            LastTransaction = new CaptureDbTransaction(this, isolationLevel)
            {
                ThrowOnRollback = ThrowOnTransactionRollback,
                ThrowOnCommit = ThrowOnTransactionCommit
            };
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
            LastTransaction = new CaptureDbTransaction(this, isolationLevel)
            {
                ThrowOnRollback = ThrowOnTransactionRollback,
                ThrowOnCommit = ThrowOnTransactionCommit
            };
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

        public override ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void SetParameters(IEnumerable<CaptureDbParameter> parameters) =>
            LastCreatedParameters = parameters.ToList();

        public void SetCommand(string commandText, CommandType commandType, IEnumerable<CaptureDbParameter> parameters)
        {
            LastCommandText = commandText;
            ExecutedCommandTexts.Add(commandText);
            LastCommandType = commandType;
            foreach (var parameter in parameters.Where(item => item.Direction is ParameterDirection.Output or
                         ParameterDirection.InputOutput or ParameterDirection.ReturnValue))
            {
                if (OutputParameterValues.TryGetValue(parameter.ParameterName, out var value))
                    parameter.Value = value ?? DBNull.Value;
            }
            SetParameters(parameters);
        }

        public DbDataReader CreateReader()
        {
            ReaderCreateCount++;
            var table = ResultSet ?? new DataTable();
            var reader = new CaptureDbDataReader(table.CreateDataReader(), this);
            OnReaderCreated?.Invoke();
            return reader;
        }

        public void OnReaderDisposed() => ReaderDisposeCount++;

        public void OnReaderDisposedAsync() => AsyncReaderDisposeCount++;
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
            _connection.OnScalarExecuted?.Invoke();
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
            _connection.OnScalarExecuted?.Invoke();
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

        public override Type GetFieldType(int ordinal)
        {
            ThrowIfParserInitializationFails();
            return _reader.GetFieldType(ordinal);
        }

        public override float GetFloat(int ordinal) => _reader.GetFloat(ordinal);

        public override Guid GetGuid(int ordinal) => _reader.GetGuid(ordinal);

        public override short GetInt16(int ordinal) => _reader.GetInt16(ordinal);

        public override int GetInt32(int ordinal) => _reader.GetInt32(ordinal);

        public override long GetInt64(int ordinal) => _reader.GetInt64(ordinal);

        public override string GetName(int ordinal)
        {
            ThrowIfParserInitializationFails();
            return _reader.GetName(ordinal);
        }

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

        public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            if (_connection.OnReaderReadAsync != null)
                await _connection.OnReaderReadAsync();
            return await _reader.ReadAsync(cancellationToken);
        }

        public override T GetFieldValue<T>(int ordinal) => _reader.GetFieldValue<T>(ordinal);

        public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) =>
            _reader.GetFieldValueAsync<T>(ordinal, cancellationToken);

        public override ValueTask DisposeAsync()
        {
            _connection.OnReaderDisposedAsync();
            Dispose();
            return ValueTask.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reader.Dispose();
                _connection.OnReaderDisposed();
                if (_connection.ThrowOnReaderDispose)
                    throw new InvalidOperationException("reader dispose failed");
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 按测试配置模拟行解析器初始化失败。
        /// </summary>
        private void ThrowIfParserInitializationFails()
        {
            if (_connection.ThrowOnReaderParserInitialization)
                throw new InvalidOperationException("row parser initialization failed");
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

        /// <summary>
        /// 同步提交开始后的测试回调。
        /// </summary>
        public Action OnCommit { get; set; }

        /// <summary>
        /// 异步提交开始后的测试回调。
        /// </summary>
        public Func<Task> OnCommitAsync { get; set; }

        public override IsolationLevel IsolationLevel => _isolationLevel;

        protected override DbConnection DbConnection => _connection;

        public override void Commit()
        {
            CommitCount++;
            OnCommit?.Invoke();
            if (ThrowOnCommit)
                throw new InvalidOperationException("commit failed");
        }

        public override async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            AsyncCommitCount++;
            if (OnCommitAsync != null)
                await OnCommitAsync();
            if (ThrowOnCommit)
                throw new InvalidOperationException("commit failed");
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

        /// <summary>
        /// 是否在 DiagnosticListener 订阅层应用事件筛选。
        /// </summary>
        private readonly bool _subscribeWithEventFilter;
        private readonly IDisposable _allSubscription;
        private IDisposable _listenerSubscription;

        public SqlDiagnosticObserver(Action<DiagnosticsMessage> onMessage, Func<string, bool> eventFilter = null,
            bool subscribeWithEventFilter = false)
        {
            _onMessage = onMessage;
            _eventFilter = eventFilter ?? (name => name == SqlQueryDiagnosticListenerNames.BeforeExecute);
            _subscribeWithEventFilter = subscribeWithEventFilter;
            _allSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name != SqlQueryDiagnosticListenerNames.DiagnosticListenerName)
                return;
            _listenerSubscription = _subscribeWithEventFilter
                ? value.Subscribe(this, (name, payload, context) => _eventFilter(name))
                : value.Subscribe(this);
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