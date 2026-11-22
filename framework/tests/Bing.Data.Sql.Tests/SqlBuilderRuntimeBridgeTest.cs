using Bing.Data.Sql.Builders;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>查询运行边界拒绝来自第三方 Builder 的写入操作。</summary>
public sealed class SqlBuilderRuntimeBridgeTest
{
    /// <summary>冻结计划仍必须保留可诊断的 SQL 和参数快照。</summary>
    [Fact]
    public void FrozenPlan_ShouldKeepDebugSqlAndSensitiveParameterRedaction()
    {
        var builder = new SqlServerBuilder();
        builder.Select("Id").From("Users").AppendWhere("[Name]=@name And [ApiToken]=@ApiToken");
        builder.AddParam("name", "frozen-name");
        builder.AddParam("ApiToken", "secret-value");
        var plan = SqlQueryPlan.Create(builder).Freeze();

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan, true);

        Assert.False(plan.IsBuilderPlan);
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Name]=@name And [ApiToken]=@ApiToken", snapshot.Sql);
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Name]='frozen-name' And [ApiToken]='<redacted>'", snapshot.DebugSql);
        Assert.Equal("secret-value", snapshot.Parameters.Single(parameter => parameter.Name == "@ApiToken").Value);
    }

    /// <summary>快照存在性不依赖 Builder 存活，原生文本仍保留原始绑定路径。</summary>
    [Fact]
    public void HasExecutionSnapshot_ShouldDistinguishFrozenBuilderAndRawPlans()
    {
        var plan = SqlQueryPlan.Create(new SqlServerBuilder().Select("Id").From("Users"));
        Assert.False(SqlBuilderRuntimeBridge.HasExecutionSnapshot(plan));
        Assert.True(SqlBuilderRuntimeBridge.HasExecutionSnapshot(plan.Freeze()));
        Assert.False(SqlBuilderRuntimeBridge.HasExecutionSnapshot(SqlQueryPlan.Create("select 1", null)));
    }

    [Theory]
    [InlineData(SqlOperationKind.InsertValues)]
    [InlineData(SqlOperationKind.InsertSelect)]
    [InlineData(SqlOperationKind.Update)]
    [InlineData(SqlOperationKind.Delete)]
    public void ValidateQueryPlan_ShouldRejectThirdPartyWriteBuilder(SqlOperationKind operation)
    {
        var builder = new Mock<ISqlBuilder>();
        builder.SetupGet(item => item.OperationKind).Returns(operation);
        builder.Setup(item => item.Clone()).Returns(builder.Object);
        var plan = SqlQueryPlan.Create(builder.Object);
        Assert.Throws<InvalidOperationException>(() => SqlBuilderRuntimeBridge.ValidateQueryPlan(plan));
    }

    [Fact]
    public void ValidateQueryPlan_ShouldAllowQueryBuilderAndRawSql()
    {
        Assert.False(SqlBuilderRuntimeBridge.ValidateQueryPlan(SqlQueryPlan.Create(new SqlServerBuilder().Select("Id").From("rows"))));
        Assert.False(SqlBuilderRuntimeBridge.ValidateQueryPlan(SqlQueryPlan.Create("select 1", null)));
    }
}
