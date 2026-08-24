using Bing.Data.Sql.Tests.Samples;
using Bing.Data.Enums;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 独立 SQL 查询描述生命周期测试。
/// </summary>
public class SqlQueryLifecycleTest
{
    /// <summary>
    /// 测试目的：ToSql 只渲染查询快照，不应冻结描述，后续合法修改仍应生效。
    /// </summary>
    [Fact]
    public void ToSql_WhenQueryIsDraft_ShouldNotFreezeDescription()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var query = CreateQuery(executor);

        // Act
        var beforeWhere = query.ToSql();
        query.Where<Sample>(item => item.IntValue == 7);
        var afterWhere = query.ToSql();

        // Assert
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s]", beforeWhere);
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue]=@_p_0",
            afterWhere);
        Assert.NotEqual(beforeWhere, afterWhere);
    }

    /// <summary>
    /// 测试目的：同一查询形状重复渲染应命中实例缓存，结构成功变更后必须失效并重新渲染。
    /// </summary>
    [Fact]
    public void ToSql_WhenShapeIsUnchanged_ShouldReuseCachedSqlUntilMutation()
    {
        // Arrange
        var builder = new CountingTestSqlBuilder();
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });

        // Act
        var first = query.ToSql();
        var second = query.ToSql();
        query.Where<Sample>(item => item.IntValue == 8);
        var third = query.ToSql();

        // Assert
        Assert.Equal(first, second);
        Assert.NotEqual(second, third);
        Assert.Equal(2, builder.Counters.ToSqlCallCount);
    }

    /// <summary>
    /// 测试目的：动态软删除过滤状态变化后不得命中上一次环境的 SQL 缓存。
    /// </summary>
    [Fact]
    public void ToSql_WhenDataFilterStateChanges_ShouldRenderCurrentEnvironment()
    {
        // Arrange
        var dataFilter = new DataFilter();
        var builder = new TestSqlBuilder(new SqlBuilderServices(dataFilter: dataFilter), TestDialect.Instance);
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample5>("s")
            .Select<Sample5>(item => new object[] { item.StringValue });

        // Act
        string disabledSql;
        using (dataFilter.Disable<ISoftDelete>())
            disabledSql = query.ToSql();
        var enabledSql = query.ToSql();

        // Assert
        Assert.Equal("Select [s].[StringValue] \r\nFrom [Sample5] As [s]", disabledSql);
        Assert.Equal("Select [s].[StringValue] \r\nFrom [Sample5] As [s] \r\nWhere [s].[IsDeleted]=@_p_0",
            enabledSql);
    }

    /// <summary>
    /// 测试目的：参数形状变化必须重新渲染，IN 长度和 null 条件必须分别生成完整 SQL 与参数快照。
    /// </summary>
    [Fact]
    public void ToSql_WhenParameterShapeChanges_ShouldPreserveSqlAndParameterSnapshot()
    {
        // Arrange
        var inQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>())
            .Where<Sample, object>(item => item.StringValue, new object[] { "a", "b" }, Operator.In);
        var nullQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>())
            .Where<Sample, string>(item => item.StringValue, null);

        // Act
        var inSql = inQuery.ToSql();
        var nullSql = nullQuery.ToSql();
        var inParameters = ((ISqlCommonPartAccessor)inQuery.GetBuilder()).ParameterManager.GetParams();
        var nullParameters = ((ISqlCommonPartAccessor)nullQuery.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[StringValue] In (@_p_0,@_p_1)",
            inSql);
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[StringValue] Is Null",
            nullSql);
        Assert.Equal(new[] { "@_p_0", "@_p_1" }, inParameters.Keys);
        Assert.Equal(new object[] { "a", "b" }, inParameters.Values);
        Assert.Empty(nullParameters);
    }

    /// <summary>
    /// 测试目的：WhereIf(false) 不得改变查询形状，WhereIf(true) 只在成功提交后追加一次条件。
    /// </summary>
    [Fact]
    public void ToSql_WhenWhereIfChanges_ShouldTouchOnlySuccessfulMutation()
    {
        // Arrange
        var falseQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>());
        var trueQuery = CreateQuery(new Mock<ISqlQueryPlanExecutor>());

        // Act
        falseQuery.WhereIf<Sample>(item => item.IntValue == 7, false);
        trueQuery.WhereIf<Sample>(item => item.IntValue == 7, true);

        // Assert
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s]", falseQuery.ToSql());
        Assert.Equal("Select [s].[IntValue] \r\nFrom [Sample] As [s] \r\nWhere [s].[IntValue]=@_p_0",
            trueQuery.ToSql());
    }

    /// <summary>
    /// 测试目的：失败 Join 候选不得污染 SQL、参数或缓存状态，失败后合法查询仍可继续渲染。
    /// </summary>
    [Fact]
    public void ToSql_WhenJoinCandidateFails_ShouldKeepCachedShapeUnchanged()
    {
        // Arrange
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new CountingTestSqlBuilder())
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });
        var beforeSql = query.ToSql();
        var beforeParameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Act
        Assert.Throws<InvalidOperationException>(() => query.Join<Sample, Sample>(
            (left, right) => left.IntValue == right.IntValue, alias: "r", leftAlias: "missing"));
        var afterSql = query.ToSql();
        var afterParameters = ((ISqlCommonPartAccessor)query.GetBuilder()).ParameterManager.GetParams();

        // Assert
        Assert.Equal(beforeSql, afterSql);
        Assert.Equal(beforeParameters, afterParameters);
    }

    /// <summary>
    /// 测试目的：实例缓存只能保存 SQL 形状数据，不得持有 Builder、连接、事务、Scope 或失效的参数布局缓存。
    /// </summary>
    [Fact]
    public void QueryInstanceCache_ShouldNotHoldExecutionResourcesOrParameterLayout()
    {
        // Arrange
        var fields = typeof(SqlQuery).GetFields(System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        // Act
        var cachedFields = fields.Where(field => field.Name.Contains("cached", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Assert
        Assert.Contains(cachedFields, field => field.Name == "_cachedSql" && field.FieldType == typeof(string));
        Assert.DoesNotContain(fields, field => field.Name == "_cachedParameterLayout");
        Assert.DoesNotContain(cachedFields, field => field.FieldType != typeof(string) && field.FieldType != typeof(long));
        Assert.DoesNotContain(cachedFields, field => field.FieldType.Name.Contains("Builder", StringComparison.Ordinal));
        Assert.DoesNotContain(cachedFields, field => field.FieldType.Name.Contains("Connection", StringComparison.Ordinal));
        Assert.DoesNotContain(cachedFields, field => field.FieldType.Name.Contains("Transaction", StringComparison.Ordinal));
    }

    /// <summary>
    /// 测试目的：Raw 文本描述不得复用结构化查询实例的 SQL 缓存，且公开参数读取必须返回独立快照。
    /// </summary>
    [Fact]
    public void RawQueryInstance_ShouldNotShareStructuredSqlCache()
    {
        // Arrange
        var raw = new SqlTextQuery(new Mock<ISqlQueryPlanExecutor>().Object, "Select @value",
            new Dictionary<string, object> { ["value"] = "first" });

        // Act
        var firstParameters = Assert.IsType<Dictionary<string, object>>(raw.Parameters);
        firstParameters["value"] = "changed";
        var secondParameters = Assert.IsType<Dictionary<string, object>>(raw.Parameters);

        // Assert
        Assert.Null(typeof(SqlTextQuery).GetField("_cachedSql",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        Assert.NotSame(firstParameters, secondParameters);
        Assert.Equal("first", secondParameters["value"]);
    }

    /// <summary>
    /// 测试目的：不同 Provider 方言、映射配置和租户过滤环境创建的查询实例不得互用 SQL 或参数缓存。
    /// </summary>
    [Fact]
    public void QueryInstanceCache_WhenEnvironmentDiffers_ShouldKeepSqlAndParametersIsolated()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var metadata = new SqlMetadataOptions();
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "default",
            MappingProfile = "read",
            TableName = "users_default",
            Columns =
            {
                [nameof(Sample.StringValue)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(Sample.StringValue),
                    ColumnName = "status_default"
                }
            }
        });
        metadata.EntityMappings.Add(new EntityMappingOptions
        {
            EntityType = typeof(Sample),
            DbKey = "reporting",
            MappingProfile = "write",
            TableName = "users_reporting",
            Columns =
            {
                [nameof(Sample.StringValue)] = new ColumnMappingOptions
                {
                    PropertyName = nameof(Sample.StringValue),
                    ColumnName = "status_reporting"
                }
            }
        });

        var defaultQuery = CreateEnvironmentQuery(executor.Object, metadata, TestDialect.Instance, "default",
            "read", "tenant-a");
        var reportingQuery = CreateEnvironmentQuery(executor.Object, metadata, TestDialect.Instance, "reporting",
            "write", "tenant-b");
        var alternateDialectQuery = CreateEnvironmentQuery(executor.Object, metadata, TestDialect2.Instance, "default",
            "read", "tenant-a");

        // Act
        var defaultSql = defaultQuery.ToSql();
        var reportingSql = reportingQuery.ToSql();
        var alternateDialectSql = alternateDialectQuery.ToSql();

        // Assert
        Assert.Equal("Select [s].[status_default] \r\nFrom [as_Sample].[users_default] As [s] \r\nWhere [s].[status_default]=@_p_0",
            defaultSql);
        Assert.Equal("Select [s].[status_reporting] \r\nFrom [as_Sample].[users_reporting] As [s] \r\nWhere [s].[status_reporting]=@_p_0",
            reportingSql);
        Assert.Equal("Select $$s&&&.$$status_default&&& \r\nFrom $as_Sample&.$users_default& As $s& \r\nWhere $$s&&&.$$status_default&&&=*_p_0",
            alternateDialectSql);
        Assert.NotEqual(defaultSql, reportingSql);
        Assert.NotEqual(defaultSql, alternateDialectSql);
        Assert.Equal("tenant-a", GetParameterValue(defaultQuery));
        Assert.Equal("tenant-b", GetParameterValue(reportingQuery));
        Assert.Equal("tenant-a", GetParameterValue(alternateDialectQuery));
    }

    /// <summary>
    /// 测试目的：Frozen/Completed 查询克隆后应得到独立 Draft，并建立来源父上下文关系。
    /// </summary>
    [Fact]
    public void Clone_WhenSourceIsCompleted_ShouldCreateIndependentDraftWithParentContext()
    {
        // Arrange
        var plans = new List<SqlQueryPlan>();
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) =>
            {
                plans.Add(plan);
                return Execute(plan, () => new List<Sample>());
            });
        var source = CreateQuery(executor);

        // Act
        source.ToList<Sample>();
        var clone = source.Clone();
        clone.Where<Sample>(item => item.IntValue == 7);
        clone.ToList<Sample>();

        // Assert
        Assert.Equal(2, plans.Count);
        Assert.NotEqual(plans[0].QueryContextId, plans[1].QueryContextId);
        Assert.Equal(plans[0].QueryContextId, plans[1].ParentQueryContextId);
        Assert.Throws<InvalidOperationException>(() => source.Where<Sample>(item => item.IntValue == 8));
        Assert.Contains("@_p_0", clone.ToSql());
    }

    /// <summary>
    /// 测试目的：首次终结执行应冻结查询描述，之后继续修改必须立即拒绝。
    /// </summary>
    [Fact]
    public void Terminal_WhenQueryIsExecuted_ShouldFreezeDescription()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () => new List<Sample>()));
        var query = CreateQuery(executor);

        // Act
        query.ToList<Sample>();
        var exception = Assert.Throws<InvalidOperationException>(() => query.Where<Sample>(item => item.IntValue == 7));

        // Assert
        Assert.Equal("查询已冻结，不能继续修改查询描述。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Frozen/Completed 查询应允许重复执行，每次执行都必须获得独立执行回调。
    /// </summary>
    [Fact]
    public void Terminal_WhenQueryIsExecutedRepeatedly_ShouldReuseFrozenDescription()
    {
        // Arrange
        var executionCount = 0;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () =>
            {
                executionCount++;
                return new List<Sample>();
            }));
        var query = CreateQuery(executor);

        // Act
        var first = query.ToList<Sample>();
        var second = query.ToList<Sample>();

        // Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(2, executionCount);
    }

    /// <summary>
    /// 测试目的：同一查询执行期间重入另一个终结入口必须拒绝，并在外层执行结束后恢复可用。
    /// </summary>
    [Fact]
    public void Terminal_WhenExecutionIsActive_ShouldRejectReentrantExecutionAndReleaseLease()
    {
        // Arrange
        var reentrantException = default(InvalidOperationException);
        var reentered = false;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        var query = CreateQuery(executor);
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () =>
            {
                if (!reentered)
                {
                    reentered = true;
                    reentrantException = Assert.Throws<InvalidOperationException>(() => query.ToList<Sample>());
                }

                return new List<Sample>();
            }));

        // Act
        query.ToList<Sample>();
        query.ToList<Sample>();

        // Assert
        Assert.NotNull(reentrantException);
        Assert.Equal("当前查询正在执行，不能并发执行同一查询描述。", reentrantException.Message);
        Assert.True(reentered);
    }

    /// <summary>
    /// 测试目的：执行失败时仍应完成查询生命周期并释放租约，避免后续执行永久被拒绝。
    /// </summary>
    [Fact]
    public void Terminal_WhenExecutionFails_ShouldReleaseLease()
    {
        // Arrange
        var shouldFail = true;
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () =>
            {
                if (shouldFail)
                {
                    shouldFail = false;
                    throw new InvalidOperationException("受控查询执行异常。");
                }

                return new List<Sample>();
            }));
        var query = CreateQuery(executor);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => query.ToList<Sample>());
        var result = query.ToList<Sample>();

        // Assert
        Assert.Equal("受控查询执行异常。", exception.Message);
        Assert.Empty(result);
    }

    /// <summary>
    /// 测试目的：流式枚举未结束时应持有执行租约，枚举 Dispose 后必须释放租约并允许再次执行。
    /// </summary>
    [Fact]
    public void Streaming_WhenEnumeratorDisposed_ShouldReleaseLease()
    {
        // Arrange
        var executor = new Mock<ISqlQueryPlanExecutor>();
        executor.Setup(item => item.AsEnumerable<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Stream(plan));
        executor.Setup(item => item.ToList<Sample>(It.IsAny<SqlQueryPlan>(), It.IsAny<int?>()))
            .Returns((SqlQueryPlan plan, int? _) => Execute(plan, () => new List<Sample>()));
        var query = CreateQuery(executor);

        // Act
        using var enumerator = query.AsEnumerable<Sample>().GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var activeException = Assert.Throws<InvalidOperationException>(() => query.ToList<Sample>());
        enumerator.Dispose();
        var result = query.ToList<Sample>();

        // Assert
        Assert.Equal("当前查询正在执行，不能并发执行同一查询描述。", activeException.Message);
        Assert.Empty(result);
    }

    /// <summary>
    /// 创建带有确定投影的非泛型 Lambda 查询描述。
    /// </summary>
    /// <param name="executor">受控计划执行器。</param>
    /// <returns>待测试查询描述。</returns>
    private static SqlLambdaQuery CreateQuery(Mock<ISqlQueryPlanExecutor> executor) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(executor.Object, new TestSqlBuilder())
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.IntValue });

    private static SqlLambdaQuery CreateEnvironmentQuery(ISqlQueryPlanExecutor executor,
        SqlMetadataOptions metadata, IDialect dialect, string dbKey, string mappingProfile, string tenantId)
    {
        var options = new SqlOptions().SetDatabaseContext(new DatabaseContext
        {
            DbKey = dbKey,
            MappingProfile = mappingProfile,
            TenantId = tenantId,
            DataSource = new SqlDataSourceDescriptor
            {
                Key = dbKey,
                DatabaseType = DatabaseType.SqlServer,
                ConnectionString = $"Server={dbKey};"
            }
        });
        var services = new SqlBuilderServices(metadataOptions: metadata, options: options,
            entityModelMetadataProvider: new TestEntityMetadata(), filters: new ISqlFilter[] { new TenantFilter() });
        var builder = new TestSqlBuilder(services, dialect);
        return SqlQueryRuntimeFactory.CreateLambdaQuery(executor, builder)
            .From<Sample>("s")
            .Select<Sample>(item => new object[] { item.StringValue });
    }

    private static object GetParameterValue(SqlLambdaQuery query)
    {
        var snapshot = ((SqlBuilderBase)query.GetBuilder()).CreateExecutionBuilderSnapshot();
        _ = snapshot.ToSql();
        return ((ISqlCommonPartAccessor)snapshot).ParameterManager.GetParams().Values.Single();
    }

    /// <summary>
    /// 按真实执行器的生命周期回调顺序执行受控操作。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="plan">待执行计划。</param>
    /// <param name="operation">受控数据库操作。</param>
    /// <returns>受控操作结果。</returns>
    private static TResult Execute<TResult>(SqlQueryPlan plan, Func<TResult> operation)
    {
        var started = false;
        try
        {
            plan.NotifyExecutionStarted();
            started = true;
            return operation();
        }
        finally
        {
            if (started)
                plan.NotifyExecutionFinished();
        }
    }

    /// <summary>
    /// 创建持有租约直到枚举完成的同步结果流。
    /// </summary>
    /// <param name="plan">待执行计划。</param>
    /// <returns>受控结果流。</returns>
    private static IEnumerable<Sample> Stream(SqlQueryPlan plan)
    {
        var started = false;
        try
        {
            plan.NotifyExecutionStarted();
            started = true;
            yield return new Sample { IntValue = 1 };
        }
        finally
        {
            if (started)
                plan.NotifyExecutionFinished();
        }
    }

    /// <summary>
    /// 统计 SQL 渲染次数的测试 Builder。
    /// </summary>
    private sealed class CountingTestSqlBuilder : TestSqlBuilder
    {
        public CountingTestSqlBuilder() : this(new RenderCounters())
        {
        }

        private CountingTestSqlBuilder(RenderCounters counters) => Counters = counters;

        public RenderCounters Counters { get; }

        public override string ToSql()
        {
            Counters.ToSqlCallCount++;
            return base.ToSql();
        }

        public override ISqlBuilder Clone()
        {
            var builder = new CountingTestSqlBuilder(Counters);
            builder.Clone(this);
            return builder;
        }

        public sealed class RenderCounters
        {
            public int ToSqlCallCount { get; set; }
        }
    }

    private sealed class TenantFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context)
        {
            foreach (var source in context.Sources.Where(item => item.EntityType == typeof(Sample)))
                context.AddPredicate(source, context.GetColumn(source, nameof(Sample.StringValue)),
                    context.DatabaseContext?.TenantId);
        }
    }
}