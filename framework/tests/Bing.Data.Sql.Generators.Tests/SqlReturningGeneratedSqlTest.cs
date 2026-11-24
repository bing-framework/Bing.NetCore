using System.Reflection;
using System.Collections.Generic;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    private const string GeneratedSqlReturningConsumer = """
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;

        public sealed class GeneratedReturningRow
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public static class GeneratedSqlReturningConsumer
        {
            public static ISqlInsertBuilder ApplyInsert(ISqlInsertBuilder source) =>
                source.Returning<GeneratedReturningRow>(row => new
                {
                    Display = row.Name,
                    row.Id,
                    Repeated = row.Name
                });

            public static ISqlUpdateBuilder ApplyUpdate(ISqlUpdateBuilder source) =>
                source.Returning<GeneratedReturningRow>(row => new
                {
                    Display = row.Name,
                    row.Id,
                    Repeated = row.Name
                });

            public static ISqlDeleteBuilder ApplyDelete(ISqlDeleteBuilder source) =>
                source.Returning<GeneratedReturningRow>(row => new
                {
                    Display = row.Name,
                    row.Id,
                    Repeated = row.Name
                });

            public static ISqlUpdateBuilder ApplyTwice(ISqlUpdateBuilder source) =>
                source.Returning<GeneratedReturningRow>(row => new
                {
                    Display = row.Name,
                    row.Id
                }).Returning<GeneratedReturningRow>(row => new
                {
                    Repeated = row.Name
                });
        }
        """;

    private const string GeneratedReturningVariantsConsumer = """
        using System.ComponentModel.DataAnnotations.Schema;
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;

        public sealed class AttributedReturningRow
        {
            [Column("physical_id")] public int Id { get; set; }
            [Column("physical_name")] public string Name { get; set; }
        }

        public sealed class AttributedReturningDto
        {
            public string Display { get; set; }
            public int Key { get; set; }
        }

        public static class GeneratedReturningVariantsConsumer
        {
            public static ISqlUpdateBuilder ApplyDto(ISqlUpdateBuilder source) =>
                source.Returning<AttributedReturningRow>(row => new AttributedReturningDto
                {
                    Display = row.Name,
                    Key = row.Id
                });

            public static ISqlUpdateBuilder ApplySingle(ISqlUpdateBuilder source) =>
                source.Returning<AttributedReturningRow>(row => row.Id);
        }
        """;

    /// <summary>生成器应输出静态列布局、按 Provider 和操作选择的完整子句及快速路径调用。</summary>
    [Fact]
    public void Generate_WhenReturningLayoutIsStaticallyKnown_ShouldEmitSqlFactories()
    {
        var result = RunGenerator(GeneratedSqlReturningConsumer);

        AssertNoCompilationErrors(result);
        Assert.Matches("ReturningColumns_\\d+\\s*=", result.GeneratedSource);
        Assert.Matches("private static string ReturningSql_\\d+\\(string provider, global::Bing.Data.Sql.SqlExecutionKind kind\\)",
            result.GeneratedSource);
        Assert.Contains("returning.TryAddGenerated(generatedColumns, sqlFactory)", result.GeneratedSource,
            StringComparison.Ordinal);
        Assert.Matches("ApplyReturning<[^>]+>\\(source, ReturningLayout_\\d+, ReturningColumns_\\d+, ReturningSql_\\d+\\)",
            result.GeneratedSource);
        Assert.Contains(" Returning `Name` As `Display`, `Id` As `Id`, `Name` As `Repeated`",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(" Returning \\\"Name\\\" As \\\"Display\\\", \\\"Id\\\" As \\\"Id\\\", \\\"Name\\\" As \\\"Repeated\\\"",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(" Output [Inserted].[Name] As [Display], [Inserted].[Id] As [Id], [Inserted].[Name] As [Repeated]",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains(" Output [Deleted].[Name] As [Display], [Deleted].[Id] As [Id], [Deleted].[Name] As [Repeated]",
            result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>静态 Returning 片段应覆盖三个 Provider 和 Insert、Update、Delete 的完整 SQL。</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    public void GeneratedReturning_WhenExecuted_ShouldRenderCompleteMutationSql(string providerKey)
    {
        var result = RunGenerator(GeneratedSqlReturningConsumer);
        AssertNoCompilationErrors(result);
        Assert.Contains("returning.TryAddGenerated(generatedColumns, sqlFactory)", result.GeneratedSource,
            StringComparison.Ordinal);

        var provider = GetReturningProvider(providerKey);
        var resolver = new MatchingReturningMappingResolver();
        var assembly = LoadGeneratedAssembly(result);
        var insert = CreateInsertReturningBuilder(provider, resolver);
        var update = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);
        var delete = CreateDeleteReturningBuilder(provider, resolver);

        ApplyGeneratedReturning(assembly, "ApplyInsert", insert);
        ApplyGeneratedReturning(assembly, "ApplyUpdate", update);
        ApplyGeneratedReturning(assembly, "ApplyDelete", delete);

        var expected = providerKey switch
        {
            "sqlserver" => (
                "Insert Into [samples] ([Name]) Output [Inserted].[Name] As [Display], [Inserted].[Id] As [Id], [Inserted].[Name] As [Repeated] Values (@_p_0)",
                "Update [samples] Set [Name] = @_p_0 Output [Inserted].[Name] As [Display], [Inserted].[Id] As [Id], [Inserted].[Name] As [Repeated] Where [Id]=@_p_1",
                "Delete From [samples] Output [Deleted].[Name] As [Display], [Deleted].[Id] As [Id], [Deleted].[Name] As [Repeated] Where [Id]=@_p_0"),
            "postgresql" => (
                "Insert Into \"samples\" (\"Name\") Values (@_p_0) Returning \"Name\" As \"Display\", \"Id\" As \"Id\", \"Name\" As \"Repeated\"",
                "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning \"Name\" As \"Display\", \"Id\" As \"Id\", \"Name\" As \"Repeated\"",
                "Delete From \"samples\" Where \"Id\"=@_p_0 Returning \"Name\" As \"Display\", \"Id\" As \"Id\", \"Name\" As \"Repeated\""),
            _ => (
                "Insert Into `samples` (`Name`) Values (@_p_0) Returning `Name` As `Display`, `Id` As `Id`, `Name` As `Repeated`",
                "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning `Name` As `Display`, `Id` As `Id`, `Name` As `Repeated`",
                "Delete From `samples` Where `Id`=@_p_0 Returning `Name` As `Display`, `Id` As `Id`, `Name` As `Repeated`")
        };

        Assert.Equal(expected.Item1, insert.ToSql());
        Assert.Equal(expected.Item2, update.ToSql());
        Assert.Equal(expected.Item3, delete.ToSql());
    }

    /// <summary>映射物理列与生成列名不符时，应回退到当前上下文并保留完整 SQL。</summary>
    [Fact]
    public void GeneratedReturning_WhenCurrentMappingDiffers_ShouldUseRuntimeColumns()
    {
        var result = RunGenerator(GeneratedSqlReturningConsumer);
        AssertNoCompilationErrors(result);
        Assert.Contains("returning.TryAddGenerated(generatedColumns, sqlFactory)", result.GeneratedSource,
            StringComparison.Ordinal);

        var resolver = new ReturningMappingResolver { Prefix = "physical_" };
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder);

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
                     "Returning `physical_name` As `Display`, `physical_id` As `Id`, " +
                     "`physical_name` As `Repeated`", builder.ToSql());
    }

    /// <summary>首次使用静态子句后再次追加投影，应只输出一个关键字并保留两组列。</summary>
    [Fact]
    public void GeneratedReturning_WhenProjectionIsAppended_ShouldKeepOneClauseAndBothLayouts()
    {
        var result = RunGenerator(GeneratedSqlReturningConsumer);
        AssertNoCompilationErrors(result);
        Assert.Contains("returning.TryAddGenerated(generatedColumns, sqlFactory)", result.GeneratedSource,
            StringComparison.Ordinal);

        var resolver = new MatchingReturningMappingResolver();
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyTwice", builder);

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
                     "Returning `Name` As `Display`, `Id` As `Id`, `Name` As `Repeated`", builder.ToSql());
    }

    /// <summary>列属性物理名、DTO 成员初始化和单属性投影应走静态 SQL 路径。</summary>
    [Fact]
    public void GeneratedReturning_WhenColumnAttributesAndDtoOrSinglePropertyAreUsed_ShouldRenderCompleteSql()
    {
        var result = RunGenerator(GeneratedReturningVariantsConsumer);
        AssertNoCompilationErrors(result);
        Assert.Contains("Returning `physical_name` As `Display`, `physical_id` As `Key`",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("Returning `physical_id` As `Id`", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("returning.TryAddGenerated(generatedColumns, sqlFactory)", result.GeneratedSource,
            StringComparison.Ordinal);

        var assembly = LoadGeneratedAssembly(result);
        var resolver = new ReturningMappingResolver();
        var dtoBuilder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        var singleBuilder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        ApplyGeneratedReturning(assembly, "ApplyDto", dtoBuilder, "GeneratedReturningVariantsConsumer");
        ApplyGeneratedReturning(assembly, "ApplySingle", singleBuilder, "GeneratedReturningVariantsConsumer");

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
                     "Returning `physical_name` As `Display`, `physical_id` As `Key`", dtoBuilder.ToSql());
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
                     "Returning `physical_id` As `Id`", singleBuilder.ToSql());
    }

    /// <summary>缺失或签名不匹配的运行库合同应继续生成可编译的动态兼容路径。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("public bool TryAddGenerated(object columns, object factory) => false;")]
    public void GeneratedReturning_WhenGeneratedContractIsMissingOrIncompatible_ShouldUseFallback(string member)
    {
        var legacyClause = "namespace Bing.Data.Sql.Builders.Mutations.Clauses { " +
            "public sealed class ReturningClause { " + member + " } }";
        var result = RunGenerator(GeneratedSqlReturningConsumer + "\n" + legacyClause);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("ReturningSql_", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddGenerated", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("source.ReturningClause.AddRange(columns)", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>有表别名时应整组回退并保留限定符及列顺序。</summary>
    [Fact]
    public void GeneratedReturning_WhenTableQualifierIsPresent_ShouldUseQualifiedRuntimeColumns()
    {
        var result = RunGenerator(GeneratedSqlReturningConsumer);
        AssertNoCompilationErrors(result);
        var resolver = new MatchingReturningMappingResolver();
        var builder = new SqlUpdateBuilder(PostgreSqlSqlProvider.Instance,
                new SqlBuilderServices(entityMappingResolver: resolver))
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .Set("Name", "updated")
            .Where("Id", 1);

        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder);

        const string expectedSql = "Update \"samples\" As \"t\" Set \"Name\" = @_p_0 " +
            "Where \"Id\"=@_p_1 Returning \"t\".\"Name\" As \"Display\", \"t\".\"Id\" As \"Id\", " +
            "\"t\".\"Name\" As \"Repeated\"";
        Assert.Equal(expectedSql, builder.ToSql());
    }

    /// <summary>生成路径中途遇到未映射列时不得先写入静态投影列。</summary>
    [Fact]
    public void GeneratedReturning_WhenGeneratedMappingIsIncomplete_ShouldNotAppendPartialColumns()
    {
        var result = RunGenerator(GeneratedSqlReturningConsumer);
        AssertNoCompilationErrors(result);
        var resolver = new MatchingReturningMappingResolver { OmitId = true };
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        var apply = LoadGeneratedAssembly(result).GetType("GeneratedSqlReturningConsumer")!
            .GetMethod("ApplyUpdate")!;

        var error = Assert.Throws<TargetInvocationException>(() => apply.Invoke(null, new object[] { builder }));

        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1", builder.ToSql());
    }

    private static ISqlProvider GetReturningProvider(string providerKey) => providerKey switch
    {
        "sqlserver" => SqlServerSqlProvider.Instance,
        "postgresql" => PostgreSqlSqlProvider.Instance,
        _ => SqliteSqlProvider.Instance
    };

    private static ISqlInsertBuilder CreateInsertReturningBuilder(ISqlProvider provider,
        IEntityMappingResolver resolver) =>
        new SqlInsertBuilder(provider, new SqlBuilderServices(entityMappingResolver: resolver))
            .InsertInto("samples")
            .Columns("Name")
            .Values("new");

    private static ISqlDeleteBuilder CreateDeleteReturningBuilder(ISqlProvider provider,
        IEntityMappingResolver resolver) =>
        new SqlDeleteBuilder(provider, new SqlBuilderServices(entityMappingResolver: resolver))
            .DeleteFrom(new SqlTableReference { TableName = "samples" })
            .Where("Id", 7);

    private static ISqlUpdateBuilder CreateGeneratedSqlReturningExecutionBuilder(ISqlProvider provider,
        IEntityMappingResolver resolver) =>
        new SqlUpdateBuilder(provider, new SqlBuilderServices(entityMappingResolver: resolver))
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "updated")
            .Where("Id", 1);

    private static void ApplyGeneratedReturning(Assembly assembly, string methodName, object builder,
        string consumerType = "GeneratedSqlReturningConsumer")
    {
        var consumer = assembly.GetType(consumerType) ??
            throw new InvalidOperationException("生成的 Returning consumer 类型不存在。");
        var apply = consumer.GetMethod(methodName) ??
            throw new InvalidOperationException($"生成的 Returning 方法 {methodName} 不存在。");
        Assert.Same(builder, apply.Invoke(null, new[] { builder }));
    }

    private sealed class MatchingReturningMappingResolver : IEntityMappingResolver
    {
        internal bool OmitId { get; set; }

        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
        {
            var columns = new Dictionary<string, ColumnMappingMetadata>
            {
                ["name"] = new() { PropertyName = "Name", ColumnName = "Name" }
            };
            if (!OmitId) columns["id"] = new() { PropertyName = "Id", ColumnName = "Id" };
            return new EntityMappingMetadata { EntityType = entityType, Columns = columns };
        }
    }
}
