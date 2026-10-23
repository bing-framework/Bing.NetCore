using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Dapper.Core.Tests.DependencyInjection;

/// <summary>
/// <see cref="DapperCoreServiceCollectionExtensions"/> 单元测试。
/// </summary>
public class DapperCoreServiceCollectionExtensionsTest
{
    /// <summary>
    /// 测试目的：重复注册核心服务时工厂应按作用域创建，元数据仍为跨作用域单例。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenCalledRepeatedly_ShouldKeepScopedFactoryRegistrationsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSqlCore();
        var factoryTypes = new[]
        {
            typeof(ISqlQueryFactory),
            typeof(ISqlExecutorFactory),
            typeof(ISqlMultipleQueryExecutorFactory),
            typeof(ISqlTransactionScopeFactory)
        };

        foreach (var factoryType in factoryTypes)
        {
            var registration = Assert.Single(services, item => item.ServiceType == factoryType);
            Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        }

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        foreach (var factoryType in factoryTypes)
        {
            var first = firstScope.ServiceProvider.GetRequiredService(factoryType);
            Assert.Same(first, firstScope.ServiceProvider.GetRequiredService(factoryType));
            Assert.NotSame(first, secondScope.ServiceProvider.GetRequiredService(factoryType));
        }

        Assert.Same(firstScope.ServiceProvider.GetRequiredService<SqlMetadataOptions>(),
            secondScope.ServiceProvider.GetRequiredService<SqlMetadataOptions>());
        Assert.Same(firstScope.ServiceProvider.GetRequiredService<IEntityMappingResolver>(),
            secondScope.ServiceProvider.GetRequiredService<IEntityMappingResolver>());
        Assert.NotSame(firstScope.ServiceProvider.GetRequiredService<IDataFilter>(),
            secondScope.ServiceProvider.GetRequiredService<IDataFilter>());
    }

    /// <summary>
    /// 测试目的：默认 Query 和 Executor 保持 transient，不随 Uow 资源共享而缓存实例。
    /// </summary>
    [Fact]
    public void AddSqlCore_ShouldKeepQueryAndExecutorServicesTransient()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();

        foreach (var serviceType in new[] { typeof(ISqlQuery), typeof(ISqlExecutor), typeof(ISqlMultipleQueryExecutor) })
        {
            var registration = Assert.Single(services, item => item.ServiceType == serviceType);
            Assert.Equal(ServiceLifetime.Transient, registration.Lifetime);
        }
    }

    /// <summary>
    /// 测试目的：AddSqlCore 不得覆盖调用方预注册的工厂类型、委托或实例。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenFactoryIsPreRegistered_ShouldPreserveTypeFactoryAndInstance()
    {
        var typeServices = new ServiceCollection();
        typeServices.AddScoped<ISqlQueryFactory, TestSqlQueryFactory>();
        typeServices.AddSqlCore();
        var typeRegistration = Assert.Single(typeServices, item => item.ServiceType == typeof(ISqlQueryFactory));
        Assert.Equal(typeof(TestSqlQueryFactory), typeRegistration.ImplementationType);

        var instance = new TestSqlQueryFactory();
        var instanceServices = new ServiceCollection();
        instanceServices.AddSingleton<ISqlQueryFactory>(instance);
        instanceServices.AddSqlCore();
        var instanceRegistration = Assert.Single(instanceServices, item => item.ServiceType == typeof(ISqlQueryFactory));
        Assert.Same(instance, instanceRegistration.ImplementationInstance);

        var factoryServices = new ServiceCollection();
        Func<IServiceProvider, ISqlQueryFactory> factory = _ => instance;
        factoryServices.AddScoped(factory);
        factoryServices.AddSqlCore();
        var factoryRegistration = Assert.Single(factoryServices, item => item.ServiceType == typeof(ISqlQueryFactory));
        Assert.Same(factory, factoryRegistration.ImplementationFactory);
    }

    /// <summary>
    /// 测试目的：调用方在 AddSqlCore 后追加的工厂仍按 DI 后注册规则覆盖解析结果。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenFactoryIsRegisteredAfterCore_ShouldResolveLastRegistration()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        var customFactory = new TestSqlQueryFactory();
        services.AddSingleton<ISqlQueryFactory>(customFactory);

        using var provider = services.BuildServiceProvider();

        Assert.Same(customFactory, provider.GetRequiredService<ISqlQueryFactory>());
        Assert.Equal(2, services.Count(item => item.ServiceType == typeof(ISqlQueryFactory)));
    }

    /// <summary>
    /// 测试目的：直接向 Singleton 注入请求作用域工厂时应由 scope 校验明确拒绝。
    /// </summary>
    [Fact]
    public void SingletonConsumer_WhenInjectingScopedFactory_ShouldFailScopeValidation()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSingleton<DirectFactorySingletonConsumer>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<DirectFactorySingletonConsumer>());
    }

    /// <summary>
    /// 测试目的：Singleton 消费者可为每次业务操作创建 scope 并在 scope 内解析 SQL 工厂。
    /// </summary>
    [Fact]
    public void SingletonConsumer_WhenResolvingFactoryInsideOperationScope_ShouldSucceed()
    {
        var services = new ServiceCollection();
        services.AddSqlCore();
        services.AddSingleton<ScopedFactoryOperation>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var consumer = provider.GetRequiredService<ScopedFactoryOperation>();

        Assert.Equal("Bing.Data.Sql.SqlQueryFactory", consumer.ResolveFactoryTypeForOperation());
        Assert.Equal("Bing.Data.Sql.SqlQueryFactory", consumer.ResolveFactoryTypeForOperation());
    }

    /// <summary>
    /// 测试目的：自定义元数据提供器应作为组合提供器的前置层解析，且不得造成依赖循环。
    /// </summary>
    [Fact]
    public void AddEntityModelMetadataProvider_WhenRegisteredBeforeCore_ShouldPrecedeDefaultProviders()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddEntityModelMetadataProvider<TestEntityModelMetadataProvider>();
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var metadata = provider.GetRequiredService<IEntityModelMetadataProvider>().GetMetadata(typeof(TestEntity));

        // Assert
        Assert.Equal("custom_entities", metadata.TableName);
    }

    /// <summary>
    /// 测试目的：Provider 或创建委托为空以及空白 Key 时应拒绝注册。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenArgumentsInvalid_ShouldThrowArgumentException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider("test");

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => DapperCoreServiceCollectionExtensions.AddSqlBuilderProvider(null, provider, _ => null));
        Assert.Throws<ArgumentNullException>(() => services.AddSqlBuilderProvider(null, _ => null));
        Assert.Throws<ArgumentNullException>(() => services.AddSqlBuilderProvider(provider, null));
        Assert.Throws<ArgumentException>(() => services.AddSqlBuilderProvider(new TestSqlProvider(" "), _ => null));
    }

    /// <summary>
    /// 测试目的：同一 Provider 实例和创建委托重复注册应幂等，不同实例使用相同 Key 时应拒绝。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenKeyRepeated_ShouldBeIdempotentOrThrowForDifferentInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider(" custom ");

        // Act
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);

        // Assert
        Assert.Single(services, item => item.ServiceType == typeof(ISqlProvider));
        Assert.Single(services, item => item.ImplementationInstance is SqlBuilderFactoryRegistration);
        Assert.Throws<InvalidOperationException>(() =>
            services.AddSqlBuilderProvider(new TestSqlProvider("CUSTOM"), CreateTestBuilder));
    }

    /// <summary>
    /// 测试目的：同一 Provider 使用不同 Builder 创建委托时必须拒绝，不能因 Provider 已注册而忽略委托冲突。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenCreatorDiffersForSameProvider_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider("custom");
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddSqlBuilderProvider(provider, CreateAlternativeTestBuilder));
    }

    /// <summary>
    /// 测试目的：预先直接注册的同一 Provider 应可补充 Builder 创建委托，保证注册链完整。
    /// </summary>
    [Fact]
    public void AddSqlBuilderProvider_WhenProviderWasRegisteredDirectly_ShouldAddOnlyBuilderRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = new TestSqlProvider("custom");
        services.AddSingleton<ISqlProvider>(provider);

        // Act
        services.AddSqlBuilderProvider(provider, CreateTestBuilder);

        // Assert
        Assert.Single(services, item => item.ServiceType == typeof(ISqlProvider));
        Assert.Single(services, item => item.ImplementationInstance is SqlBuilderFactoryRegistration);
    }

    /// <summary>
    /// 测试目的：多个元数据配置操作应按注册顺序应用。
    /// </summary>
    [Fact]
    public void ConfigureSqlMetadata_WhenMultipleActionsRegistered_ShouldApplyInOrder()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.BoolTrueValue = "first");
        services.ConfigureSqlMetadata(options => options.BoolTrueValue = "second");
        services.AddSqlCore();

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqlMetadataOptions>();

        // Assert
        Assert.Equal("second", options.BoolTrueValue);
    }

    /// <summary>
    /// 测试目的：通过依赖注入配置的映射缓存容量应传递给单例解析器，并限制不同最终映射的缓存条目数。
    /// </summary>
    [Fact]
    public void ConfigureSqlMetadata_WhenMappingCacheCapacityIsConfigured_ShouldApplyToResolver()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options =>
        {
            options.EntityMappingCacheCapacity = 1;
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(TestEntity),
                MappingProfile = "primary",
                TableName = "primary_entities"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(TestEntity),
                MappingProfile = "reporting",
                TableName = "reporting_entities"
            });
        });
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();
        var resolver = Assert.IsType<DefaultEntityMappingResolver>(provider.GetRequiredService<IEntityMappingResolver>());

        // Act
        var primary = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "primary" });
        var reporting = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "reporting" });
        var cachedPrimary = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "primary" });
        var uncachedReporting = resolver.Resolve(typeof(TestEntity), new DatabaseContext { MappingProfile = "reporting" });

        // Assert
        Assert.Equal("primary_entities", primary.Table.TableName);
        Assert.Equal("reporting_entities", reporting.Table.TableName);
        Assert.Same(primary, cachedPrimary);
        Assert.NotSame(reporting, uncachedReporting);
    }

    /// <summary>
    /// 测试目的：空键数据源应使用默认 Key，Doris 数据源默认只读且关闭本地事务。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenKeyMissingOrDorisConfigured_ShouldUseDefaultKeyAndApplyReadOnlyDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource(null, DatabaseType.Doris, "Server=doris;Database=analytics;");
        services.AddSqlCore();

        // Act
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqlMetadataOptions>();
        var dataSource = options.DataSources.DataSources[options.DataSources.DefaultDataSourceKey];

        // Assert
        Assert.Equal("default", dataSource.Key);
        Assert.Equal(DatabaseType.Doris, dataSource.DatabaseType);
        Assert.True(dataSource.IsReadOnly);
        Assert.False(dataSource.SupportsTransactions);
        Assert.Equal("Server=doris;Database=analytics;", dataSource.ConnectionString);
    }

    /// <summary>
    /// 测试目的：Doris 兼容数据源不得由配置回调解除只读和无事务边界，避免误将分析端点作为可写 MySQL 使用。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDorisSetupActionEnablesWrites_ShouldPreserveReadOnlyTransactionBoundary()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource("doris", DatabaseType.Doris, "Server=doris;Database=analytics;",
            setupAction: source =>
            {
                source.MappingProfile = "analytics";
                source.IsReadOnly = false;
                source.SupportsTransactions = true;
            });
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var dataSource = provider.GetRequiredService<SqlMetadataOptions>().DataSources.DataSources["doris"];

        // Assert
        Assert.Equal("analytics", dataSource.MappingProfile);
        Assert.True(dataSource.IsReadOnly);
        Assert.False(dataSource.SupportsTransactions);
    }

    /// <summary>
    /// 测试目的：直接配置的 Doris 描述符也必须保留只读和无事务边界，不能绕过数据源注册扩展。
    /// </summary>
    [Fact]
    public void ConfigureSqlMetadata_WhenDorisDescriptorEnablesWrites_ShouldNormalizeReadOnlyTransactionBoundary()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.DataSources.DataSources["doris"] = new SqlDataSourceDescriptor
        {
            Key = "doris",
            DatabaseType = DatabaseType.Doris,
            ConnectionString = "Server=doris;Database=analytics;",
            IsReadOnly = false,
            SupportsTransactions = true
        });
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var dataSource = provider.GetRequiredService<SqlMetadataOptions>().DataSources.DataSources["doris"];

        // Assert
        Assert.True(dataSource.IsReadOnly);
        Assert.False(dataSource.SupportsTransactions);
    }

    /// <summary>
    /// 测试目的：预注册的 Doris 元数据选项实例也必须归一化，不能因跳过核心选项工厂而解除只读和无事务边界。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenDorisOptionsArePreRegistered_ShouldNormalizeReadOnlyTransactionBoundary()
    {
        // Arrange
        var options = new SqlMetadataOptions();
        options.DataSources.DataSources["doris"] = new SqlDataSourceDescriptor
        {
            Key = "doris",
            DatabaseType = DatabaseType.Doris,
            ConnectionString = "Server=doris;Database=analytics;",
            IsReadOnly = false,
            SupportsTransactions = true
        };
        var services = new ServiceCollection();
        services.AddSingleton(options);

        // Act
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();
        var dataSource = provider.GetRequiredService<SqlMetadataOptions>().DataSources.DataSources["doris"];

        // Assert
        Assert.True(dataSource.IsReadOnly);
        Assert.False(dataSource.SupportsTransactions);
    }

    /// <summary>
    /// 测试目的：预注册工厂创建的 Doris 元数据也必须在解析时归一化，不能绕过只读和无事务边界。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenDorisOptionsFactoryIsPreRegistered_ShouldNormalizeReadOnlyTransactionBoundary()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<SqlMetadataOptions>(_ =>
        {
            var options = new SqlMetadataOptions();
            options.DataSources.DataSources["doris"] = new SqlDataSourceDescriptor
            {
                Key = "doris",
                DatabaseType = DatabaseType.Doris,
                ConnectionString = "Server=doris;Database=analytics;",
                IsReadOnly = false,
                SupportsTransactions = true
            };
            return options;
        });

        // Act
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();
        var dataSource = provider.GetRequiredService<SqlMetadataOptions>().DataSources.DataSources["doris"];

        // Assert
        Assert.True(dataSource.IsReadOnly);
        Assert.False(dataSource.SupportsTransactions);
    }

    /// <summary>
    /// 测试目的：预注册实现类型创建的 Doris 元数据也必须在解析时归一化，并保留原单例生命周期。
    /// </summary>
    [Fact]
    public void AddSqlCore_WhenDorisOptionsImplementationTypeIsPreRegistered_ShouldNormalizeBoundaryAndKeepSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<SqlMetadataOptions, WritableDorisMetadataOptions>();

        // Act
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SqlMetadataOptions>();
        var dataSource = options.DataSources.DataSources["doris"];

        // Assert
        Assert.True(dataSource.IsReadOnly);
        Assert.False(dataSource.SupportsTransactions);
        Assert.Same(options, provider.GetRequiredService<SqlMetadataOptions>());
    }

    /// <summary>
    /// 测试目的：默认数据源已绑定其他 Provider 时，无键重复注册不得覆盖其类型。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDefaultProviderChanges_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource(null, DatabaseType.Sqlite, "Data Source=first.db");
        services.AddSqlDataSource(null, DatabaseType.SqlServer, "Server=second");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<SqlMetadataOptions>());
    }

    /// <summary>
    /// 测试目的：相同具名数据源以不同连接配置重复注册时必须拒绝，不能由后注册项静默覆盖。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenNamedKeyIsRepeatedWithDifferentConfiguration_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource("reporting", DatabaseType.Sqlite, "Data Source=first.db");
        services.AddSqlDataSource(" REPORTING ", DatabaseType.Sqlite, "Data Source=second.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act and Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<SqlMetadataOptions>());
        Assert.Contains("reporting", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("重复注册", exception.Message);
    }

    /// <summary>
    /// 测试目的：相同具名数据源重复注册应保持幂等，避免旧注册组合产生重复配置。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenNamedConfigurationIsIdentical_ShouldRemainIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSqlDataSource("reporting", DatabaseType.Sqlite, "Data Source=reporting.db");
        services.AddSqlDataSource(" REPORTING ", DatabaseType.Sqlite, "Data Source=reporting.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlMetadataOptions>();

        // Assert
        Assert.Single(options.DataSources.DataSources);
        Assert.Equal("reporting", options.DataSources.DataSources["reporting"].Key);
    }

    /// <summary>
    /// 测试目的：默认 Key 被显式清空时，首个具名数据源不得隐式成为默认源，解析器只能通过唯一数据源规则回退。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDefaultKeyIsEmptyAndNamedSourceAdded_ShouldKeepDefaultKeyEmpty()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = null);
        services.AddSqlDataSource("reporting", DatabaseType.Sqlite, "Data Source=reporting.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<SqlMetadataOptions>();
        var dataSource = provider.GetRequiredService<ISqlDataSourceResolver>().Resolve();

        // Assert
        Assert.Null(options.DataSources.DefaultDataSourceKey);
        Assert.Equal("reporting", dataSource.Key);
    }

    /// <summary>
    /// 测试目的：未配置默认 Key 时，无键数据源注册必须明确失败，避免将空键写入数据源集合。
    /// </summary>
    [Fact]
    public void AddSqlDataSource_WhenDefaultKeyIsEmptyAndKeyMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = null);
        services.AddSqlDataSource(null, DatabaseType.Sqlite, "Data Source=default.db");
        services.AddSqlCore();
        using var provider = services.BuildServiceProvider();

        // Act and Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<SqlMetadataOptions>());
        Assert.Contains(nameof(SqlDataSourceOptions.DefaultDataSourceKey), exception.Message);
    }

    /// <summary>
    /// 仅用于 Provider Key 注册测试的 SQL Provider。
    /// </summary>
    private sealed class TestSqlProvider : ISqlProvider
    {
        /// <summary>
        /// 初始化一个 <see cref="TestSqlProvider"/> 类型的实例。
        /// </summary>
        /// <param name="key">Provider 标识。</param>
        public TestSqlProvider(string key) => Key = key;

        /// <inheritdoc />
        public string Key { get; }

        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.Sqlite;

        /// <inheritdoc />
        public IDialect Dialect => null;

        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => null;

        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => null;

        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => null;

        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => null;

        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => null;
    }

    /// <summary>
    /// 创建用于验证同一委托幂等注册的占位 Builder 委托。
    /// </summary>
    /// <param name="services">传入创建委托的查询级共享服务。</param>
    /// <returns>始终返回 <see langword="null"/> 的测试占位值；本测试不创建实际 Builder。</returns>
    private static ISqlBuilder CreateTestBuilder(SqlBuilderServices services) => null;

    /// <summary>
    /// 创建用于验证不同委托冲突的备用占位 Builder 委托。
    /// </summary>
    /// <param name="services">传入创建委托的查询级共享服务。</param>
    /// <returns>始终返回 <see langword="null"/> 的测试占位值；本测试不创建实际 Builder。</returns>
    private static ISqlBuilder CreateAlternativeTestBuilder(SqlBuilderServices services) => null;

    /// <summary>
    /// 仅用于实体元数据注册测试的实体。
    /// </summary>
    private sealed class TestEntity
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 用于验证实现类型注册归一化的 Doris 元数据选项。
    /// </summary>
    private sealed class WritableDorisMetadataOptions : SqlMetadataOptions
    {
        /// <summary>
        /// 初始化一个 <see cref="WritableDorisMetadataOptions"/> 类型的实例。
        /// </summary>
        public WritableDorisMetadataOptions()
        {
            DataSources.DataSources["doris"] = new SqlDataSourceDescriptor
            {
                Key = "doris",
                DatabaseType = DatabaseType.Doris,
                ConnectionString = "Server=doris;Database=analytics;",
                IsReadOnly = false,
                SupportsTransactions = true
            };
        }
    }

    /// <summary>
    /// 仅用于实体元数据注册测试的自定义提供器。
    /// </summary>
    private sealed class TestEntityModelMetadataProvider : IEntityModelMetadataProvider
    {
        /// <inheritdoc />
        public EntityModelMetadata GetMetadata(Type entityType)
        {
            if (entityType != typeof(TestEntity))
                return null;
            return new EntityModelMetadata(entityType, "custom_entities", string.Empty,
                new[] { new EntityPropertyMetadata(entityType.GetProperty(nameof(TestEntity.Id)), isKey: true) });
        }

        /// <inheritdoc />
        public EntityModelMetadata GetMetadata<TEntity>() => GetMetadata(typeof(TEntity));
    }

    /// <summary>
    /// 用于验证调用方自定义工厂注册语义的测试实现。
    /// </summary>
    private sealed class TestSqlQueryFactory : ISqlQueryFactory
    {
        /// <inheritdoc />
        public ISqlQuery Create(string dbKey = null) => throw new NotSupportedException();
    }

    /// <summary>
    /// 代表迁移前会错误捕获 Scoped 工厂的 Singleton 消费者。
    /// </summary>
    private sealed class DirectFactorySingletonConsumer
    {
        /// <summary>
        /// 初始化 Singleton 消费者。
        /// </summary>
        /// <param name="factory">请求作用域 SQL 查询工厂。</param>
        public DirectFactorySingletonConsumer(ISqlQueryFactory factory) => Factory = factory;

        /// <summary>
        /// 获取被直接捕获的 SQL 查询工厂。
        /// </summary>
        public ISqlQueryFactory Factory { get; }
    }

    /// <summary>
    /// 在每次业务操作中独立创建服务作用域的 Singleton 消费者。
    /// </summary>
    private sealed class ScopedFactoryOperation
    {
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>
        /// 初始化操作级服务作用域工厂。
        /// </summary>
        /// <param name="scopeFactory">DI 服务作用域工厂。</param>
        public ScopedFactoryOperation(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

        /// <summary>
        /// 在本次业务操作的作用域内解析 SQL 查询工厂。
        /// </summary>
        /// <returns>作用域内查询工厂实现的类型名称。</returns>
        public string ResolveFactoryTypeForOperation()
        {
            using var scope = _scopeFactory.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ISqlQueryFactory>().GetType().FullName;
        }
    }
}
