using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 查询计划上下文和执行快照测试。
/// </summary>
public class SqlQueryPlanContextTest
{
    /// <summary>
    /// 测试目的：计划开始执行时应生成执行标识，并保留查询上下文和阶段信息。
    /// </summary>
    [Fact]
    public void NotifyExecutionStarted_ShouldCreateExecutionContext()
    {
        // Arrange
        var plan = SqlQueryPlan.Create(new TestSqlBuilder().Select("Id").From("Users"));
        plan.SetContext("query-1", "parent-1", "Data");

        // Act
        plan.NotifyExecutionStarted();

        // Assert
        Assert.Equal("query-1", plan.QueryContextId);
        Assert.Equal("parent-1", plan.ParentQueryContextId);
        Assert.Equal("Data", plan.Phase);
        Assert.False(string.IsNullOrWhiteSpace(plan.ExecutionId));
    }

    /// <summary>
    /// 测试目的：冻结查询片段应直接创建无 Builder 的统一文本计划，并隔离参数副本。
    /// </summary>
    [Fact]
    public void FragmentPlan_ShouldFreezeSqlAndParametersWithoutBuilder()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var fragment = new SqlQueryFragment(
            "Select * From Users Where Id = @id And Id In (@ids)",
            new[]
            {
                new SqlParam("@id", 7),
                new SqlParam("@ids", values)
            },
            SqlQueryFragmentKind.Select);

        // Act
        var plan = SqlQueryPlan.Create(fragment, "Id");
        var defaultPlan = SqlQueryPlan.Create(fragment);
        values[0] = 99;
        var exposed = Assert.IsType<SqlParam[]>(plan.Parameters);
        exposed[0].Value = 88;
        ((int[])exposed[1].Value)[0] = 88;

        // Assert
        Assert.Equal("Select * From Users Where Id = @id And Id In (@ids)", plan.CommandText);
        Assert.Equal(System.Data.CommandType.Text, plan.CommandType);
        Assert.Equal("Id", plan.SplitOn);
        Assert.Equal("Id", defaultPlan.SplitOn);
        Assert.False(plan.IsBuilderPlan);
        Assert.Null(plan.QueryIr);
        var frozen = Assert.IsType<SqlParam[]>(plan.Parameters);
        Assert.Equal(7, frozen[0].Value);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(frozen[1].Value));
    }

    /// <summary>
    /// 测试目的：冻结片段计划请求执行快照时，应直接使用文本计划的 SQL 和参数，不再寻找 Builder。
    /// </summary>
    [Fact]
    public void FragmentPlan_WhenExecutionSnapshotRequested_ShouldUseFrozenText()
    {
        // Arrange
        var fragment = new SqlQueryFragment(
            "Select * From Users Where Id = @id",
            new[] { new SqlParam("@id", 7) },
            SqlQueryFragmentKind.Select);
        var plan = SqlQueryPlan.Create(fragment);

        // Act
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan, includeDebugSql: true);

        // Assert
        Assert.Equal("Select * From Users Where Id = @id", snapshot.Sql);
        Assert.Equal("Select * From Users Where Id = 7", snapshot.DebugSql);
        Assert.Equal(7, Assert.Single(snapshot.Parameters).Value);
    }

    /// <summary>
    /// 测试目的：Builder 计划显式冻结后应转为无 Builder 的文本计划，并保留完整 SQL 与参数。
    /// </summary>
    [Fact]
    public void BuilderPlan_Freeze_ShouldCreateBuilderlessTextPlan()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var builder = new TestSqlBuilder()
            .Select("Id")
            .From("Users")
            .Where("Id", 7)
            .In("TenantId", values.Cast<object>());
        var plan = SqlQueryPlan.Create(builder, "Id");

        // Act
        var frozen = plan.Freeze();
        builder.Where("Status", "active");
        values[0] = 99;

        // Assert
        Assert.False(frozen.IsBuilderPlan);
        Assert.NotNull(frozen.QueryIr);
        Assert.Null(frozen.PlanningBuilder);
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@_p_0 And [TenantId] In (@_p_1,@_p_2)", frozen.CommandText);
        Assert.Equal("Id", frozen.SplitOn);
        var parameters = Assert.IsType<SqlParam[]>(frozen.Parameters);
        Assert.Collection(parameters,
            first =>
            {
                Assert.Equal("@_p_0", first.Name);
                Assert.Equal(7, first.Value);
            },
            second =>
            {
                Assert.Equal("@_p_1", second.Name);
                Assert.Equal(1, second.Value);
            },
            third =>
            {
                Assert.Equal("@_p_2", third.Name);
                Assert.Equal(2, third.Value);
            });
    }

    /// <summary>
    /// 测试目的：CTE 冻结后使用与 SQL 配套的边界完成派生，不再持有规划 Builder。
    /// </summary>
    [Fact]
    public void CteBuilderPlan_Freeze_ShouldUseFrozenStructure()
    {
        var cte = new SqlQueryFragment("Select Id From Users", Array.Empty<SqlParam>(), SqlQueryFragmentKind.Cte);
        var builder = new TestSqlBuilder()
            .Select("Id")
            .From("recent_users")
            .With("recent_users", cte);

        var frozen = SqlQueryPlan.Create(builder, "Id").Freeze();

        Assert.False(frozen.IsBuilderPlan);
        Assert.NotNull(frozen.QueryIr);
        Assert.Null(frozen.PlanningBuilder);
        Assert.Equal(
            "With [recent_users] \r\nAs (Select Id From Users)\r\nSelect [Id] \r\nFrom [recent_users]",
            frozen.CommandText);
    }

    /// <summary>
    /// 测试目的：冻结 Builder 计划时应保留查询上下文、生命周期和生成式参数绑定元数据。
    /// </summary>
    [Fact]
    public void BuilderPlan_Freeze_ShouldPreserveExecutionMetadata()
    {
        // Arrange
        var builder = new TestSqlBuilder()
            .Select("Id")
            .From("Users");
        var plan = SqlQueryPlan.Create(builder);
        plan.SetContext("query-1", "parent-1", "Data");
        var started = 0;
        var completed = 0;
        var parametersBound = 0;
        var outputCompleted = 0;
        plan.SetLifecycleCallbacks(() => started++, () => completed++);
        plan.SetOutputParametersReceiver(_ => parametersBound++, () => outputCompleted++);
        Action<System.Data.Common.DbCommand, IEnumerable<SqlParam>> binder = (_, _) => { };
        plan.IsGeneratedExecution = true;
        plan.GeneratedParameterBinder = binder;

        // Act
        var frozen = plan.Freeze();
        frozen.NotifyExecutionStarted();
        frozen.NotifyParametersBound(null);
        frozen.NotifyExecutionCompleted();
        frozen.NotifyExecutionFinished();

        // Assert
        Assert.Equal("query-1", frozen.QueryContextId);
        Assert.Equal("parent-1", frozen.ParentQueryContextId);
        Assert.Equal("Data", frozen.Phase);
        Assert.True(frozen.IsGeneratedExecution);
        Assert.Same(binder, frozen.GeneratedParameterBinder);
        Assert.Equal(1, started);
        Assert.Equal(1, completed);
        Assert.Equal(1, parametersBound);
        Assert.Equal(1, outputCompleted);
    }

    /// <summary>
    /// 测试目的：已解析的参数映射应直接进入文本计划快照，并保留参数元数据。
    /// </summary>
    [Fact]
    public void TextPlan_WithResolvedParameterMap_ShouldUseMapItems()
    {
        // Arrange
        var map = new SqlParameterMap<SnapshotParameter>()
            .Add("id", item => item.Id, 7);
        var plan = SqlQueryPlan.Create("Select * From Users Where Id = @id", map);

        // Act
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan, includeDebugSql: true);

        // Assert
        Assert.Equal("Select * From Users Where Id = @id", snapshot.Sql);
        Assert.Equal("Select * From Users Where Id = 7", snapshot.DebugSql);
        var parameter = Assert.Single(snapshot.Parameters);
        Assert.Equal("@id", parameter.Name);
        Assert.Equal(7, parameter.Value);
        Assert.Equal(typeof(SnapshotParameter), parameter.EntityType);
        Assert.Equal(nameof(SnapshotParameter.Id), parameter.PropertyName);
        Assert.Equal(SqlParameterSource.RawSql, parameter.Source);
    }

    /// <summary>
    /// 测试目的：参数映射名称与 SQL 占位符大小写不一致时，仍应生成可执行参数快照。
    /// </summary>
    [Fact]
    public void TextPlan_WithCaseInsensitiveParameterToken_ShouldUsePrefixedMapName()
    {
        // Arrange
        var map = new SqlParameterMap<SnapshotParameter>()
            .Add("id", item => item.Id, 7);
        var plan = SqlQueryPlan.Create("Select * From Users Where Id = @ID", map);

        // Act
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan, includeDebugSql: true);

        // Assert
        Assert.Equal("Select * From Users Where Id = @ID", snapshot.Sql);
        Assert.Equal("Select * From Users Where Id = 7", snapshot.DebugSql);
        Assert.Equal("@id", Assert.Single(snapshot.Parameters).Name);
    }

    /// <summary>
    /// 测试目的：未解析的参数映射不能在无 Builder 的计划快照中静默变成 null。
    /// </summary>
    [Fact]
    public void TextPlan_WithUnresolvedParameterMap_ShouldRejectSnapshot()
    {
        // Arrange
        var map = new SqlParameterMap<SnapshotParameter>()
            .Map("id", item => item.Id);
        var plan = SqlQueryPlan.Create("Select * From Users Where Id = @id", map);

        // Act / Assert
        var exception = Assert.Throws<NotSupportedException>(
            () => SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan));
        Assert.Contains("id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 测试目的：派生计划共享来源查询上下文，但使用独立阶段和冻结 SQL。
    /// </summary>
    [Fact]
    public void DerivedPlan_ShouldCopyContextWithoutSharingBuilder()
    {
        // Arrange
        var sourceBuilder = new TestSqlBuilder().Select("Id").From("Users");
        var source = SqlQueryPlan.Create(sourceBuilder);
        source.SetContext("query-1", null, "Data");
        var derivedBuilder = sourceBuilder.Clone().Where("Id", 1);
        var derived = SqlQueryPlan.Create(derivedBuilder);

        // Act
        derived.CopyContextFrom(source, "Count");
        derivedBuilder.Where("Status", "changed");

        // Assert
        Assert.Equal(source.QueryContextId, derived.QueryContextId);
        Assert.Equal("Count", derived.Phase);
        Assert.NotSame(source.QueryIr, derived.QueryIr);
        Assert.Equal("Select [Id] \r\nFrom [Users]", source.CommandText);
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@_p_0", derived.CommandText);
    }

    /// <summary>
    /// 测试目的：Count/Data 派生计划应共享查询上下文但分别生成执行标识和阶段，不能复用同一执行状态。
    /// </summary>
    [Fact]
    public void DerivedCountAndDataPlans_WhenExecuted_ShouldUseIndependentExecutionContexts()
    {
        // Arrange
        var source = SqlQueryPlan.Create(new TestSqlBuilder().Select("Id").From("Users"));
        source.SetContext("query-1", "parent-1", "Data");
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        var data = SqlBuilderRuntimeBridge.CreatePagePlan(source, new Pager(1, 2, "Id"));
        count.CopyContextFrom(source, "Count");
        data.CopyContextFrom(source, "Data");

        // Act
        count.NotifyExecutionStarted();
        data.NotifyExecutionStarted();

        // Assert
        Assert.Equal(source.QueryContextId, count.QueryContextId);
        Assert.Equal(source.QueryContextId, data.QueryContextId);
        Assert.Equal("Count", count.Phase);
        Assert.Equal("Data", data.Phase);
        Assert.NotEqual(count.ExecutionId, data.ExecutionId);
        Assert.NotSame(count.QueryIr, data.QueryIr);
    }

    /// <summary>
    /// 测试目的：执行快照应深复制数组参数，调用方后续修改原数组不得污染快照。
    /// </summary>
    [Fact]
    public void ExecutionSnapshot_WhenSourceArrayChanges_ShouldRemainIndependent()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var parameter = new SqlParam("@values", values);

        // Act
        var snapshot = new SqlBuilderExecutionSnapshot("Select 1", new[] { parameter });
        values[0] = 99;

        // Assert
        var snapshotValues = Assert.IsType<int[]>(Assert.Single(snapshot.Parameters).Value);
        Assert.Equal(new[] { 1, 2 }, snapshotValues);
        Assert.NotSame(values, snapshotValues);
    }

    /// <summary>
    /// 测试目的：调用方修改公开参数副本时，不得污染已经冻结的查询快照。
    /// </summary>
    [Fact]
    public void ExecutionSnapshot_WhenExposedParameterChanges_ShouldRemainFrozen()
    {
        // Arrange
        var values = new[] { 1, 2 };
        var snapshot = new SqlBuilderExecutionSnapshot("Select 1", new[]
        {
            new SqlParam("@value", values)
            {
                OriginalValue = "original",
                IsProviderValue = true,
                ColumnName = "value"
            }
        });

        // Act
        var exposed = Assert.Single(snapshot.Parameters);
        var exposedValues = Assert.IsType<int[]>(exposed.Value);
        exposedValues[0] = 99;
        exposed.Value = "changed";
        exposed.OriginalValue = "changed-original";
        exposed.IsProviderValue = false;
        exposed.ColumnName = "changed-column";

        // Assert
        var frozen = Assert.Single(snapshot.Parameters);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(frozen.Value));
        Assert.Equal("original", frozen.OriginalValue);
        Assert.True(frozen.IsProviderValue);
        Assert.Equal("value", frozen.ColumnName);
    }

    /// <summary>
    /// 测试目的：文本计划公开参数副本被修改时，不得污染计划内部的冻结参数源。
    /// </summary>
    [Fact]
    public void TextPlan_WhenExposedParameterDictionaryChanges_ShouldRemainFrozen()
    {
        // Arrange
        var plan = SqlQueryPlan.Create("Select * From Users Where Id = @id", new Dictionary<string, object>
        {
            ["id"] = 7,
            ["tags"] = new[] { 1, 2 }
        });

        // Act
        var exposed = Assert.IsType<Dictionary<string, object>>(plan.Parameters);
        exposed["id"] = 99;
        ((int[])exposed["tags"])[0] = 99;

        // Assert
        var frozen = Assert.IsType<Dictionary<string, object>>(plan.Parameters);
        Assert.Equal(7, frozen["id"]);
        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(frozen["tags"]));
    }

    /// <summary>
    /// 测试目的：非 object 值类型的泛型字典也应在计划创建时冻结。
    /// </summary>
    [Fact]
    public void TextPlan_WhenTypedParameterDictionaryChanges_ShouldRemainFrozen()
    {
        // Arrange
        var parameters = new Dictionary<string, int>
        {
            ["id"] = 7
        };
        var plan = SqlQueryPlan.Create("Select * From Users Where Id = @id", parameters);

        // Act
        parameters["id"] = 99;

        // Assert
        var frozen = Assert.IsAssignableFrom<IDictionary<string, object>>(plan.Parameters);
        Assert.Equal(7, frozen["id"]);
    }

    /// <summary>
    /// 测试目的：嵌套类型化字典应保持字典形状并在计划创建时冻结。
    /// </summary>
    [Fact]
    public void TextPlan_WhenNestedTypedParameterDictionaryChanges_ShouldRemainDictionaryAndFrozen()
    {
        // Arrange
        var metadata = new Dictionary<string, int>
        {
            ["tenant"] = 7
        };
        var plan = SqlQueryPlan.Create("Select * From Users Where Id = @id", new Dictionary<string, object>
        {
            ["id"] = 1,
            ["metadata"] = metadata
        });

        // Act
        metadata["tenant"] = 99;

        // Assert
        var frozen = Assert.IsType<Dictionary<string, object>>(plan.Parameters);
        var nested = Assert.IsAssignableFrom<IDictionary<string, object>>(frozen["metadata"]);
        Assert.Equal(7, nested["tenant"]);
    }
    /// <summary>
    /// 测试目的：计划创建后 Builder 发生变化时，执行仍应使用创建阶段冻结的完整 SQL 和参数。
    /// </summary>
    [Fact]
    public void ExecutionPlan_WhenBuilderChangesAfterCreation_ShouldKeepFrozenSql()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id").From("Users").Where("Id", 7);
        var frozen = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);
        var plan = SqlQueryPlan.Create(builder, "Id");
        builder.Clear();

        // Act
        var execution = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan);

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=@_p_0", execution.Sql);
        Assert.Equal(frozen.Parameters.Select(item => item.Name), execution.Parameters.Select(item => item.Name));
        Assert.Equal(frozen.Parameters.Select(item => item.Value), execution.Parameters.Select(item => item.Value));
    }

    /// <summary>
    /// 测试目的：计划创建后 Builder 发生变化时，延迟请求的调试 SQL 也应使用冻结参数和 SQL。
    /// </summary>
    [Fact]
    public void ExecutionPlan_WhenBuilderChangesBeforeDebugRendering_ShouldKeepFrozenDebugSql()
    {
        // Arrange
        var builder = new TestSqlBuilder().Select("Id").From("Users").Where("Id", 7);
        var plan = SqlQueryPlan.Create(builder, "Id");
        builder.Clear();

        // Act
        var execution = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(plan, includeDebugSql: true);

        // Assert
        Assert.Equal("Select [Id] \r\nFrom [Users] \r\nWhere [Id]=7", execution.DebugSql);
    }

    /// <summary>
    /// 测试目的：Count 派生完成后应只保留纯 IR，执行不应再次读取可变 Builder 状态。
    /// </summary>
    [Fact]
    public void CountPlan_WhenDerivedBuilderChangesAfterCreation_ShouldKeepFrozenSql()
    {
        // Arrange
        var sourceBuilder = new TestSqlBuilder().Select("Id").From("Users").Where("Id", 7);
        var source = SqlQueryPlan.Create(sourceBuilder, "Id");

        // Act
        var count = SqlBuilderRuntimeBridge.CreateCountPlan(source);
        var frozen = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(count);

        // Assert
        Assert.False(count.IsBuilderPlan);
        Assert.NotNull(count.QueryIr);
        Assert.Equal("Select Count(*) \r\nFrom [Users] \r\nWhere [Id]=@_p_0", count.CommandText);
        Assert.Equal(frozen.Sql, SqlBuilderRuntimeBridge.CreateExecutionSnapshot(count).Sql);
    }

    private sealed class SnapshotParameter
    {
        public int Id { get; set; }
    }
}
