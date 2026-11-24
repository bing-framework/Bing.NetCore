using System.Linq.Expressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string NegatedReturningConsumer = """
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;
        public sealed class ComputedBoundaryRow
        {
            public int Id { get; set; }
            public decimal DecimalValue { get; set; }
            public int? OptionalCount { get; set; }
            public string Name { get; set; }
        }
        public static class NegatedReturningConsumer
        {
            public static ISqlInsertBuilder Insert(ISqlInsertBuilder source) =>
                source.Returning<ComputedBoundaryRow>(row => new
                { Negative = -row.Id, Decimal = -row.DecimalValue, Optional = -row.OptionalCount,
                  Mixed = -(row.Id + -row.Id), row.Name });
            public static ISqlUpdateBuilder Update(ISqlUpdateBuilder source) =>
                source.Returning<ComputedBoundaryRow>(row => new
                { Negative = -row.Id, Decimal = -row.DecimalValue, Optional = -row.OptionalCount,
                  Mixed = -(row.Id + -row.Id), row.Name });
            public static ISqlDeleteBuilder Delete(ISqlDeleteBuilder source) =>
                source.Returning<ComputedBoundaryRow>(row => new
                { Negative = -row.Id, Decimal = -row.DecimalValue, Optional = -row.OptionalCount,
                  Mixed = -(row.Id + -row.Id), row.Name });
        }
        """;

    /// <summary>直接负号与嵌套负号均生成静态 SQL，并保留三种写入操作的限定符。</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    public void GeneratedReturning_WhenNumericNegationIsUsed_ShouldRenderAllMutationSql(string providerKey)
    {
        var generated = RunGenerator(NegatedReturningConsumer);
        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, item => item.Id == "BINGSQLG002");
        Assert.Contains(".CreateArithmetic(", generated.GeneratedSource, StringComparison.Ordinal);
        var provider = GetReturningProvider(providerKey);
        var resolver = new ComputedBoundaryMappingResolver();
        var insert = CreateInsertReturningBuilder(provider, resolver);
        var update = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);
        var delete = CreateDeleteReturningBuilder(provider, resolver);
        var assembly = LoadGeneratedAssembly(generated);
        ApplyGeneratedReturning(assembly, "Insert", insert, "NegatedReturningConsumer");
        ApplyGeneratedReturning(assembly, "Update", update, "NegatedReturningConsumer");
        ApplyGeneratedReturning(assembly, "Delete", delete, "NegatedReturningConsumer");
        var expected = NegatedReturningSql(providerKey);

        Assert.Equal(expected[0], insert.ToSql());
        Assert.Equal(expected[1], update.ToSql());
        Assert.Equal(expected[2], delete.ToSql());
        Assert.Equal(expected[1], update.ToSql());
        Assert.Equal(2, update.MutationContext.ParameterManager.Count);
    }

    /// <summary>非内联表达式走运行时布局时应与生成路径使用相同语义。</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    public void RuntimeReturning_WhenNumericNegationIsUsed_ShouldMatchGeneratedSql(string providerKey)
    {
        var builder = CreateGeneratedSqlReturningExecutionBuilder(GetReturningProvider(providerKey),
            new ComputedBoundaryMappingResolver());
        Expression<Func<RuntimeNegatedRow, object>> projection = row => new
        {
            Negative = -row.Id, Decimal = -row.DecimalValue, Optional = -row.OptionalCount,
            Mixed = -(row.Id + -row.Id), row.Name
        };

        builder.Returning(projection);

        Assert.Equal(NegatedReturningSql(providerKey)[1], builder.ToSql());
    }

    /// <summary>映射和表别名变化后仍绑定实际列，不使用静态物理名。</summary>
    [Fact]
    public void GeneratedReturning_WhenNegatedMappingAndQualifierChange_ShouldBindCurrentColumns()
    {
        var generated = RunGenerator(NegatedReturningConsumer);
        AssertNoCompilationErrors(generated);
        var builder = new SqlUpdateBuilder(PostgreSqlSqlProvider.Instance,
                new SqlBuilderServices(entityMappingResolver: new NegatedMappingResolver()))
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .Set("Name", "updated").Where("Id", 1);

        ApplyGeneratedReturning(LoadGeneratedAssembly(generated), "Update", builder, "NegatedReturningConsumer");

        Assert.Equal("Update \"samples\" As \"t\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning " +
            "(-\"t\".\"physical_Id\") As \"Negative\", (-\"t\".\"physical_DecimalValue\") As \"Decimal\", " +
            "(-\"t\".\"physical_OptionalCount\") As \"Optional\", " +
            "(-(\"t\".\"physical_Id\" + (-\"t\".\"physical_Id\"))) As \"Mixed\", " +
            "\"t\".\"physical_Name\" As \"Name\"", builder.ToSql());
    }

    /// <summary>checked 运算保持回退诊断，运行时失败不得追加部分投影。</summary>
    [Fact]
    public void Returning_WhenNegationIsChecked_ShouldRejectWithoutChangingExistingColumns()
    {
        var generated = RunGenerator(CreateUnsupportedComputedConsumer("checked(-row.Id)"));
        AssertNoCompilationErrors(generated);
        Assert.Contains(generated.Diagnostics, item => item.Id == "BINGSQLG002");
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
            new ComputedBoundaryMappingResolver());
        builder.Returning<RuntimeNegatedRow>(row => new { row.Name });

        Assert.Throws<ArgumentException>(() => builder.Returning<RuntimeNegatedRow>(row =>
            new { row.Name, Value = checked(-row.Id) }));

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning `Name` As `Name`", builder.ToSql());
    }

    /// <summary>新生成器遇到缺少一元布局合同的旧运行库时保留原始调用。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("instance")]
    public void GeneratedReturning_WhenUnaryContractIsMissing_ShouldUseCompatibleFallback(string variant)
    {
        var generated = RunGenerator(NegatedReturningConsumer,
            metadataReferences: ComputedReturningRuntimeReferences(variant, "CreateArithmetic"));

        AssertNoCompilationErrors(generated);
        Assert.Contains(generated.Diagnostics, item => item.Id == "BINGSQLG002");
        Assert.DoesNotContain(".CreateArithmetic(", generated.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>仅缺少一元布局合同时，既有二元算术仍使用旧生成合同。</summary>
    [Fact]
    public void GeneratedReturning_WhenOnlyUnaryContractIsMissing_ShouldKeepBinaryGeneration()
    {
        var generated = RunGenerator(ComputedBoundaryConsumer,
            metadataReferences: ComputedReturningRuntimeReferences("missing", "CreateArithmetic"));

        AssertNoCompilationErrors(generated);
        Assert.DoesNotContain(generated.Diagnostics, item => item.Id == "BINGSQLG002");
        Assert.Contains(".CreateComputed(", generated.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>真实 SQLite 读取验证负数与 NULL，不仅验证生成文本。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedReturning_WhenNegatedColumnsAreExecuted_ShouldPreserveValuesAndNull(bool asynchronous)
    {
        var generated = RunGenerator(NegatedReturningConsumer);
        AssertNoCompilationErrors(generated);
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
            new ComputedBoundaryMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(generated), "Update", builder, "NegatedReturningConsumer");
        var command = builder.BuildCommand();
        Assert.Equal(NegatedReturningSql("sqlite")[1], command.Sql);
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "CREATE TABLE samples (Id INTEGER PRIMARY KEY, DecimalValue NUMERIC, OptionalCount INTEGER, Name TEXT); " +
                "INSERT INTO samples VALUES (1, 12.5, NULL, 'original');";
            await setup.ExecuteNonQueryAsync();
        }
        using var update = connection.CreateCommand();
        update.CommandText = command.Sql;
        foreach (var parameter in command.Parameters) update.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await using var reader = asynchronous ? await update.ExecuteReaderAsync() : update.ExecuteReader();

        Assert.True(asynchronous ? await reader.ReadAsync() : reader.Read());
        Assert.Equal(-1, reader.GetInt32(0));
        Assert.Equal(-12.5m, reader.GetDecimal(1));
        Assert.True(reader.IsDBNull(2));
        Assert.Equal(0, reader.GetInt32(3));
        Assert.Equal("updated", reader.GetString(4));
        Assert.False(asynchronous ? await reader.ReadAsync() : reader.Read());
    }

    private static string[] NegatedReturningSql(string providerKey) => providerKey switch
    {
        "sqlserver" => new[]
        {
            "Insert Into [samples] ([Name]) Output (-[Inserted].[Id]) As [Negative], (-[Inserted].[DecimalValue]) As [Decimal], (-[Inserted].[OptionalCount]) As [Optional], (-([Inserted].[Id] + (-[Inserted].[Id]))) As [Mixed], [Inserted].[Name] As [Name] Values (@_p_0)",
            "Update [samples] Set [Name] = @_p_0 Output (-[Inserted].[Id]) As [Negative], (-[Inserted].[DecimalValue]) As [Decimal], (-[Inserted].[OptionalCount]) As [Optional], (-([Inserted].[Id] + (-[Inserted].[Id]))) As [Mixed], [Inserted].[Name] As [Name] Where [Id]=@_p_1",
            "Delete From [samples] Output (-[Deleted].[Id]) As [Negative], (-[Deleted].[DecimalValue]) As [Decimal], (-[Deleted].[OptionalCount]) As [Optional], (-([Deleted].[Id] + (-[Deleted].[Id]))) As [Mixed], [Deleted].[Name] As [Name] Where [Id]=@_p_0"
        },
        "postgresql" => new[]
        {
            "Insert Into \"samples\" (\"Name\") Values (@_p_0) Returning (-\"Id\") As \"Negative\", (-\"DecimalValue\") As \"Decimal\", (-\"OptionalCount\") As \"Optional\", (-(\"Id\" + (-\"Id\"))) As \"Mixed\", \"Name\" As \"Name\"",
            "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning (-\"Id\") As \"Negative\", (-\"DecimalValue\") As \"Decimal\", (-\"OptionalCount\") As \"Optional\", (-(\"Id\" + (-\"Id\"))) As \"Mixed\", \"Name\" As \"Name\"",
            "Delete From \"samples\" Where \"Id\"=@_p_0 Returning (-\"Id\") As \"Negative\", (-\"DecimalValue\") As \"Decimal\", (-\"OptionalCount\") As \"Optional\", (-(\"Id\" + (-\"Id\"))) As \"Mixed\", \"Name\" As \"Name\""
        },
        _ => new[]
        {
            "Insert Into `samples` (`Name`) Values (@_p_0) Returning (-`Id`) As `Negative`, (-`DecimalValue`) As `Decimal`, (-`OptionalCount`) As `Optional`, (-(`Id` + (-`Id`))) As `Mixed`, `Name` As `Name`",
            "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning (-`Id`) As `Negative`, (-`DecimalValue`) As `Decimal`, (-`OptionalCount`) As `Optional`, (-(`Id` + (-`Id`))) As `Mixed`, `Name` As `Name`",
            "Delete From `samples` Where `Id`=@_p_0 Returning (-`Id`) As `Negative`, (-`DecimalValue`) As `Decimal`, (-`OptionalCount`) As `Optional`, (-(`Id` + (-`Id`))) As `Mixed`, `Name` As `Name`"
        }
    };

    private sealed class RuntimeNegatedRow
    {
        public int Id { get; set; }
        public decimal DecimalValue { get; set; }
        public int? OptionalCount { get; set; }
        public string Name { get; set; }
    }

    private sealed class NegatedMappingResolver : IEntityMappingResolver
    {
        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();
        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) => new()
        {
            EntityType = entityType,
            Columns = new[] { "Id", "DecimalValue", "OptionalCount", "Name" }.ToDictionary(name => name,
                name => new ColumnMappingMetadata { PropertyName = name, ColumnName = "physical_" + name })
        };
    }
}
