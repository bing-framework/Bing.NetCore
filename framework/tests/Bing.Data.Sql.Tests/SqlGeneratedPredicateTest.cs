using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>验证生成的复合条件与当前查询上下文的绑定。</summary>
public class SqlGeneratedPredicateTest
{
    private static SqlLambdaQuery Create() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<SqlGeneratedQueryBridgeRegressionTest.Node>("n")
            .Select<SqlGeneratedQueryBridgeRegressionTest.Node>(n => n.Id);

    private static SqlGeneratedColumn Column(string name) => new(0, name, null, typeof(int), null, false);

    /// <summary>同一逻辑条件可为不同请求独立绑定值，同时保留映射与参数元数据。</summary>
    [Fact]
    public void WherePredicate_WhenParameterIsSharedByConditions_ShouldIsolateRequests()
    {
        var metadata = new TestEntityMetadata();
        SqlLambdaQuery CreateMapped(string alias)
        {
            var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
                entityMappingResolver: new DefaultEntityMappingResolver(metadata),
                sqlParameterFactory: new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector()));
            return SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
                .From<Sample>(alias).Select<Sample>(s => s.IntValue, alias);
        }
        var predicate = SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.CompareParameter(Column("IntValue"), Operator.Equal),
            SqlGeneratedPredicate.CompareParameter(Column("IntValue"), Operator.Greater));
        var first = CreateMapped("a");
        var second = CreateMapped("b");
        SqlGeneratedQueryBridge.WherePredicate(first, new[] { typeof(Sample) }, new[] { "s" }, new[] { "a" }, predicate, 7);
        SqlGeneratedQueryBridge.WherePredicate(second, new[] { typeof(Sample) }, new[] { "s" }, new[] { "b" }, predicate, 9);

        Assert.Equal("Select [a].[Sample_IntValue] \r\nFrom [as_Sample].[t_Sample] As [a] \r\nWhere ([a].[Sample_IntValue]=@_p_0 Or [a].[Sample_IntValue]>@_p_1)", first.ToSql());
        Assert.Equal("Select [b].[Sample_IntValue] \r\nFrom [as_Sample].[t_Sample] As [b] \r\nWhere ([b].[Sample_IntValue]=@_p_0 Or [b].[Sample_IntValue]>@_p_1)", second.ToSql());
        Assert.Equal(first.ToSql(), first.Clone().ToSql());
        var firstParameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)first.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
        var secondParameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)second.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
        Assert.Equal(new object[] { 7, 7 }, firstParameters.Values.Select(parameter => parameter.Value));
        Assert.Equal(new object[] { 9, 9 }, secondParameters.Values.Select(parameter => parameter.Value));
        Assert.All(firstParameters.Values, parameter =>
        {
            Assert.Equal(typeof(Sample), parameter.EntityType);
            Assert.Equal(nameof(Sample.IntValue), parameter.PropertyName);
            Assert.Equal("Sample_IntValue", parameter.ColumnName);
        });
    }

    /// <summary>显式值为空时生成空值比较，不把槽位标记作为实际参数。</summary>
    [Fact]
    public void WherePredicate_WhenParameterIsNull_ShouldGenerateNullComparison()
    {
        var query = Create();
        var name = new SqlGeneratedColumn(0, "Name", null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.CompareParameter(name, Operator.Equal),
            SqlGeneratedPredicate.CompareParameter(name, Operator.NotEqual));

        SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, null, predicate, null);

        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere ([n].[Name] Is Null Or [n].[Name] Is Not Null)", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>显式空值不能用于大小比较，失败时前面的参数也应回滚。</summary>
    [Fact]
    public void WherePredicate_WhenRelationalParameterIsNull_ShouldRollback()
    {
        var query = Create();
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.Equal, 1),
            SqlGeneratedPredicate.CompareParameter(Column("ParentId"), Operator.Greater));

        Assert.Throws<ArgumentNullException>(() => SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, null, predicate, null));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>取反条件保留分组、参数、空值比较和克隆后的来源绑定。</summary>
    [Fact]
    public void WherePredicate_WhenNegated_ShouldPreserveGroupingParametersAndClone()
    {
        var query = Create();
        var name = new SqlGeneratedColumn(0, "Name", null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.GreaterEqual, 1),
            SqlGeneratedPredicate.Not(SqlGeneratedPredicate.Or(
                SqlGeneratedPredicate.Compare(name, null, Operator.Equal, null),
                SqlGeneratedPredicate.Compare(Column("ParentId"), null, Operator.Greater, 5))));

        SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, null, predicate);

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]>=@_p_0 And Not (([n].[Name] Is Null Or [n].[ParentId]>@_p_2))";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(2, parameters.Count);
        Assert.Equal(1, parameters["@_p_0"]);
        Assert.Equal(5, parameters["@_p_2"]);
    }

    /// <summary>取反条件内部绑定失败时整次变更及已分配参数都应回滚。</summary>
    [Fact]
    public void WherePredicate_WhenNegatedChildIsInvalid_ShouldRollback()
    {
        var query = Create();
        var predicate = SqlGeneratedPredicate.Not(SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.Equal, 1),
            SqlGeneratedPredicate.Compare(Column("Missing"), null, Operator.Equal, 2)));

        Assert.Throws<InvalidOperationException>(() => SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, null, predicate));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>嵌套条件保留逻辑优先级、参数顺序及重复渲染语义。</summary>
    [Fact]
    public void WherePredicate_WhenNested_ShouldBindColumnsAndParameters()
    {
        var query = Create();
        var predicate = SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.And(
                SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.GreaterEqual, 1),
                SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.Less, 10)),
            SqlGeneratedPredicate.Compare(Column("ParentId"), Column("Id"), Operator.Equal, null));

        Assert.Same(query, SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, null, predicate));

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere ([n].[Id]>=@_p_0 And [n].[Id]<@_p_1 Or [n].[ParentId]=[n].[Id])";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(2, parameters.Count);
        Assert.Equal(1, parameters["@_p_0"]);
        Assert.Equal(10, parameters["@_p_1"]);
    }

    /// <summary>后续条件绑定失败时，先前分配的参数和整个条件均应回滚。</summary>
    [Fact]
    public void WherePredicate_WhenLaterColumnIsInvalid_ShouldRollbackAndAllowRetry()
    {
        var query = Create();
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.Equal, 1),
            SqlGeneratedPredicate.Compare(Column("Missing"), null, Operator.Equal, 2));
        var types = new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) };
        var names = new[] { "n" };

        Assert.Throws<InvalidOperationException>(() => SqlGeneratedQueryBridge.WherePredicate(query, types, names, null, predicate));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());

        SqlGeneratedQueryBridge.WherePredicate(query, types, names, null,
            SqlGeneratedPredicate.And(
                SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.Equal, 3),
                SqlGeneratedPredicate.Compare(Column("ParentId"), null, Operator.Equal, 4)));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]=@_p_0 And [n].[ParentId]=@_p_1", query.ToSql());
    }

    /// <summary>列映射、空值比较和参数元数据由当前查询上下文决定。</summary>
    [Fact]
    public void WherePredicate_WhenColumnsAreMapped_ShouldPreserveNullAndParameterMetadata()
    {
        var metadata = new TestEntityMetadata();
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
            entityMappingResolver: new DefaultEntityMappingResolver(metadata),
            sqlParameterFactory: new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector()));
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("mapped").Select<Sample>(s => s.StringValue);
        var predicate = SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.Compare(new SqlGeneratedColumn(0, "StringValue", null, typeof(string), null, false),
                null, Operator.Equal, null),
            SqlGeneratedPredicate.Compare(Column("IntValue"), null, Operator.GreaterEqual, 7));

        SqlGeneratedQueryBridge.WherePredicate(query, new[] { typeof(Sample) }, new[] { "s" }, new[] { "mapped" }, predicate);

        const string expected = "Select [mapped].[Sample_StringValue] \r\nFrom [as_Sample].[t_Sample] As [mapped] \r\nWhere ([mapped].[Sample_StringValue] Is Null Or [mapped].[Sample_IntValue]>=@_p_1)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        var parameters = (IAdvancedParameterManager)((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager;
        var parameter = Assert.Single(parameters.GetSqlParams()).Value;
        Assert.Equal(typeof(Sample), parameter.EntityType);
        Assert.Equal(nameof(Sample.IntValue), parameter.PropertyName);
        Assert.Equal("Sample_IntValue", parameter.ColumnName);
        Assert.Equal(7, parameter.Value);
    }

    /// <summary>相同实体的多个来源使用各自的显式别名和列。</summary>
    [Fact]
    public void WherePredicate_WhenSourcesHaveSameType_ShouldBindEachAlias()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<SqlGeneratedQueryBridgeRegressionTest.Node>("a")
            .Join<SqlGeneratedQueryBridgeRegressionTest.Node, SqlGeneratedQueryBridgeRegressionTest.Node>(
                (a, b) => a.ParentId == b.Id, "b")
            .Select<SqlGeneratedQueryBridgeRegressionTest.Node>(a => a.Id, "a");
        var rightId = new SqlGeneratedColumn(1, "Id", null, typeof(int), null, false);
        var predicate = SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.Compare(Column("Id"),
                new SqlGeneratedColumn(1, "ParentId", null, typeof(int), null, false), Operator.Equal, null),
            SqlGeneratedPredicate.Compare(rightId, null, Operator.Greater, 2));

        SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node), typeof(SqlGeneratedQueryBridgeRegressionTest.Node) },
            new[] { "left", "right" }, new[] { "a", "b" }, predicate);

        const string expected = "Select [a].[Id] \r\nFrom [Nodes] As [a] \r\nJoin [Nodes] As [b] On [a].[ParentId]=[b].[Id] \r\nWhere ([a].[Id]=[b].[ParentId] Or [b].[Id]>@_p_0)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>复合条件中的字符串匹配按各自运算符绑定参数并保留括号。</summary>
    [Fact]
    public void WherePredicate_WhenStringMatchesAreCombined_ShouldBindLikeValues()
    {
        var query = Create();
        var name = new SqlGeneratedColumn(0, "Name", null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(Column("Id"), null, Operator.GreaterEqual, 1),
            SqlGeneratedPredicate.Or(
                SqlGeneratedPredicate.Compare(name, null, Operator.Contains, "dat"),
                SqlGeneratedPredicate.And(
                    SqlGeneratedPredicate.Compare(name, null, Operator.Starts, "sec"),
                    SqlGeneratedPredicate.Compare(name, null, Operator.Ends, "ond"))));

        SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, null, predicate);

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]>=@_p_0 And ([n].[Name] Like @_p_1 Or [n].[Name] Like @_p_2 And [n].[Name] Like @_p_3)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(4, parameters.Count);
        Assert.Equal(1, parameters["@_p_0"]);
        Assert.Equal("%dat%", parameters["@_p_1"]);
        Assert.Equal("sec%", parameters["@_p_2"]);
        Assert.Equal("%ond", parameters["@_p_3"]);
    }
}
