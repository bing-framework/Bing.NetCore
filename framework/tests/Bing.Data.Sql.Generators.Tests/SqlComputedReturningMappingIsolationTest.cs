using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Xunit;

namespace Bing.Data.Sql.Generators.Tests;

public sealed partial class SqlQueryGeneratorTest
{
    /// <summary>共享同一生成调用点的并发 Builder 不应互相覆盖已绑定的映射列。</summary>
    [Fact]
    public async Task GeneratedComputedReturning_WhenBuildersUseConcurrentMappings_ShouldKeepEachLayoutIsolated()
    {
        var generated = RunGenerator(ComputedReturningConsumer);
        AssertNoCompilationErrors(generated);
        var assembly = LoadGeneratedAssembly(generated);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(index => Task.Run(() =>
        {
            var prefix = "tenant_" + index + "_";
            var resolver = new ComputedReturningLayoutResolver { Prefix = prefix };
            var builder = CreateGeneratedSqlReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);
            ApplyGeneratedReturning(assembly, "ApplyUpdate", builder, "ComputedReturningConsumer");
            var expected = "Update `samples` Set `Name` = @_p_0 Where `Id`=@_p_1 Returning " +
                $"((`{prefix}physical_id` + `{prefix}physical_id`) * " +
                $"(`{prefix}physical_id` - `{prefix}physical_id`)) As `Total`, `{prefix}display_name` As `Name`";

            Assert.Equal(expected, builder.ToSql());
            Assert.Equal(expected, builder.Clone().ToSql());
            Assert.Equal(expected, builder.ToSql());
            Assert.Equal(1, resolver.ResolveCount);
        })));
    }
}
