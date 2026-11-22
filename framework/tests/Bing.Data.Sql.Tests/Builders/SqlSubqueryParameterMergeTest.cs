using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Xunit.Abstractions;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>子查询参数预检的增量分配、命名及失败原子性合同。</summary>
public sealed class SqlSubqueryParameterMergeTest(ITestOutputHelper output)
{
    [Fact]
    public void WideRender_With128Children_ShouldAvoidCopyingAllAccumulatedParameters()
    {
        var builder = new TestSqlBuilder();
        builder.Select("Id").From("Rows").Where("Enabled", true);
        for (var i = 0; i < 128; i++)
        {
            var child = new TestSqlBuilder();
            child.Select("Id").From("Rows").Where("Depth", i);
            builder.With("wide" + i.ToString("D3"), child);
        }
        var expected = builder.ToSql();
        for (var i = 0; i < 4; i++) builder.ToSql();
        const int iterations = 8;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < iterations; i++) Assert.Equal(expected, builder.ToSql());
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
        output.WriteLine($"128-child repeat render: {bytes} B/op");
        Assert.InRange(bytes, 0, 2000000);
        Assert.Equal(129, builder.GetParams().Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_WithSameAndDifferentValues_ShouldKeepNamesMetadataAndSql(bool limited)
    {
        IParameterManager manager = new ParameterManager(TestDialect.Instance);
        if (limited) manager = new AdvancedParameterLimitManager((IAdvancedParameterManager)manager, 4, "test");
        manager.Add("id", 1);
        manager.Add("_p_0", 99);
        var target = new TestSqlBuilder(parameterManager: manager);
        var sourceManager = new ParameterManager(TestDialect.Instance);
        sourceManager.Add(new SqlParam("id", 2, System.Data.DbType.Int32) { IsProviderValue = true });
        sourceManager.Add("_p_0", 99);
        var source = new TestSqlBuilder(parameterManager: sourceManager);

        var sql = target.MergeSubqueryParameters(source, "Select @id, @_p_0, '@id' -- @id");

        Assert.Equal("Select @_p_1, @_p_0, '@id' -- @id", sql);
        Assert.Equal(new object[] { 1, 99, 2 }, manager.GetParams().Values.ToArray());
        var parameter = ((IAdvancedParameterManager)manager).GetSqlParams()["@_p_1"];
        Assert.Equal(System.Data.DbType.Int32, parameter.DbType);
        Assert.True(parameter.IsProviderValue);
        Assert.Equal("Select @_p_1, @_p_0, '@id' -- @id",
            target.MergeSubqueryParameters(source, "Select @id, @_p_0, '@id' -- @id"));
        Assert.Equal(3, manager.Count);
        Assert.Equal(2, sourceManager.GetValue("id"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_WhenLaterParameterExceedsLimit_ShouldKeepTargetAndNameSequenceUnchanged(bool advanced)
    {
        var inner = new ParameterManager(TestDialect.Instance);
        IParameterManager manager = advanced
            ? new AdvancedParameterLimitManager(inner, 2, "test")
            : new ParameterLimitManager(inner, 2, "test");
        manager.Add("id", 1);
        var target = new TestSqlBuilder(parameterManager: manager);
        var sourceManager = new ParameterManager(TestDialect.Instance);
        sourceManager.Add("id", 2);
        sourceManager.Add("other", 3);
        var source = new TestSqlBuilder(parameterManager: sourceManager);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            target.MergeSubqueryParameters(source, "Select @id, @other"));

        Assert.Equal("SQL Provider 'test' 的参数数量超出上限。当前参数数量: 2；尝试添加后数量: 3；最大参数数量: 2。", exception.Message);
        Assert.Equal(new object[] { 1 }, manager.GetParams().Values.ToArray());
        Assert.Equal("@_p_0", manager.GenerateName());
        var validManager = new ParameterManager(TestDialect.Instance);
        validManager.Add("id", 2);
        Assert.Equal("Select @_p_1", target.MergeSubqueryParameters(
            new TestSqlBuilder(parameterManager: validManager), "Select @id"));
        Assert.Equal(new object[] { 1, 2 }, manager.GetParams().Values.ToArray());
    }

    [Theory]
    [InlineData(1, 2, "outer")]
    [InlineData(2, 1, "inner")]
    [InlineData(1, 1, "outer")]
    public void Merge_WithNestedLimits_ShouldPreserveFirstApplicableLimit(int outerLimit, int innerLimit, string provider)
    {
        var inner = new ParameterManager(TestDialect.Instance);
        var manager = new ParameterLimitManager(new AdvancedParameterLimitManager(inner, innerLimit, "inner"), outerLimit, "outer");
        manager.Add("id", 1);
        var target = new TestSqlBuilder(parameterManager: manager);
        var sourceManager = new ParameterManager(TestDialect.Instance);
        sourceManager.Add("other", 2);

        var exception = Assert.Throws<InvalidOperationException>(() => target.MergeSubqueryParameters(
            new TestSqlBuilder(parameterManager: sourceManager), "Select @other"));

        Assert.Equal($"SQL Provider '{provider}' 的参数数量超出上限。当前参数数量: 1；尝试添加后数量: 2；最大参数数量: 1。", exception.Message);
        Assert.Single(manager.GetParams());
    }

    [Fact]
    public void Merge_WhenLaterDialectConversionFails_ShouldNotPublishEarlierParameters()
    {
        var manager = new ParameterManager(new FailingDialect());
        manager.Add("existing", 1);
        var source = new ParameterManager(TestDialect.Instance);
        source.Add("first", 2);
        source.Add("second", 3);
        var target = new TestSqlBuilder(parameterManager: manager);

        var exception = Assert.Throws<InvalidOperationException>(() => target.MergeSubqueryParameters(
            new TestSqlBuilder(parameterManager: source), "Select @first, @second"));

        Assert.Equal("conversion", exception.Message);
        Assert.Equal(new object[] { 1 }, manager.GetParams().Values.ToArray());
        Assert.Equal("@_p_0", manager.GenerateName());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_WithCustomManager_ShouldRetainClonePreflight(bool limited)
    {
        var custom = new CustomManager();
        IParameterManager manager = limited ? new AdvancedParameterLimitManager(custom, 3, "custom") : custom;
        manager.Add("id", 1);
        var source = new ParameterManager(TestDialect.Instance);
        source.Add("id", 2);

        Assert.Equal("Select @_p_0", new TestSqlBuilder(parameterManager: manager).MergeSubqueryParameters(
            new TestSqlBuilder(parameterManager: source), "Select @id"));
        Assert.Equal(1, custom.CloneCalls);
        Assert.Equal(new object[] { 1, 2 }, manager.GetParams().Values.ToArray());
    }

    private sealed class CustomManager() : ParameterManager(TestDialect.Instance)
    {
        internal int CloneCalls;
        public override IParameterManager Clone() { CloneCalls++; return base.Clone(); }
    }

    /// <summary>预检和正式写入各转换一次新增值，重复合并不转换已有值；基础视图不提升元数据。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_WithLimitView_ShouldPreserveConversionCountAndMetadataCapability(bool advanced)
    {
        var dialect = new CountingDialect();
        var inner = new ParameterManager(dialect);
        IParameterManager manager = advanced
            ? new AdvancedParameterLimitManager(inner, 4, "test")
            : new ParameterLimitManager(inner, 4, "test");
        manager.Add("same", 1);
        manager.Add("collision", 2);
        dialect.ConversionCount = 0;
        var source = new ParameterManager(TestDialect.Instance);
        source.Add("same", 1);
        source.Add(new SqlParam("collision", 3, System.Data.DbType.Int32));
        source.Add("extra", 4);
        var sourceBuilder = new TestSqlBuilder(parameterManager: source);
        var target = new TestSqlBuilder(parameterManager: manager);

        Assert.Equal("Select @same, @_p_0, @extra",
            target.MergeSubqueryParameters(sourceBuilder, "Select @same, @collision, @extra"));
        Assert.Equal(4, dialect.ConversionCount);
        Assert.Equal(new object[] { 1, 2, 3, 4 }, manager.GetParams().Values.ToArray());
        Assert.Equal(advanced ? System.Data.DbType.Int32 : (System.Data.DbType?)null,
            inner.GetSqlParams()["@_p_0"].DbType);
        Assert.Equal("Select @same, @_p_0, @extra",
            target.MergeSubqueryParameters(sourceBuilder, "Select @same, @collision, @extra"));
        Assert.Equal(4, dialect.ConversionCount);
        Assert.Equal(new object[] { 1, 3, 4 }, source.GetParams().Values.ToArray());
    }

    private sealed class CountingDialect : Bing.Data.Sql.Builders.Core.DialectBase
    {
        internal int ConversionCount;
        public override char OpeningIdentifier => '[';
        public override char ClosingIdentifier => ']';
        public override string GetPrefix() => "@";
        public override object GetParamValue(object value) { ConversionCount++; return value; }
    }

    private sealed class FailingDialect : Bing.Data.Sql.Builders.Core.DialectBase
    {
        public override char OpeningIdentifier => '[';
        public override char ClosingIdentifier => ']';
        public override string GetPrefix() => "@";
        public override object GetParamValue(object value) => Equals(value, 3)
            ? throw new InvalidOperationException("conversion") : value;
    }
}
