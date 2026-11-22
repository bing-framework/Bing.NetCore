using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;
using Node = Bing.Data.Sql.Tests.SqlGeneratedQueryBridgeRegressionTest.Node;

namespace Bing.Data.Sql.Tests;

/// <summary>验证生成式元组值条件与兼容路径的绑定契约。</summary>
public class SqlGeneratedTuplePredicateTest
{
    private static SqlLambdaQuery Create() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<Node>("n").Select<Node>(n => n.Id, "n");

    /// <summary>独立槽位和重复槽位应按当前请求绑定，兼容路径使用同样的参数化语义。</summary>
    [Fact]
    public void WherePredicate_WhenTupleFieldsAreReused_ShouldBindIndependentValues()
    {
        var runtime = Create().Where<Node, (int id, string name)>((7, "ab"),
            (n, value) => n.Id == value.id && (n.Name.Contains(value.name) || n.ParentId == value.id), "n");
        var generated = Create();
        var id = new SqlGeneratedColumn(0, nameof(Node.Id), null, typeof(int), null, false);
        var parent = new SqlGeneratedColumn(0, nameof(Node.ParentId), null, typeof(int), null, false);
        var name = new SqlGeneratedColumn(0, nameof(Node.Name), null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.And(SqlGeneratedPredicate.CompareParameter(id, Operator.Equal, 0),
            SqlGeneratedPredicate.Or(SqlGeneratedPredicate.CompareParameter(name, Operator.Contains, 1),
                SqlGeneratedPredicate.CompareParameter(parent, Operator.Equal, 0)));
        SqlGeneratedQueryBridge.WherePredicate(generated, new[] { typeof(Node) }, new[] { "n" },
            new[] { "n" }, predicate, new object[] { 7, "ab" });

        const string sql = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\n" +
            "Where [n].[Id]=@_p_0 And ([n].[Name] Like @_p_1 Or [n].[ParentId]=@_p_2)";
        foreach (var query in new[] { runtime, generated, generated.Clone() })
        {
            Assert.Equal(sql, query.ToSql());
            Assert.Equal(sql, query.ToSql());
            var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
            Assert.Equal(3, parameters.Count);
            Assert.Equal(7, parameters["@_p_0"]);
            Assert.Equal("%ab%", parameters["@_p_1"]);
            Assert.Equal(7, parameters["@_p_2"]);
        }
    }

    /// <summary>多个槽位包含空值时必须保留 SQL 空值语义和其他参数。</summary>
    [Fact]
    public void WherePredicate_WhenTupleFieldIsNull_ShouldUseNullComparison()
    {
        var runtime = Create().Where<Node, (int id, string name)>((4, null),
            (n, value) => n.Id == value.id && n.Name == value.name, "n");
        var generated = Create();
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Node.Id), null, typeof(int), null, false), Operator.Equal, 0),
            SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Node.Name), null, typeof(string), null, false), Operator.Equal, 1));
        SqlGeneratedQueryBridge.WherePredicate(generated, new[] { typeof(Node) }, new[] { "n" },
            new[] { "n" }, predicate, new object[] { 4, null });

        const string sql = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]=@_p_0 And [n].[Name] Is Null";
        foreach (var query in new[] { runtime, generated })
        {
            Assert.Equal(sql, query.ToSql());
            var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
            Assert.Equal(4, Assert.Single(parameters).Value);
        }
    }

    /// <summary>每个值槽必须使用各自目标列的映射元数据。</summary>
    [Fact]
    public void WherePredicate_WhenTupleFieldsUseMappedColumns_ShouldPreserveMetadata()
    {
        var metadata = new TestEntityMetadata();
        SqlLambdaQuery CreateMapped()
        {
            var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
                entityMappingResolver: new DefaultEntityMappingResolver(metadata),
                sqlParameterFactory: new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector()));
            return SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
                .From<Sample>("s").Select<Sample>(s => s.IntValue, "s");
        }

        var runtime = CreateMapped().Where<Sample, (int id, string name)>((17, "abc"),
            (s, value) => s.IntValue == value.id && s.StringValue == value.name, "s");
        var generated = CreateMapped();
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Sample.IntValue), null, typeof(int), null, false), Operator.Equal, 0),
            SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Sample.StringValue), null, typeof(string), null, false), Operator.Equal, 1));
        SqlGeneratedQueryBridge.WherePredicate(generated, new[] { typeof(Sample) }, new[] { "s" },
            new[] { "s" }, predicate, new object[] { 17, "abc" });

        const string sql = "Select [s].[Sample_IntValue] \r\nFrom [as_Sample].[t_Sample] As [s] \r\n" +
            "Where [s].[Sample_IntValue]=@_p_0 And [s].[Sample_StringValue]=@_p_1";
        foreach (var query in new[] { runtime, generated })
        {
            Assert.Equal(sql, query.ToSql());
            var parameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
            Assert.Equal((17, nameof(Sample.IntValue), "Sample_IntValue"),
                (parameters["@_p_0"].Value, parameters["@_p_0"].PropertyName, parameters["@_p_0"].ColumnName));
            Assert.Equal(("abc", nameof(Sample.StringValue), "Sample_StringValue"),
                (parameters["@_p_1"].Value, parameters["@_p_1"].PropertyName, parameters["@_p_1"].ColumnName));
        }
    }

    /// <summary>同一数值的不同提升结果分别按目标列绑定，重复槽位保持原类型。</summary>
    [Fact]
    public void WherePredicate_WhenPromotedValuesUseDifferentColumns_ShouldBindEachTargetType()
    {
        var metadata = new TestEntityMetadata();
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
            entityMappingResolver: new DefaultEntityMappingResolver(metadata),
            sqlParameterFactory: new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector()));
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("s").Select<Sample>(s => s.IntValue, "s");
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Sample.LongValue), null, typeof(long), null, false), Operator.Equal, 0),
            SqlGeneratedPredicate.And(
                SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Sample.DoubleValue), null, typeof(double), null, false), Operator.Equal, 1),
                SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Sample.IntValue), null, typeof(int), null, false), Operator.Equal, 2)));
        const int input = 16777217;
        SqlGeneratedQueryBridge.WherePredicate(query, new[] { typeof(Sample) }, new[] { "s" },
            new[] { "s" }, predicate, new object[] { (long)input, (double)input, input });

        Assert.Equal("Select [s].[Sample_IntValue] \r\nFrom [as_Sample].[t_Sample] As [s] \r\n" +
            "Where [s].[Sample_LongValue]=@_p_0 And [s].[Sample_DoubleValue]=@_p_1 And [s].[Sample_IntValue]=@_p_2", query.ToSql());
        var parameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
        Assert.Equal((long)input, Assert.IsType<long>(parameters["@_p_0"].Value));
        Assert.Equal((double)input, Assert.IsType<double>(parameters["@_p_1"].Value));
        Assert.Equal(input, Assert.IsType<int>(parameters["@_p_2"].Value));
        Assert.Equal(nameof(Sample.LongValue), parameters["@_p_0"].PropertyName);
        Assert.Equal(nameof(Sample.DoubleValue), parameters["@_p_1"].PropertyName);
        Assert.Equal(nameof(Sample.IntValue), parameters["@_p_2"].PropertyName);
    }

    /// <summary>后续槽位绑定失败时不得留下已添加的条件或参数。</summary>
    [Fact]
    public void WherePredicate_WhenLaterTupleFieldCannotBind_ShouldRollback()
    {
        var query = Create();
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Node.Id), null, typeof(int), null, false), Operator.Equal, 0),
            SqlGeneratedPredicate.CompareParameter(new SqlGeneratedColumn(0, nameof(Node.ParentId), null, typeof(int), null, false), Operator.Greater, 1));

        Assert.Throws<ArgumentNullException>(() => SqlGeneratedQueryBridge.WherePredicate(query,
            new[] { typeof(Node) }, new[] { "n" }, new[] { "n" }, predicate, new object[] { 7, null }));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }
}
