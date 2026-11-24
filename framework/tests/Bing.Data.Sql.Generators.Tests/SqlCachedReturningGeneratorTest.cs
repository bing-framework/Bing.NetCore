using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Mutations;
using Microsoft.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>缓存投影经生成后，同步和异步执行应返回相同的计算结果与 DTO 别名。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedReturning_WhenExecutedAgainstSqlite_ShouldPreserveProjection(bool asynchronous)
    {
        var result = RunGenerator(Source("""
            private static readonly Expression<Func<Row, object>> Projection = row =>
                new Summary { Key = -(row.Id + row.Id), Caption = row.Name };
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) => source.Returning<Row>(Projection);
            """));
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, new MatchingReturningMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "Apply", builder, "Consumer");
        var command = builder.BuildCommand();
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning (-(`Id` + `Id`)) As `Key`, `Name` As `Caption`", command.Sql);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "CREATE TABLE samples (Id INTEGER PRIMARY KEY, Name TEXT); INSERT INTO samples VALUES (1, 'before');";
            await setup.ExecuteNonQueryAsync();
        }
        await using var update = connection.CreateCommand();
        update.CommandText = command.Sql;
        foreach (var parameter in command.Parameters) update.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await using var reader = asynchronous ? await update.ExecuteReaderAsync() : update.ExecuteReader();
        Assert.True(asynchronous ? await reader.ReadAsync() : reader.Read());
        Assert.Equal("Key", reader.GetName(0));
        Assert.Equal("Caption", reader.GetName(1));
        Assert.Equal(-2, reader.GetInt32(0));
        Assert.Equal(command.Parameters.First().Value, reader.GetString(1));
        Assert.False(asynchronous ? await reader.ReadAsync() : reader.Read());
    }

    /// <summary>并发复用同一静态投影时，各 Builder 的当前映射保持独立。</summary>
    [Fact]
    public async Task CachedReturning_WhenReusedConcurrently_ShouldKeepEachMappingIsolated()
    {
        var result = RunGenerator(Source("""
            private static readonly Expression<Func<Row, object>> Projection = row =>
                new Summary { Key = -(row.Id + row.Id), Caption = row.Name };
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) => source.Returning<Row>(Projection);
            """));
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        var assembly = LoadGeneratedAssembly(result);
        await Task.WhenAll(Enumerable.Range(0, 32).Select(index => Task.Run(() =>
        {
            var prefix = "tenant_" + index + "_";
            var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance,
                new ReturningMappingResolver { Prefix = prefix });
            ApplyGeneratedReturning(assembly, "Apply", builder, "Consumer");
            var sql = "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning (-(`" + prefix +
                "id` + `" + prefix + "id`)) As `Key`, `" + prefix + "name` As `Caption`";
            Assert.Equal(sql, builder.ToSql());
            Assert.Equal(sql, builder.ToSql());
            Assert.Equal(2, builder.MutationContext.ParameterManager.Count);
        })));
    }

    /// <summary>投影与调用点分属不同文件时，应使用声明处的语义模型。</summary>
    [Theory]
    [InlineData("row => new { Display = row.Name }", "Returning `Name` As `Display`")]
    [InlineData("row => new { Total = -(row.Id + row.Id) }", "Returning (-(`Id` + `Id`)) As `Total`")]
    public void CachedReturning_WhenProjectionIsDeclaredInAnotherFile_ShouldBindItsSemanticModel(
        string projection, string returningSql)
    {
        var result = RunGenerator(Source("""
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) => source.Returning<Row>(SharedProjection.Value);
            """), additionalSource: $$"""
            using System;
            using System.Linq.Expressions;
            public static class SharedProjection
            {
                public static readonly Expression<Func<Row, object>> Value = {{projection}};
            }
            """);
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, new MatchingReturningMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "Apply", builder, "Consumer");
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " + returningSql, builder.ToSql());
    }

    /// <summary>类型初始化时的未赋值字段必须按原 API 顺序报告 source 或 columns。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CachedReturning_WhenFieldIsReadBeforeInitialization_ShouldKeepArgumentValidation(bool computed, bool nullSource)
    {
        var projection = computed ? "row => new { Total = -(row.Id + row.Id) }" : "row => row.Id";
        var source = nullSource ? "null" :
            "new Bing.Data.Sql.Builders.Mutations.Builders.SqlUpdateBuilder(" +
            "Bing.Data.Sql.Builders.SqliteSqlProvider.Instance, Bing.Data.Sql.Builders.Core.SqlBuilderServices.CreateDefault())";
        var result = RunGenerator(Source($$"""
            public static readonly string Failure = Capture();
            private static readonly Expression<Func<Row, object>> Projection = {{projection}};
            private static string Capture()
            {
                ISqlUpdateBuilder source = {{source}};
                try { source.Returning<Row>(Projection); return "missing error"; }
                catch (ArgumentNullException error) { return error.ParamName; }
            }
            """));
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.Equal(nullSource ? "source" : "columns",
            LoadGeneratedAssembly(result).GetType("Consumer")!.GetField("Failure")!.GetValue(null));
    }

    /// <summary>静态构造器重写字段后，回退路径必须采用实际值而非声明处的 Id。</summary>
    [Fact]
    public void CachedReturning_WhenStaticConstructorReassignsField_ShouldUseActualProjection()
    {
        var result = RunGenerator(Source("""
            private static readonly Expression<Func<Row, object>> Projection = row => row.Id;
            static Consumer() { Projection = row => row.Name; }
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) => source.Returning<Row>(Projection);
            """));
        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, new MatchingReturningMappingResolver());
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "Apply", builder, "Consumer");
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning `Name` As `Name`", builder.ToSql());
    }

    /// <summary>复用只读表达式时仍生成三个方言的 Returning，不重复解析运行时表达式。</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    public void CachedReturning_WhenStaticReadonlyProjectionIsReused_ShouldGenerateAllMutations(string providerKey)
    {
        var result = RunGenerator(Source("""
            private static readonly Expression<Func<Row, object>> Projection = row => new { row.Id, Display = row.Name };
            public static ISqlInsertBuilder ApplyInsert(ISqlInsertBuilder source) => source.Returning<Row>(Projection);
            public static ISqlUpdateBuilder ApplyUpdate(ISqlUpdateBuilder source) => source.Returning<Row>(columns: Projection);
            public static ISqlDeleteBuilder ApplyDelete(ISqlDeleteBuilder source) => source.Returning<Row>(Projection);
            """));

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.Equal(3, result.GeneratedSource.Split("private static string ReturningSql_", StringSplitOptions.None).Length - 1);
        var assembly = LoadGeneratedAssembly(result);
        var provider = GetReturningProvider(providerKey);
        var resolver = new MatchingReturningMappingResolver();
        var insert = CreateInsertReturningBuilder(provider, resolver);
        var update = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);
        var delete = CreateDeleteReturningBuilder(provider, resolver);
        ApplyGeneratedReturning(assembly, "ApplyInsert", insert, "Consumer");
        ApplyGeneratedReturning(assembly, "ApplyUpdate", update, "Consumer");
        ApplyGeneratedReturning(assembly, "ApplyDelete", delete, "Consumer");

        var expected = providerKey switch
        {
            "sqlserver" => new[]
            {
                "Insert Into [samples] ([Name]) Output [Inserted].[Id] As [Id], [Inserted].[Name] As [Display] Values (@_p_0)",
                "Update [samples] Set [Name] = @_p_0 Output [Inserted].[Id] As [Id], [Inserted].[Name] As [Display] Where [Id]=@_p_1",
                "Delete From [samples] Output [Deleted].[Id] As [Id], [Deleted].[Name] As [Display] Where [Id]=@_p_0"
            },
            "postgresql" => new[]
            {
                "Insert Into \"samples\" (\"Name\") Values (@_p_0) Returning \"Id\" As \"Id\", \"Name\" As \"Display\"",
                "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 Returning \"Id\" As \"Id\", \"Name\" As \"Display\"",
                "Delete From \"samples\" Where \"Id\"=@_p_0 Returning \"Id\" As \"Id\", \"Name\" As \"Display\""
            },
            _ => new[]
            {
                "Insert Into `samples` (`Name`) Values (@_p_0) Returning `Id` As `Id`, `Name` As `Display`",
                "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning `Id` As `Id`, `Name` As `Display`",
                "Delete From `samples` Where `Id`=@_p_0 Returning `Id` As `Id`, `Name` As `Display`"
            }
        };
        Assert.Equal(expected[0], insert.ToSql());
        Assert.Equal(expected[1], update.ToSql());
        Assert.Equal(expected[2], delete.ToSql());
        Assert.Equal(expected[1], update.ToSql());
        Assert.Equal(2, update.MutationContext.ParameterManager.Count);
    }

    /// <summary>复用计算型投影时仍绑定当前映射，不把首次使用的物理列缓存到投影中。</summary>
    [Fact]
    public void CachedReturning_WhenComputedProjectionIsReused_ShouldRebindCurrentMapping()
    {
        var result = RunGenerator(Source("""
            private static readonly Expression<Func<Row, object>> Projection = row =>
                new Summary { Key = -(row.Id + row.Id), Caption = row.Name };
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) => source.Returning<Row>(Projection);
            """));

        AssertNoCompilationErrors(result);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002");
        Assert.Contains("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        var assembly = LoadGeneratedAssembly(result);
        var resolver = new ReturningMappingResolver { Prefix = "first_" };
        var first = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        ApplyGeneratedReturning(assembly, "Apply", first, "Consumer");
        const string firstSql = "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning (-(`first_id` + `first_id`)) As `Key`, `first_name` As `Caption`";
        Assert.Equal(firstSql, first.ToSql());

        resolver.Prefix = "second_";
        var second = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        ApplyGeneratedReturning(assembly, "Apply", second, "Consumer");
        Assert.Equal(firstSql.Replace("first_", "second_", StringComparison.Ordinal), second.ToSql());
        Assert.Equal(firstSql, first.ToSql());
    }

    /// <summary>变量或构造器中的赋值不能被误当作固定投影。</summary>
    [Theory]
    [InlineData("private static Expression<Func<Row, object>> Projection = row => row.Id;")]
    [InlineData("private static readonly Expression<Func<Row, object>> Projection; static Consumer() { Projection = row => row.Id; }")]
    [InlineData("private static readonly Expression<Func<Row, object>> Projection = row => row.Id; static Consumer() { Projection = row => row.Name; }")]
    [InlineData("private static Expression<Func<Row, object>> Projection => row => row.Id;")]
    [InlineData("private static readonly Expression<Func<Row, object>> Projection = Create(); private static Expression<Func<Row, object>> Create() => row => row.Id;")]
    public void CachedReturning_WhenProjectionIsNotFixedAtDeclaration_ShouldKeepFallback(string declaration)
    {
        var result = RunGenerator(Source(declaration + """
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) => source.Returning<Row>(Projection);
            """), analyzerConfigOptionsProvider: StrictOptions());

        AssertNoCompilationErrors(result);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "BINGSQLG002" &&
            diagnostic.Severity == DiagnosticSeverity.Info);
        Assert.DoesNotContain("ReturningSql_", result.GeneratedSource, StringComparison.Ordinal);
    }
}
