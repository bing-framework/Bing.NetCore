using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Bing.Data.Enums;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>
/// 验证身份键和已分配键插入对生成 SQL 的选择及回退边界。
/// </summary>
public sealed class GeneratedKeyInsertSqlCommandTest
{
    private const string GeneratedIdentitySql =
        "Insert Into `generated_identity_rows` (`Title`) Values (@_p_0) Returning `Id`";
    private const string GeneratedIdentityOnlySql =
        "Insert Into `generated_identity_only_rows` DEFAULT VALUES RETURNING `Id`";
    private const string GeneratedAssignedSql =
        "Insert Into `generated_assigned_rows` (`Id`, `Title`) Values (@_p_0, @_p_1)";

    /// <summary>
    /// SQLite 身份键插入应复用完整生成 SQL，同时保留参数、Returning 标志和生成绑定器。
    /// </summary>
    [Fact]
    public void Build_IdentityWithInsertableColumns_ShouldReuseCompleteSqlAndBinder()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows");

        var command = BuildIdentity(fixture, new IdentityRow { Title = "first" });

        Assert.Equal(GeneratedIdentitySql, command.Sql);
        Assert.Same(GeneratedIdentitySql, command.Sql);
        Assert.Equal(new object[] { "first" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new[] { "@_p_0" }, command.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(SqlOperationKind.InsertValues, command.OperationKind);
        Assert.True(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// 仅含身份键的实体应复用 DEFAULT VALUES Returning 常量。
    /// </summary>
    [Fact]
    public void Build_IdentityOnly_ShouldReuseDefaultValuesConstant()
    {
        var fixture = CreateFixture(CreateIdentityOnlyModel(), typeof(IdentityOnlyRow),
            "generated_identity_only_rows");

        var command = BuildIdentityOnly(fixture, new IdentityOnlyRow());

        Assert.Equal(GeneratedIdentityOnlySql, command.Sql);
        Assert.Same(GeneratedIdentityOnlySql, command.Sql);
        Assert.Empty(command.Parameters);
        Assert.True(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// assigned 策略应使用现有 GeneratedMutationSql Insert 常量，而不追加 Returning。
    /// </summary>
    [Fact]
    public void Build_AssignedKey_ShouldReuseGeneratedMutationInsert()
    {
        var fixture = CreateFixture(CreateAssignedModel(), typeof(AssignedRow), "generated_assigned_rows");

        var command = BuildAssigned(fixture, new AssignedRow { Id = 91, Title = "assigned" });

        Assert.Equal(GeneratedAssignedSql, command.Sql);
        Assert.Same(GeneratedAssignedSql, command.Sql);
        Assert.Equal(new object[] { 91L, "assigned" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.False(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>
    /// 最终表名或列名被覆盖时，身份键插入应使用运行时映射重新渲染。
    /// </summary>
    [Theory]
    [InlineData("mapped_identity_rows", null,
        "Insert Into `mapped_identity_rows` (`Title`) Values (@_p_0) Returning `Id`")]
    [InlineData(null, "DisplayTitle",
        "Insert Into `generated_identity_rows` (`DisplayTitle`) Values (@_p_0) Returning `Id`")]
    public void Build_WhenTableOrColumnMappingDiffers_ShouldRenderMappedSql(
        string tableName, string titleColumn, string expectedSql)
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows",
            tableName, titleColumn);

        var command = BuildIdentity(fixture, new IdentityRow { Title = "mapped" });

        Assert.Equal(expectedSql, command.Sql);
        Assert.NotSame(GeneratedIdentitySql, command.Sql);
        Assert.Equal(new object[] { "mapped" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.HasReturning);
    }

    /// <summary>
    /// SQLite 身份键 SQL 不能用于 SQL Server Provider，必须保留其 OUTPUT 方言。
    /// </summary>
    [Fact]
    public void Build_WhenProviderDiffers_ShouldRenderProviderSql()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows",
            provider: SqlServerSqlProvider.Instance);

        var command = BuildIdentity(fixture, new IdentityRow { Title = "server" });

        Assert.Equal("Insert Into [generated_identity_rows] ([Title]) Output [Inserted].[Id] " +
                     "Values (@_p_0)", command.Sql);
        Assert.NotSame(GeneratedIdentitySql, command.Sql);
        Assert.Equal(SqlServerSqlProvider.Instance.Key, command.ProviderKey);
        Assert.True(command.HasReturning);
    }

    /// <summary>
    /// 自定义对象名称格式化器应使静态 SQL 失效并参与完整语句渲染。
    /// </summary>
    [Fact]
    public void Build_WhenCustomObjectNameFormatterIsConfigured_ShouldRenderFormattedSql()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows",
            objectNameFormatter: new PrefixingObjectNameFormatter());

        var command = BuildIdentity(fixture, new IdentityRow { Title = "formatted" });

        Assert.Equal("Insert Into `formatted_generated_identity_rows` (`Title`) Values (@_p_0) " +
                     "Returning `Id`", command.Sql);
        Assert.NotSame(GeneratedIdentitySql, command.Sql);
        Assert.Equal(new object[] { "formatted" }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 自定义参数工厂改变参数名时应回退渲染，并保持 SQL 与绑定参数一致。
    /// </summary>
    [Fact]
    public void Build_WhenCustomParameterFactoryChangesNames_ShouldRenderMatchingSqlAndParameters()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows",
            parameterFactory: new RenamingParameterFactory());

        var command = BuildIdentity(fixture, new IdentityRow { Title = "renamed" });

        Assert.Equal("Insert Into `generated_identity_rows` (`Title`) Values (@custom_p_0) " +
                     "Returning `Id`", command.Sql);
        Assert.NotSame(GeneratedIdentitySql, command.Sql);
        Assert.Equal(new[] { "@custom_p_0" }, command.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { "renamed" }, command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>
    /// 多次请求应命中同一映射计划，同时隔离各命令的实体值和参数快照。
    /// </summary>
    [Fact]
    public void Build_ReusedPlan_ShouldKeepRequestParametersIsolated()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows");

        var first = BuildIdentity(fixture, new IdentityRow { Title = "first" });
        var second = BuildIdentity(fixture, new IdentityRow { Title = "second" });
        var planCache = SqlMutationPlanCaches.Get(fixture.Services.EntityMappingResolver,
            fixture.Services.MetadataOptions, fixture.Services.EntityModelMetadataProvider);

        Assert.Same(GeneratedIdentitySql, first.Sql);
        Assert.Same(first.Sql, second.Sql);
        Assert.Equal(new object[] { "first" }, first.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { "second" }, second.Parameters.Select(parameter => parameter.Value));
        Assert.NotSame(first.Parameters, second.Parameters);
        Assert.Equal(1, planCache.PlanCacheMissCount);
        Assert.Equal(1, planCache.PlanCacheHitCount);
        Assert.Equal(1, fixture.Resolver.MappingCacheStatistics.CacheMissCount);
        Assert.Equal(1, fixture.Resolver.MappingCacheStatistics.CacheHitCount);
    }

    /// <summary>
    /// 自定义 Mutation Clause Factory 应被尊重，不能被默认生成语句绕过。
    /// </summary>
    [Fact]
    public void Build_WithCustomMutationClauseFactory_ShouldPreserveCustomClauseSql()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows");
        var builder = new SqlInsertBuilder(SqliteSqlProvider.Instance, fixture.Services,
            clauseFactory: new PrefixInsertMutationClauseFactory());

        var command = new SqlKeyInsertCommandBuilder(builder, CreateIdentityPolicy())
            .Build(new IdentityRow { Title = "custom-clause" });

        Assert.Equal("/*custom*/ Insert Into `generated_identity_rows` (`Title`) Values (@_p_0) " +
                     "Returning `Id`", command.Sql);
        Assert.NotSame(GeneratedIdentitySql, command.Sql);
        Assert.True(command.HasReturning);
    }

    /// <summary>
    /// 已占用参数名称时应使用 Builder 当前参数游标渲染，不能套用默认 SQL 常量。
    /// </summary>
    [Fact]
    public void Build_WithPrefilledBuilderParameter_ShouldPreserveExistingParameterState()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows");
        var parameterManager = new ParameterManager(SqliteSqlProvider.Instance.Dialect);
        parameterManager.Add("@_p_0", "prefilled");
        var builder = new SqlInsertBuilder(SqliteSqlProvider.Instance, fixture.Services, parameterManager);

        var command = new SqlKeyInsertCommandBuilder(builder, CreateIdentityPolicy())
            .Build(new IdentityRow { Title = "new" });

        Assert.Equal("Insert Into `generated_identity_rows` (`Title`) Values (@_p_1) " +
                     "Returning `Id`", command.Sql);
        Assert.NotSame(GeneratedIdentitySql, command.Sql);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, command.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { "prefilled", "new" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.HasReturning);
    }

    /// <summary>
    /// 仅推进参数命名游标时也应重新渲染 SQL，且不把未添加的名称计入命令参数。
    /// </summary>
    [Fact]
    public void Build_WithAdvancedParameterNameAndNoParameters_ShouldRenderUsingCurrentName()
    {
        var fixture = CreateFixture(CreateIdentityModel(), typeof(IdentityRow), "generated_identity_rows");
        var parameterManager = new ParameterManager(SqliteSqlProvider.Instance.Dialect);
        Assert.Equal("@_p_0", parameterManager.GenerateName());
        Assert.Equal(0, parameterManager.Count);
        var builder = new SqlInsertBuilder(SqliteSqlProvider.Instance, fixture.Services, parameterManager);

        var command = new SqlKeyInsertCommandBuilder(builder, CreateIdentityPolicy())
            .Build(new IdentityRow { Title = "cursor-advanced" });

        Assert.Equal("Insert Into `generated_identity_rows` (`Title`) Values (@_p_1) Returning `Id`",
            command.Sql);
        Assert.NotSame(GeneratedIdentitySql, command.Sql);
        Assert.Equal(new[] { "@_p_1" }, command.Parameters.Select(parameter => parameter.Name));
        Assert.Equal(new object[] { "cursor-advanced" }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.HasReturning);
    }

    private static SqlWriteCommand BuildIdentity(TestFixture fixture, IdentityRow entity) =>
        new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(fixture.Provider, fixture.Services),
            CreateIdentityPolicy()).Build(entity);

    private static SqlWriteCommand BuildIdentityOnly(TestFixture fixture, IdentityOnlyRow entity) =>
        new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(fixture.Provider, fixture.Services),
            EntityKeyPolicy.CreateGenerated<IdentityOnlyRow, long>(nameof(IdentityOnlyRow.Id),
                EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, value) => row.Id = value))
            .Build(entity);

    private static SqlWriteCommand BuildAssigned(TestFixture fixture, AssignedRow entity) =>
        new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(fixture.Provider, fixture.Services),
            EntityKeyPolicy.CreateGenerated<AssignedRow, long>(nameof(AssignedRow.Id),
                EntityKeyGenerationKind.Assigned, static row => row.Id, static (row, value) => row.Id = value))
            .Build(entity);

    private static EntityKeyPolicy CreateIdentityPolicy() =>
        EntityKeyPolicy.CreateGenerated<IdentityRow, long>(nameof(IdentityRow.Id),
            EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, value) => row.Id = value);

    private static EntityModelMetadata CreateIdentityModel() => new(typeof(IdentityRow),
        "generated_identity_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(IdentityRow.Id), typeof(long), true, true, isKey: true,
                databaseGeneratedOption: DatabaseGeneratedOption.Identity,
                generatedGetter: static source => ((IdentityRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(IdentityRow.Title), typeof(string), true, true,
                generatedGetter: static source => ((IdentityRow)source).Title)
        }, static () => new IdentityRow())
    {
        GeneratedKeyInsertSql = providerKey => string.Equals(providerKey, SqliteSqlProvider.Instance.Key,
            StringComparison.Ordinal) ? GeneratedIdentitySql : null
    };

    private static EntityModelMetadata CreateIdentityOnlyModel() => new(typeof(IdentityOnlyRow),
        "generated_identity_only_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(IdentityOnlyRow.Id), typeof(long), true, true,
                isKey: true, databaseGeneratedOption: DatabaseGeneratedOption.Identity,
                generatedGetter: static source => ((IdentityOnlyRow)source).Id)
        }, static () => new IdentityOnlyRow())
    {
        GeneratedKeyInsertSql = providerKey => string.Equals(providerKey, SqliteSqlProvider.Instance.Key,
            StringComparison.Ordinal) ? GeneratedIdentityOnlySql : null
    };

    private static EntityModelMetadata CreateAssignedModel() => new(typeof(AssignedRow),
        "generated_assigned_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(AssignedRow.Id), typeof(long), true, true,
                isKey: true, generatedGetter: static source => ((AssignedRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(AssignedRow.Title), typeof(string), true, true,
                generatedGetter: static source => ((AssignedRow)source).Title)
        }, static () => new AssignedRow())
    {
        GeneratedMutationSql = (providerKey, operation) =>
            string.Equals(providerKey, SqliteSqlProvider.Instance.Key, StringComparison.Ordinal) &&
            operation == SqlOperationKind.InsertValues ? GeneratedAssignedSql : null
    };

    private static TestFixture CreateFixture(EntityModelMetadata model, Type entityType, string modelTable,
        string tableName = null, string titleColumn = null, ISqlProvider provider = null,
        ISqlObjectNameFormatter objectNameFormatter = null, ISqlParameterFactory parameterFactory = null)
    {
        provider ??= SqliteSqlProvider.Instance;
        var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        options.DefaultDatabaseContext.DataSource = new SqlDataSourceDescriptor { DatabaseType = provider.DatabaseType };
        var mapping = new EntityMappingOptions { EntityType = entityType, TableName = tableName ?? modelTable };
        if (titleColumn != null)
            mapping.Columns[nameof(IdentityRow.Title)] = new ColumnMappingOptions
            {
                PropertyName = nameof(IdentityRow.Title),
                ColumnName = titleColumn
            };
        options.EntityMappings.Add(mapping);
        var modelProvider = new RegisteredEntityModelMetadataProvider(model);
        var resolver = new DefaultEntityMappingResolver(null, options, null, modelProvider);
        var services = new SqlBuilderServices(entityMappingResolver: resolver, parameterFactory: parameterFactory,
            metadataOptions: options, objectNameFormatter: objectNameFormatter,
            entityModelMetadataProvider: modelProvider);
        return new TestFixture(provider, services, resolver);
    }

    private sealed record TestFixture(ISqlProvider Provider, SqlBuilderServices Services,
        DefaultEntityMappingResolver Resolver);

    private sealed class IdentityRow
    {
        public long Id { get; set; }
        public string Title { get; set; }
    }

    private sealed class IdentityOnlyRow
    {
        public long Id { get; set; }
    }

    private sealed class AssignedRow
    {
        public long Id { get; set; }
        public string Title { get; set; }
    }

    private sealed class PrefixingObjectNameFormatter : ISqlObjectNameFormatter
    {
        public string Format(SqlTableReference reference, IDialect dialect, DatabaseType? databaseType) =>
            dialect.SafeName($"formatted_{reference.TableName}");
    }

    private sealed class RenamingParameterFactory : ISqlParameterFactory
    {
        private readonly ISqlParameterFactory _inner =
            new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector());

        public SqlParam Create(string name, object value, ColumnMappingMetadata column,
            DatabaseContext databaseContext, Type entityType = null,
            SqlParameterSource source = SqlParameterSource.Unknown)
        {
            var original = _inner.Create(name, value, column, databaseContext, entityType, source);
            return new SqlParam($"@custom_{original.Name.TrimStart('@', '_')}", original.Value, original.DbType,
                original.Direction, original.Size, original.Precision, original.Scale)
            {
                OriginalValue = original.OriginalValue,
                IsProviderValue = original.IsProviderValue,
                EntityType = original.EntityType,
                PropertyName = original.PropertyName,
                ColumnName = original.ColumnName,
                DatabaseType = original.DatabaseType,
                ProviderTypeName = original.ProviderTypeName,
                Source = original.Source,
                MetadataLevel = original.MetadataLevel,
                StorageKind = original.StorageKind,
                ConverterKind = original.ConverterKind,
                CustomConverterName = original.CustomConverterName
            };
        }
    }

    private sealed class PrefixInsertMutationClauseFactory : ISqlMutationClauseFactory
    {
        private readonly DefaultSqlMutationClauseFactory _inner = new();

        public IInsertClause CreateInsert(SqlMutationContext context) =>
            new PrefixInsertClause(_inner.CreateInsert(context));

        public IInsertColumnsClause CreateInsertColumns(SqlMutationContext context) =>
            _inner.CreateInsertColumns(context);

        public IValuesClause CreateValues(SqlMutationContext context) => _inner.CreateValues(context);

        public IUpdateClause CreateUpdate(SqlMutationContext context) => _inner.CreateUpdate(context);

        public ISetClause CreateSet(SqlMutationContext context) => _inner.CreateSet(context);

        public IDeleteClause CreateDelete(SqlMutationContext context) => _inner.CreateDelete(context);

        public IMutationWhereClause CreateWhere(SqlMutationContext context) => _inner.CreateWhere(context);
    }

    private sealed class PrefixInsertClause : IInsertClause
    {
        private readonly IInsertClause _inner;

        public PrefixInsertClause(IInsertClause inner) => _inner = inner;

        public SqlTableReference Table => _inner.Table;

        public void Into(SqlTableReference table) => _inner.Into(table);

        public void AppendTo(StringBuilder builder)
        {
            builder.Append("/*custom*/ ");
            _inner.AppendTo(builder);
        }

        public void Clear() => _inner.Clear();

        public IInsertClause Clone(SqlMutationContext context) => new PrefixInsertClause(_inner.Clone(context));

        public void Validate(SqlValidationContext context) => _inner.Validate(context);
    }
}
