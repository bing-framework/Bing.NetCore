using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Benchmarks.Models;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>
/// 比较实体 Returning 匿名投影和 DTO 成员初始化的命令构建分配。
/// </summary>
[MemoryDiagnoser]
public class SqlReturningProjectionBenchmarks
{
    internal const string ExpectedSql = "Update `returning_projection_benchmarks` Set `display_name` = @_p_0 " +
                                        "Where `row_id`=@_p_1 Returning `row_id` As `Key`, " +
                                        "`display_name` As `Display`";
    internal const string ExpectedComputedSql = "Update `returning_projection_benchmarks` Set `display_name` = @_p_0 " +
                                                 "Where `row_id`=@_p_1 Returning ((`row_id` + `row_id`) * `row_id`) " +
                                                 "As `Total`";
    internal const string ExpectedRemappedComputedSql = "Update `returning_projection_benchmarks` Set `tenant_name` = @_p_0 " +
        "Where `tenant_id`=@_p_1 Returning ((`tenant_id` + `tenant_id`) * `tenant_id`) As `Total`";
    internal const string ExpectedQuotedSql = "Update `returning_projection_benchmarks` Set `display_name` = @_p_0 " +
        "Where `row_id`=@_p_1 Returning `row id` As `Id`, `display``name` As `Name`";
    internal const string ExpectedQuotedComputedSql = "Update `returning_projection_benchmarks` Set `display_name` = @_p_0 " +
        "Where `row_id`=@_p_1 Returning (-(`row id` + `row id`)) As `Total`, `display``name` As `Name`";

    private SqlBuilderServices _services;
    private SqlBuilderServices _remappedServices;
    private Expression<Func<ReturningProjectionRow, object>> _anonymousProjection;
    private Expression<Func<ReturningProjectionRow, object>> _dtoProjection;
    private Expression<Func<ReturningProjectionRow, object>> _computedProjection;

    /// <summary>
    /// 初始化并预热直接列和计算投影，逐条核对完整 SQL。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _services = SqlBuilderServices.CreateDefault();
        _remappedServices = new SqlBuilderServices(entityMappingResolver: new RemappedReturningResolver());
        _anonymousProjection = entity => new { Key = entity.Id, Display = entity.Name };
        _dtoProjection = entity => new ReturningProjectionDto { Key = entity.Id, Display = entity.Name };
        _computedProjection = entity => new { Total = (entity.Id + entity.Id) * entity.Id };

        var commands = new[]
        {
            BuildAnonymousProjectionCommand(), BuildDtoMemberInitCommand(),
            BuildRuntimeInlineAnonymousCommand(), BuildRuntimeInlineDtoCommand(),
            BuildGeneratedAnonymousCommand(), BuildGeneratedDtoCommand(), BuildGeneratedCachedProjection()
        };
        if (commands.Any(command => command.Sql != ExpectedSql))
            throw new InvalidOperationException("Returning 投影基准未生成相同的预期 SQL。");

        var computedCommands = new[]
        {
            BuildCachedComputedCommand(), BuildRuntimeInlineComputedCommand(), BuildGeneratedInlineComputedCommand(),
            BuildGeneratedCachedComputedProjection()
        };
        if (computedCommands.Any(command => command.Sql != ExpectedComputedSql))
            throw new InvalidOperationException("Returning 算术投影基准未生成相同的预期 SQL。");
        if (BuildCachedRemappedComputedProjection().Sql != ExpectedRemappedComputedSql ||
            BuildGeneratedRemappedComputedProjection().Sql != ExpectedRemappedComputedSql)
            throw new InvalidOperationException("Returning 动态映射基准未生成相同的预期 SQL。");
        if (BuildGeneratedQuotedProjection().Sql != ExpectedQuotedSql ||
            BuildGeneratedQuotedComputedProjection().Sql != ExpectedQuotedComputedSql)
            throw new InvalidOperationException("Returning 标识符基准未生成相同的预期 SQL。");
    }

    /// <summary>
    /// 测量复用匿名对象表达式的 Update Builder 与命令构建。
    /// </summary>
    /// <returns>包含 SQL 与参数快照的写入命令。</returns>
    [Benchmark(Baseline = true)]
    public SqlWriteCommand BuildAnonymousProjection() => BuildAnonymousProjectionCommand();

    /// <summary>
    /// 测量复用 DTO 成员初始化表达式的 Update Builder 与命令构建。
    /// </summary>
    /// <returns>包含 SQL 与参数快照的写入命令。</returns>
    [Benchmark]
    public SqlWriteCommand BuildDtoMemberInitProjection() => BuildDtoMemberInitCommand();

    /// <summary>测量每次创建 Lambda 表达式树的运行时匿名投影。</summary>
    [Benchmark]
    public SqlWriteCommand BuildRuntimeInlineAnonymousProjection() => BuildRuntimeInlineAnonymousCommand();

    /// <summary>测量每次创建 Lambda 表达式树的运行时 DTO 投影。</summary>
    [Benchmark]
    public SqlWriteCommand BuildRuntimeInlineDtoProjection() => BuildRuntimeInlineDtoCommand();

    /// <summary>测量独立 Models 程序集中的生成式匿名投影调用。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedAnonymousProjection() => BuildGeneratedAnonymousCommand();

    /// <summary>测量独立 Models 程序集中的生成式 DTO 投影调用。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedDtoProjection() => BuildGeneratedDtoCommand();

    /// <summary>测量复用算术表达式树的 Returning 命令构建。</summary>
    [Benchmark]
    public SqlWriteCommand BuildCachedComputedProjection() => BuildCachedComputedCommand();

    /// <summary>测量每次创建算术 Lambda 表达式树的运行时内联 Returning。</summary>
    [Benchmark]
    public SqlWriteCommand BuildRuntimeInlineComputedProjection() => BuildRuntimeInlineComputedCommand();

    /// <summary>测量 Models 程序集内实际生成拦截的算术 Returning 调用。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedInlineComputedProjection() => BuildGeneratedInlineComputedCommand();

    /// <summary>测量静态只读投影同时复用表达式树和生成 SQL 的分配。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedCachedProjection()
    {
        var builder = CreateUpdateBuilder();
        ReturningProjectionContext.ApplyCachedAnonymous(builder);
        return builder.BuildCommand();
    }

    /// <summary>测量静态只读计算投影的生成 SQL，表达式树只初始化一次。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedCachedComputedProjection()
    {
        var builder = CreateUpdateBuilder();
        ReturningProjectionContext.ApplyCachedComputedArithmetic(builder);
        return builder.BuildCommand();
    }

    /// <summary>测量动态物理映射下复用算术表达式的运行时路径。</summary>
    [Benchmark]
    public SqlWriteCommand BuildCachedRemappedComputedProjection()
    {
        var builder = CreateRemappedUpdateBuilder();
        builder.Returning<ReturningProjectionRow>(_computedProjection);
        return builder.BuildCommand();
    }

    /// <summary>测量动态物理映射下的生成式算术布局绑定，包含调用点表达式树创建。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedRemappedComputedProjection()
    {
        var builder = CreateRemappedUpdateBuilder();
        ReturningProjectionContext.ApplyComputedArithmetic(builder);
        return builder.BuildCommand();
    }

    /// <summary>测量固定列名需要引用和转义时的生成式直接列路径。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedQuotedProjection()
    {
        var builder = CreateUpdateBuilder();
        ReturningProjectionContext.ApplyQuotedColumns(builder);
        return builder.BuildCommand();
    }

    /// <summary>测量固定列名需要引用和转义时的生成式列算术路径。</summary>
    [Benchmark]
    public SqlWriteCommand BuildGeneratedQuotedComputedProjection()
    {
        var builder = CreateUpdateBuilder();
        ReturningProjectionContext.ApplyQuotedArithmetic(builder);
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildAnonymousProjectionCommand()
    {
        var builder = CreateUpdateBuilder();
        builder.Returning<ReturningProjectionRow>(_anonymousProjection);
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildDtoMemberInitCommand()
    {
        var builder = CreateUpdateBuilder();
        builder.Returning<ReturningProjectionRow>(_dtoProjection);
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildRuntimeInlineAnonymousCommand()
    {
        var builder = CreateUpdateBuilder();
        builder.Returning<ReturningProjectionRow>(entity => new { Key = entity.Id, Display = entity.Name });
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildRuntimeInlineDtoCommand()
    {
        var builder = CreateUpdateBuilder();
        builder.Returning<ReturningProjectionRow>(entity => new ReturningProjectionDto
        {
            Key = entity.Id,
            Display = entity.Name
        });
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildGeneratedAnonymousCommand()
    {
        var builder = CreateUpdateBuilder();
        ReturningProjectionContext.ApplyAnonymous(builder);
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildGeneratedDtoCommand()
    {
        var builder = CreateUpdateBuilder();
        ReturningProjectionContext.ApplyDto(builder);
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildCachedComputedCommand()
    {
        var builder = CreateUpdateBuilder();
        builder.Returning<ReturningProjectionRow>(_computedProjection);
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildRuntimeInlineComputedCommand()
    {
        var builder = CreateUpdateBuilder();
        builder.Returning<ReturningProjectionRow>(entity => new { Total = (entity.Id + entity.Id) * entity.Id });
        return builder.BuildCommand();
    }

    private SqlWriteCommand BuildGeneratedInlineComputedCommand()
    {
        var builder = CreateUpdateBuilder();
        ReturningProjectionContext.ApplyComputedArithmetic(builder);
        return builder.BuildCommand();
    }

    private ISqlUpdateBuilder CreateUpdateBuilder() =>
        new SqlUpdateBuilder(SqliteSqlProvider.Instance, _services)
            .Update(new SqlTableReference { TableName = "returning_projection_benchmarks" })
            .Set("display_name", "updated")
            .Where("row_id", 42);

    private ISqlUpdateBuilder CreateRemappedUpdateBuilder() =>
        new SqlUpdateBuilder(SqliteSqlProvider.Instance, _remappedServices)
            .Update(new SqlTableReference { TableName = "returning_projection_benchmarks" })
            .Set("tenant_name", "updated")
            .Where("tenant_id", 42);

    private sealed class RemappedReturningResolver : IEntityMappingResolver
    {
        private readonly EntityMappingMetadata _mapping = new()
        {
            EntityType = typeof(ReturningProjectionRow),
            Columns = new Dictionary<string, ColumnMappingMetadata>
            {
                [nameof(ReturningProjectionRow.Id)] = new() { PropertyName = nameof(ReturningProjectionRow.Id), ColumnName = "tenant_id" },
                [nameof(ReturningProjectionRow.Name)] = new() { PropertyName = nameof(ReturningProjectionRow.Name), ColumnName = "tenant_name" }
            }
        };

        public EntityDescriptor GetDescriptor(Type entityType) => throw new NotSupportedException();
        public EntityMappingMetadata Resolve(Type entityType, DatabaseContext databaseContext) => _mapping;
    }
}
