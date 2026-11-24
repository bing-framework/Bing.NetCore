using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// <summary>计算型 Returning 应生成 Provider SQL，并由布局绑定处理运行时映射。</summary>
    [Fact]
    public void Generate_WhenComputedReturningUsesRuntimeLayoutBinding_ShouldNotEmitExpressionFallback()
    {
        var result = RunGenerator(ComputedReturningConsumer);

        AssertNoCompilationErrors(result);
        Assert.Contains("ApplyComputedReturning<", result.GeneratedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "global::Bing.Data.Sql.MutationClauseExtensions.Returning<T, TEntity>(source, expression)",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("Returning ((`Id` + `Id`) * (`Id` - `Id`)) As `Total`",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("Returning ((\\\"Id\\\" + \\\"Id\\\") * (\\\"Id\\\" - \\\"Id\\\")) As \\\"Total\\\"",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("Output (([Inserted].[Id] + [Inserted].[Id]) * ([Inserted].[Id] - [Inserted].[Id])) As [Total]",
            result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("Output (([Deleted].[Id] + [Deleted].[Id]) * ([Deleted].[Id] - [Deleted].[Id])) As [Total]",
            result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>动态物理列名应绑定一次，并在三个 Provider 上稳定渲染完整 SQL。</summary>
    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgresql")]
    [InlineData("sqlserver")]
    public void GeneratedReturning_WhenMappingChanges_ShouldBindOnceAndRenderStableSql(string providerKey)
    {
        var result = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(result);

        var resolver = new ComputedReturningLayoutResolver();
        var builder = CreateGeneratedSqlReturningExecutionBuilder(GetReturningProvider(providerKey), resolver);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder,
            "ComputedReturningConsumer");

        var expected = providerKey switch
        {
            "sqlserver" => "Update [samples] Set [Name] = @_p_0 Output " +
                "(([Inserted].[physical_id] + [Inserted].[physical_id]) * " +
                "([Inserted].[physical_id] - [Inserted].[physical_id])) As [Total], " +
                "[Inserted].[display_name] As [Name] Where [Id]=@_p_1",
            "postgresql" => "Update \"samples\" Set \"Name\" = @_p_0 Where \"Id\"=@_p_1 " +
                "Returning ((\"physical_id\" + \"physical_id\") * " +
                "(\"physical_id\" - \"physical_id\")) As \"Total\", " +
                "\"display_name\" As \"Name\"",
            _ => "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
                "Returning ((`physical_id` + `physical_id`) * " +
                "(`physical_id` - `physical_id`)) As `Total`, " +
                "`display_name` As `Name`"
        };

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(1, resolver.ResolveCount);
    }

    /// <summary>列布局绑定必须组合目标别名和当前物理列映射。</summary>
    [Fact]
    public void GeneratedReturning_WhenMappingChangesAndTargetHasAlias_ShouldRenderQualifiedPostgreSql()
    {
        var result = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(result);

        var resolver = new ComputedReturningLayoutResolver();
        var builder = new SqlUpdateBuilder(PostgreSqlSqlProvider.Instance,
                new SqlBuilderServices(entityMappingResolver: resolver))
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .Set("Name", "updated")
            .Where("Id", 1);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder,
            "ComputedReturningConsumer");

        const string expected = "Update \"samples\" As \"t\" Set \"Name\" = @_p_0 " +
            "Where \"Id\"=@_p_1 Returning ((\"t\".\"physical_id\" + \"t\".\"physical_id\") * " +
            "(\"t\".\"physical_id\" - \"t\".\"physical_id\")) As \"Total\", " +
            "\"t\".\"display_name\" As \"Name\"";

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(1, resolver.ResolveCount);
    }

    /// <summary>相同编译期物理名也必须按 CLR 属性分别绑定运行时映射。</summary>
    [Fact]
    public void GeneratedReturning_WhenCompileTimeColumnsCollide_ShouldUseEachRuntimePropertyMapping()
    {
        const string consumer = """
            using System.ComponentModel.DataAnnotations.Schema;
            using Bing.Data.Sql;
            using Bing.Data.Sql.Builders.Mutations;

            public sealed class TwinMappedReturningRow
            {
                [Column("shared")] public int Left { get; set; }
                [Column("shared")] public int Right { get; set; }
            }

            public static class TwinMappedReturningConsumer
            {
                public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) =>
                    source.Returning<TwinMappedReturningRow>(row => new { Total = row.Left + row.Right });
            }
            """;
        var result = RunGenerator(consumer);
        AssertNoCompilationErrors(result);

        var resolver = new TwinMappedReturningResolver();
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "Apply", builder, "TwinMappedReturningConsumer");

        const string expected = "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning (`left_value` + `right_value`) As `Total`";
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(1, resolver.ResolveCount);
    }

    /// <summary>映射缺列时不得追加部分计算列，也不得清除已有 Returning 列。</summary>
    [Fact]
    public void GeneratedReturning_WhenComputedMappingIsIncomplete_ShouldKeepExistingProjection()
    {
        var result = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(result);

        var resolver = new ComputedReturningLayoutResolver { OmitId = true };
        var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
        builder.Returning<ComputedReturningTestRow>(row => new { row.Name });
        resolver.ResetCount();
        var apply = LoadGeneratedAssembly(result).GetType("ComputedReturningConsumer")!
            .GetMethod("ApplyUpdate")!;

        var error = Assert.Throws<TargetInvocationException>(() => apply.Invoke(null, new object[] { builder }));

        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(1, resolver.ResolveCount);
        Assert.False(builder.ReturningClause.IsEmpty);
        Assert.Equal("Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 " +
            "Returning `display_name` As `Name`", builder.ToSql());
    }

    private sealed class ComputedReturningLayoutResolver : IEntityMappingResolver
    {
        internal string Prefix { get; set; } = "";
        internal bool OmitId { get; set; }
        internal int ResolveCount { get; private set; }

        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
        {
            ResolveCount++;
            var columns = new Dictionary<string, ColumnMappingMetadata>
            {
                ["Name"] = new() { PropertyName = "Name", ColumnName = Prefix + "display_name" }
            };
            if (!OmitId)
                columns["Id"] = new() { PropertyName = "Id", ColumnName = Prefix + "physical_id" };
            return new EntityMappingMetadata { EntityType = entityType, Columns = columns };
        }

        internal void ResetCount() => ResolveCount = 0;
    }

    private sealed class ComputedReturningTestRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class TwinMappedReturningResolver : IEntityMappingResolver
    {
        internal int ResolveCount { get; private set; }

        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
        {
            ResolveCount++;
            return new EntityMappingMetadata
            {
                EntityType = entityType,
                Columns = new Dictionary<string, ColumnMappingMetadata>
                {
                    ["Left"] = new() { PropertyName = "Left", ColumnName = "left_value" },
                    ["Right"] = new() { PropertyName = "Right", ColumnName = "right_value" }
                }
            };
        }
    }
}
