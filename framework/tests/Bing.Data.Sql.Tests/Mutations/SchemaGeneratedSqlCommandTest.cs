using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Xunit;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>静态 Schema 的完整生成 SQL 进入主链，运行时映射变更仍使用最终对象名。</summary>
public sealed class SchemaGeneratedSqlCommandTest
{
    public static IEnumerable<object[]> Providers()
    {
        yield return new object[] { MySqlSqlProvider.Instance, '`', '`' };
        yield return new object[] { PostgreSqlSqlProvider.Instance, '"', '"' };
        yield return new object[] { SqlServerSqlProvider.Instance, '[', ']' };
    }

    /// <summary>Insert、Update 和 Delete 直接复用 SQL 常量；并发原始值和静态 Binder 保持有效。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_WithStaticSchema_ShouldReuseConstantsAndBinder(ISqlProvider provider, char open, char close)
    {
        var fixture = Create(provider, open, close);
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
        var row = new Row { Id = 7, Name = "first", Version = 3 };
        var insert = builder.Insert(row);
        var update = builder.Update(row, new SqlUpdateOptions<Row>().Original(item => item.Version, 2));
        var delete = builder.Purge(row);
        Assert.Same(fixture.Insert, insert.Sql);
        Assert.Same(fixture.Update, update.Sql);
        Assert.Same(fixture.Delete, delete.Sql);
        Assert.Equal(new object[] { "first", 3 }, insert.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { "first", 3, 7, 2 }, update.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 7, 3 }, delete.Parameters.Select(parameter => parameter.Value));
        Assert.True(insert.IsGeneratedExecution && update.IsGeneratedExecution && delete.IsGeneratedExecution);
        Assert.True(update.ValidateAffectedRows);
    }

    /// <summary>有写入列和仅默认值的身份键命令都保留 Provider 的完整 Returning 语法。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void KeyInsert_WithStaticSchema_ShouldReuseConstants(ISqlProvider provider, char open, char close)
    {
        foreach (var keyOnly in new[] { false, true })
        {
            var fixture = Create(provider, open, close, keyOnly: keyOnly);
            var command = BuildKeyInsert(provider, fixture.Services);
            Assert.Same(fixture.KeyInsert, command.Sql);
            Assert.Equal(provider != MySqlSqlProvider.Instance, command.HasReturning);
            Assert.True(command.IsGeneratedExecution);
            Assert.Equal(keyOnly ? Array.Empty<object>() : new object[] { "key", 1 },
                command.Parameters.Select(parameter => parameter.Value));
        }
    }

    /// <summary>有限列形状和双行 Insert 同样复用限定 Schema 的常量，不新增形状缓存。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_WithStaticSchemaAndFiniteShape_ShouldReuseConstants(ISqlProvider provider, char open, char close)
    {
        var fixture = Create(provider, open, close);
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
        var row = new Row { Id = 7, Name = "one", Version = 3 };
        var insert = builder.Insert(row, new SqlInsertOptions { IncludeProperties = new[] { nameof(Row.Name) } });
        var update = builder.Update(row, new SqlUpdateOptions { IncludeProperties = new[] { nameof(Row.Name) } });
        var combined = builder.InsertCombined(new[] { row, new Row { Id = 8, Name = "two", Version = 4 } });
        Assert.Same(fixture.SingleInsert, insert.Sql);
        Assert.Same(fixture.SingleUpdate, update.Sql);
        Assert.Same(fixture.DoubleInsert, combined.Sql);
        Assert.Equal(new object[] { "one", 3, "two", 4 }, combined.Parameters.Select(parameter => parameter.Value));
        Assert.True(insert.IsGeneratedExecution && update.IsGeneratedExecution && combined.IsGeneratedExecution);
    }

    /// <summary>配置 Schema 或大小写变化都必须回退，不能误用其他租户的编译期名称。</summary>
    [Theory, MemberData(nameof(Providers))]
    public void Mutation_WhenSchemaChanges_ShouldRenderCurrentSchema(ISqlProvider provider, char open, char close)
    {
        foreach (var schema in new[] { "tenant_2", "SALES_2026" })
        {
            var fixture = Create(provider, open, close, mappedSchema: schema);
            var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
            var row = new Row { Id = 7, Name = "tenant", Version = 3 };
            var insert = builder.Insert(row);
            var update = builder.Update(row);
            var delete = builder.Purge(row);
            var keyInsert = BuildKeyInsert(provider, fixture.Services);
            Assert.Equal(fixture.Insert.Replace("sales_2026", schema, StringComparison.Ordinal), insert.Sql);
            Assert.Equal(fixture.Update.Replace("sales_2026", schema, StringComparison.Ordinal), update.Sql);
            Assert.Equal(fixture.Delete.Replace("sales_2026", schema, StringComparison.Ordinal), delete.Sql);
            Assert.Equal(fixture.KeyInsert.Replace("sales_2026", schema, StringComparison.Ordinal), keyInsert.Sql);
            Assert.NotSame(fixture.Insert, insert.Sql);
            Assert.NotSame(fixture.Update, update.Sql);
            Assert.NotSame(fixture.Delete, delete.Sql);
            Assert.NotSame(fixture.KeyInsert, keyInsert.Sql);
        }
    }

    /// <summary>配置了 Database 的 SQL Server 三段表名不能套用两段常量。</summary>
    [Fact]
    public void Mutation_WhenDatabaseIsConfigured_ShouldRenderQualifiedDatabase()
    {
        var provider = SqlServerSqlProvider.Instance;
        var fixture = Create(provider, '[', ']', database: "archive");
        var command = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services).Insert(new Row());
        Assert.Equal("Insert Into [archive].[sales_2026].[rows] ([Name], [Version]) Values (@_p_0, @_p_1)", command.Sql);
        Assert.NotSame(fixture.Insert, command.Sql);
    }

    /// <summary>生成 SQL 不能绕过 SQLite 不支持 Schema 的既有能力校验。</summary>
    [Theory]
    [InlineData("Insert")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("KeyInsert")]
    [InlineData("KeyInsertDefault")]
    public void Mutation_WhenSqliteHasSchema_ShouldRejectBeforeReturningCommand(string operation)
    {
        var provider = SqliteSqlProvider.Instance;
        var fixture = Create(provider, '`', '`', keyOnly: operation == "KeyInsertDefault");
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, fixture.Services);
        var row = new Row { Id = 7, Version = 3 };
        var exception = Assert.Throws<NotSupportedException>(() => operation switch
        {
            "Insert" => builder.Insert(row),
            "Update" => builder.Update(row),
            "Delete" => builder.Purge(row),
            _ => BuildKeyInsert(provider, fixture.Services)
        });
        Assert.True(SqlCapabilityFailure.TryGetReason(exception, out var reason));
        Assert.Equal(SqlCapabilityFailureReason.DatabaseUnsupported, reason);
        Assert.Equal("SchemaQualifiedReference", exception.Data["Bing.Data.Sql.Capability"]);
    }

    /// <summary>普通列和 DEFAULT VALUES 的完整生成键 SQL 均不得绕过标识符验证。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyInsert_WhenSchemaIdentifierIsInvalid_ShouldReject(bool keyOnly)
    {
        var provider = PostgreSqlSqlProvider.Instance;
        var fixture = Create(provider, '"', '"', keyOnly: keyOnly, schema: "bad;schema");
        Assert.Throws<ArgumentException>(() => BuildKeyInsert(provider, fixture.Services));
    }

    /// <summary>常量、映射回退和参数名回退均只验证表引用一次，避免在热路径重复分配。</summary>
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public void KeyInsert_ShouldValidateTableOnce(bool keyOnly, bool overrideSchema, bool advanceParameterNames)
    {
        var validator = new CountingTableValidator();
        var provider = PostgreSqlSqlProvider.Instance;
        var fixture = Create(provider, '"', '"', keyOnly: keyOnly,
            mappedSchema: overrideSchema ? "tenant_2" : null, validator: validator);
        BuildKeyInsert(provider, fixture.Services, advanceParameterNames);
        Assert.Equal(1, validator.Calls);
    }

    private static SqlWriteCommand BuildKeyInsert(ISqlProvider provider, SqlBuilderServices services,
        bool advanceParameterNames = false)
    {
        var builder = new SqlInsertBuilder(provider, services);
        if (advanceParameterNames) builder.MutationContext.ParameterManager.GenerateName();
        return new SqlKeyInsertCommandBuilder(builder,
            EntityKeyPolicy.CreateGenerated<Row, int>(nameof(Row.Id), EntityKeyGenerationKind.DatabaseIdentity,
                static row => row.Id, static (row, key) => row.Id = key)).Build(new Row { Name = "key", Version = 1 });
    }

    private static Fixture Create(ISqlProvider provider, char open, char close, bool keyOnly = false,
        string mappedSchema = null, string database = null, string schema = "sales_2026",
        ISqlTableReferenceValidator validator = null)
    {
        string Q(string value) => $"{open}{value}{close}";
        var table = Q(schema) + "." + Q("rows");
        var insert = $"Insert Into {table} ({Q("Name")}, {Q("Version")}) Values (@_p_0, @_p_1)";
        var update = $"Update {table} Set {Q("Name")} = @_p_0, {Q("Version")} = @_p_1 Where {Q("Id")} = @_p_2 And {Q("Version")} = @_p_3";
        var delete = $"Delete From {table} Where {Q("Id")} = @_p_0 And {Q("Version")} = @_p_1";
        var keySql = keyOnly ? provider == MySqlSqlProvider.Instance ? $"Insert Into {table} () VALUES ()" :
            provider == SqlServerSqlProvider.Instance ? $"Insert Into {table} OUTPUT INSERTED.{Q("Id")} DEFAULT VALUES" :
            $"Insert Into {table} DEFAULT VALUES RETURNING {Q("Id")}" :
            provider == MySqlSqlProvider.Instance ? insert : provider == SqlServerSqlProvider.Instance ?
            $"Insert Into {table} ({Q("Name")}, {Q("Version")}) Output [Inserted].[Id] Values (@_p_0, @_p_1)" :
            insert + $" Returning {Q("Id")}";
        var singleInsert = $"Insert Into {table} ({Q("Name")}) Values (@_p_0)";
        var singleUpdate = $"Update {table} Set {Q("Name")} = @_p_0 Where {Q("Id")} = @_p_1 And {Q("Version")} = @_p_2";
        var doubleInsert = insert + ", (@_p_2, @_p_3)";
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
        var model = new EntityModelMetadata(typeof(Row), "rows", schema, properties, static () => new Row())
        {
            GeneratedMutationSql = (key, operation) => key == provider.Key ? operation switch
            {
                SqlOperationKind.InsertValues => insert,
                SqlOperationKind.Update => update,
                SqlOperationKind.Delete => delete,
                _ => null
            } : null,
            GeneratedKeyInsertSql = key => key == provider.Key ? keySql : null,
            GeneratedMutationShapeSql = (key, operation, property, rows) => key != provider.Key ? null : (operation, property, rows) switch
            {
                (SqlOperationKind.InsertValues, nameof(Row.Name), 1) => singleInsert,
                (SqlOperationKind.Update, nameof(Row.Name), 1) => singleUpdate,
                (SqlOperationKind.InsertValues, null, 2) => doubleInsert,
                _ => null
            }
        };
        var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        if (mappedSchema != null || database != null)
            options.EntityMappings.Add(new EntityMappingOptions { EntityType = typeof(Row), Schema = mappedSchema, Database = database });
        return new Fixture(new SqlBuilderServices(metadataOptions: options,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model),
            tableReferenceValidator: validator),
            insert, update, delete, keySql, singleInsert, singleUpdate, doubleInsert);
    }

    private sealed class CountingTableValidator : ISqlTableReferenceValidator
    {
        private readonly DefaultSqlTableReferenceValidator _inner = new();
        public int Calls { get; private set; }
        public void Validate(SqlTableReference table, Bing.Data.Enums.DatabaseType databaseType)
        {
            Calls++;
            _inner.Validate(table, databaseType);
        }
    }

    private sealed record Fixture(SqlBuilderServices Services, string Insert, string Update, string Delete,
        string KeyInsert, string SingleInsert, string SingleUpdate, string DoubleInsert);

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }
}
