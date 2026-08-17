using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text;
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
/// Delete Mutation Builder 测试。
/// </summary>
public sealed class SqlDeleteBuilderTest
{
    /// <summary>
    /// 测试目的：未显式允许时，无 Where 的 Delete 必须被拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenWhereIsMissingAndAllRowsNotAllowed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples" });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("拒绝执行无条件 Delete 操作。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Delete 允许全表时应输出无 Where SQL，Where Fluent 应返回原对象。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenAllRowsAllowed_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var result = builder.DeleteFrom(new SqlTableReference { TableName = "samples" }).AllowAllRows();

        // Assert
        Assert.Same(builder, result);
        Assert.Equal("Delete From [samples]", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Delete Where 应复用标准 Condition 组合模型。
    /// </summary>
    [Fact]
    public void Where_WhenMultipleConditionsConfigured_ShouldComposeWithAnd()
    {
        // Arrange
        const string expectedSql = "Delete From [samples] Where [Id]=@_p_0 And [TenantId]=@_p_1";
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples" });

        // Act
        builder.Where(new EqualCondition("[Id]", "@_p_0"))
            .Where(new EqualCondition("[TenantId]", "@_p_1"));

        // Assert
        SqlAssert.Equal(expectedSql, builder.ToSql(), TestMutationSqlProvider.Instance.Key, builder.GetParameters().ToArray());
    }

    /// <summary>
    /// 测试目的：强类型 Delete Where 应通过实体映射创建参数化物理列条件，不能拼接调用方输入。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenTypedWhereConfigured_ShouldRenderMappedParameterizedSql()
    {
        // Arrange
        const string expectedSql = "Delete From [typed_delete_samples] Where [Id]=@_p_0";
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        builder.DeleteFrom<TypedDeleteSample>()
            .Where<TypedDeleteSample, int>(item => item.Id, 7);

        // Assert
        var command = builder.BuildCommand();
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", 7, DbType.Int32);
        Assert.Equal(TestMutationSqlProvider.Instance.Key, command.ProviderKey);
        Assert.Equal(SqlOperationKind.Delete, command.OperationKind);
        Assert.False(command.HasReturning);
    }

    /// <summary>
    /// 测试目的：Delete Using 应按目标表、来源表和结构化列条件的固定顺序输出完整 SQL。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenStructuredTablesAreConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Delete From [samples] As [t] Using [sample_deletes] As [s] Where [t].[Id]=[s].[Id]", sql);
    }

    /// <summary>
    /// 测试目的：DeleteUsing 的列对列 In 和 NotIn 条件必须输出完整集合谓词。
    /// </summary>
    /// <param name="operator">集合比较操作符。</param>
    /// <param name="operatorSql">预期 SQL 操作符文本。</param>
    [Theory]
    [InlineData(Operator.In, "In")]
    [InlineData(Operator.NotIn, "Not In")]
    public void WhereUsing_WhenSetOperatorIsConfigured_ShouldRenderExpectedSql(Operator @operator,
        string operatorSql)
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        builder.DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id", @operator);

        // Assert
        Assert.Equal($"Delete From [samples] As [t] Using [sample_deletes] As [s] Where [t].[Id] {operatorSql} ([s].[Id])",
            builder.ToSql());
        Assert.Empty(builder.GetParameters());
    }

    /// <summary>
    /// 测试目的：Delete Using 不能替代 Delete 的无条件写保护。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenWhereIsMissing_ShouldRejectUnconditionalDelete()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("拒绝执行无条件 Delete 操作。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Delete Using 来源表必须具有别名，确保结构化列引用可唯一定位。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenSourceAliasIsMissing_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes" })
            .AllowAllRows();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Delete Using 来源表必须指定别名。", exception.Message);
    }

    /// <summary>
    /// 测试目的：WhereUsing 必须要求 Delete 目标表别名，失败时不得写入 Where。
    /// </summary>
    [Fact]
    public void WhereUsing_WhenTargetAliasIsMissing_ShouldThrowWithoutChangingWhere()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.WhereUsing("Id", "Id"));

        // Assert
        Assert.Equal("WhereUsing 要求 Delete 目标表指定别名。", exception.Message);
        Assert.True(builder.WhereClause.IsEmpty);
    }

    /// <summary>
    /// 测试目的：WhereUsing 应拒绝表达式和限定列名，避免调用方绕过结构化标识符边界。
    /// </summary>
    [Theory]
    [InlineData("t.Id")]
    [InlineData("Id = 1")]
    [InlineData("Id;Delete")]
    public void WhereUsing_WhenColumnIsNotSingleIdentifier_ShouldThrowArgumentException(string column)
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" });

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.WhereUsing(column, "Id"));

        // Assert
        Assert.Equal("列名必须是单段结构化标识符。 (Parameter 'targetColumn')", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clone 应保留独立 Delete Using 状态，Clear 不得影响副本。
    /// </summary>
    [Fact]
    public void Clone_WhenDeleteUsingIsConfigured_ShouldRemainIndependentAfterClear()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var clone = builder.Clone();
        builder.Clear();

        // Assert
        Assert.Equal("Delete From [samples] As [t] Using [sample_deletes] As [s] Where [t].[Id]=[s].[Id]",
            clone.ToSql());
        Assert.Null(builder.DeleteUsingClause.Table);
    }

    /// <summary>
    /// 测试 - 结构化软删除实体 Delete 必须执行物理删除且附加未删除边界。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenSoftDeleteEntityIsConfigured_ShouldAppendDataBoundary()
    {
        // 目标 SQL：物理 Delete 的 Where 同时包含调用方 Id 和 IsDeleted=false。
        const string expectedSql = "Delete From [Sample5] Where [IntValue]=@_p_0 And [IsDeleted]=@_p_1";

        // 目标参数：@_p_0 = 7；@_p_1 = false。
        // 结果语义：已删除记录不会被再次物理删除，Delete 不会隐式改写为 Update。
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        builder.DeleteFrom<Sample5>().Where<Sample5, int>(item => item.IntValue, 7);

        // Act
        var command = builder.BuildCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", 7, DbType.Int32);
        SqlParameterAssert.Equal(command.Parameters, "@_p_1", false, DbType.Boolean);
    }

    /// <summary>
    /// 测试 - 显式 AllowAllRows 只能解除无条件写保护，不能绕过软删除数据边界。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenSoftDeleteEntityAllowsAllRows_ShouldRetainDataBoundary()
    {
        // 目标 SQL：即使允许全表 Delete，软删除实体仍限定到未删除记录。
        const string expectedSql = "Delete From [Sample5] Where [IsDeleted]=@_p_0";

        // 目标参数：@_p_0 = false。
        // 结果语义：AllowAllRows 不会隐式跨越全局数据边界。
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        builder.DeleteFrom<Sample5>().AllowAllRows();

        // Act
        var command = builder.BuildCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", false, DbType.Boolean);
    }

    /// <summary>
    /// 测试 - 禁用共享软删除过滤后，结构化 Delete 不得生成 IsDeleted 边界。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenSoftDeleteFilterIsDisabled_ShouldOmitDataBoundary()
    {
        // 目标 SQL：禁用后只保留调用方条件。
        const string expectedSql = "Delete From [Sample5] Where [IntValue]=@_p_0";

        // 目标参数：@_p_0 = 7。
        // 结果语义：只有显式禁用过滤的维护作用域可物理删除已删除记录。
        var dataFilter = new DataFilter();
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices(dataFilter: dataFilter));
        builder.DeleteFrom<Sample5>().Where<Sample5, int>(item => item.IntValue, 7);

        // Act
        using var _ = dataFilter.Disable<ISoftDelete>();
        var command = builder.BuildCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", 7, DbType.Int32);
    }

    /// <summary>
    /// 测试 - 同一 Delete Builder 在软删除过滤作用域切换后必须从独立渲染快照恢复边界。
    /// </summary>
    [Fact]
    public void ToSql_WhenSoftDeleteFilterScopeChanges_ShouldRenderCurrentBoundaryWithoutMutatingBuilder()
    {
        // 目标 SQL：启用时包含 IsDeleted，禁用时只保留 Id，释放后恢复 IsDeleted。
        const string enabledExpectedSql = "Delete From [Sample5] Where [IntValue]=@_p_0 And [IsDeleted]=@_p_1";
        const string disabledExpectedSql = "Delete From [Sample5] Where [IntValue]=@_p_0";

        // 目标参数：启用快照 @_p_0 = 7、@_p_1 = false；禁用快照仅 @_p_0 = 7。
        // 结果语义：过滤 scope 不会被首次渲染永久固定，原 Builder 参数不包含渲染边界参数。
        var dataFilter = new DataFilter();
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices(dataFilter: dataFilter));
        builder.DeleteFrom<Sample5>().Where<Sample5, int>(item => item.IntValue, 7);

        // Act
        var first = builder.ToSql();
        string disabled;
        using (dataFilter.Disable<ISoftDelete>())
            disabled = builder.ToSql();
        var restored = builder.ToSql();

        // Assert
        SqlAssert.Equal(enabledExpectedSql, first, TestMutationSqlProvider.Instance.Key);
        SqlAssert.Equal(disabledExpectedSql, disabled, TestMutationSqlProvider.Instance.Key);
        SqlAssert.Equal(enabledExpectedSql, restored, TestMutationSqlProvider.Instance.Key);
        Assert.Single(builder.GetParameters());
        SqlParameterAssert.Equal(builder.GetParameters(), "@_p_0", 7, DbType.Int32);
    }

    /// <summary>
    /// 测试 - 结构化租户实体 Delete 必须保持物理删除语义并追加参数化 TenantId 边界。
    /// </summary>
    [Fact]
    public void DeleteFrom_WhenTenantFilterIsEnabled_ShouldAppendTenantBoundary()
    {
        // Arrange
        const string expectedSql = "Delete From [tenant_delete_samples] Where [Id]=@_p_0 And [TenantId]=@_p_1";
        var services = new SqlBuilderServices(filters: new ISqlFilter[] { new TenantIdFilter(new TenantDeleteContributor()) });
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, services);

        // Act
        var command = builder.DeleteFrom<TenantDeleteSample>()
            .Where<TenantDeleteSample, int>(item => item.Id, 7)
            .BuildCommand();

        // Assert
        SqlAssert.Equal(expectedSql, command.Sql, TestMutationSqlProvider.Instance.Key, command.Parameters);
        SqlParameterAssert.Equal(command.Parameters, "@_p_0", 7, DbType.Int32);
        SqlParameterAssert.Equal(command.Parameters, "@_p_1", "tenant-a", DbType.String);
    }

    /// <summary>
    /// 测试目的：直接 AppendTo 应在数据边界后续失败时保留 Delete Builder 的原始参数和 Where 状态。
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
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, services);
        builder.DeleteFrom<SoftDeleteTenantDeleteSample>()
            .Where<SoftDeleteTenantDeleteSample, int>(item => item.Id, 7);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.AppendTo(new StringBuilder()));

        // Assert
        Assert.Equal("租户过滤已启用，但实体 SoftDeleteTenantDeleteSample 未解析到当前租户值。", exception.Message);
        Assert.Single(builder.GetParameters());
        using (dataFilter.Disable<TenantIdFilter>())
            Assert.Equal("Delete From [SoftDeleteTenantDeleteSample] Where [Id]=@_p_0 And [IsDeleted]=@_p_1", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Delete AppendTo 的目标表引用验证失败时，不得向调用方缓冲区遗留 SQL 前缀。
    /// </summary>
    [Fact]
    public void AppendTo_WhenTargetTableIsInvalid_ShouldKeepCallerBufferUnchanged()
    {
        // Arrange
        var builder = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .DeleteFrom(new SqlTableReference { TableName = "orders;" });
        builder.AllowAllRows();
        var result = new StringBuilder("Prefix:");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => builder.AppendTo(result));

        // Assert
        Assert.Equal("表引用包含无效标识符字符。 (Parameter 'identifier')", exception.Message);
        Assert.Equal("Prefix:", result.ToString());
    }

    /// <summary>
    /// 强类型 Delete 条件的映射实体。
    /// </summary>
    [Table("typed_delete_samples")]
    private sealed class TypedDeleteSample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }
    }

    /// <summary>
    /// 映射到租户 Delete 测试表的实体。
    /// </summary>
    [Table("tenant_delete_samples")]
    private sealed class TenantDeleteSample
    {
        /// <summary>主键。</summary>
        [Key]
        public int Id { get; set; }

        /// <summary>租户标识。</summary>
        public string TenantId { get; set; }
    }

    private sealed class TenantDeleteContributor : ISqlTenantFilterContributor
    {
        public bool IsTenantEntity(Type entityType) => entityType == typeof(TenantDeleteSample);
        public object GetTenantId(SqlTenantFilterContext context) => "tenant-a";
    }

    private sealed class SoftDeleteTenantDeleteSample : ISoftDelete
    {
        [Key]
        public int Id { get; set; }
        public string TenantId { get; set; }
        public bool IsDeleted { get; set; }
    }

    private sealed class MissingTenantSoftDeleteContributor : ISqlTenantFilterContributor
    {
        public bool IsTenantEntity(Type entityType) => entityType == typeof(SoftDeleteTenantDeleteSample);
        public object GetTenantId(SqlTenantFilterContext context) => null;
    }
}