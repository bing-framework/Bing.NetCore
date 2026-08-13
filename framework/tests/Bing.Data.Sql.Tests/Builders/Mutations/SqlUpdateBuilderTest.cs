using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Filters;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Bing.Test.Shared;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Update Mutation Builder 测试。
/// </summary>
public sealed class SqlUpdateBuilderTest
{
    /// <summary>
    /// 测试目的：Update Fluent API 应按 Update、Set、Where 顺序输出 SQL，并返回原 Builder。
    /// </summary>
    [Fact]
    public void Update_WhenSetAndWhereConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        const string expectedSql = "Update [samples] Set [Name] = @_p_0 Where [Id]=@_p_1";
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        builder.MutationContext.ParameterManager.Add("@_p_1", 7);

        // Act
        var result = builder.Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .Where(new EqualCondition("[Id]", "@_p_1"));

        // Assert
        Assert.Same(builder, result);
        var parameters = builder.GetParameters().ToArray();
        SqlAssert.Equal(expectedSql, builder.ToSql(), TestMutationSqlProvider.Instance.Key, parameters);
        SqlParameterAssert.Equal(parameters, "@_p_0", "Bing");
        SqlParameterAssert.Equal(parameters, "@_p_1", 7);
    }

    /// <summary>
    /// 测试目的：未显式允许时，无 Where 的 Update 必须被拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenWhereIsMissingAndAllRowsNotAllowed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("拒绝执行无条件 Update 操作。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clear 后 Builder 应保留共享配置并清空所有 Mutation 状态。
    /// </summary>
    [Fact]
    public void Clear_WhenBuilderHasState_ShouldAllowReuseWithoutPreviousState()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .AllowAllRows();

        // Act
        builder.Clear();
        builder.Update(new SqlTableReference { TableName = "samples" }).Set("Name", "Framework").AllowAllRows();

        // Assert
        Assert.Equal("Update [samples] Set [Name] = @_p_0", builder.ToSql());
        Assert.Single(builder.GetParameters());
    }

    /// <summary>
    /// 测试目的：强类型 Set 与 Where 应使用实体映射列和带元数据参数输出完整 Update SQL。
    /// </summary>
    [Fact]
    public void Update_WhenTypedSetAndWhereConfigured_ShouldRenderMappedParameterizedSql()
    {
        // Arrange
        const string expectedSql = "Update [typed_samples] Set [Name] = @_p_0 Where [Id]=@_p_1";
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        builder.Update<TypedMutationSample>()
            .Set<TypedMutationSample, string>(item => item.Name, "Bing")
            .Where<TypedMutationSample, int>(item => item.Id, 7);

        // Assert
        var command = builder.BuildCommand();
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", "Bing", DbType.String);
        SqlParameterAssert.Equal(command.Parameters, "@_p_1", 7, DbType.Int32);
        Assert.All(command.Parameters, item => Assert.Equal(SqlParameterMetadataLevel.Full, item.MetadataLevel));
    }

    /// <summary>
    /// 测试目的：强类型 Set 不得更新实体主键，避免 Fluent API 绕过实体写入安全规则。
    /// </summary>
    [Fact]
    public void Set_WhenTypedPrimaryKeyIsProvided_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update<TypedMutationSample>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Set<TypedMutationSample, int>(item => item.Id, 7));

        // Assert
        Assert.Equal("实体 TypedMutationSample 的属性 Id 不能用于更新。", exception.Message);
    }

    /// <summary>
    /// 测试目的：UpdateFrom 应使用结构化来源列完成 Set 和主键关联，并按 PostgreSQL 兼容顺序输出子句。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenStructuredColumnsAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        builder.Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id");

        // Assert
        Assert.Equal("Update [samples] As [t] Set [Name] = [s].[Name] From [sample_updates] As [s] Where [t].[Id]=[s].[Id]",
            builder.ToSql());
        Assert.Empty(builder.GetParameters());
    }

    /// <summary>
    /// 测试目的：UpdateFrom 的来源表和来源列应随 Clone 独立复制，并在 Clear 后完整移除。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenClonedAndCleared_ShouldKeepInstancesIndependent()
    {
        // Arrange
        var source = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id");

        // Act
        var clone = source.Clone();
        source.Clear();

        // Assert
        Assert.Equal("Update [samples] As [t] Set [Name] = [s].[Name] From [sample_updates] As [s] Where [t].[Id]=[s].[Id]",
            clone.ToSql());
        Assert.Throws<InvalidOperationException>(() => source.ToSql());
    }

    /// <summary>
    /// 测试 - 结构化软删除实体 Update 必须将未删除边界写入 Where，并保留调用方条件。
    /// </summary>
    [Fact]
    public void Update_WhenSoftDeleteEntityIsConfigured_ShouldAppendDataBoundary()
    {
        // 目标 SQL：调用方 Id 条件与软删除边界共同进入 Where。
        const string expectedSql = "Update [Sample5] Set [StringValue] = @_p_0 Where [IntValue]=@_p_1 And [IsDeleted]=@_p_2";

        // 目标参数：@_p_0 = "Bing"；@_p_1 = 7；@_p_2 = false。
        // 结果语义：更新不会命中已软删除记录。
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        builder.Update<Sample5>()
            .Set<Sample5, string>(item => item.StringValue, "Bing")
            .Where<Sample5, int>(item => item.IntValue, 7);

        // Act
        var command = builder.BuildCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", "Bing", DbType.String);
        SqlParameterAssert.Equal(command.Parameters, "@_p_1", 7, DbType.Int32);
        SqlParameterAssert.Equal(command.Parameters, "@_p_2", false, DbType.Boolean);
    }

    /// <summary>
    /// 测试 - 共享 DataFilter 禁用软删除后，结构化 Update 不得保留软删除边界。
    /// </summary>
    [Fact]
    public void Update_WhenSoftDeleteFilterIsDisabled_ShouldOmitDataBoundary()
    {
        // 目标 SQL：禁用后只保留调用方条件。
        const string expectedSql = "Update [Sample5] Set [StringValue] = @_p_0 Where [IntValue]=@_p_1";

        // 目标参数：@_p_0 = "Bing"；@_p_1 = 7。
        // 结果语义：Host 或维护作用域显式禁用过滤后可更新已删除记录。
        var dataFilter = new DataFilter();
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices(dataFilter: dataFilter));
        builder.Update<Sample5>()
            .Set<Sample5, string>(item => item.StringValue, "Bing")
            .Where<Sample5, int>(item => item.IntValue, 7);

        // Act
        using var _ = dataFilter.Disable<ISoftDelete>();
        var command = builder.BuildCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", "Bing", DbType.String);
        SqlParameterAssert.Equal(command.Parameters, "@_p_1", 7, DbType.Int32);
    }

    /// <summary>
    /// 测试 - 结构化租户实体 Update 必须追加参数化 TenantId 写入边界。
    /// </summary>
    [Fact]
    public void Update_WhenTenantFilterIsEnabled_ShouldAppendTenantBoundary()
    {
        // Arrange
        const string expectedSql = "Update [TenantMutationSample] Set [Name] = @_p_0 Where [Id]=@_p_1 And [TenantId]=@_p_2";
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { new TenantIdFilter(new TenantMutationContributor()) });
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, services);

        // Act
        var command = builder.Update<TenantMutationSample>()
            .Set<TenantMutationSample, string>(item => item.Name, "Bing")
            .Where<TenantMutationSample, int>(item => item.Id, 7)
            .BuildCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_2", "tenant-a", DbType.String);
    }

    /// <summary>
    /// 测试目的：直接 AppendTo 应在数据边界后续失败时保留 Update Builder 的原始参数和 Where 状态。
    /// </summary>
    [Fact]
    public void AppendTo_WhenTenantBoundaryFailsAfterSoftDeleteBoundary_ShouldNotMutateBuilder()
    {
        // Arrange
        var dataFilter = new DataFilter();
        var services = new SqlBuilderServices(dataFilter: dataFilter, filters: new ISqlFilter[]
        {
            new IsDeletedFilter(),
            new TenantIdFilter(new MissingTenantSoftDeleteContributor())
        });
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, services);
        builder.Update<SoftDeleteTenantMutationSample>()
            .Set<SoftDeleteTenantMutationSample, string>(item => item.Name, "Bing")
            .Where<SoftDeleteTenantMutationSample, int>(item => item.Id, 7);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AppendTo(new StringBuilder()));

        // Assert
        Assert.Equal("租户过滤已启用，但实体 SoftDeleteTenantMutationSample 未解析到当前租户值。", exception.Message);
        Assert.Equal(2, builder.GetParameters().Count);
        using (dataFilter.Disable<TenantIdFilter>())
            Assert.Equal("Update [SoftDeleteTenantMutationSample] Set [Name] = @_p_0 Where [Id]=@_p_1 And [IsDeleted]=@_p_2", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Update AppendTo 的目标表引用验证失败时，不得向调用方缓冲区遗留 SQL 前缀。
    /// </summary>
    [Fact]
    public void AppendTo_WhenTargetTableIsInvalid_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "orders;" })
            .Set("Name", "Bing");
        builder.AllowAllRows();
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.AppendTo(result));

        // Assert
        Assert.Equal("表引用包含无效标识符字符。 (Parameter 'identifier')", exception.Message);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 测试目的：SetFrom 只接受单段结构化列名，避免把 Raw SQL 注入标识符入口。
    /// </summary>
    [Fact]
    public void SetFrom_WhenColumnIsNotSingleIdentifier_ShouldThrowWithoutChangingSetClause()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" });

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.SetFrom("Name", "s.Name"));

        // Assert
        Assert.Equal("sourceColumn", exception.ParamName);
        Assert.Equal(0, builder.SetClause.Count);
    }

    /// <summary>
    /// 强类型 Fluent Mutation 的映射实体。
    /// </summary>
    [Table("typed_samples")]
    private sealed class TypedMutationSample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 可更新名称。
        /// </summary>
        public string Name { get; set; }
    }

    private sealed class TenantMutationSample
    {
        [Key]
        public int Id { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
    }

    private sealed class TenantMutationContributor : ISqlTenantFilterContributor
    {
        public bool IsTenantEntity(Type entityType) => entityType == typeof(TenantMutationSample);
        public object GetTenantId(SqlTenantFilterContext context) => "tenant-a";
    }

    private sealed class SoftDeleteTenantMutationSample : ISoftDelete
    {
        [Key]
        public int Id { get; set; }
        public string TenantId { get; set; }
        public string Name { get; set; }
        public bool IsDeleted { get; set; }
    }

    private sealed class MissingTenantSoftDeleteContributor : ISqlTenantFilterContributor
    {
        public bool IsTenantEntity(Type entityType) => entityType == typeof(SoftDeleteTenantMutationSample);
        public object GetTenantId(SqlTenantFilterContext context) => null;
    }
}