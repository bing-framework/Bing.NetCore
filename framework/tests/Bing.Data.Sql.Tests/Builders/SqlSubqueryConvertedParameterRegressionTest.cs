using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using System.Data;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>验证嵌套 Builder 合并参数时不重复执行方言值转换。</summary>
public sealed class SqlSubqueryConvertedParameterRegressionTest
{
    /// <summary>From 子查询参数已由来源 Builder 转换，合并后应保持原值。</summary>
    [Fact]
    public void From_WhenMergingBuilder_ShouldNotReconvertItsParameter()
    {
        var dialect = new NonIdempotentDialect();
        var child = new TestSqlBuilder(dialect).From("Child").Where("Name", "one");
        var parent = new TestSqlBuilder(dialect).From(child, "child");

        Assert.Equal("Select * \r\nFrom (Select * \r\nFrom [Child] \r\nWhere [Name]=@_p_0) As [child]", parent.ToSql());
        Assert.Equal("one!", parent.GetParam("@_p_0"));
    }

    /// <summary>Union 分支参数已由来源 Builder 转换，合并后应保持原值。</summary>
    [Fact]
    public void Union_WhenMergingBuilder_ShouldNotReconvertItsParameter()
    {
        var dialect = new NonIdempotentDialect();
        var branch = new TestSqlBuilder(dialect).Select("Id").From("Archive").Where("Name", "one");
        var parent = new TestSqlBuilder(dialect).Select("Id").From("Current").Union(branch);

        Assert.Equal("(Select [Id] \r\nFrom [Current] \r\n) \r\nUnion \r\n(Select [Id] \r\nFrom [Archive] \r\nWhere [Name]=@_p_0 \r\n)", parent.ToSql());
        Assert.Equal("one!", parent.GetParam("@_p_0"));
    }

    /// <summary>CTE 合并在重复渲染时保留同方言的参数快照。</summary>
    [Fact]
    public void Cte_WhenMergingBuilder_ShouldNotReconvertItsParameter()
    {
        var dialect = new NonIdempotentDialect();
        var child = new TestSqlBuilder(dialect).Select("Id").From("Child").Where("Name", "one");
        var parent = new TestSqlBuilder(dialect).Select("Id").From("active").With("active", child);
        const string sql = "With [active] \r\nAs (Select [Id] \r\nFrom [Child] \r\nWhere [Name]=@_p_0)\r\nSelect [Id] \r\nFrom [active]";

        Assert.Equal(sql, parent.ToSql());
        Assert.Equal(sql, parent.ToSql());
        Assert.Equal("one!", parent.GetParam("@_p_0"));
    }

    /// <summary>原始 SQL 片段的 CLR 参数由目标 Builder 首次转换一次。</summary>
    [Fact]
    public void From_WhenMergingRawFragment_ShouldConvertItsParameterOnce()
    {
        var dialect = new NonIdempotentDialect();
        var fragment = new SqlQueryFragment("Select @Name", new[] { new SqlParam("Name", "one") });
        var parent = new TestSqlBuilder(dialect).From(fragment, "raw");

        Assert.Equal("Select * \r\nFrom (Select @Name) As [raw]", parent.ToSql());
        Assert.Equal("one!", parent.GetParam("@Name"));
    }

    /// <summary>同名冲突重命名、重复渲染和元数据均保留已转换的快照。</summary>
    [Fact]
    public void From_WhenParameterConflicts_ShouldRenameWithoutReconversion()
    {
        var dialect = new NonIdempotentDialect();
        var manager = new ParameterManager(dialect);
        manager.Add(new SqlParam("id", "child", DbType.String, size: 30));
        var child = new TestSqlBuilder(dialect, parameterManager: manager)
            .Select("Id").From("Child").AppendWhere("[Id]=@id");
        var parent = new TestSqlBuilder(dialect).AddParam("id", "parent").From(child, "child");

        const string sql = "Select * \r\nFrom (Select [Id] \r\nFrom [Child] \r\nWhere [Id]=@_p_0) As [child]";
        Assert.Equal(sql, parent.ToSql());
        Assert.Equal(sql, parent.ToSql());
        Assert.Equal("parent!", parent.GetParam("@id"));
        Assert.Equal("child!", parent.GetParam("@_p_0"));
        Assert.Equal("child!", child.GetParam("@id"));
        var parameter = ((IAdvancedParameterManager)((ISqlCommonPartAccessor)parent).ParameterManager)
            .GetSqlParams()["@_p_0"];
        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal(30, parameter.Size);
    }

    /// <summary>相同方言的多个嵌套层不会放大转换次数。</summary>
    [Fact]
    public void From_WhenNestedTwice_ShouldKeepConvertedValue()
    {
        var dialect = new NonIdempotentDialect();
        var child = new TestSqlBuilder(dialect).From("Child").Where("Name", "one");
        var middle = new TestSqlBuilder(dialect).From(child, "c");
        var parent = new TestSqlBuilder(dialect).From(middle, "m");

        Assert.Equal("Select * \r\nFrom (Select * \r\nFrom (Select * \r\nFrom [Child] \r\nWhere [Name]=@_p_0) As [c]) As [m]", parent.ToSql());
        Assert.Equal("one!", parent.GetParam("@_p_0"));
    }

    /// <summary>不同方言上下文仍执行目标方言转换。</summary>
    [Fact]
    public void From_WhenDialectContextDiffers_ShouldUseTargetConversion()
    {
        var child = new TestSqlBuilder(new NonIdempotentDialect()).From("Child").Where("Name", "one");
        var parent = new TestSqlBuilder(new NonIdempotentDialect()).From(child, "child");

        Assert.Equal("Select * \r\nFrom (Select * \r\nFrom [Child] \r\nWhere [Name]=@_p_0) As [child]", parent.ToSql());
        Assert.Equal("one!!", parent.GetParam("@_p_0"));
    }

    /// <summary>注入的参数管理器可以使用与 Builder 不同的方言上下文。</summary>
    [Fact]
    public void From_WhenInjectedManagerUsesDifferentDialect_ShouldUseManagerConversion()
    {
        var builderDialect = new NonIdempotentDialect();
        var targetDialect = new NonIdempotentDialect();
        var child = new TestSqlBuilder(builderDialect).From("Child").Where("Name", "one");
        var parent = new TestSqlBuilder(builderDialect, parameterManager: new ParameterManager(targetDialect))
            .From(child, "child");

        Assert.Equal("Select * \r\nFrom (Select * \r\nFrom [Child] \r\nWhere [Name]=@_p_0) As [child]", parent.ToSql());
        Assert.Equal("one!!", parent.GetParam("@_p_0"));
    }

    /// <summary>预检阶段不应重新执行同一方言的转换。</summary>
    [Fact]
    public void From_WhenSharingDialect_ShouldNotRepeatConversionDuringPreflight()
    {
        var dialect = new NonIdempotentDialect();
        var child = new TestSqlBuilder(dialect).From("Child").Where("Name", "one");
        var parent = new TestSqlBuilder(dialect).From(child, "child");
        _ = parent.ToSql();

        Assert.Equal(1, dialect.ConversionCount);
    }

    /// <summary>快照仍经过每层参数上限检查，普通视图不意外提升元数据。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_WithLimitedManager_ShouldPreserveSnapshotAndEnforceLimit(bool advanced)
    {
        var dialect = new NonIdempotentDialect();
        var inner = new ParameterManager(dialect);
        IParameterManager target = advanced
            ? new AdvancedParameterLimitManager(inner, 1, "test")
            : new ParameterLimitManager(inner, 1, "test");
        var source = new ParameterManager(dialect);
        source.Add(new SqlParam("name", "one", DbType.String));
        var parent = new TestSqlBuilder(dialect, parameterManager: target);
        var child = new TestSqlBuilder(dialect, parameterManager: source);

        Assert.Equal("Select @name", parent.MergeSubqueryParameters(child, "Select @name"));
        Assert.Equal("one!", target.GetValue("name"));
        Assert.Equal(advanced ? DbType.String : (DbType?)null, inner.GetSqlParams()["@name"].DbType);
        Assert.Equal(1, dialect.ConversionCount);

        var second = new ParameterManager(dialect);
        second.Add("other", "two");
        Assert.Throws<InvalidOperationException>(() => parent.MergeSubqueryParameters(
            new TestSqlBuilder(dialect, parameterManager: second), "Select @other"));
        Assert.Single(target.GetParams());
        Assert.Equal("one!", target.GetValue("name"));
    }

    /// <summary>嵌套限额按最严格的上限预检，失败时不写入任何参数。</summary>
    [Theory]
    [InlineData(1, 2, "outer")]
    [InlineData(2, 1, "inner")]
    public void Merge_WithNestedLimits_ShouldRejectAtomically(int outerLimit, int innerLimit, string provider)
    {
        var dialect = new NonIdempotentDialect();
        IParameterManager target = new ParameterLimitManager(
            new ParameterLimitManager(new ParameterManager(dialect), innerLimit, "inner"), outerLimit, "outer");
        var source = new ParameterManager(dialect);
        source.Add("first", "one");
        source.Add("second", "two");
        var parent = new TestSqlBuilder(dialect, parameterManager: target);

        var error = Assert.Throws<InvalidOperationException>(() => parent.MergeSubqueryParameters(
            new TestSqlBuilder(dialect, parameterManager: source), "Select @first, @second"));
        Assert.Contains(provider, error.Message);
        Assert.Empty(target.GetParams());
    }

    private sealed class NonIdempotentDialect : DialectBase
    {
        public int ConversionCount { get; private set; }
        public override char OpeningIdentifier => '[';
        public override char ClosingIdentifier => ']';
        public override string GetPrefix() => "@";
        public override object GetParamValue(object value)
        {
            ConversionCount++;
            return value is string text ? text + "!" : value;
        }
    }
}
