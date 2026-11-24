using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Xunit;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证 Oracle 静态 Schema 生成 SQL 的运行时复用与映射回退。</summary>
public sealed class OracleSchemaGeneratedSqlCommandTest
{
    private const string Schema = "sales_2026";

    /// <summary>Insert、Update 和 Delete 使用完整的 Oracle Schema SQL 常量。</summary>
    [Fact]
    public void Mutation_WithStaticSchema_ShouldReuseConstants()
    {
        Assert.Same(OracleSqlProvider.Instance.Dialect, OracleSqlProvider.Instance.Profile.Mutation.GeneratedSqlDialect);
        var fixture = CreateFixture();
        var builder = new DefaultSqlEntityMutationCommandBuilder(OracleSqlProvider.Instance, fixture.Services);
        var row = new Row { Id = 7, Name = "first", Version = 3 };

        var insert = builder.Insert(row);
        var update = builder.Update(row, new SqlUpdateOptions<Row>().Original(item => item.Version, 2));
        var delete = builder.Purge(row);

        Assert.Equal("Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\", \"Version\") Values (:p_0, :p_1)", insert.Sql);
        Assert.Equal("Update \"sales_2026\".\"schema_rows\" Set \"display_name\" = :p_0, \"Version\" = :p_1 Where \"row_id\" = :p_2 And \"Version\" = :p_3", update.Sql);
        Assert.Equal("Delete From \"sales_2026\".\"schema_rows\" Where \"row_id\" = :p_0 And \"Version\" = :p_1", delete.Sql);
        Assert.Same(fixture.Insert, insert.Sql);
        Assert.Same(fixture.Update, update.Sql);
        Assert.Same(fixture.Delete, delete.Sql);
        // Oracle 方言沿用数值转字符串的既有参数合同，Schema 常量路径不改变转换。
        Assert.Equal(new object[] { "first", "3" }, insert.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { "first", "3", "7", "2" }, update.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { "7", "3" }, delete.Parameters.Select(parameter => parameter.Value));
        Assert.True(insert.IsGeneratedExecution && update.IsGeneratedExecution && delete.IsGeneratedExecution);
    }

    /// <summary>普通列和仅身份键插入均复用常量并附加 Oracle 输出参数。</summary>
    [Theory]
    [InlineData(false, "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\", \"Version\") Values (:p_0, :p_1) Returning \"row_id\" Into :bing_identity_key")]
    [InlineData(true, "Insert Into \"sales_2026\".\"schema_rows\" (\"row_id\") Values (DEFAULT) Returning \"row_id\" Into :bing_identity_key")]
    public void KeyInsert_WithStaticSchema_ShouldReuseConstantAndBindOutput(bool keyOnly, string expected)
    {
        var fixture = CreateFixture(keyOnly: keyOnly);
        var command = BuildKeyInsert(fixture.Services, new Row { Id = 8, Name = "key", Version = 4 });

        Assert.Equal(expected, command.Sql);
        Assert.Same(fixture.KeyInsert, command.Sql);
        Assert.False(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        AssertOutputParameter(command, keyOnly ? Array.Empty<object>() : new object[] { "key", "4" });
    }

    /// <summary>最终映射 Schema 覆盖静态 Schema 时，完整 Mutation SQL 使用运行时对象名。</summary>
    [Theory]
    [InlineData("tenant_2")]
    [InlineData("SALES_2026")]
    public void Mutation_WhenSchemaIsOverridden_ShouldRenderCurrentSchema(string schema)
    {
        var fixture = CreateFixture(mappedSchema: schema);
        var builder = new DefaultSqlEntityMutationCommandBuilder(OracleSqlProvider.Instance, fixture.Services);
        var row = new Row { Id = 7, Name = "tenant", Version = 3 };
        var table = "\"" + schema + "\".\"schema_rows\"";

        var insert = builder.Insert(row);
        var update = builder.Update(row, new SqlUpdateOptions<Row>().Original(item => item.Version, 2));
        var delete = builder.Purge(row);

        Assert.Equal("Insert Into " + table + " (\"display_name\", \"Version\") Values (:p_0, :p_1)", insert.Sql);
        Assert.Equal("Update " + table + " Set \"display_name\" = :p_0, \"Version\" = :p_1 Where \"row_id\" = :p_2 And \"Version\" = :p_3", update.Sql);
        Assert.Equal("Delete From " + table + " Where \"row_id\" = :p_0 And \"Version\" = :p_1", delete.Sql);
        Assert.NotSame(fixture.Insert, insert.Sql);
        Assert.NotSame(fixture.Update, update.Sql);
        Assert.NotSame(fixture.Delete, delete.Sql);
        Assert.True(insert.IsGeneratedExecution && update.IsGeneratedExecution && delete.IsGeneratedExecution);
    }

    /// <summary>最终映射 Schema 覆盖静态 Schema 时，identity SQL 回退到当前 Oracle 表名。</summary>
    [Theory]
    [InlineData("tenant_2", false)]
    [InlineData("tenant_2", true)]
    [InlineData("SALES_2026", false)]
    [InlineData("SALES_2026", true)]
    public void KeyInsert_WhenSchemaIsOverridden_ShouldRenderCurrentSchema(string schema, bool keyOnly)
    {
        var fixture = CreateFixture(keyOnly: keyOnly, mappedSchema: schema);
        var table = "\"" + schema + "\".\"schema_rows\"";
        var expected = keyOnly
            ? "Insert Into " + table + " (\"row_id\") Values (DEFAULT) Returning \"row_id\" Into :bing_identity_key"
            : "Insert Into " + table + " (\"display_name\", \"Version\") Values (:p_0, :p_1) Returning \"row_id\" Into :bing_identity_key";

        var command = BuildKeyInsert(fixture.Services, new Row { Id = 8, Name = "key", Version = 4 });

        Assert.Equal(expected, command.Sql);
        Assert.NotSame(fixture.KeyInsert, command.Sql);
        Assert.False(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        AssertOutputParameter(command, keyOnly ? Array.Empty<object>() : new object[] { "key", "4" });
    }

    private static void AssertOutputParameter(SqlWriteCommand command, object[] inputValues)
    {
        var parameters = command.Parameters.ToArray();
        Assert.Equal(inputValues.Length + 1, parameters.Length);
        Assert.Equal(inputValues, parameters.Take(inputValues.Length).Select(parameter => parameter.Value));
        var output = parameters[^1];
        Assert.Equal("bing_identity_key", output.Name);
        Assert.Equal(DbType.Decimal, output.DbType);
        Assert.Equal(ParameterDirection.Output, output.Direction);
        Assert.Null(output.Value);
    }

    private static SqlWriteCommand BuildKeyInsert(SqlBuilderServices services, Row row) =>
        new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(OracleSqlProvider.Instance, services),
            EntityKeyPolicy.CreateGenerated<Row, long>(nameof(Row.Id), EntityKeyGenerationKind.DatabaseIdentity,
                static entity => entity.Id, static (entity, key) => entity.Id = key)).Build(row);

    private static Fixture CreateFixture(bool keyOnly = false, string mappedSchema = null)
    {
        const string table = "schema_rows";
        const string target = "\"sales_2026\".\"schema_rows\"";
        const string insert = "Insert Into \"sales_2026\".\"schema_rows\" (\"display_name\", \"Version\") Values (:p_0, :p_1)";
        const string update = "Update \"sales_2026\".\"schema_rows\" Set \"display_name\" = :p_0, \"Version\" = :p_1 Where \"row_id\" = :p_2 And \"Version\" = :p_3";
        const string delete = "Delete From \"sales_2026\".\"schema_rows\" Where \"row_id\" = :p_0 And \"Version\" = :p_1";
        var keyInsert = keyOnly
            ? "Insert Into " + target + " (\"row_id\") Values (DEFAULT) Returning \"row_id\" Into :bing_identity_key"
            : insert + " Returning \"row_id\" Into :bing_identity_key";
        var properties = new List<EntityPropertyMetadata>
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(long), true, true,
                columnName: "row_id", isKey: true,
                databaseGeneratedOption: DatabaseGeneratedOption.Identity,
                generatedGetter: static source => ((Row)source).Id)
        };
        if (!keyOnly)
        {
            properties.Add(EntityPropertyMetadata.CreateGenerated(nameof(Row.Name), typeof(string), true, true,
                columnName: "display_name", generatedGetter: static source => ((Row)source).Name));
            properties.Add(EntityPropertyMetadata.CreateGenerated(nameof(Row.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: static source => ((Row)source).Version));
        }
        Func<string, SqlOperationKind, string> mutationSql = null;
        if (!keyOnly)
        {
            mutationSql = (provider, operation) => provider == OracleSqlProvider.Instance.Key
                ? operation switch
                {
                    SqlOperationKind.InsertValues => insert,
                    SqlOperationKind.Update => update,
                    SqlOperationKind.Delete => delete,
                    _ => null
                }
                : null;
        }
        var model = new EntityModelMetadata(typeof(Row), table, Schema, properties, static () => new Row())
        {
            GeneratedMutationSql = mutationSql,
            GeneratedKeyInsertSql = provider => provider == OracleSqlProvider.Instance.Key ? keyInsert : null
        };
        var metadataOptions = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        if (mappedSchema != null)
            metadataOptions.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(Row),
                Schema = mappedSchema
            });
        var services = new SqlBuilderServices(metadataOptions: metadataOptions,
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model));
        return new Fixture(services, insert, update, delete, keyInsert);
    }

    private sealed record Fixture(SqlBuilderServices Services, string Insert, string Update, string Delete,
        string KeyInsert);

    private sealed class Row
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }
}
