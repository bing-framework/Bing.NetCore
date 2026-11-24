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
        Assert.True(SqlBuilderRuntimeBridge.HasExecutionSnapshot(plan));
        Assert.True(SqlBuilderRuntimeBridge.HasExecutionSnapshot(plan.Freeze()));
        Assert.False(SqlBuilderRuntimeBridge.HasExecutionSnapshot(SqlQueryPlan.Create("select 1", null)));
    }

    /// <summary>无参数文本使用空快照，仍能生成完整调试 SQL。</summary>
    [Fact]
    public void CreateExecutionSnapshot_WhenRawPlanHasNoParameters_ShouldUseEmptySnapshot()
    {
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(SqlQueryPlan.Create("Select 1", null), true);
        Assert.Equal("Select 1", snapshot.Sql);
        Assert.Equal("Select 1", snapshot.DebugSql);
        Assert.Empty(snapshot.Parameters);
    }

    /// <summary>任意对象交给执行器绑定，快照桥接不能恢复为反射或寻找 Builder。</summary>
    [Fact]
    public void CreateExecutionSnapshot_WhenRawParametersNeedBinding_ShouldRejectUnresolvedSource()
    {
        var plan = SqlQueryPlan.Create("Select @id", new { id = 7 });
        Assert.Throws<NotSupportedException>(() => SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan));
    }

    [Theory]
    [InlineData(SqlOperationKind.InsertValues)]
    [InlineData(SqlOperationKind.InsertSelect)]
    [InlineData(SqlOperationKind.Update)]
    [InlineData(SqlOperationKind.Delete)]
    public void CreatePlan_ShouldRejectThirdPartyWriteBuilder(SqlOperationKind operation)
    {
        var builder = new Mock<ISqlBuilder>();
        builder.SetupGet(item => item.OperationKind).Returns(operation);
        builder.Setup(item => item.Clone()).Returns(builder.Object);
        Assert.Throws<InvalidOperationException>(() => SqlQueryPlan.Create(builder.Object));
    }

    [Fact]
    public void CreatePlan_ShouldAllowQueryBuilderAndRawSql()
    {
        Assert.Equal("Select [Id] \r\nFrom [rows]", SqlQueryPlan.Create(new SqlServerBuilder().Select("Id").From("rows")).CommandText);
        Assert.Equal("select 1", SqlQueryPlan.Create("select 1", null).CommandText);
    }
}
