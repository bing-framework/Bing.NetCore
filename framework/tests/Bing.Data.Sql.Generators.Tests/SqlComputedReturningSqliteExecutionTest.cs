using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>实际生成的计算列绑定应在物理映射变化后仍可同步或异步执行。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedComputedReturning_WhenMappingChanges_ShouldExecuteSqlite(bool asynchronous)
    {
        const string source = """
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;
            public sealed class MappedComputedRow { public int Id { get; set; } public string Name { get; set; } }
            public static class MappedComputedConsumer
            {
                public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) =>
                    source.Returning<MappedComputedRow>(row => new { Total = (row.Id + row.Id) * row.Id, row.Name });
            }
            """;
        var generated = RunGenerator(source);
        AssertNoCompilationErrors(generated);
        var resolver = new SqliteComputedMappingResolver();
        var builder = new SqlUpdateBuilder(SqliteSqlProvider.Instance,
                new SqlBuilderServices(entityMappingResolver: resolver))
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("display_name", "updated").Where("physical_id", 3);

        ApplyGeneratedReturning(LoadGeneratedAssembly(generated), "Apply", builder, "MappedComputedConsumer");
        var command = builder.BuildCommand();

        Assert.Equal("Update `samples` Set `display_name` = @_p_0 Where `physical_id`=@_p_1 Returning " +
            "((`physical_id` + `physical_id`) * `physical_id`) As `Total`, `display_name` As `Name`", command.Sql);
        Assert.Equal(1, resolver.ResolveCalls);
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "CREATE TABLE samples (physical_id INTEGER PRIMARY KEY, display_name TEXT); " +
                                "INSERT INTO samples VALUES (3, 'original');";
            await setup.ExecuteNonQueryAsync();
        }
        using var update = connection.CreateCommand();
        update.CommandText = command.Sql;
        foreach (var parameter in command.Parameters)
            update.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await using var reader = asynchronous ? await update.ExecuteReaderAsync() : update.ExecuteReader();

        Assert.True(asynchronous ? await reader.ReadAsync() : reader.Read());
        Assert.Equal(18, reader.GetInt32(0));
        Assert.Equal("updated", reader.GetString(1));
        Assert.False(asynchronous ? await reader.ReadAsync() : reader.Read());
    }

    private sealed class SqliteComputedMappingResolver : IEntityMappingResolver
    {
        internal int ResolveCalls { get; private set; }
        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
        {
            ResolveCalls++;
            return new EntityMappingMetadata
            {
                EntityType = entityType,
                Columns = new Dictionary<string, ColumnMappingMetadata>
                {
                    ["Id"] = new() { PropertyName = "Id", ColumnName = "physical_id" },
                    ["Name"] = new() { PropertyName = "Name", ColumnName = "display_name" }
                }
            };
        }
    }
}
