using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Enums;
using Bing.Data.Sql.Metadata;
using Moq;

namespace Bing.Data.Sql.Tests;

/// <summary>完整生成链的有界结构复用与请求隔离合同。</summary>
public sealed class SqlGeneratedQueryTemplateTest
{
    private static readonly Type[] Types = { typeof(Row) };
    private static readonly string[] Names = { "r" };
    private static readonly SqlGeneratedColumn Id = new(0, "Id", null, typeof(int));
    private static readonly SqlGeneratedColumn Name = new(0, "Name", null, typeof(string));
    private static readonly SqlGeneratedColumn[] Columns = { Id, Name };
    private static readonly SqlGeneratedColumn[] AggregateColumns =
        { new(0, "Id", "Total", typeof(long), SqlAggregateFunction.Count) };

    private static SqlGeneratedQueryTemplate Template(Action replayed = null) => new(new[]
    {
        new SqlGeneratedQueryTemplate.Operation((q,a) => { replayed?.Invoke(); SqlGeneratedQueryBridge.Compare(q, false,
            Types, Names, null, Id, null, Operator.GreaterEqual, a[0]); }, a => a[0], _ => Operator.GreaterEqual, 0),
        new SqlGeneratedQueryTemplate.Operation((q,a) => { replayed?.Invoke(); SqlGeneratedQueryBridge.OptionalCompare(q, (bool)a[0], false,
            Types, Names, null, Id, null, (Operator)a[2], a[1]); }, a => a[1], a => (Operator)a[2], 1, 0),
        new SqlGeneratedQueryTemplate.Operation((q,a) => { replayed?.Invoke(); SqlGeneratedQueryBridge.Project(q, "Select", Types, Names, null, Columns); }),
        new SqlGeneratedQueryTemplate.Operation((q,a) => { replayed?.Invoke(); SqlGeneratedQueryBridge.Project(q, "OrderBy", Types, Names, null, new[] { Id }, (bool)a[0]); })
    });

    private static SqlLambdaQuery Query(SqlGeneratedQueryTemplate template, SqlBuilderServices services,
        int minimum, bool enabled, object value, Operator comparison = Operator.In, string alias = "r", bool descending = false,
        ISqlBuilder builder = null, ISqlQueryPlanExecutor executor = null)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(executor ?? new Mock<ISqlQueryPlanExecutor>().Object,
            builder ?? new SqliteBuilder(services)).From<Row>(alias);
        SqlGeneratedQueryTemplate.Apply(query, template, 0, new object[] { minimum });
        SqlGeneratedQueryTemplate.Apply(query, template, 1, new object[] { enabled, value, comparison });
        SqlGeneratedQueryTemplate.Apply(query, template, 2, Array.Empty<object>());
        SqlGeneratedQueryTemplate.Apply(query, template, 3, new object[] { descending });
        return query;
    }

    private static string Sql(string predicate, string alias = "r", bool descending = false) =>
        $"Select `{alias}`.`Id`,`{alias}`.`Name` \r\nFrom `AutoRows` As `{alias}` \r\nWhere `{alias}`.`Id`>=@_p_0" + predicate +
        $" \r\nOrder By `{alias}`.`Id`" + (descending ? " Desc" : "");

    private static SqlGeneratedQueryTemplate AggregateTemplate(Action replayed = null) => new(new[]
    {
        new SqlGeneratedQueryTemplate.Operation((q, a) => { replayed?.Invoke(); SqlGeneratedQueryBridge.OptionalCompare(q, (bool)a[0], false,
            Types, Names, null, Id, null, Operator.GreaterEqual, a[1]); }, a => a[1], _ => Operator.GreaterEqual, 1, 0),
        new SqlGeneratedQueryTemplate.Operation((q, a) => { replayed?.Invoke(); SqlGeneratedQueryBridge.Project(q, "Select", Types, Names, null, AggregateColumns); })
    });

    private static SqlLambdaQuery AggregateQuery(SqlGeneratedQueryTemplate template, SqlBuilderServices services, int minimum)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
            new SqliteBuilder(services)).From<Row>("r");
        SqlGeneratedQueryTemplate.Apply(query, template, 0, new object[] { true, minimum });
        SqlGeneratedQueryTemplate.Apply(query, template, 1, Array.Empty<object>());
        return query;
    }

    private static EntityModelMetadata SummaryMetadata(bool writable = true, bool ignored = false, Type propertyType = null) =>
        new(typeof(Summary), null, null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Summary.Total), propertyType ?? typeof(long), true, writable, isIgnored: ignored)
        });

    private static IEntityModelMetadataProvider SummaryProvider(Func<EntityModelMetadata> metadata,
        Func<EntityModelMetadata> sourceMetadata = null)
    {
        var row = SqlBuilderServices.CreateDefault().EntityModelMetadataProvider.GetMetadata(typeof(Row));
        var provider = new Mock<IEntityModelMetadataProvider>();
        provider.Setup(value => value.GetMetadata(typeof(Row))).Returns(() => sourceMetadata == null ? row : sourceMetadata());
        provider.Setup(value => value.GetMetadata(typeof(Summary))).Returns(metadata);
        return provider.Object;
    }

    /// <summary>共享查询结构不能绕过本次结果模型的聚合列校验。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("ignored")]
    [InlineData("readonly")]
    [InlineData("type")]
    public void AggregateShape_WhenResultMetadataDiffers_ShouldValidateCurrentProvider(string scenario)
    {
        var replayed = 0;
        var template = AggregateTemplate(() => replayed++);
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        var valid = new SqlBuilderServices(entityMappingResolver: resolver,
            entityModelMetadataProvider: SummaryProvider(() => SummaryMetadata()));
        var first = AggregateQuery(template, valid, 1);
        Assert.True(first.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out var plan, out _));
        Assert.Equal("Select Count(`r`.`Id`) As `Total` \r\nFrom `AutoRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", plan.CommandText);

        EntityModelMetadata Invalid() => scenario switch
        {
            "missing" => null,
            "empty" => new EntityModelMetadata(typeof(Summary), null, null, Array.Empty<EntityPropertyMetadata>()),
            "ignored" => SummaryMetadata(ignored: true),
            "readonly" => SummaryMetadata(writable: false),
            _ => SummaryMetadata(propertyType: typeof(string))
        };
        var invalid = new SqlBuilderServices(entityMappingResolver: resolver,
            entityModelMetadataProvider: SummaryProvider(Invalid));
        var second = AggregateQuery(template, invalid, 2);
        if (scenario == "missing")
            Assert.Throws<NotSupportedException>(() => second.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));
        else
            Assert.Throws<InvalidOperationException>(() => second.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));

        var third = AggregateQuery(template, valid, 3);
        Assert.True(third.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out var reused, out _));
        Assert.Same(plan.CommandText, reused.CommandText);
        Assert.True(third.GeneratedCore.TemplateState.TryPrepare(typeof(Summary), out var snapshot, out _));
        Assert.Equal(3, Assert.Single(snapshot.Parameters).Value);
        Assert.Equal(2, replayed);
    }

    /// <summary>同一查询再次使用缓存时，仍以当前模型配置校验聚合目标。</summary>
    [Fact]
    public void AggregateShape_WhenProviderChangesForSameQuery_ShouldValidateAgain()
    {
        var metadata = SummaryMetadata();
        var services = new SqlBuilderServices(entityModelMetadataProvider: SummaryProvider(() => metadata));
        var query = AggregateQuery(AggregateTemplate(), services, 1);
        Assert.True(query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));
        metadata = SummaryMetadata(writable: false);
        Assert.Throws<InvalidOperationException>(() => query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));
        metadata = SummaryMetadata();
        Assert.True(query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));
    }

    /// <summary>结果缓存和仅渲染 SQL 都要使用当前来源元数据校验聚合参数。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AggregateShape_WhenSourceMemberIsUnreadable_ShouldRejectWarmShape(bool renderOnly)
    {
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        var template = AggregateTemplate();
        var valid = new SqlBuilderServices(entityMappingResolver: resolver,
            entityModelMetadataProvider: SummaryProvider(() => SummaryMetadata()));
        var first = AggregateQuery(template, valid, 1);
        var resultType = renderOnly ? typeof(void) : typeof(Summary);
        Assert.True(first.GeneratedCore.TryGeneratedTemplatePlan(resultType, out _, out _));
        var unreadable = new EntityModelMetadata(typeof(Row), null, null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), false, true)
        });
        var invalid = new SqlBuilderServices(entityMappingResolver: resolver,
            entityModelMetadataProvider: SummaryProvider(() => SummaryMetadata(), () => unreadable));
        var second = AggregateQuery(template, invalid, 2);
        Assert.Throws<NotSupportedException>(() => second.GeneratedCore.TryGeneratedTemplatePlan(resultType, out _, out _));
    }

    /// <summary>即使后续替换投影，缓存也不能跳过之前的聚合参数校验。</summary>
    [Fact]
    public void AggregateShape_WhenProjectionWasReplaced_ShouldStillValidateSource()
    {
        var template = new SqlGeneratedQueryTemplate(new[]
        {
            new SqlGeneratedQueryTemplate.Operation((q, a) => SqlGeneratedQueryBridge.OptionalCompare(q, true, false,
                Types, Names, null, Id, null, Operator.GreaterEqual, a[0]), a => a[0], _ => Operator.GreaterEqual, 0),
            new SqlGeneratedQueryTemplate.Operation((q, a) => SqlGeneratedQueryBridge.Project(q, "Select", Types, Names, null, AggregateColumns)),
            new SqlGeneratedQueryTemplate.Operation((q, a) => SqlGeneratedQueryBridge.Project(q, "ReplaceSelect", Types, Names, null, new[] { Id }))
        });
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        SqlLambdaQuery QueryWith(IEntityModelMetadataProvider provider)
        {
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new SqliteBuilder(new SqlBuilderServices(entityMappingResolver: resolver, entityModelMetadataProvider: provider))).From<Row>("r");
            SqlGeneratedQueryTemplate.Apply(query, template, 0, new object[] { 1 });
            SqlGeneratedQueryTemplate.Apply(query, template, 1, Array.Empty<object>());
            SqlGeneratedQueryTemplate.Apply(query, template, 2, Array.Empty<object>());
            return query;
        }
        var valid = QueryWith(SummaryProvider(() => SummaryMetadata()));
        Assert.Equal("Select `r`.`Id` \r\nFrom `AutoRows` As `r` \r\nWhere `r`.`Id`>=@_p_0", valid.ToSql());
        var unreadable = new EntityModelMetadata(typeof(Row), null, null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), false, true)
        });
        var invalid = QueryWith(SummaryProvider(() => SummaryMetadata(), () => unreadable));
        Assert.Throws<NotSupportedException>(() => invalid.ToSql());
    }

    /// <summary>仅排序子句使用聚合源成员时，也要校验当前来源模型。</summary>
    [Fact]
    public void AggregateShape_WhenOrderUsesAggregate_ShouldValidateSource()
    {
        var template = new SqlGeneratedQueryTemplate(new[]
        {
            new SqlGeneratedQueryTemplate.Operation((q, a) => SqlGeneratedQueryBridge.OptionalCompare(q, true, false,
                Types, Names, null, Id, null, Operator.GreaterEqual, a[0]), a => a[0], _ => Operator.GreaterEqual, 0),
            new SqlGeneratedQueryTemplate.Operation((q, a) => SqlGeneratedQueryBridge.Project(q, "Select", Types, Names, null,
                new[] { new SqlGeneratedColumn(0, null, "Total", typeof(long), SqlAggregateFunction.Count) })),
            new SqlGeneratedQueryTemplate.Operation((q, a) => SqlGeneratedQueryBridge.Project(q, "OrderBy", Types, Names, null,
                new[] { new SqlGeneratedColumn(0, "Id", null, typeof(long), SqlAggregateFunction.Count) }))
        });
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        SqlLambdaQuery QueryWith(IEntityModelMetadataProvider provider)
        {
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object,
                new SqliteBuilder(new SqlBuilderServices(entityMappingResolver: resolver, entityModelMetadataProvider: provider))).From<Row>("r");
            SqlGeneratedQueryTemplate.Apply(query, template, 0, new object[] { 1 });
            SqlGeneratedQueryTemplate.Apply(query, template, 1, Array.Empty<object>());
            SqlGeneratedQueryTemplate.Apply(query, template, 2, Array.Empty<object>());
            return query;
        }
        var valid = QueryWith(SummaryProvider(() => SummaryMetadata()));
        Assert.Equal("Select Count(*) As `Total` \r\nFrom `AutoRows` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nOrder By Count(`r`.`Id`)", valid.ToSql());
        var unreadable = new EntityModelMetadata(typeof(Row), null, null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), false, true)
        });
        var invalid = QueryWith(SummaryProvider(() => SummaryMetadata(), () => unreadable));
        Assert.Throws<NotSupportedException>(() => invalid.ToSql());
    }

    /// <summary>来源模型未注册时，冷编译和热复用均返回明确的聚合参数错误。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AggregateShape_WhenSourceMetadataMissing_ShouldRejectColdAndWarm(bool warm)
    {
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        var template = AggregateTemplate();
        if (warm)
        {
            var valid = new SqlBuilderServices(entityMappingResolver: resolver,
                entityModelMetadataProvider: SummaryProvider(() => SummaryMetadata()));
            Assert.True(AggregateQuery(template, valid, 1).GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));
        }
        var invalid = new SqlBuilderServices(entityMappingResolver: resolver,
            entityModelMetadataProvider: SummaryProvider(() => SummaryMetadata(), () => null));
        var query = AggregateQuery(template, invalid, 2);
        Assert.Throws<NotSupportedException>(() => query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));
    }

    /// <summary>普通投影命中模板时不额外读取模型元数据。</summary>
    [Fact]
    public void NonAggregateShape_WhenWarm_ShouldNotReadSourceOrResultMetadata()
    {
        var sourceReads = 0;
        var resultReads = 0;
        var row = SqlBuilderServices.CreateDefault().EntityModelMetadataProvider.GetMetadata(typeof(Row));
        var provider = SummaryProvider(() => { resultReads++; return SummaryMetadata(); },
            () => { sourceReads++; return row; });
        var services = new SqlBuilderServices(entityModelMetadataProvider: provider);
        var template = Template();
        Assert.Equal(Sql(""), Query(template, services, 1, false, null).ToSql());
        sourceReads = resultReads = 0;
        Assert.Equal(Sql(""), Query(template, services, 2, false, null).ToSql());
        Assert.Equal(0, sourceReads);
        Assert.Equal(0, resultReads);
    }

    /// <summary>并发请求共享聚合 SQL 时，分别校验当前服务并绑定各自参数。</summary>
    [Fact]
    public async Task AggregateShape_WhenRequestsAreConcurrent_ShouldIsolateMetadataAndValues()
    {
        var replayed = 0;
        var template = AggregateTemplate(() => Interlocked.Increment(ref replayed));
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        var good = new SqlBuilderServices(entityMappingResolver: resolver,
            entityModelMetadataProvider: SummaryProvider(() => SummaryMetadata()));
        var bad = new SqlBuilderServices(entityMappingResolver: resolver,
            entityModelMetadataProvider: SummaryProvider(() => SummaryMetadata(writable: false)));
        Assert.True(AggregateQuery(template, good, 0).GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out var first, out _));
        await Task.WhenAll(Enumerable.Range(1, 32).Select(value => Task.Run(() =>
        {
            var query = AggregateQuery(template, value % 2 == 0 ? good : bad, value);
            if (value % 2 != 0)
            {
                Assert.Throws<InvalidOperationException>(() => query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out _, out _));
                return;
            }
            Assert.True(query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Summary), out var plan, out _));
            Assert.Same(first.CommandText, plan.CommandText);
            Assert.True(query.GeneratedCore.TemplateState.TryPrepare(typeof(Summary), out var snapshot, out _));
            Assert.Equal(value, Assert.Single(snapshot.Parameters).Value);
        })));
        Assert.Equal(2, replayed);
    }

    /// <summary>同形状改变值只绑定参数，不再重放子句或重新渲染 SQL。</summary>
    [Fact]
    public void WarmShape_ShouldReuseSqlAndSkipReplay_ButBindCurrentRequestValues()
    {
        var replayed = 0;
        var template = Template(() => replayed++);
        var services = SqlBuilderServices.CreateDefault();
        var first = Query(template, services, 1, true, new[] { 2, 3 });
        var second = Query(template, services, 7, true, new[] { 8, 9 });
        var sql = first.ToSql();

        Assert.Equal(Sql(" And `r`.`Id` In (@_p_1,@_p_2)"), sql);
        Assert.Same(sql, second.ToSql());
        Assert.Equal(4, replayed);
        Assert.Equal(new object[] { 7, 8, 9 }, Snapshot(second).Parameters.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 1, 2, 3 }, Snapshot(first).Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>开关、集合长度、空值和运算符分别决定 SQL 结构，禁用条件不读取集合。</summary>
    [Fact]
    public void OptionalAndCollectionShapes_ShouldMatchFullSql_AndEnumerateOnce()
    {
        var template = Template();
        var services = SqlBuilderServices.CreateDefault();
        Assert.Equal(Sql(""), Query(template, services, 1, false, new ThrowingEnumerable()).ToSql());
        Assert.Equal(Sql(" And 1 = 0"), Query(template, services, 1, true, Array.Empty<int>()).ToSql());
        Assert.Equal(Sql(" And `r`.`Id` In (@_p_1)"), Query(template, services, 1, true, new[] { 2 }).ToSql());
        var values = new CountingEnumerable();
        Assert.Equal(Sql(" And `r`.`Id` In (@_p_1,@_p_2)"), Query(template, services, 1, true, values).ToSql());
        Assert.Equal(1, values.Count);
        Assert.Equal(Sql(" And `r`.`Id` Is Null"), Query(template, services, 1, true, null, Operator.Equal).ToSql());
        Assert.Equal(Sql(" And `r`.`Id`=@_p_1"), Query(template, services, 1, true, 2, Operator.Equal).ToSql());
        Assert.Equal(Sql(" And `r`.`Id` Not In (@_p_1)"), Query(template, services, 1, true, new[] { 2 }, Operator.NotIn).ToSql());
        Assert.Throws<ArgumentException>(() => Query(template, services, 1, true, "bad"));
        Assert.Throws<ArgumentException>(() => Query(template, services, 1, true, new byte[] { 1 }));
    }

    /// <summary>并发请求独立持有值与执行快照，同一形状只编译一次。</summary>
    [Fact]
    public async Task ConcurrentQueries_ShouldShareOnlyStructure()
    {
        var replayed = 0;
        var template = Template(() => Interlocked.Increment(ref replayed));
        var services = SqlBuilderServices.CreateDefault();
        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
        {
            var query = Query(template, services, index, true, new[] { index + 1, index + 2 });
            return Snapshot(query);
        })));
        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal(Sql(" And `r`.`Id` In (@_p_1,@_p_2)"), results[index].Sql);
            Assert.Equal(new object[] { index, index + 1, index + 2 }, results[index].Parameters.Select(parameter => parameter.Value));
        }
        Assert.Equal(4, replayed);
    }

    /// <summary>运行期别名、排序、别名开关及 Provider 不得串用缓存。</summary>
    [Fact]
    public void AliasAndProviderAndOrder_ShouldIsolateShapes()
    {
        var template = Template();
        var services = SqlBuilderServices.CreateDefault();
        Assert.Equal(Sql("", "x"), Query(template, services, 1, false, null, alias: "x").ToSql());
        Assert.Equal(Sql("", descending: true), Query(template, services, 1, false, null, descending: true).ToSql());
        Assert.Equal("Select [r].[Id],[r].[Name] \r\nFrom [AutoRows] As [r] \r\nWhere [r].[Id]>=@_p_0 \r\nOrder By [r].[Id]",
            Query(template, services, 1, false, null, builder: new SqlServerBuilder(services)).ToSql());
        var noAliases = new SqlBuilderServices(options: new SqlOptions { AutoGenerateTableAlias = false });
        Assert.Equal("Select `AutoRows`.`Id`,`AutoRows`.`Name` \r\nFrom `AutoRows` \r\nWhere `AutoRows`.`Id`>=@_p_0 \r\nOrder By `AutoRows`.`Id`",
            Query(template, noAliases, 1, false, null, alias: null).ToSql());
    }

    /// <summary>失败的冷编译不污染缓存，非法关系比较可修正为合法请求再执行。</summary>
    [Fact]
    public void FailedCompilation_ShouldNotPoisonLaterRequests()
    {
        var template = Template();
        var services = SqlBuilderServices.CreateDefault();
        Assert.Throws<ArgumentNullException>(() => Query(template, services, 1, true, null, Operator.Greater).ToSql());
        Assert.Equal(Sql(" And `r`.`Id`>@_p_1"), Query(template, services, 1, true, 4, Operator.Greater).ToSql());
    }

    /// <summary>已有条件的草稿不得误认为完整空白链；保留所有原条件。</summary>
    [Fact]
    public void ConfiguredBuilder_ShouldUseExistingBridge()
    {
        var services = SqlBuilderServices.CreateDefault();
        var builder = new SqliteBuilder(services);
        builder.Where("Name", "existing");
        var query = Query(Template(), services, 1, false, null, builder: builder);
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `AutoRows` As `r` \r\nWhere `Name`=@_p_0 And `r`.`Id`>=@_p_1 \r\nOrder By `r`.`Id`", query.ToSql());
    }

    private static SqlBuilderExecutionSnapshot Snapshot(SqlLambdaQuery query)
    {
        Assert.True(query.GeneratedCore.TemplateState.TryPrepare(typeof(void), out var snapshot, out _));
        return snapshot;
    }

    /// <summary>生成计划冻结后，克隆仍保留完整结构，修改原查询失败也不丢失已记录的操作。</summary>
    [Fact]
    public void FrozenTemplate_ShouldRemainReadableAndCloneable_AfterRejectedMutation()
    {
        var query = Query(Template(), SqlBuilderServices.CreateDefault(), 1, true, new[] { 2, 3 });
        Assert.True(query.GeneratedCore.TryGeneratedTemplatePlan(typeof(Row), out var plan, out _));
        var expected = Sql(" And `r`.`Id` In (@_p_1,@_p_2)");
        Assert.Equal(expected, plan.CommandText);
        Assert.Throws<InvalidOperationException>(() => query.Where<Row>(row => row.Id == 9));
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        Assert.Equal(expected, query.GeneratedCore.GeneratedPlan().CommandText);
    }

    /// <summary>调用点缓存最多保留 256 个形状，命中复用、淘汰后重新编译且参数仍正确。</summary>
    [Fact]
    public void ShapeCache_ShouldHitAndEvict_WithoutRetainingRequestValues()
    {
        var replayed = 0;
        var template = Template(() => replayed++);
        var services = SqlBuilderServices.CreateDefault();
        var first = Query(template, services, 1, false, null, alias: "r0").ToSql();
        Assert.Same(first, Query(template, services, 2, false, null, alias: "r0").ToSql());
        Assert.Equal(4, replayed);
        for (var index = 1; index <= 256; index++)
            Assert.Equal(Sql("", "r" + index), Query(template, services, index, false, null, alias: "r" + index).ToSql());
        Assert.Equal(257 * 4, replayed);
        var afterEviction = Query(template, services, 999, false, null, alias: "r0");
        Assert.Equal(Sql("", "r0"), afterEviction.ToSql());
        Assert.NotSame(first, afterEviction.ToSql());
        Assert.Equal(258 * 4, replayed);
        Assert.Equal(999, Assert.Single(Snapshot(afterEviction).Parameters).Value);
    }

    /// <summary>同一 Provider 和映射切换执行上下文后，不能跳过新数据库类型的表名校验。</summary>
    [Fact]
    public void ExecutionDatabaseType_ShouldIsolateShapeAndValidateUnsupportedSchema()
    {
        var original = SqlBuilderServices.CreateDefault().EntityMappingResolver.Resolve(typeof(Row), null);
        var mapping = new EntityMappingMetadata { EntityType = typeof(Row), Model = original.Model,
            Table = original.Table with { Schema = "sales" }, Columns = original.Columns };
        var resolver = new Mock<IEntityMappingResolver>();
        resolver.Setup(value => value.Resolve(typeof(Row), It.IsAny<DatabaseContext>())).Returns(mapping);
        var accessor = new AsyncLocalDatabaseContextAccessor();
        var services = new SqlBuilderServices(entityMappingResolver: resolver.Object, databaseContextAccessor: accessor);
        var template = Template();
        accessor.Current = new DatabaseContext { DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.SqlServer } };
        Assert.Equal("Select `r`.`Id`,`r`.`Name` \r\nFrom `sales`.`AutoRows` As `r` \r\nWhere `r`.`Id`>=@_p_0 \r\nOrder By `r`.`Id`",
            Query(template, services, 1, false, null).ToSql());
        accessor.Current = new DatabaseContext { DataSource = new SqlDataSourceDescriptor { DatabaseType = DatabaseType.Sqlite } };
        Assert.Throws<NotSupportedException>(() => Query(template, services, 1, false, null).ToSql());
    }

    /// <summary>同一查询复用自身快照时，也必须检查当前表名称能力。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameQuerySnapshot_WhenTableCapabilitiesChange_ShouldValidateAgain(bool renderOnly)
    {
        var original = SqlBuilderServices.CreateDefault().EntityMappingResolver.Resolve(typeof(Row), null);
        var mapping = new EntityMappingMetadata { EntityType = typeof(Row), Model = original.Model,
            Table = original.Table with { Schema = "sales" }, Columns = original.Columns };
        var resolver = new Mock<IEntityMappingResolver>();
        resolver.Setup(value => value.Resolve(typeof(Row), It.IsAny<DatabaseContext>())).Returns(mapping);
        var capabilities = new SqlObjectNameCapabilities { SupportsSchema = true, MaximumNameParts = 2 };
        var provider = new Mock<ISqlObjectNameCapabilityProvider>();
        provider.Setup(value => value.GetCapabilities(It.IsAny<DatabaseType?>())).Returns(() => capabilities);
        var services = new SqlBuilderServices(entityMappingResolver: resolver.Object,
            tableReferenceValidator: new DefaultSqlTableReferenceValidator(provider.Object));
        var replayed = 0;
          var template = Template(() => replayed++);
          var query = Query(template, services, 7, false, null,
            builder: new SqlServerBuilder(services));
        var resultType = renderOnly ? typeof(void) : typeof(Row);
          string Prepare(SqlLambdaQuery current)
          {
              if (renderOnly) return current.ToSql();
              Assert.True(current.GeneratedCore.TryGeneratedTemplatePlan(resultType, out var plan, out _));
              return plan.CommandText;
          }

          var first = Prepare(query);
        Assert.Equal("Select [r].[Id],[r].[Name] \r\nFrom [sales].[AutoRows] As [r] \r\nWhere [r].[Id]>=@_p_0 \r\nOrder By [r].[Id]",
              first);
          var second = Query(template, services, 8, false, null, builder: new SqlServerBuilder(services));
          Assert.Same(first, Prepare(second));
        capabilities = new SqlObjectNameCapabilities { MaximumNameParts = 1 };
          Assert.Throws<NotSupportedException>(() => { Prepare(query); });
          Assert.Throws<NotSupportedException>(() => { Prepare(second); });
          var third = Query(template, services, 9, false, null, builder: new SqlServerBuilder(services));
          Assert.Throws<NotSupportedException>(() => { Prepare(third); });

        capabilities = new SqlObjectNameCapabilities { SupportsSchema = true, MaximumNameParts = 2 };
          Assert.Same(first, Prepare(query));
          Assert.Same(first, Prepare(second));
          Assert.Same(first, Prepare(third));
        Assert.True(query.GeneratedCore.TemplateState.TryPrepare(resultType, out var snapshot, out _));
        Assert.Equal(7, Assert.Single(snapshot.Parameters).Value);
          Assert.True(second.GeneratedCore.TemplateState.TryPrepare(resultType, out snapshot, out _));
          Assert.Equal(8, Assert.Single(snapshot.Parameters).Value);
          Assert.True(third.GeneratedCore.TemplateState.TryPrepare(resultType, out snapshot, out _));
          Assert.Equal(9, Assert.Single(snapshot.Parameters).Value);
        Assert.Equal(4, replayed);
    }

    /// <summary>自定义跨库策略保留原桥接路径，不假设扩展策略具有可缓存语义。</summary>
    [Fact]
    public void CustomCrossDatabaseValidator_ShouldRemainOnExistingPath()
    {
        var validator = new Mock<ISqlCrossDatabaseQueryValidator>();
        var services = new SqlBuilderServices(crossDatabaseQueryValidator: validator.Object);
        var replayed = 0;
        var template = Template(() => replayed++);
        var first = Query(template, services, 1, false, null);
        var second = Query(template, services, 2, false, null);
        Assert.Null(first.GeneratedCore.TemplateState);
        Assert.Null(second.GeneratedCore.TemplateState);
        Assert.Equal(Sql(""), first.ToSql());
        Assert.Equal(Sql(""), second.ToSql());
        Assert.Equal(8, replayed);
    }

    /// <summary>查询工厂每次创建服务包；结构按映射快照共享，请求参数仍分别绑定。</summary>
    [Fact]
    public void QueryScopedServicePackages_ShouldShareShape_ButBindCurrentValues()
    {
        var replayed = 0;
        var template = Template(() => replayed++);
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        var first = Query(template, new SqlBuilderServices(entityMappingResolver: resolver), 1, false, null);
        var second = Query(template, new SqlBuilderServices(entityMappingResolver: resolver), 2, false, null);
        Assert.Equal(Sql(""), first.ToSql());
        Assert.Same(first.ToSql(), second.ToSql());
        Assert.Equal(4, replayed);
        Assert.Equal(1, Assert.Single(Snapshot(first).Parameters).Value);
        Assert.Equal(2, Assert.Single(Snapshot(second).Parameters).Value);
    }

    /// <summary>自定义参数工厂不必提供完整列元数据，沿用桥接以保留它的转换行为。</summary>
    [Fact]
    public void CustomParameterFactory_ShouldUseBridgeAndCurrentConversions()
    {
        var replayed = 0;
        var template = Template(() => replayed++);
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        var firstFactory = new Mock<ISqlParameterFactory>();
        var secondFactory = new Mock<ISqlParameterFactory>();
        foreach (var (factory, offset) in new[] { (firstFactory, 10), (secondFactory, 100) })
            factory.Setup(value => value.Create(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<ColumnMappingMetadata>(),
                It.IsAny<DatabaseContext>(), It.IsAny<Type>(), It.IsAny<SqlParameterSource>()))
                .Returns((string name, object value, ColumnMappingMetadata column, DatabaseContext context, Type entity, SqlParameterSource source) =>
                    new SqlParam(name, (int)value + offset));
        var first = Query(template, new SqlBuilderServices(entityMappingResolver: resolver, parameterFactory: firstFactory.Object), 1, false, null);
        var second = Query(template, new SqlBuilderServices(entityMappingResolver: resolver, parameterFactory: secondFactory.Object), 2, false, null);
        Assert.Equal(Sql(""), first.ToSql());
        Assert.Equal(first.ToSql(), second.ToSql());
        Assert.Equal(8, replayed);
        Assert.Null(first.GeneratedCore.TemplateState);
        Assert.Null(second.GeneratedCore.TemplateState);
        Assert.Equal(11, Assert.Single(((IAdvancedParameterManager)((SqlBuilderBase)first.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams()).Value.Value);
        Assert.Equal(102, Assert.Single(((IAdvancedParameterManager)((SqlBuilderBase)second.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams()).Value.Value);
    }

    /// <summary>命中只复用结构；默认工厂内的转换器和本次原值仍逐请求运行。</summary>
    [Fact]
    public void WarmShape_ShouldUseCurrentConverterAndPreserveOriginalValue()
    {
        var resolver = SqlBuilderServices.CreateDefault().EntityMappingResolver;
        var template = Template();
        SqlBuilderServices Services(int offset)
        {
            var selector = new Mock<IFieldValueConverterSelector>();
            selector.Setup(value => value.ConvertToProvider(It.IsAny<object>(), It.IsAny<ColumnMappingMetadata>(), It.IsAny<DatabaseContext>()))
                .Returns((object value, ColumnMappingMetadata column, DatabaseContext context) => (int)value + offset);
            return new SqlBuilderServices(entityMappingResolver: resolver, parameterFactory: new DefaultSqlParameterFactory(selector.Object));
        }
        var first = Query(template, Services(10), 1, false, null);
        var second = Query(template, Services(100), 2, false, null);
        Assert.Equal(Sql(""), first.ToSql());
        Assert.Same(first.ToSql(), second.ToSql());
        var parameter = Assert.Single(Snapshot(second).Parameters);
        Assert.Equal(102, parameter.Value);
        Assert.Equal(2, parameter.OriginalValue);
    }

    /// <summary>同一解析器中的表路由和最终列映射变化必须独立编译，恢复原映射后应命中原形状。</summary>
    [Fact]
    public void FinalMappingAndRoute_ShouldIsolateCache_AndHitWhenRestored()
    {
        var original = SqlBuilderServices.CreateDefault().EntityMappingResolver.Resolve(typeof(Row), null);
        var columns = original.Columns.ToDictionary(pair => pair.Key, pair => pair.Value);
        columns[nameof(Row.Id)] = new ColumnMappingMetadata { PropertyName = nameof(Row.Id), ColumnName = "row_id",
            Column = new ColumnIdentifier("row_id"), ClrType = typeof(int) };
        var mapped = new EntityMappingMetadata { EntityType = typeof(Row), Model = original.Model,
            Table = original.Table with { TableName = "AutoRows_B" }, Columns = columns };
        var current = original;
        var resolver = new Mock<IEntityMappingResolver>();
        resolver.Setup(value => value.Resolve(typeof(Row), It.IsAny<DatabaseContext>())).Returns(() => current);
        var services = new SqlBuilderServices(entityMappingResolver: resolver.Object);
        var template = Template();
        var firstSql = Query(template, services, 1, false, null).ToSql();
        Assert.Equal(Sql(""), firstSql);
        current = mapped;
        Assert.Equal("Select `r`.`row_id`,`r`.`Name` \r\nFrom `AutoRows_B` As `r` \r\nWhere `r`.`row_id`>=@_p_0 \r\nOrder By `r`.`row_id`",
            Query(template, services, 2, false, null).ToSql());
        current = original;
        Assert.Same(firstSql, Query(template, services, 3, false, null).ToSql());
    }

    /// <summary>同一生成描述符在各 Provider 使用独立 SQL，非法比较值仍拒绝。</summary>
    [Theory]
    [InlineData("sqlite", "`", "`")]
    [InlineData("mysql", "`", "`")]
    [InlineData("pgsql", "\"", "\"")]
    [InlineData("sqlserver", "[", "]")]
    public void ProviderShapes_ShouldRenderCompleteSqlAndRejectInvalidComparison(string provider, string open, string close)
    {
        var services = SqlBuilderServices.CreateDefault();
        var template = Template();
        ISqlBuilder Builder() => provider switch { "mysql" => new MySqlBuilder(services),
            "pgsql" => new PostgreSqlBuilder(services), "sqlserver" => new SqlServerBuilder(services), _ => new SqliteBuilder(services) };
        var expected = Sql(" And `r`.`Id`=@_p_1").Replace("`r`", open + "r" + close)
            .Replace("`Id`", open + "Id" + close).Replace("`Name`", open + "Name" + close)
            .Replace("`AutoRows`", open + "AutoRows" + close);
        var first = Query(template, services, 1, true, 2, Operator.Equal, builder: Builder());
        Assert.Equal(expected, first.ToSql());
        Assert.Same(first.ToSql(), Query(template, services, 3, true, 4, Operator.Equal, builder: Builder()).ToSql());
        Assert.Throws<ArgumentNullException>(() => Query(template, services, 1, true, null, Operator.Greater, builder: Builder()).ToSql());
    }

    [Table("AutoRows")]
    public sealed class Row { public int Id { get; set; } public string Name { get; set; } }
    public sealed class Summary { public long Total { get; set; } }
    private sealed class ThrowingEnumerable : IEnumerable { public IEnumerator GetEnumerator() => throw new InvalidOperationException("disabled"); }
    private sealed class CountingEnumerable : IEnumerable
    {
        internal int Count;
        public IEnumerator GetEnumerator() { Count++; return new object[] { 2, 3 }.GetEnumerator(); }
    }
}
