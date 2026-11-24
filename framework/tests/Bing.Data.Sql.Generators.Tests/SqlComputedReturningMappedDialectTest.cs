using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>SQL Server 目标别名不应覆盖 Returning 所需的 Inserted 或 Deleted 伪表限定符。</summary>
    [Theory]
    [InlineData("update")]
    [InlineData("delete")]
    public void GeneratedReturning_WhenSqlServerTargetHasAliasAndMappingChanges_ShouldUsePseudoTableQualifier(
        string operation)
    {
        var result = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(result);

        var resolver = new ComputedReturningLayoutResolver();
        var services = new SqlBuilderServices(entityMappingResolver: resolver);
        string firstSql;
        string secondSql;
        string expected;

        if (operation == "update")
        {
            var builder = new SqlUpdateBuilder(SqlServerSqlProvider.Instance, services)
                .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
                .Set("Name", "updated")
                .Where("Id", 1);
            ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder,
                "ComputedReturningConsumer");
            firstSql = builder.ToSql();
            secondSql = builder.ToSql();
            expected = "Update [samples] As [t] Set [Name] = @_p_0 Output " +
                "(([Inserted].[physical_id] + [Inserted].[physical_id]) * " +
                "([Inserted].[physical_id] - [Inserted].[physical_id])) As [Total], " +
                "[Inserted].[display_name] As [Name] Where [Id]=@_p_1";
        }
        else
        {
            var builder = new SqlDeleteBuilder(SqlServerSqlProvider.Instance, services)
                .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
                .Where("Id", 1);
            ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyDelete", builder,
                "ComputedReturningConsumer");
            firstSql = builder.ToSql();
            secondSql = builder.ToSql();
            // 默认 SQL Server Delete 目标不渲染别名，回传仍按 Deleted 限定当前列。
            expected = "Delete From [samples] Output " +
                "(([Deleted].[physical_id] + [Deleted].[physical_id]) * " +
                "([Deleted].[physical_id] - [Deleted].[physical_id])) As [Total], " +
                "[Deleted].[display_name] As [Name] Where [Id]=@_p_0";
        }

        Assert.Equal(expected, firstSql);
        Assert.Equal(expected, secondSql);
        Assert.Equal(1, resolver.ResolveCount);
    }

    /// <summary>自定义方言在动态映射下应使用当前物理列名完整渲染 Returning。</summary>
    [Fact]
    public void GeneratedReturning_WhenCustomAngleDialectAndMappingChanges_ShouldRenderMappedColumns()
    {
        var result = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(result);

        var resolver = new ComputedReturningLayoutResolver();
        var provider = new ComputedBoundaryDialectProvider(SqliteSqlProvider.Instance);
        var builder = CreateGeneratedSqlReturningExecutionBuilder(provider, resolver);
        ApplyGeneratedReturning(LoadGeneratedAssembly(result), "ApplyUpdate", builder,
            "ComputedReturningConsumer");

        const string expected = "Update <samples> Set <Name> = @_p_0 Where <Id>=@_p_1 " +
            "Returning ((<physical_id> + <physical_id>) * " +
            "(<physical_id> - <physical_id>)) As <Total>, <display_name> As <Name>";

        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(expected, builder.ToSql());
        Assert.Equal(1, resolver.ResolveCount);
    }
}
