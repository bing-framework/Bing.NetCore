using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Bing.Data.KeyGeneration;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证 Oracle 自增插入的 SQL 和输出绑定。</summary>
public sealed class SqlKeyInsertCommandBuilderTest
{
    /// <summary>Oracle 在普通列后绑定输出参数，只有自增列时使用 DEFAULT。</summary>
    [Theory]
    [InlineData(false, "Insert Into \"identity_rows\" (\"Name\") Values (:p_0) Returning \"row_id\" Into :bing_identity_key")]
    [InlineData(true, "Insert Into \"identity_defaults\" (\"row_id\") Values (DEFAULT) Returning \"row_id\" Into :bing_identity_key")]
    public void Build_OracleIdentity_ShouldBindOutputAfterInputs(bool keyOnly, string expected)
    {
        var command = keyOnly ? Build(new IdentityOnlyRow()) : Build(new IdentityRow { Name = "parent" });
        var parameters = command.Parameters.ToArray();
        Assert.Equal(expected, command.Sql);
        Assert.False(command.HasReturning);
        Assert.Equal(keyOnly ? 1 : 2, parameters.Length);
        if (!keyOnly)
            Assert.Equal("parent", parameters[0].Value);
        Assert.Equal("bing_identity_key", parameters[^1].Name);
        Assert.Equal(DbType.Decimal, parameters[^1].DbType);
        Assert.Equal(ParameterDirection.Output, parameters[^1].Direction);
    }

    /// <summary>只有参数序列处于默认起点才复用生成 SQL。</summary>
    [Theory]
    [InlineData(false, "Insert Into \"identity_rows\" (\"Name\") Values (:p_0) Returning \"row_id\" Into :bing_identity_key")]
    [InlineData(true, "Insert Into \"identity_rows\" (\"Name\") Values (:p_1) Returning \"row_id\" Into :bing_identity_key")]
    public void Build_OracleGeneratedIdentity_ShouldRespectParameterIndex(bool advanceIndex, string expected)
    {
        var generatedSql = new string(("Insert Into \"identity_rows\" (\"Name\") Values (:p_0) Returning \"row_id\" Into :bing_identity_key").ToCharArray());
        var model = new EntityModelMetadata(typeof(IdentityRow), "identity_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(IdentityRow.Id), typeof(long), true, true,
                columnName: "row_id", isKey: true, databaseGeneratedOption: DatabaseGeneratedOption.Identity,
                generatedGetter: static source => ((IdentityRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(IdentityRow.Name), typeof(string), true, true,
                generatedGetter: static source => ((IdentityRow)source).Name)
        }, static () => new IdentityRow()) { GeneratedKeyInsertSql = _ => generatedSql };
        var services = new SqlBuilderServices(metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model));
        var provider = OracleSqlProvider.Instance;
        var parameters = new ParameterManager(provider.Dialect);
        if (advanceIndex) parameters.GenerateName();
        var builder = new SqlInsertBuilder(provider, services, parameters);
        var policy = EntityKeyPolicy.CreateGenerated<IdentityRow, long>(nameof(IdentityRow.Id),
            EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, id) => row.Id = id);

        var command = new SqlKeyInsertCommandBuilder(builder, policy).Build(new IdentityRow { Name = "now" });

        Assert.Equal(expected, command.Sql);
        Assert.Equal(!advanceIndex, ReferenceEquals(generatedSql, command.Sql));
        Assert.True(command.IsGeneratedExecution);
    }

    private static SqlWriteCommand Build<TEntity>(TEntity entity) where TEntity : IdentityKey
    {
        var policy = EntityKeyPolicy.CreateGenerated<TEntity, long>(nameof(IdentityKey.Id),
            EntityKeyGenerationKind.DatabaseIdentity, static row => row.Id, static (row, id) => row.Id = id);
        return new SqlKeyInsertCommandBuilder(new SqlInsertBuilder(OracleSqlProvider.Instance, new SqlBuilderServices()), policy)
            .Build(entity);
    }

    private abstract class IdentityKey
    {
        [Key, Column("row_id"), DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
    }

    [Table("identity_defaults")]
    private sealed class IdentityOnlyRow : IdentityKey { }

    [Table("identity_rows")]
    private sealed class IdentityRow : IdentityKey
    {
        public string Name { get; set; }
    }
}
