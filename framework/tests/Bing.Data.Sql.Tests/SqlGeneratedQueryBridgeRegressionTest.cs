using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 验证生成桥接与普通查询一致性的回归测试。
/// </summary>
/// <remarks>
/// 生成桥接和普通 Lambda 必须遵守相同来源、分组及失败回滚契约。
/// </remarks>
public class SqlGeneratedQueryBridgeRegressionTest
{
    /// <summary>
    /// 创建生成查询桥接测试实例。
    /// </summary>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery Create() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder());

    /// <summary>
    /// 验证连接选项覆盖参数名后保留来源别名与架构。
    /// </summary>
    /// <param name="leftJoin">是否使用左外连接。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Join_WhenOptionsOverrideParameterNames_ShouldPreserveSourceAliasAndSchema(bool leftJoin)
    {
        SqlLambdaQuery Start() => Create().From<Node>("a")
            .Join<Node, Node>((a, b) => a.ParentId == b.Id, "b").Select<Node>(a => new { a.Id });
        var options = new SqlJoinOptions { LeftAlias = "a", RightAlias = "chosen", Schema = "archive" };
        var runtime = Start();
        if (leftJoin) runtime.LeftJoin<Node, Node>((b, c) => b.ParentId == c.Id, options);
        else runtime.Join<Node, Node>((b, c) => b.ParentId == c.Id, options);
        var generated = Start();
        SqlGeneratedQueryBridge.Join(generated, leftJoin, typeof(Node), typeof(Node), "b", "c", "ParentId", "Id", options);
        var expected = "Select [a].[Id] As [Id] \r\nFrom [Nodes] As [a] \r\nJoin [Nodes] As [b] On [a].[ParentId]=[b].[Id] \r\n" +
            (leftJoin ? "Left Join" : "Join") + " [archive].[Nodes] As [chosen] On [a].[ParentId]=[chosen].[Id]";
        Assert.Equal(expected, runtime.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Equal(expected, generated.Clone().ToSql());
    }

    /// <summary>
    /// 验证缺失的显式左来源触发回滚且允许重新配置。
    /// </summary>
    [Fact]
    public void Join_WhenExplicitLeftSourceIsMissing_ShouldRollbackAndAllowCorrection()
    {
        var query = Create().From<Node>("a").Select<Node>(a => a.Id);
        const string expected = "Select [a].[Id] \r\nFrom [Nodes] As [a]";
        Assert.Throws<InvalidOperationException>(() => SqlGeneratedQueryBridge.Join(query, false, typeof(Node), typeof(Node),
            "a", "b", "ParentId", "Id", new SqlJoinOptions { LeftAlias = "missing", RightAlias = "b" }));
        Assert.Equal(expected, query.ToSql());
        SqlGeneratedQueryBridge.Join(query, false, typeof(Node), typeof(Node), "a", "b", "ParentId", "Id",
            new SqlJoinOptions { LeftAlias = "a", RightAlias = "b" });
        Assert.Equal(expected + " \r\nJoin [Nodes] As [b] On [a].[ParentId]=[b].[Id]", query.ToSql());
    }

    /// <summary>
    /// 验证生成 WhereIf 在 false 条件下保持 Builder 和参数状态，在 true 条件下生成完整 SQL。
    /// </summary>
    [Fact]
    public void OptionalCompare_WhenConditionChanges_ShouldPreserveQueryShapeSemantics()
    {
        var query = Create().From<Node>("n").Select<Node>(n => n.Id);
        var column = new SqlGeneratedColumn(0, "Id", null, typeof(int), null, false);
        const string baseSql = "Select [n].[Id] \r\nFrom [Nodes] As [n]";

        Assert.Same(query, SqlGeneratedQueryBridge.OptionalCompare(query, false, false,
            new[] { typeof(Node) }, new[] { "n" }, null, column, null, Operator.Equal, 7));
        Assert.Equal(baseSql, query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());

        Assert.Same(query, SqlGeneratedQueryBridge.OptionalCompare(query, true, false,
            new[] { typeof(Node) }, new[] { "n" }, null, column, null, Operator.Equal, 7));
        Assert.Equal(baseSql + " \r\nWhere [n].[Id]=@_p_0", query.ToSql());
        Assert.Equal(7, ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams()["@_p_0"]);
    }

    /// <summary>布尔列生成条件应与普通谓词的 SQL、参数及条件关闭语义一致。</summary>
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    public void Compare_WhenBooleanPredicateIsUsed_ShouldMatchRuntime(bool value, bool optional, bool enabled)
    {
        var runtime = Create().From<BooleanNode>("b").Select<BooleanNode>(b => b.Id);
        var generated = Create().From<BooleanNode>("b").Select<BooleanNode>(b => b.Id);
        if (optional)
        {
            if (value) runtime.WhereIf<BooleanNode>(enabled, b => b.Enabled);
            else runtime.WhereIf<BooleanNode>(enabled, b => !b.Enabled);
        }
        else if (value) runtime.Where<BooleanNode>(b => b.Enabled);
        else runtime.Where<BooleanNode>(b => !b.Enabled);

        var column = new SqlGeneratedColumn(0, "Enabled", null, typeof(bool), null, false);
        if (optional)
            SqlGeneratedQueryBridge.OptionalCompare(generated, enabled, false, new[] { typeof(BooleanNode) },
                new[] { "b" }, null, column, null, Operator.Equal, value);
        else
            SqlGeneratedQueryBridge.Compare(generated, false, new[] { typeof(BooleanNode) },
                new[] { "b" }, null, column, null, Operator.Equal, value);

        var expected = "Select [b].[Id] \r\nFrom [BooleanNodes] As [b]" +
            (optional && !enabled ? string.Empty : " \r\nWhere [b].[Enabled]=@_p_0");
        Assert.Equal(expected, runtime.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Equal(expected, generated.Clone().ToSql());
        var runtimeParameters = ((ISqlCommonPartAccessor)runtime.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        var generatedParameters = ((ISqlCommonPartAccessor)generated.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        if (optional && !enabled)
        {
            Assert.Empty(runtimeParameters);
            Assert.Empty(generatedParameters);
        }
        else
        {
            Assert.Equal(value, runtimeParameters["@_p_0"]);
            Assert.Equal(value, generatedParameters["@_p_0"]);
        }
    }

    /// <summary>
    /// 生成路径应与普通查询一样逐项展开集合，且重复渲染与克隆保持参数绑定。
    /// </summary>
    [Theory]
    [InlineData(Operator.In, "In")]
    [InlineData(Operator.NotIn, "Not In")]
    [InlineData(Operator.Contains, "In")]
    public void OptionalCompare_WhenValueIsCollection_ShouldMatchRuntime(Operator comparison, string sqlOperator)
    {
        var ids = new[] { 2, 5 };
        var runtime = Create().From<Node>("n").Select<Node>(n => n.Id)
            .WhereIf<Node>(true, n => n.Id, ids, comparison);
        var generated = Create().From<Node>("n").Select<Node>(n => n.Id);
        var column = new SqlGeneratedColumn(0, "Id", null, typeof(int), null, false);
        SqlGeneratedQueryBridge.OptionalCompare(generated, true, false, new[] { typeof(Node) }, new[] { "n" },
            null, column, null, comparison, ids);

        var expected = $"Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id] {sqlOperator} (@_p_0,@_p_1)";
        Assert.Equal(expected, runtime.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Equal(expected, generated.Clone().ToSql());
        var parameters = ((ISqlCommonPartAccessor)generated.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(2, parameters.Count);
        Assert.Equal(2, parameters["@_p_0"]);
        Assert.Equal(5, parameters["@_p_1"]);
    }

    /// <summary>
    /// 生成路径应保留空集合和关闭条件的普通查询语义。
    /// </summary>
    [Theory]
    [InlineData(Operator.In, "1 = 0")]
    [InlineData(Operator.NotIn, "1 = 1")]
    public void OptionalCompare_WhenCollectionIsEmpty_ShouldMatchRuntime(Operator comparison, string predicate)
    {
        var runtime = Create().From<Node>("n").Select<Node>(n => n.Id)
            .WhereIf<Node>(true, n => n.Id, Array.Empty<int>(), comparison);
        var generated = Create().From<Node>("n").Select<Node>(n => n.Id);
        var column = new SqlGeneratedColumn(0, "Id", null, typeof(int), null, false);
        SqlGeneratedQueryBridge.OptionalCompare(generated, false, false, new[] { typeof(Node) }, new[] { "n" },
            null, column, null, comparison, "not a collection");
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", generated.ToSql());
        SqlGeneratedQueryBridge.OptionalCompare(generated, true, false, new[] { typeof(Node) }, new[] { "n" },
            null, column, null, comparison, Array.Empty<int>());

        var expected = $"Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere {predicate}";
        Assert.Equal(expected, runtime.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)generated.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>
    /// 生成路径的模糊匹配应使用与普通查询相同的通配符参数。
    /// </summary>
    [Theory]
    [InlineData(Operator.Contains, "%abc%")]
    [InlineData(Operator.Starts, "abc%")]
    [InlineData(Operator.Ends, "%abc")]
    public void OptionalCompare_WhenValueIsString_ShouldMatchRuntimePattern(Operator comparison, string parameter)
    {
        var runtime = Create().From<Node>("n").Select<Node>(n => n.Id)
            .WhereIf<Node>(true, n => n.Name, "abc", comparison);
        var generated = Create().From<Node>("n").Select<Node>(n => n.Id);
        var column = new SqlGeneratedColumn(0, "Name", null, typeof(string), null, false);
        SqlGeneratedQueryBridge.OptionalCompare(generated, true, false, new[] { typeof(Node) }, new[] { "n" },
            null, column, null, comparison, "abc");

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Name] Like @_p_0";
        Assert.Equal(expected, runtime.ToSql());
        Assert.Equal(expected, generated.ToSql());
        Assert.Equal(parameter, ((ISqlCommonPartAccessor)runtime.GeneratedCore.ReadBuilder).ParameterManager.GetParams()["@_p_0"]);
        Assert.Equal(parameter, ((ISqlCommonPartAccessor)generated.GeneratedCore.ReadBuilder).ParameterManager.GetParams()["@_p_0"]);
    }

    /// <summary>
    /// 生成条件参数应使用当前实体列映射和参数工厂。
    /// </summary>
    [Fact]
    public void Compare_WhenColumnIsMapped_ShouldPreserveParameterMetadata()
    {
        var metadata = new TestEntityMetadata();
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
            entityMappingResolver: new DefaultEntityMappingResolver(metadata),
            sqlParameterFactory: new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector()));
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("s").Select<Sample>(s => s.StringValue);
        SqlGeneratedQueryBridge.Compare(query, false, new[] { typeof(Sample) }, new[] { "s" }, null,
            new SqlGeneratedColumn(0, "StringValue", null, typeof(string), null, false), null, Operator.Contains, "abc");

        Assert.Equal("Select [s].[Sample_StringValue] \r\nFrom [as_Sample].[t_Sample] As [s] \r\nWhere [s].[Sample_StringValue] Like @_p_0", query.ToSql());
        var parameters = (IAdvancedParameterManager)((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager;
        var parameter = Assert.Single(parameters.GetSqlParams()).Value;
        Assert.Equal(typeof(Sample), parameter.EntityType);
        Assert.Equal(nameof(Sample.StringValue), parameter.PropertyName);
        Assert.Equal("Sample_StringValue", parameter.ColumnName);
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(20, parameter.Size);
        Assert.Equal(SqlParameterSource.Lambda, parameter.Source);
        Assert.Equal(SqlParameterMetadataLevel.Full, parameter.MetadataLevel);
        Assert.Equal("%abc%", parameter.Value);
        Assert.True(parameter.IsProviderValue);
    }

    /// <summary>
    /// 无效比较值应在调用时失败，失败后仍可继续使用该查询。
    /// </summary>
    [Fact]
    public void OptionalCompare_WhenComparisonValueIsInvalid_ShouldRollback()
    {
        var query = Create().From<Node>("n").Select<Node>(n => n.Id);
        var column = new SqlGeneratedColumn(0, "Id", null, typeof(int), null, false);
        Assert.Throws<ArgumentNullException>(() => SqlGeneratedQueryBridge.OptionalCompare(query, true, false,
            new[] { typeof(Node) }, new[] { "n" }, null, column, null, Operator.Greater, null));
        Assert.Throws<ArgumentException>(() => SqlGeneratedQueryBridge.OptionalCompare(query, true, false,
            new[] { typeof(Node) }, new[] { "n" }, null, column, null, Operator.In, "invalid"));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
        SqlGeneratedQueryBridge.OptionalCompare(query, true, false, new[] { typeof(Node) }, new[] { "n" },
            null, column, null, Operator.Equal, 7);
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]=@_p_0", query.ToSql());
    }

    /// <summary>
    /// 验证未分组的普通列筛选被拒绝且允许修正。
    /// </summary>
    /// <param name="aggregate">是否使用聚合投影。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Having_WhenOrdinaryColumnIsNotGrouped_ShouldRejectAndAllowCorrection(bool aggregate)
    {
        var query = Create().From<Node>("n").Select<Node>(n => new { n.ParentId }).GroupBy<Node>(n => n.ParentId);
        if (aggregate) query.Select<Node>((n, f) => new { Count = f.CountAll() });
        query.Having<Node>(n => (n.ParentId > 0 && n.Id > 0) || n.ParentId == 42);
        Assert.Throws<InvalidOperationException>(() => query.ToSql());
        Assert.Throws<InvalidOperationException>(() => query.Clone().ToSql());
        query.Having<Node>(n => n.ParentId > 0);
        var expected = "Select [n].[ParentId] As [ParentId]" + (aggregate ? ",Count(*) As [Count]" : "") +
            " \r\nFrom [Nodes] As [n] \r\nGroup By [n].[ParentId] Having [n].[ParentId]>@_p_3";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>
    /// 验证生成聚合筛选中的结果别名被原子拒绝。
    /// </summary>
    [Fact]
    public void Having_WhenGeneratedAggregateHasResultAlias_ShouldRejectAtomically()
    {
        var query = Create().From<Node>("n").Select<Node>((n, f) => new { Count = f.CountAll() });
        const string expected = "Select Count(*) As [Count] \r\nFrom [Nodes] As [n]";
        var bad = new SqlGeneratedColumn(0, null, "bad", typeof(long), SqlAggregateFunction.Count, false);
        Assert.Throws<ArgumentException>(() => query.Having<Node>((n, f) => f.CountAll("bad") > 0));
        Assert.Equal(expected, query.ToSql());
        Assert.Throws<ArgumentException>(() => SqlGeneratedQueryBridge.Compare(query, true, new[] { typeof(Node) },
            new[] { "renamed" }, null, bad, null, Operator.Greater, 0));
        Assert.Equal(expected, query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
        SqlGeneratedQueryBridge.Compare(query, true, new[] { typeof(Node) }, new[] { "renamed" }, null,
            new SqlGeneratedColumn(0, null, null, typeof(long), SqlAggregateFunction.Count, false), null, Operator.Greater, 2L);
        Assert.Equal(expected + " \r\nHaving Count(*)>@_p_0", query.ToSql());
        Assert.Equal(2L, ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams()["@_p_0"]);
    }

    /// <summary>
    /// SQLite 的 Schema 限制必须在生成连接提交前检查，失败不遗留来源。
    /// </summary>
    [Fact]
    public void Join_WhenSqliteRejectsSchema_ShouldRollbackBeforeRendering()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new SqliteBuilder())
            .From<Node>("a").Select<Node>(a => a.Id);
        const string expected = "Select `a`.`Id` \r\nFrom `Nodes` As `a`";
        Assert.Throws<NotSupportedException>(() => SqlGeneratedQueryBridge.Join(query, false, typeof(Node), typeof(Node),
            "a", "b", "ParentId", "Id", new SqlJoinOptions { Schema = "archive" }));
        Assert.Equal(expected, query.ToSql());
        Assert.Throws<NotSupportedException>(() => query.Join<Node, Node>((a, b) => a.ParentId == b.Id,
            new SqlJoinOptions { Schema = "archive" }));
        Assert.Equal(expected, query.ToSql());
    }

    /// <summary>提供布尔谓词测试使用的实体记录。</summary>
    [Table("BooleanNodes")]
    public class BooleanNode
    {
        /// <summary>获取或设置记录标识。</summary>
        public int Id { get; set; }
        /// <summary>获取或设置启用状态。</summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// 提供生成查询桥接测试使用的实体记录。
    /// </summary>
    [Table("Nodes")]
    public class Node
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置父记录标识。
        /// </summary>
        public int ParentId { get; set; }
        /// <summary>
        /// 获取或设置记录名称。
        /// </summary>
        public string Name { get; set; }
    }
}
