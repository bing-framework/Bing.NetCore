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
    private const string ReturningConsumer = """
        using Bing.Data.Sql;
        using Bing.Data.Sql.Builders.Mutations;
        public sealed class ReturningRow { public int Id { get; set; } public string Name { get; set; } }
        public sealed class ReturningDto
        {
            public ReturningDto() => throw new System.Exception("Projection must not run");
            public string Display { get; set; }
            public int Key { get; set; }
        }
        public static class ReturningConsumer
        {
            public static ISqlUpdateBuilder Apply(ISqlUpdateBuilder source) =>
                source.Returning<ReturningRow>(row => new ReturningDto { Display = row.Name, Key = row.Id });
        }
        """;

    /// <summary>生成布局应复用运行库的列查找合同，避免复制默认索引和自定义回退规则。</summary>
    [Fact]
    public void GeneratedReturning_WhenColumnLookupContractExists_ShouldReuseMappingLookup()
    {
        var result = RunGenerator(ReturningConsumer);
        AssertNoCompilationErrors(result);
        Assert.Contains("mapping?.FindColumn(layout[i].Property)", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>引用不包含可选查找合同的运行库时，生成器继续输出兼容扫描。</summary>
    [Fact]
    public void GeneratedReturning_WhenColumnLookupContractIsAbsent_ShouldKeepCompatibleScan()
    {
        // 用源类型遮蔽可选元数据合同，仅模拟生成器能力发现，不替代真实消费执行测试。
        var result = RunGenerator(ReturningConsumer + "\nnamespace Bing.Data.Sql.Metadata { public sealed class EntityMappingMetadata {} }");
        AssertNoCompilationErrors(result);
        Assert.DoesNotContain("mapping?.FindColumn", result.GeneratedSource, StringComparison.Ordinal);
        Assert.Contains("foreach (var item in mapping.Columns.Values)", result.GeneratedSource, StringComparison.Ordinal);
    }

    /// <summary>编译并执行生成调用，确认物理映射仍由当前上下文决定且不实例化 DTO。</summary>
    [Theory]
    [InlineData("sqlite", "Update `samples` Set `name` = @_p_0 Where `id`=@_p_1 Returning `physical_name` As `Display`, `physical_id` As `Key`")]
    [InlineData("postgresql", "Update \"samples\" Set \"name\" = @_p_0 Where \"id\"=@_p_1 Returning \"physical_name\" As \"Display\", \"physical_id\" As \"Key\"")]
    [InlineData("sqlserver", "Update [samples] Set [name] = @_p_0 Output [Inserted].[physical_name] As [Display], [Inserted].[physical_id] As [Key] Where [id]=@_p_1")]
    public void GeneratedReturning_WhenExecuted_ShouldUseCurrentMappingAndProvider(string providerKey, string expectedSql)
    {
        var result = RunGenerator(ReturningConsumer);
        Assert.Contains("ApplyReturning", result.GeneratedSource, StringComparison.Ordinal);
        var apply = LoadGeneratedAssembly(result).GetType("ReturningConsumer")!.GetMethod("Apply")!;
        var resolver = new ReturningMappingResolver();
        ISqlProvider provider = providerKey switch
        {
            "sqlserver" => SqlServerSqlProvider.Instance,
            "postgresql" => PostgreSqlSqlProvider.Instance,
            _ => SqliteSqlProvider.Instance
        };
        var builder = CreateReturningExecutionBuilder(provider, resolver);

        Assert.Same(builder, apply.Invoke(null, new object[] { builder }));
        Assert.Equal(expectedSql, builder.ToSql());
        Assert.Equal(expectedSql, builder.ToSql());
        Assert.Equal(1, resolver.ResolveCalls);
        Assert.Same(builder.MutationContext.ExecutionContext.DatabaseContext, resolver.LastContext);

        // 同一个调用点的静态布局不能冻结前一次的物理映射。
        resolver.Prefix = "tenant_";
        var other = CreateReturningExecutionBuilder(provider, resolver);
        apply.Invoke(null, new object[] { other });
        Assert.Equal(expectedSql.Replace("physical_", "tenant_", StringComparison.Ordinal), other.ToSql());
        Assert.Equal(2, resolver.ResolveCalls);
    }

    /// <summary>生成路径的映射失败也必须保持子句原子性。</summary>
    [Fact]
    public void GeneratedReturning_WhenMappingIsMissing_ShouldNotAppendPartialColumns()
    {
        var result = RunGenerator(ReturningConsumer);
        Assert.Contains("ApplyReturning", result.GeneratedSource, StringComparison.Ordinal);
        var apply = LoadGeneratedAssembly(result).GetType("ReturningConsumer")!.GetMethod("Apply")!;
        var resolver = new ReturningMappingResolver { OmitId = true };
        var builder = CreateReturningExecutionBuilder(SqliteSqlProvider.Instance, resolver);

        var error = Assert.Throws<TargetInvocationException>(() => apply.Invoke(null, new object[] { builder }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.True(builder.ReturningClause.IsEmpty);
        Assert.Equal("Update `samples` Set `name` = @_p_0 Where `id`=@_p_1", builder.ToSql());
    }

    private static ISqlUpdateBuilder CreateReturningExecutionBuilder(ISqlProvider provider, IEntityMappingResolver resolver) =>
        new SqlUpdateBuilder(provider, new SqlBuilderServices(entityMappingResolver: resolver))
            .Update(new SqlTableReference { TableName = "samples" }).Set("name", "updated").Where("id", 1);

    private sealed class ReturningMappingResolver : IEntityMappingResolver
    {
        internal string Prefix { get; set; } = "physical_";
        internal bool OmitId { get; set; }
        internal int ResolveCalls { get; private set; }
        internal DatabaseContext LastContext { get; private set; }

        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();

        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext)
        {
            ResolveCalls++;
            LastContext = databaseContext;
            var columns = new Dictionary<string, ColumnMappingMetadata>
            {
                ["name"] = new() { PropertyName = "Name", ColumnName = Prefix + "name" }
            };
            if (!OmitId) columns["id"] = new() { PropertyName = "Id", ColumnName = Prefix + "id" };
            return new EntityMappingMetadata { EntityType = entityType, Columns = columns };
        }
    }
}
