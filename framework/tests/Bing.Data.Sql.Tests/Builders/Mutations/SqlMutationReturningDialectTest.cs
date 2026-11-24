using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>静态 Returning SQL 不得绕过自定义 Update/Delete 方言。</summary>
public sealed class SqlMutationReturningDialectTest
{
    [Theory]
    [InlineData(SqlExecutionKind.Update, false)]
    [InlineData(SqlExecutionKind.Delete, false)]
    [InlineData(SqlExecutionKind.Update, true)]
    [InlineData(SqlExecutionKind.Delete, true)]
    public void EntityReturning_WhenOnlyNonInsertDialectChanges_ShouldRenderCustomDialect(
        SqlExecutionKind operation, bool changeKeyword)
    {
        var provider = new CustomProvider(operation, changeKeyword);
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, isKey: true,
                databaseGeneratedOption: DatabaseGeneratedOption.None, generatedGetter: row => ((Row)row).Id),
            EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                generatedGetter: row => ((Row)row).Name)
        }, () => new Row())
        {
            GeneratedMutationReturningSql = static (_, kind) => kind switch
            {
                SqlOperationKind.Update => "Update [rows] Set [Name] = @_p_0 Output [Inserted].[Id] As [Id], [Inserted].[Name] As [Name] Where [Id] = @_p_1",
                SqlOperationKind.Delete => "Delete From [rows] Output [Deleted].[Id] As [Id], [Deleted].[Name] As [Name] Where [Id] = @_p_0",
                _ => null
            }
        };
        var builder = new DefaultSqlEntityMutationCommandBuilder(provider, new SqlBuilderServices(
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));
        var entity = new Row { Id = 7, Name = "changed" };
        var command = operation == SqlExecutionKind.Update ? builder.UpdateReturning(entity) : builder.DeleteReturning(entity);
        var qualifier = changeKeyword ? operation == SqlExecutionKind.Update ? "Inserted" : "Deleted" : "Changed";
        var keyword = changeKeyword ? "CustomOutput" : "Output";
        var projection = $" {keyword} [{qualifier}].[Id] As [Id], [{qualifier}].[Name] As [Name]";
        var expected = operation == SqlExecutionKind.Update
            ? "Update [rows] Set [Name] = @_p_0" + projection + " Where [Id] = @_p_1"
            : "Delete From [rows]" + projection + " Where [Id] = @_p_0";

        Assert.Equal(expected, command.Sql);
        Assert.True(command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        Assert.Equal(operation == SqlExecutionKind.Update ? new object[] { "changed", 7 } : new object[] { 7 },
            command.Parameters.Select(parameter => parameter.Value));
    }

    private sealed class CustomProvider(SqlExecutionKind operation, bool changeKeyword) :
        ISqlProvider, ISqlProviderProfileProvider, ISqlReturningDialect
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
        public SqlReturningClausePosition Position => SqlReturningClausePosition.BeforeSource;
        public string GetKeyword(SqlExecutionKind kind) => kind == operation && changeKeyword ? "CustomOutput" : "Output";
        public string GetQualifier(SqlExecutionKind kind, string qualifier) => kind == operation && !changeKeyword
            ? "Changed" : kind == SqlExecutionKind.Delete ? "Deleted" : "Inserted";
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
