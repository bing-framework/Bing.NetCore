using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string IdentifierReturningConsumer = """
        using System.ComponentModel.DataAnnotations.Schema;
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;
        public sealed class IdentifierReturningRow
        {
            [Column(COLUMN)] public int Id { get; set; }
            public string Name { get; set; }
        }
        public static class IdentifierReturningConsumer
        {
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) =>
                source.Returning<IdentifierReturningRow>(row => new { Total = -(row.Id + row.Id), row.Name });
        }
        """;

    /// <summary>无效名称仍保留原路径，不能把禁止字符嵌入静态 SQL。</summary>
    [Theory]
    [InlineData("id;select 1")]
    [InlineData("id\nname")]
    [InlineData("id\rname")]
    public void Generate_WhenReturningIdentifierHasForbiddenCharacters_ShouldKeepRuntimeValidation(string column)
    {
        var result = RunGenerator(IdentifierReturningConsumer.Replace("COLUMN", SymbolDisplay.FormatLiteral(column, true)));

        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.DoesNotContain("ReturningSql_", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>标识符中的引号和注释文本都只是列名；生成 SQL 必须能真实同步、异步执行。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedReturning_WhenIdentifierNeedsQuoting_ShouldExecuteSqlite(bool asynchronous)
    {
        const string column = "row`id--value";
        var result = RunGenerator(IdentifierReturningConsumer.Replace("COLUMN", SymbolDisplay.FormatLiteral(column, true)));
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id is "BINGSQLG001" or "BINGSQLG002");
        var builder = new SqlUpdateBuilder(SqliteSqlProvider.Instance,
                new SqlBuilderServices(entityMappingResolver: new IdentifierReturningResolver(column)))
            .Update(new SqlTableReference { TableName = "samples" }).Set("Name", "updated").Where(column, 3);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "Apply", builder, "IdentifierReturningConsumer");
        var command = builder.BuildCommand();
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `row``id--value`=@_p_1 " +
            "Returning (-(`row``id--value` + `row``id--value`)) As `Total`, `Name` As `Name`", command.Sql);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "CREATE TABLE samples (`row``id--value` INTEGER PRIMARY KEY, Name TEXT); " +
                "INSERT INTO samples VALUES (3, 'original');";
            await setup.ExecuteNonQueryAsync();
        }
        await using var update = connection.CreateCommand();
        update.CommandText = command.Sql;
        foreach (var parameter in command.Parameters) update.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await using var reader = asynchronous ? await update.ExecuteReaderAsync() : update.ExecuteReader();
        Assert.True(asynchronous ? await reader.ReadAsync() : reader.Read());
        Assert.Equal(-6, reader.GetInt32(0));
        Assert.Equal("updated", reader.GetString(1));
        Assert.False(asynchronous ? await reader.ReadAsync() : reader.Read());
    }

    private sealed class IdentifierReturningResolver(string column) : IEntityMappingResolver
    {
        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) => new()
        {
            EntityType = entityType,
            Columns = new Dictionary<string, ColumnMappingMetadata>
            {
                ["Id"] = new() { PropertyName = "Id", ColumnName = column },
                ["Name"] = new() { PropertyName = "Name", ColumnName = "Name" }
            }
        };
    }
}
