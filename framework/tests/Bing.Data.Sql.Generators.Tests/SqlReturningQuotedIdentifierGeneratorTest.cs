using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    public static TheoryData<string, string> ReturningQuotedIdentifierCases => new()
    {
        { "display name", "display name" },
        { "tick`name", "tick`name" },
        { "bracket]name", "bracket]name" },
        { "double\"name", "double\"name" },
        { "schema.name", "schema.name" },
        { "[wrapped name]", "wrapped name" },
        { "  padded name  ", "padded name" }
    };

    /// <summary>特殊物理列名仍生成静态 Returning SQL，并对三种方言的三类写入输出完整语句。</summary>
    [Theory]
    [MemberData(nameof(ReturningQuotedIdentifierCases))]
    public void GeneratedReturning_WhenPhysicalIdentifiersNeedDialectEscaping_ShouldRenderAllMutations(
        string sourceIdColumn, string expectedIdColumn)
    {
        var result = RunGenerator(CreateQuotedIdentifierConsumer(sourceIdColumn));

        AssertNoCompilationErrors(result);
        // 每个调用点都有 SQL 工厂，避免只由同文件中的另一种投影满足断言。
        Assert.Equal(6, result.GeneratedSource.Split("private static string ReturningSql_", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");

        var assembly = LoadGeneratedAssembly(result);
        foreach (var providerKey in new[] { "sqlite", "postgresql", "sqlserver" })
        {
            var provider = GetReturningProvider(providerKey);
            var resolver = new QuotedIdentifierReturningMappingResolver(sourceIdColumn, "[display name]");
            var insert = CreateInsertReturningBuilder(provider, resolver);
            var update = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);
            var delete = CreateDeleteReturningBuilder(provider, resolver);

            ApplyGeneratedReturning(assembly, "ApplyInsert", insert, "QuotedIdentifierReturningConsumer");
            ApplyGeneratedReturning(assembly, "ApplyUpdate", update, "QuotedIdentifierReturningConsumer");
            ApplyGeneratedReturning(assembly, "ApplyDelete", delete, "QuotedIdentifierReturningConsumer");

            Assert.Equal(ExpectedQuotedIdentifierSql(providerKey, "insert", expectedIdColumn), insert.ToSql());
            Assert.Equal(ExpectedQuotedIdentifierSql(providerKey, "update", expectedIdColumn), update.ToSql());
            Assert.Equal(ExpectedQuotedIdentifierSql(providerKey, "delete", expectedIdColumn), delete.ToSql());
        }
    }

    /// <summary>纯直接列投影也应为特殊物理列名生成静态 SQL，并输出三种方言的完整语句。</summary>
    [Theory]
    [MemberData(nameof(ReturningQuotedIdentifierCases))]
    public void GeneratedReturning_WhenDirectPhysicalIdentifiersNeedDialectEscaping_ShouldRenderAllMutations(
        string sourceIdColumn, string expectedIdColumn)
    {
        var result = RunGenerator(CreateQuotedIdentifierConsumer(sourceIdColumn));

        AssertNoCompilationErrors(result);
        // 每个调用点都有 SQL 工厂，避免只由同文件中的另一种投影满足断言。
        Assert.Equal(6, result.GeneratedSource.Split("private static string ReturningSql_", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");

        var assembly = LoadGeneratedAssembly(result);
        foreach (var providerKey in new[] { "sqlite", "postgresql", "sqlserver" })
        {
            var provider = GetReturningProvider(providerKey);
            var resolver = new QuotedIdentifierReturningMappingResolver(sourceIdColumn, "[display name]");
            var insert = CreateInsertReturningBuilder(provider, resolver);
            var update = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);
            var delete = CreateDeleteReturningBuilder(provider, resolver);

            ApplyGeneratedReturning(assembly, "ApplyDirectInsert", insert, "QuotedIdentifierReturningConsumer");
            ApplyGeneratedReturning(assembly, "ApplyDirectUpdate", update, "QuotedIdentifierReturningConsumer");
            ApplyGeneratedReturning(assembly, "ApplyDirectDelete", delete, "QuotedIdentifierReturningConsumer");

            Assert.Equal(ExpectedDirectQuotedIdentifierSql(providerKey, "insert", expectedIdColumn), insert.ToSql());
            Assert.Equal(ExpectedDirectQuotedIdentifierSql(providerKey, "update", expectedIdColumn), update.ToSql());
            Assert.Equal(ExpectedDirectQuotedIdentifierSql(providerKey, "delete", expectedIdColumn), delete.ToSql());
        }
    }

    /// <summary>运行时映射覆盖编译期物理名时，Returning 应改用当前映射列。</summary>
    [Fact]
    public void GeneratedReturning_WhenRuntimeMappingOverridesPhysicalNames_ShouldUseCurrentColumns()
    {
        var result = RunGenerator(CreateQuotedIdentifierConsumer("compiled id"));

        AssertNoCompilationErrors(result);
        // 每个调用点都有 SQL 工厂，避免只由同文件中的另一种投影满足断言。
        Assert.Equal(6, result.GeneratedSource.Split("private static string ReturningSql_", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");

        var resolver = new QuotedIdentifierReturningMappingResolver("runtime id", "[runtime name]");
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder,
            "QuotedIdentifierReturningConsumer");

        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
                     "Returning `runtime name` As `Name`, (-(`runtime id` + `runtime id`)) As `NegativeTotal`",
            builder.ToSql());
    }

    private static string CreateQuotedIdentifierConsumer(string idColumn) => $$"""
        using System.ComponentModel.DataAnnotations.Schema;
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;

        public sealed class QuotedIdentifierReturningRow
        {
            [Column({{SymbolDisplay.FormatLiteral(idColumn, true)}})]
            public int Id { get; set; }

            [Column("[display name]")]
            public string Name { get; set; }
        }

        public static class QuotedIdentifierReturningConsumer
        {
            public static ISqlInsertBuilder ApplyInsert(ISqlInsertBuilder source) =>
                source.Returning<QuotedIdentifierReturningRow>(row => new
                {
                    row.Name,
                    NegativeTotal = -(row.Id + row.Id)
                });

            public static ISqlUpdateBuilder ApplyUpdate(ISqlUpdateBuilder source) =>
                source.Returning<QuotedIdentifierReturningRow>(row => new
                {
                    row.Name,
                    NegativeTotal = -(row.Id + row.Id)
                });

            public static ISqlDeleteBuilder ApplyDelete(ISqlDeleteBuilder source) =>
                source.Returning<QuotedIdentifierReturningRow>(row => new
                {
                    row.Name,
                    NegativeTotal = -(row.Id + row.Id)
                });

            public static ISqlInsertBuilder ApplyDirectInsert(ISqlInsertBuilder source) =>
                source.Returning<QuotedIdentifierReturningRow>(row => new { row.Name, row.Id });

            public static ISqlUpdateBuilder ApplyDirectUpdate(ISqlUpdateBuilder source) =>
                source.Returning<QuotedIdentifierReturningRow>(row => new { row.Name, row.Id });

            public static ISqlDeleteBuilder ApplyDirectDelete(ISqlDeleteBuilder source) =>
                source.Returning<QuotedIdentifierReturningRow>(row => new { row.Name, row.Id });
        }
        """;

    private static string ExpectedQuotedIdentifierSql(string providerKey, string operation, string idColumn)
    {
        var quote = providerKey switch
        {
            "sqlserver" => (Open: "[", Close: "]"),
            "postgresql" => (Open: "\"", Close: "\""),
            _ => (Open: "`", Close: "`")
        };
        string Name(string value) => quote.Open + value.Replace(quote.Close,
            new string(quote.Close[0], 2), StringComparison.Ordinal) + quote.Close;

        var qualifier = providerKey == "sqlserver"
            ? operation == "delete" ? "[Deleted]." : "[Inserted]."
            : string.Empty;
        var projection = qualifier + Name("display name") + " As " + Name("Name") +
            ", (-(" + qualifier + Name(idColumn) + " + " + qualifier + Name(idColumn) + ")) As " +
            Name("NegativeTotal");

        return (providerKey, operation) switch
        {
            ("sqlserver", "insert") => "Insert Into [samples] ([Name]) Output " + projection + " Values (@_p_0)",
            ("sqlserver", "update") => "Update [samples] Set [Name] = @_p_0 Output " + projection + " Where [Id]=@_p_1",
            ("sqlserver", "delete") => "Delete From [samples] Output " + projection + " Where [Id]=@_p_0",
            ("postgresql", "insert") => "Insert Into \"samples\" (\"Name\") Values (@_p_0) Returning " + projection,
            ("postgresql", "update") => "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning " + projection,
            ("postgresql", "delete") => "Delete From \"samples\" Where \"Id\"=@_p_0 Returning " + projection,
            ("sqlite", "insert") => "Insert Into `samples` (`Name`) Values (@_p_0) Returning " + projection,
            ("sqlite", "update") => "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning " + projection,
            _ => "Delete From `samples` Where `Id`=@_p_0 Returning " + projection
        };
    }

    private static string ExpectedDirectQuotedIdentifierSql(string providerKey, string operation, string idColumn)
    {
        var quote = providerKey switch
        {
            "sqlserver" => (Open: "[", Close: "]"),
            "postgresql" => (Open: "\"", Close: "\""),
            _ => (Open: "`", Close: "`")
        };
        string Name(string value) => quote.Open + value.Replace(quote.Close,
            new string(quote.Close[0], 2), StringComparison.Ordinal) + quote.Close;

        var qualifier = providerKey == "sqlserver"
            ? operation == "delete" ? "[Deleted]." : "[Inserted]."
            : string.Empty;
        var projection = qualifier + Name("display name") + " As " + Name("Name") + ", " +
            qualifier + Name(idColumn) + " As " + Name("Id");

        return (providerKey, operation) switch
        {
            ("sqlserver", "insert") => "Insert Into [samples] ([Name]) Output " + projection + " Values (@_p_0)",
            ("sqlserver", "update") => "Update [samples] Set [Name] = @_p_0 Output " + projection + " Where [Id]=@_p_1",
            ("sqlserver", "delete") => "Delete From [samples] Output " + projection + " Where [Id]=@_p_0",
            ("postgresql", "insert") => "Insert Into \"samples\" (\"Name\") Values (@_p_0) Returning " + projection,
            ("postgresql", "update") => "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning " + projection,
            ("postgresql", "delete") => "Delete From \"samples\" Where \"Id\"=@_p_0 Returning " + projection,
            ("sqlite", "insert") => "Insert Into `samples` (`Name`) Values (@_p_0) Returning " + projection,
            ("sqlite", "update") => "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning " + projection,
            _ => "Delete From `samples` Where `Id`=@_p_0 Returning " + projection
        };
    }

    private sealed class QuotedIdentifierReturningMappingResolver : IEntityMappingResolver
    {
        private readonly string _idColumn;
        private readonly string _nameColumn;

        internal QuotedIdentifierReturningMappingResolver(string idColumn, string nameColumn)
        {
            _idColumn = idColumn;
            _nameColumn = nameColumn;
        }

        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) => new()
        {
            EntityType = entityType,
            Columns = new Dictionary<string, ColumnMappingMetadata>
            {
                ["id"] = new() { PropertyName = "Id", ColumnName = _idColumn },
                ["name"] = new() { PropertyName = "Name", ColumnName = _nameColumn }
            }
        };
    }
}
