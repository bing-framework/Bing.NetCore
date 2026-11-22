using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>验证显式传值的复合条件在兼容路径中的行为。</summary>
public class SqlLambdaExplicitPredicateTest
{
    private static SqlLambdaQuery Create() =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<SqlGeneratedQueryBridgeRegressionTest.Node>("n")
            .Select<SqlGeneratedQueryBridgeRegressionTest.Node>(n => n.Id);

    /// <summary>相同的显式值应绑定到各条件，保持分组和克隆行为。</summary>
    [Fact]
    public void Where_WhenValueIsUsedTwice_ShouldBindBothOperands()
    {
        var query = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, int>(
            7, (n, id) => n.Id == id || n.ParentId == id, "n");

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere ([n].[Id]=@_p_0 Or [n].[ParentId]=@_p_1)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(2, parameters.Count);
        Assert.Equal(7, parameters["@_p_0"]);
        Assert.Equal(7, parameters["@_p_1"]);
    }

    /// <summary>兼容路径应按每个字段的映射创建参数，与生成路径保持一致。</summary>
    [Fact]
    public void Where_WhenValuesUseMappedColumns_ShouldPreserveParameterMetadata()
    {
        var metadata = new TestEntityMetadata();
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
            entityMappingResolver: new DefaultEntityMappingResolver(metadata),
            sqlParameterFactory: new DefaultSqlParameterFactory(new DefaultFieldValueConverterSelector()));
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("s").Select<Sample>(s => s.IntValue, "s")
            .Where<Sample, int>(7, (s, value) => s.IntValue == value || s.IntValue > value, "s");

        Assert.Equal("Select [s].[Sample_IntValue] \r\nFrom [as_Sample].[t_Sample] As [s] \r\nWhere ([s].[Sample_IntValue]=@_p_0 Or [s].[Sample_IntValue]>@_p_1)", query.ToSql());
        var parameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
        Assert.Equal(2, parameters.Count);
        Assert.All(parameters.Values, parameter =>
        {
            Assert.Equal(7, parameter.Value);
            Assert.Equal(typeof(Sample), parameter.EntityType);
            Assert.Equal(nameof(Sample.IntValue), parameter.PropertyName);
            Assert.Equal("Sample_IntValue", parameter.ColumnName);
        });
    }

    /// <summary>值处于任一侧时都应应用该列的值转换器，且克隆时不再次转换。</summary>
    [Fact]
    public void Where_WhenValueIsOnEitherSide_ShouldUseMappedConverterOnce()
    {
        var metadata = new TestEntityMetadata();
        var selector = new DefaultFieldValueConverterSelector(new[] { new OffsetIntConverter() });
        var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
            entityMappingResolver: new DefaultEntityMappingResolver(metadata),
            sqlParameterFactory: new DefaultSqlParameterFactory(selector));
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
            .From<Sample>("s").Select<Sample>(s => s.IntValue, "s")
            .Where<Sample, int>(7, (s, value) => value < s.IntValue || s.IntValue == value, "s");

        const string expected = "Select [s].[Sample_IntValue] \r\nFrom [as_Sample].[t_Sample] As [s] \r\nWhere (@_p_0<[s].[Sample_IntValue] Or [s].[Sample_IntValue]=@_p_1)";
        Assert.Equal(expected, query.ToSql());
        var clone = query.Clone();
        Assert.Equal(expected, clone.ToSql());
        foreach (var current in new[] { query, clone })
        {
            var parameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)current.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
            Assert.Equal(2, parameters.Count);
            Assert.All(parameters.Values, parameter =>
            {
                Assert.Equal(107, parameter.Value);
                Assert.Equal(7, parameter.OriginalValue);
                Assert.True(parameter.IsProviderValue);
            });
        }
    }

    private sealed class OffsetIntConverter : IFieldValueConverter
    {
        public bool CanConvert(ColumnMappingMetadata column, DatabaseContext databaseContext) =>
            column?.PropertyName == nameof(Sample.IntValue);
        public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) => (int)value + 100;
        public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) => (int)value - 100;
    }

    /// <summary>枚举显式值在生成与兼容路径应以相同类型传给字段转换器。</summary>
    [Fact]
    public void Where_WhenValueIsEnum_ShouldUseSameConverterInputAsGeneratedPredicate()
    {
        var metadata = new TestEntityMetadata();
        SqlLambdaQuery CreateMapped()
        {
            var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
                entityMappingResolver: new DefaultEntityMappingResolver(metadata),
                sqlParameterFactory: new DefaultSqlParameterFactory(
                    new DefaultFieldValueConverterSelector(new[] { new EnumNameConverter() })));
            return SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
                .From<EnumRow>("e").Select<EnumRow>(row => row.State, "e");
        }

        var compatible = CreateMapped().Where<EnumRow, RowState>(RowState.Ready,
            (row, value) => row.State == value, "e");
        var generated = CreateMapped();
        var column = new SqlGeneratedColumn(0, nameof(EnumRow.State), null, typeof(RowState), null, false);
        SqlGeneratedQueryBridge.WherePredicate(generated, new[] { typeof(EnumRow) }, new[] { "row" },
            new[] { "e" }, SqlGeneratedPredicate.CompareParameter(column, Operator.Equal), RowState.Ready);

        const string expected = "Select [e].[EnumRow_State] \r\nFrom [as_EnumRow].[t_EnumRow] As [e] \r\nWhere [e].[EnumRow_State]=@_p_0";
        Assert.Equal(expected, compatible.ToSql());
        Assert.Equal(expected, generated.ToSql());
        var compatibleParameter = Assert.Single(((IAdvancedParameterManager)((ISqlCommonPartAccessor)compatible.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams()).Value;
        var generatedParameter = Assert.Single(((IAdvancedParameterManager)((ISqlCommonPartAccessor)generated.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams()).Value;
        Assert.Equal(generatedParameter.OriginalValue, compatibleParameter.OriginalValue);
        Assert.Equal(generatedParameter.Value, compatibleParameter.Value);
        Assert.Equal(nameof(RowState.Ready), generatedParameter.Value);
    }

    public enum RowState { Ready = 1 }
    public sealed class EnumRow { public RowState State { get; set; } }

    private sealed class EnumNameConverter : IFieldValueConverter
    {
        public bool CanConvert(ColumnMappingMetadata column, DatabaseContext databaseContext) =>
            column?.ClrType == typeof(RowState);
        public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) =>
            value is RowState state ? state.ToString() : $"unexpected:{value.GetType().Name}";
        public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) => value;
    }

    /// <summary>关闭条件不访问 Builder，开启后应使用本次传入的值。</summary>
    [Fact]
    public void WhereIf_WhenEnabledOrDisabled_ShouldKeepRequestValueIsolated()
    {
        var untouched = SqlQueryRuntimeFactory.CreateLambdaQuery(
            new Mock<ISqlQueryPlanExecutor>().Object, new Mock<ISqlBuilder>(MockBehavior.Strict).Object);
        Assert.Same(untouched, untouched.WhereIf<SqlGeneratedQueryBridgeRegressionTest.Node, int>(false,
            100, (n, id) => n.Id == id || n.ParentId == id));

        var query = Create().WhereIf<SqlGeneratedQueryBridgeRegressionTest.Node, int>(true,
            3, (n, id) => n.Id == id || n.ParentId == id, "n");
        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere ([n].[Id]=@_p_0 Or [n].[ParentId]=@_p_1)", query.ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(3, parameters["@_p_0"]);
        Assert.Equal(3, parameters["@_p_1"]);
    }

    /// <summary>空值应使用 SQL 空值比较，无需在 SQL 中插入参数。</summary>
    [Fact]
    public void Where_WhenValueIsNull_ShouldUseNullComparison()
    {
        var query = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, string>(
            null, (n, value) => n.Name == value || n.Name != value, "n");

        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere ([n].[Name] Is Null Or [n].[Name] Is Not Null)", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>可空值类型应保留值及空值的比较语义。</summary>
    [Theory]
    [InlineData(7)]
    [InlineData(null)]
    public void Where_WhenNullableValueIsSupplied_ShouldBindWithoutClosure(int? value)
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<Sample>("s").Select<Sample>(s => s.NullableIntValue)
            .Where<Sample, int?>(value, (s, current) => s.NullableIntValue == current, "s");

        var suffix = value.HasValue ? "[s].[NullableIntValue]=@_p_0" : "[s].[NullableIntValue] Is Null";
        Assert.Equal("Select [s].[NullableIntValue] \r\nFrom [Sample] As [s] \r\nWhere " + suffix, query.ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        if (value.HasValue)
            Assert.Equal(value.Value, Assert.Single(parameters).Value);
        else
            Assert.Empty(parameters);
    }

    /// <summary>空值位于左侧时仍应生成正确的 SQL 空值比较。</summary>
    [Fact]
    public void Where_WhenNullableNullIsOnLeft_ShouldUseNullComparison()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<Sample>("s").Select<Sample>(s => s.NullableIntValue)
            .Where<Sample, int?>(null, (s, value) => value == s.NullableIntValue || value != s.NullableIntValue, "s");

        Assert.Equal("Select [s].[NullableIntValue] \r\nFrom [Sample] As [s] \r\nWhere ([s].[NullableIntValue] Is Null Or [s].[NullableIntValue] Is Not Null)", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>空值大小比较应与已有参数化条件一致，在变更查询前报错。</summary>
    [Fact]
    public void Where_WhenNullableRelationalValueIsNull_ShouldThrowWithoutChangingQuery()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<Sample>("s").Select<Sample>(s => s.NullableIntValue);

        Assert.Throws<ArgumentNullException>(() => query.Where<Sample, int?>(null,
            (s, value) => s.NullableIntValue == 5 || value < s.NullableIntValue, "s"));
        Assert.Equal("Select [s].[NullableIntValue] \r\nFrom [Sample] As [s]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>显式字符串匹配在兼容路径和生成路径应使用相同参数与 SQL。</summary>
    [Theory]
    [InlineData("dat", "%dat%", "dat%", "%dat")]
    [InlineData("", "", "", "")]
    [InlineData("  ", "  ", "  ", "  ")]
    [InlineData(null, null, null, null)]
    public void Where_WhenExplicitStringMatchesAreCombined_ShouldMatchGeneratedPath(
        string value, string contains, string starts, string ends)
    {
        var query = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, string>(value,
            (n, text) => n.Name.Contains(text) || n.Name.StartsWith(text) || n.Name.EndsWith(text), "n");
        var generated = Create();
        var name = new SqlGeneratedColumn(0, "Name", null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.Or(
                SqlGeneratedPredicate.CompareParameter(name, Operator.Contains),
                SqlGeneratedPredicate.CompareParameter(name, Operator.Starts)),
            SqlGeneratedPredicate.CompareParameter(name, Operator.Ends));
        SqlGeneratedQueryBridge.WherePredicate(generated,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, new[] { "n" }, predicate, value);

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere (([n].[Name] Like @_p_0 Or [n].[Name] Like @_p_1) Or [n].[Name] Like @_p_2)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        Assert.Equal(expected, generated.ToSql());
        foreach (var current in new[] { query, generated })
        {
            var parameters = ((ISqlCommonPartAccessor)current.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
            Assert.Equal(3, parameters.Count);
            Assert.Equal(contains, parameters["@_p_0"]);
            Assert.Equal(starts, parameters["@_p_1"]);
            Assert.Equal(ends, parameters["@_p_2"]);
        }
    }

    /// <summary>取反字符串条件应与生成路径一致，并在克隆后保留列绑定。</summary>
    [Fact]
    public void Where_WhenExplicitStringMatchIsNegated_ShouldMatchGeneratedPath()
    {
        var query = Create().Where<SqlGeneratedQueryBridgeRegressionTest.Node, string>("abc",
            (n, text) => n.Id > 1 && !n.Name.StartsWith(text), "n");
        var generated = Create();
        var id = new SqlGeneratedColumn(0, "Id", null, typeof(int), null, false);
        var name = new SqlGeneratedColumn(0, "Name", null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.And(
            SqlGeneratedPredicate.Compare(id, null, Operator.Greater, 1),
            SqlGeneratedPredicate.Not(SqlGeneratedPredicate.CompareParameter(name, Operator.Starts)));
        SqlGeneratedQueryBridge.WherePredicate(generated,
            new[] { typeof(SqlGeneratedQueryBridgeRegressionTest.Node) }, new[] { "n" }, new[] { "n" }, predicate, "abc");

        const string expected = "Select [n].[Id] \r\nFrom [Nodes] As [n] \r\nWhere [n].[Id]>@_p_0 And Not ([n].[Name] Like @_p_1)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        Assert.Equal(expected, generated.ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal(1, parameters["@_p_0"]);
        Assert.Equal("abc%", parameters["@_p_1"]);
    }

    /// <summary>模糊匹配的通配符和转换器应在生成与兼容路径各执行一次。</summary>
    [Fact]
    public void Where_WhenExplicitStringMatchUsesMappedColumn_ShouldApplyConverterOnce()
    {
        var metadata = new TestEntityMetadata();
        SqlLambdaQuery CreateMapped()
        {
            var builder = new TestSqlBuilder(TestDialect.Instance, metadata,
                entityMappingResolver: new DefaultEntityMappingResolver(metadata),
                sqlParameterFactory: new DefaultSqlParameterFactory(
                    new DefaultFieldValueConverterSelector(new[] { new PrefixStringConverter() })));
            return SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder)
                .From<Sample>("s").Select<Sample>(s => s.StringValue, "s");
        }

        var query = CreateMapped().Where<Sample, string>("ab",
            (s, value) => s.StringValue.Contains(value) || s.StringValue.EndsWith(value), "s");
        var generated = CreateMapped();
        var column = new SqlGeneratedColumn(0, nameof(Sample.StringValue), null, typeof(string), null, false);
        var predicate = SqlGeneratedPredicate.Or(
            SqlGeneratedPredicate.CompareParameter(column, Operator.Contains),
            SqlGeneratedPredicate.CompareParameter(column, Operator.Ends));
        SqlGeneratedQueryBridge.WherePredicate(generated, new[] { typeof(Sample) }, new[] { "s" }, new[] { "s" }, predicate, "ab");

        const string expected = "Select [s].[Sample_StringValue] \r\nFrom [as_Sample].[t_Sample] As [s] \r\nWhere ([s].[Sample_StringValue] Like @_p_0 Or [s].[Sample_StringValue] Like @_p_1)";
        foreach (var current in new[] { query, query.Clone(), generated, generated.Clone() })
        {
            Assert.Equal(expected, current.ToSql());
            var parameters = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)current.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams();
            Assert.Equal(2, parameters.Count);
            Assert.Equal("converted:%ab%", parameters["@_p_0"].Value);
            Assert.Equal("converted:%ab", parameters["@_p_1"].Value);
            Assert.All(parameters.Values, parameter =>
            {
                Assert.Equal(typeof(Sample), parameter.EntityType);
                Assert.Equal(nameof(Sample.StringValue), parameter.PropertyName);
                Assert.Equal("Sample_StringValue", parameter.ColumnName);
            });
        }
    }

    /// <summary>后续不支持的字符串方法失败时，不应保留前面已经解析的参数。</summary>
    [Fact]
    public void Where_WhenLaterStringMatchIsUnsupported_ShouldRollback()
    {
        var query = Create();

        Assert.Throws<InvalidOperationException>(() => query.Where<SqlGeneratedQueryBridgeRegressionTest.Node, string>("ab",
            (n, value) => n.Name.Contains(value) || n.Name.StartsWith(value, StringComparison.OrdinalIgnoreCase), "n"));

        Assert.Equal("Select [n].[Id] \r\nFrom [Nodes] As [n]", query.ToSql());
        Assert.Empty(((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams());
    }

    /// <summary>多个同类型来源的字符串列应分别绑定到指定别名。</summary>
    [Fact]
    public void Where_WhenMultipleSourcesUseStringMatches_ShouldBindEachAlias()
    {
        var query = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder())
            .From<SqlGeneratedQueryBridgeRegressionTest.Node>("a")
            .Join<SqlGeneratedQueryBridgeRegressionTest.Node, SqlGeneratedQueryBridgeRegressionTest.Node>(
                (a, b) => a.ParentId == b.Id, "b")
            .Select<SqlGeneratedQueryBridgeRegressionTest.Node>(a => a.Id, "a")
            .Where<SqlGeneratedQueryBridgeRegressionTest.Node, SqlGeneratedQueryBridgeRegressionTest.Node>(
                (a, b) => a.Name.Contains("one") || b.Name.StartsWith("two"), "a", "b");

        const string expected = "Select [a].[Id] \r\nFrom [Nodes] As [a] \r\nJoin [Nodes] As [b] On [a].[ParentId]=[b].[Id] \r\nWhere ([a].[Name] Like @_p_0 Or [b].[Name] Like @_p_1)";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
        var parameters = ((ISqlCommonPartAccessor)query.GeneratedCore.ReadBuilder).ParameterManager.GetParams();
        Assert.Equal("%one%", parameters["@_p_0"]);
        Assert.Equal("two%", parameters["@_p_1"]);
    }

    private sealed class PrefixStringConverter : IFieldValueConverter
    {
        public bool CanConvert(ColumnMappingMetadata column, DatabaseContext databaseContext) =>
            column?.PropertyName == nameof(Sample.StringValue);
        public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) => $"converted:{value}";
        public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext databaseContext) => value;
    }
}
