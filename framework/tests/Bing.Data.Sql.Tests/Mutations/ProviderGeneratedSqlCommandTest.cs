using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Xunit;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>内置 Provider 必须在主链复用完整生成 SQL，保持参数与映射回退一致。</summary>
public sealed class ProviderGeneratedSqlCommandTest
{
    public static IEnumerable<object[]> Providers()
    {
        yield return new object[] { SqliteSqlProvider.Instance, '`', '`' };
        yield return new object[] { MySqlSqlProvider.Instance, '`', '`' };
        yield return new object[] { PostgreSqlSqlProvider.Instance, '"', '"' };
        yield return new object[] { SqlServerSqlProvider.Instance, '[', ']' };
    }

    /// <summary>三个默认命令命中完整常量并沿用静态 Binder；并发原始值按既有顺序绑定。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_ShouldReuseProviderConstantsAndBinder(ISqlProvider provider, char open, char close)
    {
        var fixture = Create(provider, open, close);
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
        var entity = new Row { Id = 9, Name = "value", Version = 7 };
        var insert = builder.Insert(entity);
        var update = builder.Update(entity, new SqlUpdateOptions<Row>().Original(row => row.Version, 6));
        var purge = builder.Purge(entity);

        Assert.Same(fixture.Insert, insert.Sql);
        Assert.Same(fixture.Update, update.Sql);
        Assert.Same(fixture.Delete, purge.Sql);
        Assert.Equal(new object[] { "value", 7 }, insert.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { "value", 7, 9, 6 }, update.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 9, 7 }, purge.Parameters.Select(parameter => parameter.Value));
        Assert.True(insert.IsGeneratedExecution && update.IsGeneratedExecution && purge.IsGeneratedExecution);
        Assert.Equal(provider.Key, insert.ProviderKey);
        Assert.True(update.ValidateAffectedRows);
    }

    /// <summary>替换内置 Provider 的方言后，Insert、Update 和 Delete 必须按目标方言完整渲染。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_WhenProviderDialectIsCustomized_ShouldRenderCompleteSqlWithCustomDialect(
        ISqlProvider innerProvider, char open, char close)
    {
        var provider = new CustomDialectProvider(innerProvider);
        var fixture = Create(provider, open, close);
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
        var entity = new Row { Id = 9, Name = "value", Version = 7 };

        var insert = builder.Insert(entity);
        var update = builder.Update(entity, new SqlUpdateOptions<Row>().Original(row => row.Version, 6));
        var delete = builder.Purge(entity);

        Assert.Equal("Insert Into <rows> (<Name>, <Version>) Values (@_p_0, @_p_1)", insert.Sql);
        Assert.Equal("Update <rows> Set <Name> = @_p_0, <Version> = @_p_1 " +
                     "Where <Id> = @_p_2 And <Version> = @_p_3", update.Sql);
        Assert.Equal("Delete From <rows> Where <Id> = @_p_0 And <Version> = @_p_1", delete.Sql);
        Assert.NotSame(fixture.Insert, insert.Sql);
        Assert.NotSame(fixture.Update, update.Sql);
        Assert.NotSame(fixture.Delete, delete.Sql);
    }

    /// <summary>真实运行时表/列覆盖必须生成新 SQL，不能套用默认常量。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_WhenMappingChanges_ShouldRenderActualShape(ISqlProvider provider, char open, char close)
    {
        var fixture = Create(provider, open, close, mapped: true);
        var command = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services)
            .Insert(new Row { Name = "mapped", Version = 1 });
        Assert.Equal($"Insert Into {open}tenant_rows{close} ({open}title{close}, {open}Version{close}) Values (@_p_0, @_p_1)", command.Sql);
        Assert.NotSame(fixture.Insert, command.Sql);
    }

    /// <summary>列筛选按请求改变形状，仍使用同一生成 Getter 和 Binder，避免生成组合缓存。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_WhenColumnsAreFiltered_ShouldRenderSelectedColumns(ISqlProvider provider, char open, char close)
    {
        var fixture = Create(provider, open, close);
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
        var command = builder.Insert(new Row { Name = "only-name", Version = 1 }, new SqlInsertOptions
        {
            IncludeProperties = new[] { nameof(Row.Name) }
        });
        Assert.Equal($"Insert Into {open}rows{close} ({open}Name{close}) Values (@_p_0)", command.Sql);
        Assert.NotSame(fixture.Insert, command.Sql);
        Assert.Equal("only-name", Assert.Single(command.Parameters).Value);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>有列/默认值身份键均复用完整 SQL；MySQL 不添加 Returning，仍由既有执行器读取身份值。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void KeyInsert_ShouldReuseProviderConstants(ISqlProvider provider, char open, char close)
    {
        foreach (var keyOnly in new[] { false, true })
        {
            var fixture = Create(provider, open, close, keyOnly: keyOnly);
            var builder = new SqlInsertBuilder(provider, fixture.Services);
            var command = new SqlKeyInsertCommandBuilder(builder,
                EntityKeyPolicy.CreateGenerated<Row, int>(nameof(Row.Id), EntityKeyGenerationKind.DatabaseIdentity,
                    static row => row.Id, static (row, key) => row.Id = key)).Build(new Row { Name = null, Version = 8 });
            Assert.Same(fixture.KeyInsert, command.Sql);
            Assert.Equal(provider != MySqlSqlProvider.Instance, command.HasReturning);
            Assert.True(command.IsGeneratedExecution);
            Assert.Equal(keyOnly ? Array.Empty<object>() : new object[] { null, 8 },
                command.Parameters.Select(parameter => parameter.Value));
        }
    }

    /// <summary>同一可复用 Builder 的常量与计划共享，但请求参数不能串值。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_WhenConcurrent_ShouldIsolateRequestValues(ISqlProvider provider, char open, char close)
    {
        var fixture = Create(provider, open, close);
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
        var commands = new SqlWriteCommand[32];
        System.Threading.Tasks.Parallel.For(0, commands.Length, index =>
            commands[index] = builder.Insert(new Row { Name = $"request-{index}", Version = index }));
        for (var index = 0; index < commands.Length; index++)
        {
            Assert.Same(fixture.Insert, commands[index].Sql);
            Assert.Equal(new object[] { $"request-{index}", index },
                commands[index].Parameters.Select(parameter => parameter.Value));
        }
    }

    /// <summary>自定义 Returning 方言继续自己的渲染流程，完整生成 SQL 不得覆盖扩展输出。</summary>
    [Fact]
    public void KeyInsert_WhenReturningDialectIsCustomized_ShouldRenderCustomClause()
    {
        var provider = new CustomReturningProvider();
        var fixture = Create(provider, '[', ']');
        var command = new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(provider, fixture.Services),
            EntityKeyPolicy.CreateGenerated<Row, int>(nameof(Row.Id), EntityKeyGenerationKind.DatabaseIdentity,
                static row => row.Id, static (row, key) => row.Id = key)).Build(new Row { Name = "custom", Version = 1 });
        Assert.Equal("Insert Into [rows] ([Name], [Version]) Values (@_p_0, @_p_1) Returning [Id]", command.Sql);
        Assert.NotSame(fixture.KeyInsert, command.Sql);
        Assert.True(command.HasReturning);
    }

    private static Fixture Create(ISqlProvider provider, char open, char close, bool mapped = false, bool keyOnly = false)
    {
        string Q(string value) => $"{open}{value}{close}";
        var table = Q("rows");
        var insert = $"Insert Into {table} ({Q("Name")}, {Q("Version")}) Values (@_p_0, @_p_1)";
        var update = $"Update {table} Set {Q("Name")} = @_p_0, {Q("Version")} = @_p_1 Where {Q("Id")} = @_p_2 And {Q("Version")} = @_p_3";
        var delete = $"Delete From {table} Where {Q("Id")} = @_p_0 And {Q("Version")} = @_p_1";
        var keySql = keyOnly ? provider == MySqlSqlProvider.Instance ? $"Insert Into {table} () VALUES ()" :
            provider == SqlServerSqlProvider.Instance ? $"Insert Into {table} OUTPUT INSERTED.{Q("Id")} DEFAULT VALUES" :
            $"Insert Into {table} DEFAULT VALUES RETURNING {Q("Id")}" :
            provider == MySqlSqlProvider.Instance ? insert : provider == SqlServerSqlProvider.Instance ?
            $"Insert Into {table} ({Q("Name")}, {Q("Version")}) Output [Inserted].[Id] Values (@_p_0, @_p_1)" :
            insert + $" Returning {Q("Id")}";
        var properties = new List<EntityPropertyMetadata>
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true, isKey: true,
                databaseGeneratedOption: DatabaseGeneratedOption.Identity, generatedGetter: static row => ((Row)row).Id)
        };
        if (!keyOnly)
        {
            properties.Add(EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                generatedGetter: static row => ((Row)row).Name));
            properties.Add(EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: static row => ((Row)row).Version));
        }
        var model = new EntityModelMetadata(typeof(Row), "rows", null, properties, static () => new Row())
        {
            GeneratedMutationSql = (key, operation) => key == provider.Key ? operation switch
            {
                SqlOperationKind.InsertValues => insert,
                SqlOperationKind.Update => update,
                SqlOperationKind.Delete => delete,
                _ => null
            } : null,
            GeneratedKeyInsertSql = key => key == provider.Key ? keySql : null
        };
        var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        if (mapped)
        {
            var mapping = new EntityMappingOptions { EntityType = typeof(Row), TableName = "tenant_rows" };
            mapping.Columns[nameof(Row.Name)] = new ColumnMappingOptions { PropertyName = nameof(Row.Name), ColumnName = "title" };
            options.EntityMappings.Add(mapping);
        }
        var services = new SqlBuilderServices(metadataOptions: options,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model));
        return new Fixture(services, insert, update, delete, keySql);
    }

    private sealed record Fixture(SqlBuilderServices Services, string Insert, string Update, string Delete, string KeyInsert);

    private sealed class CustomReturningProvider : ISqlProvider, ISqlProviderProfileProvider, ISqlReturningDialect
    {
        private static readonly SqlServerSqlProvider Inner = SqlServerSqlProvider.Instance;
        public string Key => Inner.Key;
        public Bing.Data.Enums.DatabaseType DatabaseType => Inner.DatabaseType;
        public IDialect Dialect => Inner.Dialect;
        public ISqlClauseFactory ClauseFactory => Inner.ClauseFactory;
        public ISqlTableReferenceParser TableReferenceParser => Inner.TableReferenceParser;
        public ISqlPaginationRenderer PaginationRenderer => Inner.PaginationRenderer;
        public IParameterManagerFactory ParameterManagerFactory => Inner.ParameterManagerFactory;
        public IParamLiteralsResolver ParamLiteralsResolver => Inner.ParamLiteralsResolver;
        public SqlProviderProfile Profile => Inner.Profile;
        public SqlReturningClausePosition Position => SqlReturningClausePosition.End;
        public string GetKeyword(SqlExecutionKind kind) => "Returning";
        public string GetQualifier(SqlExecutionKind kind, string qualifier) => null;
    }

    private sealed class CustomDialectProvider(ISqlProvider inner) : ISqlProvider, ISqlProviderProfileProvider
    {
        public string Key => inner.Key;
        public Bing.Data.Enums.DatabaseType DatabaseType => inner.DatabaseType;
        public IDialect Dialect { get; } = AngleDialect.Instance;
        public ISqlClauseFactory ClauseFactory => inner.ClauseFactory;
        public ISqlTableReferenceParser TableReferenceParser => inner.TableReferenceParser;
        public ISqlPaginationRenderer PaginationRenderer => inner.PaginationRenderer;
        public IParameterManagerFactory ParameterManagerFactory => inner.ParameterManagerFactory;
        public IParamLiteralsResolver ParamLiteralsResolver => inner.ParamLiteralsResolver;
        public SqlProviderProfile Profile => ((ISqlProviderProfileProvider)inner).Profile;
    }

    private sealed class AngleDialect : DialectBase
    {
        public static AngleDialect Instance { get; } = new();
        public override char OpeningIdentifier => '<';
        public override char ClosingIdentifier => '>';
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }
}
