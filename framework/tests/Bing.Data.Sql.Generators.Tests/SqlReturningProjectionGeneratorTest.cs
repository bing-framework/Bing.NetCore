using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Mutations;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    [Fact]
    public void Generate_WhenSingleReturningPropertyIsUsed_ShouldEmitItsClrNameAndAlias()
    {
        var result = RunGenerator(Source("""
            public static ISqlUpdateBuilder Update(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>().Returning<Row>(item => item.Id);
            """));

        AssertNoCompilationErrors(result);
        Assert.Contains("new (string, string)[]{(\"Id\",\"Id\")}", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("ApplyReturning<global::Bing.Data.Sql.Builders.Mutations.ISqlUpdateBuilder, global::Row>",
            result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WhenAnonymousReturningProjectionIsUsed_ShouldKeepAliasesOrderAndRepeatedColumns()
    {
        var result = RunGenerator(Source("""
            public static ISqlUpdateBuilder Update(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>().Returning<Row>(item => new
                {
                    Display = item.Name,
                    item.Id,
                    Again = item.Name
                });
            """));

        AssertNoCompilationErrors(result);
        Assert.Contains("new (string, string)[]{(\"Name\",\"Display\"),(\"Id\",\"Id\"),(\"Name\",\"Again\")}",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Matches("ReturningLayout_\\d+", result.GeneratedSource);
    }

    [Fact]
    public void Generate_WhenDtoReturningProjectionIsUsed_ShouldUseTargetMemberAliases()
    {
        var result = RunGenerator(Source("""
            public static ISqlUpdateBuilder Update(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>().Returning<Row>(item => new Summary
                {
                    Caption = item.Name,
                    Key = item.Id
                });
            """));

        AssertNoCompilationErrors(result);
        Assert.Contains("new (string, string)[]{(\"Name\",\"Caption\"),(\"Id\",\"Key\")}",
            result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WhenEachMutationBuilderUsesReturning_ShouldEmitMatchingBuilderTypes()
    {
        var result = RunGenerator(Source("""
            public static ISqlInsertBuilder Insert(ISqlExecutor executor) =>
                executor.CreateInsertBuilder().InsertInto<Row>().Returning<Row>(item => item.Id);

            public static ISqlUpdateBuilder Update(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>().Returning<Row>(item => item.Id);

            public static ISqlDeleteBuilder Delete(ISqlExecutor executor) =>
                executor.CreateDeleteBuilder().DeleteFrom<Row>().Returning<Row>(item => item.Id);
            """));

        AssertNoCompilationErrors(result);
        Assert.Contains("ApplyReturning<global::Bing.Data.Sql.Builders.Mutations.ISqlInsertBuilder, global::Row>",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("ApplyReturning<global::Bing.Data.Sql.Builders.Mutations.ISqlUpdateBuilder, global::Row>",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("ApplyReturning<global::Bing.Data.Sql.Builders.Mutations.ISqlDeleteBuilder, global::Row>",
            result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WhenStaticAndNamedReturningArgumentsAreUsed_ShouldInterceptBothCalls()
    {
        var result = RunGenerator(Source("""
            public static ISqlUpdateBuilder ExtensionCall(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>()
                    .Returning<Row>(columns: item => new { Display = item.Name });

            public static ISqlUpdateBuilder StaticCall(ISqlExecutor executor) =>
                MutationClauseExtensions.Returning<ISqlUpdateBuilder, Row>(
                    executor.CreateUpdateBuilder().Update<Row>(),
                    columns: item => new { Display = item.Name });
            """));

        AssertNoCompilationErrors(result);
        Assert.Equal(2, Regex.Matches(result.GeneratedSource, "ApplyReturning<global::Bing\\.Data\\.Sql\\.Builders\\.Mutations\\.ISqlUpdateBuilder, global::Row>").Count);
        Assert.Equal(2, Regex.Matches(result.GeneratedSource, "new \\(string, string\\)\\[\\]\\{\\(\\\"Name\\\",\\\"Display\\\"\\)\\}").Count);
    }

    [Fact]
    public void Generate_WhenReturningProjectionIsDynamic_ShouldKeepInformationalFallbackInStrictMode()
    {
        var result = RunGenerator(Source("""
            public static ISqlUpdateBuilder Update(ISqlExecutor executor,
                Expression<Func<Row, object>> projection) =>
                executor.CreateUpdateBuilder().Update<Row>().Returning<Row>(projection);
            """), analyzerConfigOptionsProvider: StrictOptions());

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002" &&
            diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("ApplyReturning", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("item => new { Display = item.Name.Trim() }")]
    [InlineData("item => new Summary { Caption = SideEffect.Next(item.Name) }")]
    [InlineData("item => new { Value = (object)(long)item.Id }")]
    [InlineData("item => (object)(long?)(long)item.Id")]
    [InlineData("item => item[0]")]
    public void Generate_WhenReturningProjectionNeedsRuntimeEvaluation_ShouldKeepFallback(string projection)
    {
        var result = RunGenerator(Source($$"""
            public static ISqlUpdateBuilder Update(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>().Returning<Row>({{projection}});
            """), analyzerConfigOptionsProvider: StrictOptions());

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002" &&
            diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG001");
        Assert.DoesNotContain("ApplyReturning", result.GeneratedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_WhenReturningHasOneSupportedNumericConversion_ShouldEmitLayouts()
    {
        var result = RunGenerator(Source("""
            public static ISqlUpdateBuilder UpdateDto(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>()
                    .Returning<Row>(item => new NumericSummary { Key = item.Id });

            public static ISqlUpdateBuilder UpdateSingle(ISqlExecutor executor) =>
                executor.CreateUpdateBuilder().Update<Row>().Returning<Row>(item => (long)item.Id);
            """));

        AssertNoCompilationErrors(result);
        Assert.Contains("new (string, string)[]{(\"Id\",\"Key\")}", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("new (string, string)[]{(\"Id\",\"Id\")}", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
    }

    [Fact]
    public void Generate_WhenSameNamedMethodComesFromAnotherType_ShouldNotTreatItAsSqlReturning()
    {
        var source = Source("""
            public static ISqlUpdateBuilder Update(ISqlExecutor executor) =>
                OtherMutationExtensions.Returning<Row>(executor.CreateUpdateBuilder().Update<Row>(),
                    item => new { Display = item.Name });
            """) + """

            public static class OtherMutationExtensions
            {
                public static ISqlUpdateBuilder Returning<TEntity>(this ISqlUpdateBuilder source,
                    Expression<Func<TEntity, object>> columns) where TEntity : class => source;
            }
            """;
        var result = RunGenerator(source);

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("ReturningLayout_", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BINGSQLG001", result.GeneratedSource, StringComparison.Ordinal);
    }

    private static string Source(string methods) => """
        using System;
        using System.Linq.Expressions;
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;

        public sealed class Row
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int this[int index] => Id;
        }

        public sealed class Summary
        {
            public int Key { get; set; }
            public string Caption { get; set; } = string.Empty;
        }

        public sealed class NumericSummary
        {
            public long Key { get; set; }
        }

        public static class SideEffect
        {
            public static string Next(string value) => value;
        }

        public static class Consumer
        {
        """ + methods + """
        }
        """;

    private static Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider StrictOptions() =>
        new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.BingSqlAotStrict"] = "true"
        });
}
