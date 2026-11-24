using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string ComputedReturningConsumer = """
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;

        public sealed class ComputedReturningRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
        public sealed class ComputedReturningDto
        {
            public int Total { get; set; }
            public string Display { get; set; }
        }
        public static class ComputedReturningConsumer
        {
            public static ISqlInsertBuilder ApplyInsert(ISqlInsertBuilder source) =>
                source.Returning<ComputedReturningRow>(r => new { Total = (r.Id + r.Id) * (r.Id - r.Id), r.Name });
            public static ISqlUpdateBuilder ApplyUpdate(ISqlUpdateBuilder source) =>
                source.Returning<ComputedReturningRow>(r => new { Total = (r.Id + r.Id) * (r.Id - r.Id), r.Name });
            public static ISqlDeleteBuilder ApplyDelete(ISqlDeleteBuilder source) =>
                source.Returning<ComputedReturningRow>(r => new { Total = (r.Id + r.Id) * (r.Id - r.Id), r.Name });
            public static ISqlUpdateBuilder ApplyDto(ISqlUpdateBuilder source) =>
                source.Returning<ComputedReturningRow>(r => new ComputedReturningDto { Total = r.Id / r.Id, Display = r.Name });
        }
        """;

    /// <summary>计算型投影必须生成完整 Provider SQL，而非仅标记运行时回退。</summary>
    [Fact]
    public void Generate_WhenReturningUsesColumnArithmetic_ShouldEmitCompleteSql()
    {
        var result = RunGenerator(ComputedReturningConsumer);

        AssertNoCompilationErrors(result);
        Assert.Contains("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(" Returning ((`Id` + `Id`) * (`Id` - `Id`)) As `Total`, `Name` As `Name`",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
    }

    /// <summary>真实执行生成代码，分别断言三种操作的完整 SQL 及 DTO 投影。</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    public void GeneratedReturning_WhenArithmeticIsExecuted_ShouldRenderCompleteMutationSql(string providerKey)
    {
        var result = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(result);
        var assembly = LoadGeneratedAssembly(result);
        var provider = GetReturningProvider(providerKey);
        var resolver = new MatchingReturningMappingResolver();
        var insert = CreateInsertReturningBuilder(provider, resolver);
        var update = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);
        var delete = CreateDeleteReturningBuilder(provider, resolver);
        var dto = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);

        ApplyGeneratedReturning(assembly, "ApplyInsert", insert, "ComputedReturningConsumer");
        ApplyGeneratedReturning(assembly, "ApplyUpdate", update, "ComputedReturningConsumer");
        ApplyGeneratedReturning(assembly, "ApplyDelete", delete, "ComputedReturningConsumer");
        ApplyGeneratedReturning(assembly, "ApplyDto", dto, "ComputedReturningConsumer");

        var expected = providerKey switch
        {
            "sqlserver" => new[]
            {
                "Insert Into [samples] ([Name]) Output (([Inserted].[Id] + [Inserted].[Id]) * ([Inserted].[Id] - [Inserted].[Id])) As [Total], [Inserted].[Name] As [Name] Values (@_p_0)",
                "Update [samples] Set [Name] = @_p_0 Output (([Inserted].[Id] + [Inserted].[Id]) * ([Inserted].[Id] - [Inserted].[Id])) As [Total], [Inserted].[Name] As [Name] Where [Id]=@_p_1",
                "Delete From [samples] Output (([Deleted].[Id] + [Deleted].[Id]) * ([Deleted].[Id] - [Deleted].[Id])) As [Total], [Deleted].[Name] As [Name] Where [Id]=@_p_0",
                "Update [samples] Set [Name] = @_p_0 Output ([Inserted].[Id] / [Inserted].[Id]) As [Total], [Inserted].[Name] As [Display] Where [Id]=@_p_1"
            },
            "postgresql" => new[]
            {
                "Insert Into \"samples\" (\"Name\") Values (@_p_0) Returning ((\"Id\" + \"Id\") * (\"Id\" - \"Id\")) As \"Total\", \"Name\" As \"Name\"",
                "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning ((\"Id\" + \"Id\") * (\"Id\" - \"Id\")) As \"Total\", \"Name\" As \"Name\"",
                "Delete From \"samples\" Where \"Id\"=@_p_0 Returning ((\"Id\" + \"Id\") * (\"Id\" - \"Id\")) As \"Total\", \"Name\" As \"Name\"",
                "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning (\"Id\" / \"Id\") As \"Total\", \"Name\" As \"Display\""
            },
            _ => new[]
            {
                "Insert Into `samples` (`Name`) Values (@_p_0) Returning ((`Id` + `Id`) * (`Id` - `Id`)) As `Total`, `Name` As `Name`",
                "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning ((`Id` + `Id`) * (`Id` - `Id`)) As `Total`, `Name` As `Name`",
                "Delete From `samples` Where `Id`=@_p_0 Returning ((`Id` + `Id`) * (`Id` - `Id`)) As `Total`, `Name` As `Name`",
                "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning (`Id` / `Id`) As `Total`, `Name` As `Display`"
            }
        };
        Assert.Equal(expected[0], insert.ToSql());
        Assert.Equal(expected[1], update.ToSql());
        Assert.Equal(expected[2], delete.ToSql());
        Assert.Equal(expected[3], dto.ToSql());
        Assert.Equal(expected[1], update.ToSql());
        Assert.Equal(2, update.MutationContext.ParameterManager.Count);
    }

    /// <summary>运行时映射发生变化后，计算列和普通列必须一起采用当前物理名。</summary>
    [Fact]
    public void GeneratedReturning_WhenArithmeticMappingChanges_ShouldUseCurrentColumns()
    {
        var result = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(result);
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
            new ReturningMappingResolver { Prefix = "physical_" });

        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder, "ComputedReturningConsumer");

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning ((`physical_id` + `physical_id`) * (`physical_id` - `physical_id`)) As `Total`, " +
            "`physical_name` As `Name`", builder.ToSql());
    }
}
