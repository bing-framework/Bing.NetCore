using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>验证显式集合谓词的兼容路径及生成路径。</summary>
public class SqlLambdaExplicitCollectionPredicateTest
{
    private static SqlLambdaQuery Create() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<SqlGeneratedQueryBridgeRegressionTest.Node>("n")
            .Select<SqlGeneratedQueryBridgeRegressionTest.Node>(n => n.Id);

    /// <summary>集合长度改变 SQL 参数布局；两种路径应保留分组、参数与克隆行为。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(new int[0])]
    [InlineData(new[] { 2 })]
    [InlineData(new[] { 2, 3 })]
    public void Where_WhenExplicitCollectionLengthChanges_ShouldMatchGeneratedPath(int[] values)
    {
        var compatible = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, int[]>(values,
            (n, ids) => n.Id > 1 && ids.Contains(n.ParentId), "n");
        var generated = Create();
        var id = new SqlGeneratedColumn(0, "Id", null, typeof(int), null, false);
        var parent = new SqlGeneratedColumn(0, "ParentId", null, typeof(int), null, false);
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(id, null, Operator.Greater, 1),
            SqlGeneratedPredicate.CompareParameter(parent, Operator.In));
        SqlGeneratedQueryBridge.WherePredicate(generated,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, new[] { "n" }, predicate, values);

        var names = values == null ? Array.Empty<string>() : values.Select((_, index) => "@_p_" + (index + 1)).ToArray();
        var condition = names.Length == 0 ? "1 = 0" : "[n].[ParentId] In (" + string.Join(",", names) + ")";
        var expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]>@_p_0 And " + condition;
        foreach (var current in new[] { compatible, compatible.Clone(), generated, generated.Clone() })
        {
            Assert.Equal(expected, current.ToSql());
            var parameters = ((ISqlCommonPartAccessor)current.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
            Assert.Equal(names.Length + 1, parameters.Count);
            Assert.Equal(1, parameters["@_p_0"]);
            for (var index = 0; index < names.Length; index++)
                Assert.Equal(values[index], parameters[names[index]]);
        }
    }

    /// <summary>对集合条件取反时仍保留 SQL 分组。</summary>
    [Fact]
    public void Where_WhenExplicitCollectionIsNegated_ShouldPreserveGrouping()
    {
        var query = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, int[]>(new[] { 2, 3 },
            (n, ids) => !ids.Contains(n.Id), "n");

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere Not ([n].[Id] In (@_p_0,@_p_1))";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(2, parameters["@_p_0"]);
        Assert.Equal(3, parameters["@_p_1"]);
    }

    /// <summary>List 实例的 Contains 与 Enumerable.Contains 使用相同的 IN 语义。</summary>
    [Fact]
    public void Where_WhenExplicitListContainsColumn_ShouldBindElements()
    {
        var query = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, List<int>>(new List<int> { 2, 3 },
            (n, ids) => ids.Contains(n.Id), "n");

        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id] In (@_p_0,@_p_1)", query.ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(2, parameters["@_p_0"]);
        Assert.Equal(3, parameters["@_p_1"]);
    }

    /// <summary>同一集合用于多个列时，每个条件都应保留独立参数和原有参数顺序。</summary>
    [Fact]
    public void Where_WhenCollectionIsUsedTwice_ShouldPreserveParameterIsolation()
    {
        var query = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, int[]>(new[] { 2, 3 },
            (n, ids) => n.Id > 1 && (Enumerable.Contains(ids, n.Id) || Enumerable.Contains(ids, n.ParentId)), "n");

        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]>@_p_0 And ([n].[Id] In (@_p_1,@_p_2) Or [n].[ParentId] In (@_p_3,@_p_4))", query.ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(new object[] { 1, 2, 3, 2, 3 }, Enumerable.Range(0, 5).Select(index => parameters["@_p_" + index]).ToArray());
    }

    /// <summary>关闭可选条件时不应增加 SQL 或参数。</summary>
    [Fact]
    public void WhereIf_WhenCollectionConditionIsDisabled_ShouldNotChangeQuery()
    {
        var query = Create();
        var result = query.WhereIf<SqlGeneratedQueryBridgeRegressionTest.Node, int[]>(false, null,
            (n, ids) => ids.Contains(n.Id), "n");

        Assert.Same(query, result);
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>数值转换不能被当作直接列，否则会改变表达式语义。</summary>
    [Fact]
    public void Where_WhenCollectionColumnIsConverted_ShouldRejectWithoutParameters()
    {
        var query = Create();
        Assert.Throws<NotSupportedException>(() => query.Where<SqlGeneratedQueryBridgeRegressionTest.Node, int[]>(
            new[] { 2, 3 }, (n, ids) => ids.Contains((int)(long)n.Id), "n"));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>集合元素应按实体字段的映射转换，克隆不应再次改变参数。</summary>
    [Fact]
    public void Where_WhenCollectionUsesMappedColumn_ShouldApplyFieldConverter()
    {
        var metadata = new TestEntityMetadata();
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
            entityMappingResolver: new DefaultEntityMappingResolver(metadata),
            sqlParameterFactory: new DefaultSqlParameterFactory(
                new DefaultFieldValueConverterSelector(new[] { new OffsetConverter() })));
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("s").Select<Sample>(s => s.IntValue, "s")
            .Where<Sample, int[]>(new[] { 7, 8 }, (s, ids) => ids.Contains(s.IntValue), "s");

        const string expected = "Select [s].[Sample_IntValue] \r\nFrom [as_Sample].[t_Sample] As [s] \r\nWhere [s].[Sample_IntValue] In (@_p_0,@_p_1)";
        foreach (var current in new[] { query, query.Clone() })
        {
            Assert.Equal(expected, current.ToSql());
            var parameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)current.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
            Assert.Equal(107, parameters["@_p_0"].Value);
            Assert.Equal(108, parameters["@_p_1"].Value);
            Assert.All(parameters.Values, parameter =>
            {
                Assert.Equal(nameof(Sample.IntValue), parameter.PropertyName);
                Assert.True(parameter.IsProviderValue);
            });
        }
    }

    /// <summary>后续条件失败时，集合条件产生的参数也应回滚。</summary>
    [Fact]
    public void Where_WhenLaterConditionIsUnsupported_ShouldRollbackCollectionParameters()
    {
        var query = Create();
        Assert.Throws<InvalidOperationException>(() => query.Where<SqlGeneratedQueryBridgeRegressionTest.Node, int[]>(
            new[] { 2, 3 }, (n, ids) => ids.Contains(n.Id) && n.Name.StartsWith("x", StringComparison.Ordinal), "n"));
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    private sealed class OffsetConverter : IFieldValueConverter
    {
        public bool CanConvert(ColumnMappingMetadata column, DatabaseContext context) => column?.PropertyName == nameof(Sample.IntValue);
        public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext context) => (int)value + 100;
        public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext context) => (int)value - 100;
    }
}
