using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>
/// 验证生成键 Insert 命令与官方 Dapper Provider 的 SQL 方言及映射契约一致。
/// </summary>
public sealed class SqlKeyInsertCommandBuilderTest
{
    /// <summary>
    /// 获取仅含默认自增主键的 SQL 测试用例。
    /// </summary>
    public static IEnumerable<object[]> IdentityDefaultSqlCases => new[]
    {
        new object[] { DatabaseType.SqlServer, "Insert Into [identity_defaults] OUTPUT INSERTED.[Id] DEFAULT VALUES", true },
        new object[] { DatabaseType.PgSql, "Insert Into \"identity_defaults\" DEFAULT VALUES RETURNING \"Id\"", true },
        new object[] { DatabaseType.Sqlite, "Insert Into `identity_defaults` DEFAULT VALUES RETURNING `Id`", true },
        new object[] { DatabaseType.MySql, "Insert Into `identity_defaults` () VALUES ()", false }
    };

    /// <summary>
    /// 获取自增主键并包含名称列的 SQL 测试用例。
    /// </summary>
    public static IEnumerable<object[]> IdentityWithNameSqlCases => new[]
    {
        new object[] { DatabaseType.SqlServer, "Insert Into [identity_rows] ([Name]) Output [Inserted].[Id] Values (@_p_0)", true },
        new object[] { DatabaseType.PgSql, "Insert Into \"identity_rows\" (\"Name\") Values (@_p_0) Returning \"Id\"", true },
        new object[] { DatabaseType.Sqlite, "Insert Into `identity_rows` (`Name`) Values (@_p_0) Returning `Id`", true },
        new object[] { DatabaseType.MySql, "Insert Into `identity_rows` (`Name`) Values (@_p_0)", false }
    };

    /// <summary>
    /// 获取客户端生成主键的 SQL 测试用例。
    /// </summary>
    public static IEnumerable<object[]> ClientGeneratedSqlCases => new[]
    {
        new object[] { DatabaseType.SqlServer, "Insert Into [client_rows] ([Id], [Name]) Values (@_p_0, @_p_1)" },
        new object[] { DatabaseType.PgSql, "Insert Into \"client_rows\" (\"Id\", \"Name\") Values (@_p_0, @_p_1)" },
        new object[] { DatabaseType.Sqlite, "Insert Into `client_rows` (`Id`, `Name`) Values (@_p_0, @_p_1)" },
        new object[] { DatabaseType.MySql, "Insert Into `client_rows` (`Id`, `Name`) Values (@_p_0, @_p_1)" }
    };

    /// <summary>
    /// 数据库自增实体只有主键列时，命令应采用各官方 Provider 支持的默认值写入和回填语法。
    /// </summary>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <param name="expectedSql">预期生成的完整 SQL。</param>
    /// <param name="hasReturning">SQL 提供程序是否支持返回生成键。</param>
    [Theory]
    [MemberData(nameof(IdentityDefaultSqlCases))]
    public void Build_IdentityWithoutInsertableColumns_ShouldUseProviderDefaultInsert(
        DatabaseType databaseType, string expectedSql, bool hasReturning)
    {
        var command = Build(GetOfficialProvider(databaseType), new IdentityKeyOnlyRow(),
            EntityKeyGenerationKind.DatabaseIdentity, "identity_defaults");

        Assert.Equal(expectedSql, command.Sql);
        Assert.Equal(hasReturning, command.HasReturning);
    }

    /// <summary>
    /// 数据库自增实体存在普通字段时，命令应排除自增列并按 Provider 规则返回主键。
    /// </summary>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <param name="expectedSql">预期生成的完整 SQL。</param>
    /// <param name="hasReturning">SQL 提供程序是否支持返回生成键。</param>
    [Theory]
    [MemberData(nameof(IdentityWithNameSqlCases))]
    public void Build_IdentityWithInsertableColumns_ShouldReturnGeneratedKey(
        DatabaseType databaseType, string expectedSql, bool hasReturning)
    {
        var command = Build(GetOfficialProvider(databaseType), new IdentityRow { Name = "parent" },
            EntityKeyGenerationKind.DatabaseIdentity, "identity_rows");

        Assert.Equal(expectedSql, command.Sql);
        Assert.Equal(hasReturning, command.HasReturning);
        Assert.Equal(new object[] { "parent" }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 客户端 GUID 与雪花主键必须显式写入，且完整 SQL 依赖各官方 Provider 的标识符方言。
    /// </summary>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <param name="expectedSql">预期生成的完整 SQL。</param>
    [Theory]
    [MemberData(nameof(ClientGeneratedSqlCases))]
    public void Build_ClientGeneratedKeys_ShouldIncludeKeyAndOtherColumns(DatabaseType databaseType, string expectedSql)
    {
        var provider = GetOfficialProvider(databaseType);
        var snowflake = new ClientSnowflakeRow { Id = 9_007_199_254_740_993L, Name = "snowflake" };
        var guid = new ClientGuidRow
        {
            Id = Guid.Parse("0e93a3cb-843e-48da-826a-f74b4adfbada"),
            Name = "guid"
        };

        var snowflakeCommand = Build(provider, snowflake, EntityKeyGenerationKind.ClientSnowflake, "client_rows");
        var guidCommand = Build(provider, guid, EntityKeyGenerationKind.ClientGuid, "client_rows");

        Assert.Equal(expectedSql, snowflakeCommand.Sql);
        Assert.Equal(expectedSql, guidCommand.Sql);
        Assert.Equal(new object[] { snowflake.Id, snowflake.Name },
            snowflakeCommand.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { guid.Id, guid.Name },
            guidCommand.Parameters.Select(parameter => parameter.Value));
        Assert.False(snowflakeCommand.HasReturning);
        Assert.False(guidCommand.HasReturning);
    }

    /// <summary>
    /// 已缓存路由重复解析应命中，同一实体的另一数据源路由必须产生独立映射。
    /// </summary>
    [Fact]
    public void Build_WhenResolverCacheHitsAndRouteChanges_ShouldKeepMappingsIsolated()
    {
        var provider = GetOfficialProvider(DatabaseType.SqlServer);
        var metadata = new SqlMetadataOptions();
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MappingCacheRow),
            DbKey = "alpha",
            DatabaseType = DatabaseType.SqlServer,
            TableName = "rows_alpha"
        });
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(MappingCacheRow),
            DbKey = "beta",
            DatabaseType = DatabaseType.SqlServer,
            TableName = "rows_beta"
        });
        var resolver = new DefaultEntityMappingResolver(options: metadata);
        var policy = CreatePolicy<MappingCacheRow>(EntityKeyGenerationKind.ClientSnowflake);
        var alphaContext = CreateContext("alpha", DatabaseType.SqlServer);
        var betaContext = CreateContext("beta", DatabaseType.SqlServer);

        var alphaFirst = Build(provider, new MappingCacheRow { Id = 11, Name = "alpha" }, policy,
            CreateServices(resolver, metadata, alphaContext));
        var alphaCached = Build(provider, new MappingCacheRow { Id = 12, Name = "alpha" }, policy,
            CreateServices(resolver, metadata, alphaContext));
        var beta = Build(provider, new MappingCacheRow { Id = 13, Name = "beta" }, policy,
            CreateServices(resolver, metadata, betaContext));

        Assert.Equal("Insert Into [rows_alpha] ([Id], [Name]) Values (@_p_0, @_p_1)", alphaFirst.Sql);
        Assert.Equal(alphaFirst.Sql, alphaCached.Sql);
        Assert.Equal("Insert Into [rows_beta] ([Id], [Name]) Values (@_p_0, @_p_1)", beta.Sql);
        Assert.Equal(2, resolver.MappingCacheStatistics.CacheMissCount);
        Assert.Equal(1, resolver.MappingCacheStatistics.CacheHitCount);
    }

    /// <summary>
    /// 同一 Builder 重复渲染及使用新 Builder 重复构造时，都应得到稳定 SQL。
    /// </summary>
    [Fact]
    public void Build_RepeatedBuildAndRender_ShouldUseFreshBuilderAndRemainStable()
    {
        var provider = GetOfficialProvider(DatabaseType.SqlServer);
        var metadata = CreateMetadata(typeof(ClientSnowflakeRow), provider.DatabaseType, "client_rows");
        var resolver = new DefaultEntityMappingResolver(options: metadata);
        var services = CreateServices(resolver, metadata, metadata.DefaultDatabaseContext);
        var policy = CreatePolicy<ClientSnowflakeRow>(EntityKeyGenerationKind.ClientSnowflake);
        var firstBuilder = new SqlInsertBuilder(provider, services);
        var firstCommand = new SqlKeyInsertCommandBuilder(firstBuilder, policy)
            .Build(new ClientSnowflakeRow { Id = 71, Name = "same" });

        Assert.Equal(firstCommand.Sql, firstBuilder.ToSql());
        Assert.Equal(firstCommand.Sql, firstBuilder.ToSql());

        var secondBuilder = new SqlInsertBuilder(provider, services);
        var secondCommand = new SqlKeyInsertCommandBuilder(secondBuilder, policy)
            .Build(new ClientSnowflakeRow { Id = 71, Name = "same" });

        Assert.Equal(firstCommand.Sql, secondCommand.Sql);
        Assert.Equal(1, resolver.MappingCacheStatistics.CacheHitCount);
    }

    /// <summary>
    /// 测试目的：严格生成模型的键插入应直接读取计划 Getter，不创建兼容 Getter 缓存项。
    /// </summary>
    [Fact]
    public void Build_WithGeneratedModel_ShouldUsePlanGetterForAllInsertColumns()
    {
        var metadata = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(GeneratedKeyRow),
            TableName = "generated_key_rows"
        });
        var model = new EntityModelMetadata(typeof(GeneratedKeyRow), "generated_key_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedKeyRow.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source => ((GeneratedKeyRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(GeneratedKeyRow.Name), typeof(string), true, true,
                generatedGetter: source => ((GeneratedKeyRow)source).Name)
        }, () => new GeneratedKeyRow());
        var models = new RegisteredEntityModelMetadataProvider(model);
        var services = new SqlBuilderServices(metadataOptions: metadata, entityModelMetadataProvider: models);
        var policy = EntityKeyPolicy.CreateGenerated<GeneratedKeyRow, int>(nameof(GeneratedKeyRow.Id),
            EntityKeyGenerationKind.Assigned, row => row.Id, (row, value) => row.Id = value);
        var builder = new SqlInsertBuilder(SqlServerSqlProvider.Instance, services);

        var command = new SqlKeyInsertCommandBuilder(builder, policy)
            .Build(new GeneratedKeyRow { Id = 8, Name = "generated" });

        Assert.Equal("Insert Into [generated_key_rows] ([Id], [Name]) Values (@_p_0, @_p_1)", command.Sql);
        Assert.Equal(new object[] { 8, "generated" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.IsGeneratedExecution);
        var nextCommand = new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(SqlServerSqlProvider.Instance, services), policy)
            .Build(new GeneratedKeyRow { Id = 9, Name = "next" });
        Assert.Equal("Insert Into [generated_key_rows] ([Id], [Name]) Values (@_p_0, @_p_1)", nextCommand.Sql);
        Assert.Equal(new object[] { 9, "next" }, nextCommand.Parameters.Select(parameter => parameter.Value));
        Assert.True(nextCommand.IsGeneratedExecution);
        var cache = SqlMutationPlanCaches.Get(services.EntityMappingResolver, services.MetadataOptions,
            services.EntityModelMetadataProvider);
        Assert.Equal(0, cache.GetterCount);
        Assert.Equal(1, cache.PlanCount);
        Assert.Equal(1, cache.PlanCacheMissCount);
        Assert.Equal(1, cache.PlanCacheHitCount);
    }

    /// <summary>
    /// Provider 不支持自增键回填时，应在生成写入 SQL 前拒绝该策略。
    /// </summary>
    [Fact]
    public void Build_CustomOracleProviderIdentity_ShouldRejectBeforeSqlGeneration()
    {
        // 自定义 Provider 仅声明 Oracle DatabaseType，未登记官方生成键适配。
        var commandBuilder = new SqlInsertBuilder(new OracleDatabaseTypeTestProvider(),
            CreateServices(typeof(IdentityKeyOnlyRow), DatabaseType.Oracle, "oracle_identity"));
        var policy = CreatePolicy<IdentityKeyOnlyRow>(EntityKeyGenerationKind.DatabaseIdentity);

        var exception = Assert.Throws<NotSupportedException>(() =>
            new SqlKeyInsertCommandBuilder(commandBuilder, policy).Build(new IdentityKeyOnlyRow()));

        Assert.Equal("当前 Provider 尚无已验证的生成键返回适配。", exception.Message);
    }

    /// <summary>
    /// 客户端生成策略不能绑定数据库生成列或未标记为主键的列。
    /// </summary>
    /// <param name="databaseGenerated">实体键是否由数据库生成。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_WhenMappingConflictsWithClientKeyPolicy_ShouldReject(bool databaseGenerated)
    {
        var provider = GetOfficialProvider(DatabaseType.Sqlite);
        var entityType = databaseGenerated ? typeof(IdentityRow) : typeof(UnmappedKeyRow);
        var options = CreateMetadata(entityType, provider.DatabaseType,
            databaseGenerated ? "identity_rows" : "unmapped_key_rows");
        var services = CreateServices(options);
        var policy = databaseGenerated
            ? CreatePolicy<IdentityRow>(EntityKeyGenerationKind.ClientSnowflake)
            : CreatePolicy<UnmappedKeyRow>(EntityKeyGenerationKind.ClientSnowflake, nameof(UnmappedKeyRow.BusinessKey));
        var builder = new SqlInsertBuilder(provider, services);
        Action build = databaseGenerated
            ? () => new SqlKeyInsertCommandBuilder(builder, policy).Build(new IdentityRow { Name = "client" })
            : () => new SqlKeyInsertCommandBuilder(builder, policy).Build(new UnmappedKeyRow { BusinessKey = 10, Name = "client" });

        Assert.Throws<InvalidOperationException>(build);
    }

    /// <summary>
    /// 构建带实体键策略的插入命令。
    /// </summary>
    /// <typeparam name="TEntity">被聚合、映射或写入的实体类型。</typeparam>
    /// <param name="provider">目标 SQL 提供程序。</param>
    /// <param name="entity">待写入的领域实体。</param>
    /// <param name="kind">实体键的生成策略。</param>
    /// <param name="tableName">目标物理表名。</param>
    /// <returns>包含 SQL、参数及生成键信息的插入命令。</returns>
    private static SqlWriteCommand Build<TEntity>(ISqlProvider provider, TEntity entity,
        EntityKeyGenerationKind kind, string tableName) where TEntity : class
    {
        var options = CreateMetadata(typeof(TEntity), provider.DatabaseType, tableName);
        return Build(provider, entity, CreatePolicy<TEntity>(kind), CreateServices(options));
    }

    /// <summary>
    /// 验证计算主键在各方言中拒绝自增插入策略。
    /// </summary>
    /// <param name="databaseType">目标数据库类型。</param>
    [Theory]
    [InlineData(DatabaseType.SqlServer)]
    [InlineData(DatabaseType.PgSql)]
    [InlineData(DatabaseType.MySql)]
    [InlineData(DatabaseType.Sqlite)]
    public void Build_ComputedPrimaryKey_ShouldRejectIdentityPolicy(DatabaseType databaseType)
    {
        Assert.Throws<InvalidOperationException>(() => Build(GetOfficialProvider(databaseType), new ComputedKeyRow(),
            EntityKeyGenerationKind.DatabaseIdentity, "computed_rows"));
    }

    /// <summary>
    /// 提供插入命令主键处理测试使用的查询数据模型。
    /// </summary>
    private sealed class ComputedKeyRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [System.ComponentModel.DataAnnotations.Key, DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public long Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 构建带实体键策略的插入命令。
    /// </summary>
    /// <typeparam name="TEntity">被聚合、映射或写入的实体类型。</typeparam>
    /// <param name="provider">目标 SQL 提供程序。</param>
    /// <param name="entity">待写入的领域实体。</param>
    /// <param name="policy">实体键生成策略。</param>
    /// <param name="services">执行查询所需的构建器服务。</param>
    /// <returns>包含 SQL、参数及生成键信息的插入命令。</returns>
    private static SqlWriteCommand Build<TEntity>(ISqlProvider provider, TEntity entity,
        EntityKeyPolicy policy, SqlBuilderServices services) where TEntity : class
    {
        var builder = new SqlInsertBuilder(provider, services);
        return new SqlKeyInsertCommandBuilder(builder, policy).Build(entity);
    }

    /// <summary>
    /// 创建插入命令测试的构建器服务。
    /// </summary>
    /// <param name="entityType">待映射或校验的实体类型。</param>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <param name="tableName">目标物理表名。</param>
    /// <returns>包含实体映射、元数据选项及数据库上下文的构建器服务。</returns>
    private static SqlBuilderServices CreateServices(Type entityType, DatabaseType databaseType, string tableName) =>
        CreateServices(CreateMetadata(entityType, databaseType, tableName));

    /// <summary>
    /// 创建插入命令测试的构建器服务。
    /// </summary>
    /// <param name="metadata">实体映射元数据选项。</param>
    /// <returns>包含实体映射、元数据选项及数据库上下文的构建器服务。</returns>
    private static SqlBuilderServices CreateServices(SqlMetadataOptions metadata) =>
        CreateServices(new DefaultEntityMappingResolver(options: metadata), metadata, metadata.DefaultDatabaseContext);

    /// <summary>
    /// 创建插入命令测试的构建器服务。
    /// </summary>
    /// <param name="resolver">实体映射解析器。</param>
    /// <param name="metadata">实体映射元数据选项。</param>
    /// <param name="context">数据源所属的数据库上下文。</param>
    /// <returns>包含实体映射、元数据选项及数据库上下文的构建器服务。</returns>
    private static SqlBuilderServices CreateServices(DefaultEntityMappingResolver resolver,
        SqlMetadataOptions metadata, DatabaseContext context) =>
        new(entityMappingResolver: resolver, metadataOptions: metadata,
            options: new SqlOptions().SetDatabaseContext(context));

    /// <summary>
    /// 创建插入命令测试的实体映射元数据。
    /// </summary>
    /// <param name="entityType">待映射或校验的实体类型。</param>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <param name="tableName">目标物理表名。</param>
    /// <returns>包含指定数据库及实体表映射的元数据选项。</returns>
    private static SqlMetadataOptions CreateMetadata(Type entityType, DatabaseType databaseType, string tableName)
    {
        var metadata = new SqlMetadataOptions();
        metadata.DefaultDatabaseContext.DataSource = new SqlDataSourceDescriptor { DatabaseType = databaseType };
        metadata.EntityMappings.Add(new EntityMappingOptions { EntityType = entityType, TableName = tableName });
        return metadata;
    }

    /// <summary>
    /// 创建指定数据库键及类型的上下文。
    /// </summary>
    /// <param name="dbKey">数据库上下文键。</param>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <returns>包含指定数据源或映射信息的数据库上下文。</returns>
    private static DatabaseContext CreateContext(string dbKey, DatabaseType databaseType) => new()
    {
        DbKey = dbKey,
        DataSource = new SqlDataSourceDescriptor { Key = dbKey, DatabaseType = databaseType }
    };

    /// <summary>
    /// 创建指定实体属性的键生成策略。
    /// </summary>
    /// <typeparam name="TEntity">被聚合、映射或写入的实体类型。</typeparam>
    /// <param name="kind">实体键的生成策略。</param>
    /// <param name="propertyName">实体键属性名称。</param>
    /// <returns>指定实体属性的键生成策略。</returns>
    private static EntityKeyPolicy CreatePolicy<TEntity>(EntityKeyGenerationKind kind, string propertyName = "Id") =>
        new(typeof(TEntity), typeof(TEntity).GetProperty(propertyName)!, kind);

    /// <summary>
    /// 选择数据库对应的官方 SQL 提供程序。
    /// </summary>
    /// <param name="databaseType">目标数据库类型。</param>
    /// <returns>与目标数据库或数据源匹配的 SQL 提供程序。</returns>
    private static ISqlProvider GetOfficialProvider(DatabaseType databaseType) => databaseType switch
    {
        DatabaseType.SqlServer => Bing.Data.Sql.Builders.SqlServerSqlProvider.Instance,
        DatabaseType.PgSql => Bing.Data.Sql.Builders.PostgreSqlSqlProvider.Instance,
        DatabaseType.Sqlite => Bing.Data.Sql.Builders.SqliteSqlProvider.Instance,
        DatabaseType.MySql => Bing.Data.Sql.Builders.MySqlSqlProvider.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, null)
    };

    /// <summary>
    /// 为 SQL Provider 测试提供 Oracle 数据库类型配置。
    /// </summary>
    private sealed class OracleDatabaseTypeTestProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        /// <summary>
        /// 保存 Oracle 数据库类型测试使用的 SQL 构建器提供程序。
        /// </summary>
        private readonly Bing.Data.Sql.Builders.SqlServerSqlProvider _builderProvider =
            Bing.Data.Sql.Builders.SqlServerSqlProvider.Instance;

        /// <inheritdoc />
        public string Key => "test.oracle-key-rejection";
        /// <inheritdoc />
        public DatabaseType DatabaseType => DatabaseType.Oracle;
        /// <inheritdoc />
        public IDialect Dialect => _builderProvider.Dialect;
        /// <inheritdoc />
        public ISqlClauseFactory ClauseFactory => _builderProvider.ClauseFactory;
        /// <inheritdoc />
        public ISqlTableReferenceParser TableReferenceParser => _builderProvider.TableReferenceParser;
        /// <inheritdoc />
        public ISqlPaginationRenderer PaginationRenderer => _builderProvider.PaginationRenderer;
        /// <inheritdoc />
        public IParameterManagerFactory ParameterManagerFactory => _builderProvider.ParameterManagerFactory;
        /// <inheritdoc />
        public IParamLiteralsResolver ParamLiteralsResolver => _builderProvider.ParamLiteralsResolver;
        /// <inheritdoc />
        public SqlProviderProfile Profile => _builderProvider.Profile;
    }

    /// <summary>
    /// 提供插入命令主键处理测试使用的查询数据模型。
    /// </summary>
    private sealed class IdentityKeyOnlyRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
    }

    /// <summary>
    /// 使用生成 Getter 的客户端键实体。
    /// </summary>
    private sealed class GeneratedKeyRow
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置实体名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 提供插入命令主键处理测试使用的查询数据模型。
    /// </summary>
    private sealed class IdentityRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 提供插入命令主键处理测试使用的查询数据模型。
    /// </summary>
    private sealed class ClientSnowflakeRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 提供插入命令主键处理测试使用的查询数据模型。
    /// </summary>
    private sealed class ClientGuidRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 提供插入命令主键处理测试使用的查询数据模型。
    /// </summary>
    private sealed class MappingCacheRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 提供插入命令主键处理测试使用的查询数据模型。
    /// </summary>
    private sealed class UnmappedKeyRow
    {
        /// <summary>
        /// 获取或设置领域对象的业务键。
        /// </summary>
        public long BusinessKey { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }
}
