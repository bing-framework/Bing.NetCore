using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Moq;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// 专用 Mutation Builder 的 Returning 与 Insert Select 测试。
/// </summary>
public sealed class SqlMutationReturningInsertSelectTest
{
    /// <summary>
    /// 测试目的：SQL Server Insert Values 的 OUTPUT 应插在 Values 数据源之前，并写入命令元数据。
    /// </summary>
    [Fact]
    public void InsertValues_WhenReturningIsConfigured_ShouldRenderOutputAndMarkCommand()
    {
        // Arrange
        var builder = new SqlInsertBuilder(SqlServerSqlProvider.Instance, new SqlBuilderServices())
            .InsertInto("samples")
            .Columns("Name")
            .Values("Bing")
            .Returning("Name");

        // Act
        var command = builder.BuildCommand();

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Output [Inserted].[Name] Values (@_p_0)", command.Sql);
        Assert.Equal(SqlOperationKind.InsertValues, command.OperationKind);
        Assert.True(command.HasReturning);
        Assert.Equal(new object[] { "Bing" }, command.Parameters.Select(item => item.Value));
    }

    /// <summary>
    /// 测试目的：Insert Select 应隔离来源参数名、合并同一渲染快照，并且重复渲染不累积参数。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenReturningAndParameterNameConflict_ShouldRenderStableCommandSnapshot()
    {
        // Arrange
        var provider = SqlServerSqlProvider.Instance;
        var services = new SqlBuilderServices();
        var targetParameters = new ParameterManager(provider.Dialect);
        targetParameters.Add("_p_0", "reserved");
        var source = new ProviderSqlBuilder(provider, services)
            .Select("Status")
            .From("orders")
            .Where("TenantId", 7);
        var builder = new SqlInsertBuilder(provider, services, targetParameters)
            .InsertInto("archive_orders")
            .Columns("Status")
            .InsertSelect(source)
            .Returning("Status");

        // Act
        var firstSql = builder.ToSql();
        var secondSql = builder.ToSql();
        source.Clear();
        var command = builder.BuildCommand();

        // Assert
        const string expectedSql = "Insert Into [archive_orders] ([Status]) Output [Inserted].[Status] \r\nSelect [Status] \r\nFrom [orders] \r\nWhere [TenantId]=@_p_1";
        Assert.Equal(expectedSql, firstSql);
        Assert.Equal(expectedSql, secondSql);
        Assert.Equal(expectedSql, command.Sql);
        Assert.Equal(1, targetParameters.Count);
        Assert.Equal(SqlOperationKind.InsertSelect, command.OperationKind);
        Assert.True(command.HasReturning);
        Assert.Equal("reserved", command.Parameters.Single(item => item.Name == "@_p_0").Value);
        Assert.Equal(7, command.Parameters.Single(item => item.Name == "@_p_1").Value);
    }

    /// <summary>
    /// 测试目的：Insert Select 的 Clone、New 与 Clear 应隔离 Returning、来源查询和参数状态。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenClonedAndCleared_ShouldKeepIndependentState()
    {
        // Arrange
        var provider = SqlServerSqlProvider.Instance;
        var services = new SqlBuilderServices();
        var source = new ProviderSqlBuilder(provider, services).Select("Id").From("orders");
        var builder = new SqlInsertBuilder(provider, services)
            .InsertInto("archive_orders")
            .Columns("Id")
            .InsertSelect(source)
            .Returning("Id");

        // Act
        var clone = builder.Clone();
        var empty = builder.New();
        builder.Clear();
        builder.InsertInto("samples").Columns("Name").Values("Bing");

        // Assert
        Assert.Equal("Insert Into [archive_orders] ([Id]) Output [Inserted].[Id] \r\nSelect [Id] \r\nFrom [orders]", clone.ToSql());
        Assert.True(clone.BuildCommand().HasReturning);
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", builder.ToSql());
        Assert.True(empty.ReturningClause.IsEmpty);
        Assert.Empty(((SqlInsertBuilder)empty).GetParameters());
    }

    /// <summary>
    /// 测试目的：自定义非核心查询 Builder 必须显式拒绝，避免遗漏或冲突的子查询参数。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenQueryDoesNotSupportSnapshotParameterMerge_ShouldThrow()
    {
        // Arrange
        var query = new Mock<ISqlBuilder>();
        query.Setup(item => item.Provider).Returns(SqlServerSqlProvider.Instance);
        var builder = new SqlInsertBuilder(SqlServerSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.InsertSelect(query.Object));

        // Assert
        Assert.Equal("Insert Select 当前要求来源查询由 SqlBuilderBase 实现，以合并参数快照。", exception.Message);
    }

    /// <summary>
    /// 测试目的：已知 Select 投影数与显式目标列数不一致时应拒绝，并保留 Insert Values 可配置状态。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenProjectionCountDoesNotMatchTarget_ShouldThrowWithoutChangingBuilder()
    {
        // Arrange
        var provider = SqlServerSqlProvider.Instance;
        var services = new SqlBuilderServices();
        var source = new ProviderSqlBuilder(provider, services).Select("Id").From("orders");
        var builder = new SqlInsertBuilder(provider, services)
            .InsertInto("archive_orders")
            .Columns("Id", "Code");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.InsertSelect(source));

        // Assert
        Assert.Equal("Insert 目标列数量与 Select 投影数量不一致。", exception.Message);
        builder.Values(1, "A");
        Assert.Equal("Insert Into [archive_orders] ([Id], [Code]) Values (@_p_0, @_p_1)", builder.ToSql());
    }

    /// <summary>
    /// 测试目的：Insert Select 的来源参数合并后若目标验证失败，不得污染目标参数管理器。
    /// </summary>
    [Fact]
    public void InsertSelect_WhenTargetIsInvalid_ShouldNotMergeSourceParameters()
    {
        // Arrange
        var provider = SqlServerSqlProvider.Instance;
        var services = new SqlBuilderServices();
        var targetParameters = new ParameterManager(provider.Dialect);
        var source = new ProviderSqlBuilder(provider, services)
            .Select("Status")
            .From("orders")
            .Where("TenantId", 7);
        var builder = new SqlInsertBuilder(provider, services, targetParameters)
            .Columns("Status")
            .InsertSelect(source);

        // Act
        Assert.Throws<InvalidOperationException>(() => builder.BuildCommand());

        // Assert
        Assert.Empty(targetParameters.GetParams());
    }

    /// <summary>
    /// 测试目的：Update From 和 Delete Using 应正确渲染 SQL Server OUTPUT 及 PostgreSQL Returning。
    /// </summary>
    [Fact]
    public void UpdateFromAndDeleteUsing_WhenReturningIsConfigured_ShouldRenderFullStatements()
    {
        // Arrange
        var postgres = PostgreSqlSqlProvider.Instance;
        var services = new SqlBuilderServices();
        var update = new SqlUpdateBuilder(postgres, services)
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id")
            .Returning("Id");
        var delete = new SqlDeleteBuilder(postgres, services)
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id")
            .Returning("Id");
        var sqlServerUpdate = new SqlUpdateBuilder(SqlServerSqlProvider.Instance, services)
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .AllowAllRows()
            .Returning("Name");

        // Act
        var updateCommand = update.BuildCommand();
        var deleteCommand = delete.BuildCommand();
        var sqlServerCommand = sqlServerUpdate.BuildCommand();

        // Assert
        Assert.Equal("Update \"samples\" As \"t\" Set \"Name\" = \"s\".\"Name\" From \"sample_updates\" As \"s\" Where \"t\".\"Id\"=\"s\".\"Id\" Returning \"t\".\"Id\"", updateCommand.Sql);
        Assert.Equal("Delete From \"samples\" As \"t\" Using \"sample_deletes\" As \"s\" Where \"t\".\"Id\"=\"s\".\"Id\" Returning \"t\".\"Id\"", deleteCommand.Sql);
        Assert.Equal("Update [samples] Set [Name] = @_p_0 Output [Inserted].[Name]", sqlServerCommand.Sql);
        Assert.True(updateCommand.HasReturning);
        Assert.True(deleteCommand.HasReturning);
        Assert.True(sqlServerCommand.HasReturning);
    }

    /// <summary>
    /// 测试目的：不支持 Returning 的 Provider 应在 SQL 验证时返回明确能力错误。
    /// </summary>
    [Fact]
    public void Returning_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new SqlInsertBuilder(MySqlSqlProvider.Instance, new SqlBuilderServices())
            .InsertInto("samples")
            .Columns("Name")
            .Values("Bing")
            .Returning("Name");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Contains("不支持 Returning", exception.Message);
    }

    /// <summary>
    /// 测试使用指定 Provider 创建能够参与 Insert Select 参数快照的查询 Builder。
    /// </summary>
    private sealed class ProviderSqlBuilder : SqlBuilderBase
    {
        /// <summary>
        /// 初始化一个 <see cref="ProviderSqlBuilder"/> 类型的实例。
        /// </summary>
        /// <param name="provider">SQL Provider。</param>
        /// <param name="services">共享 Builder 服务。</param>
        /// <param name="parameterManager">参数管理器。</param>
        public ProviderSqlBuilder(ISqlProvider provider, SqlBuilderServices services,
            IParameterManager parameterManager = null)
            : base(provider, services, parameterManager)
        {
        }

        /// <inheritdoc />
        protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
            new ProviderSqlBuilder(Provider, Services, parameterManager);
    }
}
