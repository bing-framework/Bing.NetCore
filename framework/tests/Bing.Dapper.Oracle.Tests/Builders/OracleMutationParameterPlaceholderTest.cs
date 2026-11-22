using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Dapper.Tests.Builders;

/// <summary>验证 Oracle Mutation SQL 使用含冒号的执行参数占位符。</summary>
public sealed class OracleMutationParameterPlaceholderTest
{
    [Fact]
    public void InsertBuilder_WithObjectValues_ShouldKeepOracleParameterPrefix()
    {
        var command = new SqlInsertBuilder(OracleSqlProvider.Instance, new SqlBuilderServices())
            .InsertInto("samples")
            .Columns("Name")
            .Values("Bing")
            .BuildCommand();

        Assert.Equal("Insert Into \"samples\" (\"Name\") Values (:p_0)", command.Sql);
        Assert.Equal(":p_0", Assert.Single(command.Parameters).Name);
    }

    [Fact]
    public void InsertBuilder_WithSqlParamValues_ShouldKeepSuppliedPlaceholder()
    {
        var builder = new SqlInsertBuilder(OracleSqlProvider.Instance, new SqlBuilderServices());
        builder.InsertClause.Into(new SqlTableReference { TableName = "samples" });
        builder.InsertColumnsClause.AddRange(new[] { "Name" });
        builder.ValuesClause.AddRow(new[] { new SqlParam(":name_value", "Bing") });
        var command = builder.BuildCommand();

        Assert.Equal("Insert Into \"samples\" (\"Name\") Values (:name_value)", command.Sql);
        Assert.Equal(":name_value", Assert.Single(command.Parameters).Name);
    }

    [Fact]
    public void UpdateBuilder_WithSetAndWhere_ShouldKeepOracleParameterPrefixes()
    {
        var command = new SqlUpdateBuilder(OracleSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .Where("Id", 7)
            .BuildCommand();

        Assert.Equal("Update \"samples\" Set \"Name\" = :p_0 Where \"Id\"=:p_1", command.Sql);
        Assert.Equal(new[] { ":p_0", ":p_1" }, command.Parameters.Select(parameter => parameter.Name));
    }

    [Fact]
    public void UpdateBuilder_WithExpressionWhere_ShouldKeepOracleParameterPrefix()
    {
        var builder = new SqlUpdateBuilder(OracleSqlProvider.Instance, new SqlBuilderServices());
        builder.Update<MutationRow>()
            .Set<MutationRow, string>(row => row.Name, "Bing")
            .Where<MutationRow, int>(row => row.Id, 7);

        var command = builder.BuildCommand();

        Assert.Equal("Update \"mutation_rows\" Set \"Name\" = :p_0 Where \"Id\"=:p_1", command.Sql);
        Assert.Equal(new[] { ":p_0", ":p_1" }, command.Parameters.Select(parameter => parameter.Name));
    }

    [Fact]
    public void EntityInsertUpdateAndPurge_ShouldKeepOracleParameterPrefixes()
    {
        var builder = CreateBuilder();
        var entity = new MutationRow { Id = 7, Name = "Bing", Region = "west" };

        var insert = builder.Insert(entity);
        var update = builder.Update(entity);
        var purge = builder.Purge(entity);

        Assert.Equal("Insert Into \"mutation_rows\" (\"Id\", \"Name\", \"Region\") Values (:p_0, :p_1, :p_2)",
            insert.Sql);
        Assert.Equal("Update \"mutation_rows\" Set \"Name\" = :p_0, \"Region\" = :p_1 Where \"Id\" = :p_2",
            update.Sql);
        Assert.Equal("Delete From \"mutation_rows\" Where \"Id\" = :p_0", purge.Sql);
    }

    [Fact]
    public void EntityInsert_WithIncludedColumns_ShouldKeepOracleParameterPrefix()
    {
        var command = CreateBuilder().Insert(new MutationRow { Id = 7, Name = "Bing", Region = "west" },
            new SqlInsertOptions { IncludeProperties = new[] { nameof(MutationRow.Name) } });

        Assert.Equal("Insert Into \"mutation_rows\" (\"Name\") Values (:p_0)", command.Sql);
        Assert.Equal(":p_0", Assert.Single(command.Parameters).Name);
    }

    [Fact]
    public void DeleteCombined_WithSingleKeyInPredicate_ShouldKeepOracleParameterPrefixes()
    {
        var command = CreateBuilder().DeleteCombined(new[]
        {
            new MutationRow { Id = 7 },
            new MutationRow { Id = 8 }
        }, strategy: SqlBatchDeleteStrategy.InPredicate);

        Assert.Equal("Delete From \"mutation_rows\" Where \"Id\" In (:p_0,:p_1)", command.Sql);
        Assert.Equal(new[] { ":p_0", ":p_1" }, command.Parameters.Select(parameter => parameter.Name));
    }

    [Fact]
    public void Purge_WithDataBoundary_ShouldKeepOracleParameterPrefixes()
    {
        var builder = CreateBuilder(new RegionBoundaryFilter());
        var command = builder.Purge(new MutationRow { Id = 7 });

        Assert.Equal("Delete From \"mutation_rows\" Where \"Id\" = :p_0 And \"Region\"=:p_1", command.Sql);
        Assert.Equal(new[] { ":p_0", ":p_1" }, command.Parameters.Select(parameter => parameter.Name));
    }

    [Fact]
    public void EntityMutation_WithGeneratedSql_ShouldReuseCompleteOracleStatements()
    {
        var model = new EntityModelMetadata(typeof(GeneratedMutationRow), "generated_mutation_rows", null,
            new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(GeneratedMutationRow.Id), typeof(int), true, true,
                    isKey: true, databaseGeneratedOption: DatabaseGeneratedOption.Identity,
                    generatedGetter: static source => ((GeneratedMutationRow)source).Id),
                EntityPropertyMetadata.CreateGenerated(nameof(GeneratedMutationRow.Name), typeof(string), true, true,
                    generatedGetter: static source => ((GeneratedMutationRow)source).Name)
            }, static () => new GeneratedMutationRow())
        {
            GeneratedMutationSql = static (provider, operation) => provider == "bing.oracle"
                ? operation switch
                {
                    SqlOperationKind.InsertValues => GeneratedInsertSql,
                    SqlOperationKind.Update => GeneratedUpdateSql,
                    SqlOperationKind.Delete => GeneratedDeleteSql,
                    _ => null
                }
                : null
        };
        var options = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        var provider = new RegisteredEntityModelMetadataProvider(model);
        var builder = new DefaultSqlEntityMutationCommandBuilder(OracleSqlProvider.Instance,
            new SqlBuilderServices(metadataOptions: options, entityModelMetadataProvider: provider));
        var entity = new GeneratedMutationRow { Id = 5, Name = "Bing" };

        var insert = builder.Insert(entity);
        var update = builder.Update(entity);
        var purge = builder.Purge(entity);

        Assert.Equal(GeneratedInsertSql, insert.Sql);
        Assert.Same(GeneratedInsertSql, insert.Sql);
        Assert.Equal(GeneratedUpdateSql, update.Sql);
        Assert.Same(GeneratedUpdateSql, update.Sql);
        Assert.Equal(GeneratedDeleteSql, purge.Sql);
        Assert.Same(GeneratedDeleteSql, purge.Sql);
    }

    private const string GeneratedInsertSql =
        "Insert Into \"generated_mutation_rows\" (\"Name\") Values (:p_0)";
    private const string GeneratedUpdateSql =
        "Update \"generated_mutation_rows\" Set \"Name\" = :p_0 Where \"Id\" = :p_1";
    private const string GeneratedDeleteSql =
        "Delete From \"generated_mutation_rows\" Where \"Id\" = :p_0";

    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder(params ISqlFilter[] filters) =>
        new(OracleSqlProvider.Instance, new SqlBuilderServices(filters: filters));

    [Table("mutation_rows")]
    private sealed class MutationRow
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        public string Region { get; set; }
    }

    private sealed class GeneratedMutationRow
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    private sealed class RegionBoundaryFilter : ISqlFilter, ISqlDataBoundaryContributor
    {
        public void Filter(SqlFilterContext context) { }

        public bool ShouldApply(SqlDataBoundaryContext context) =>
            context.EntityType == typeof(MutationRow) && context.Operation == SqlDataBoundaryOperation.Purge;

        public void Apply(SqlDataBoundaryContext context) => context.AddEquals(nameof(MutationRow.Region), "west");
    }
}
