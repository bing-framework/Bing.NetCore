using System.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>验证参数恢复和删除不重新转换其他已保存的值。</summary>
public sealed class ParameterSnapshotContractTest
{
    [Fact]
    public void Restore_ShouldKeepConvertedValueAndMetadataWithoutAddingMissingParameters()
    {
        IParameterManager manager = new ParameterManager(new NonIdempotentDialect());
        manager.Add("value", "original");
        var snapshot = new SqlParam(":value", "frozen!", DbType.String)
        {
            OriginalValue = "frozen",
            IsProviderValue = true
        };

        manager.Restore(snapshot);
        manager.Restore(new SqlParam("missing", "ignored"));
        snapshot.Value = "changed";

        Assert.Equal("frozen!", manager.GetValue("value"));
        Assert.False(manager.Contains("missing"));
        var stored = ((IAdvancedParameterManager)manager).GetSqlParams()["@value"];
        Assert.Equal(DbType.String, stored.DbType);
        Assert.Equal("frozen", stored.OriginalValue);
        Assert.True(stored.IsProviderValue);
    }

    [Fact]
    public void Remove_ShouldPreserveOtherParametersAndReuseNames()
    {
        IParameterManager manager = new ParameterManager(new NonIdempotentDialect());
        var first = manager.GenerateName();
        manager.Add(first, "value");
        var page = manager.GenerateName();
        manager.Add(page, 10);

        Assert.True(manager.Remove(page));
        Assert.False(manager.Remove("missing"));

        Assert.Equal("value!", manager.GetValue(first));
        Assert.Equal(page, manager.GenerateName());
        Assert.Single(manager.GetParams());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LimitedManager_ShouldForwardRestoreAndRemoveWithoutBreakingLimits(bool advanced)
    {
        var inner = new ParameterManager(new NonIdempotentDialect());
        IParameterManager manager = advanced
            ? new AdvancedParameterLimitManager(inner, 1, "test")
            : new ParameterLimitManager(inner, 1, "test");
        manager.Add("value", "original");

        manager.Restore(new SqlParam("value", "snapshot!", DbType.String));
        Assert.Equal("snapshot!", manager.GetValue("value"));
        Assert.Throws<InvalidOperationException>(() => manager.Add("other", 1));
        Assert.True(manager.Remove("value"));
        manager.Add("other", 1);
        Assert.Equal(1, manager.Count);
        Assert.Equal(1, manager.GetValue("other"));
    }

    private sealed class NonIdempotentDialect : DialectBase
    {
        public override object GetParamValue(object value) => value is string text ? text + "!" : value;
    }
}
