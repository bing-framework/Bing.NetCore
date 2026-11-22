using System.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests;

/// <summary>内置参数描述符身份与逻辑键的关联契约。</summary>
public sealed class SqlFragmentParameterBindingTest
{
    /// <summary>克隆共享只读描述符，同值替换只使修改过的副本失效。</summary>
    [Fact]
    public void IsCurrent_WhenCloneReplacesSameValue_ShouldOnlyInvalidateClone()
    {
        var manager = new ParameterManager(TestDialect.Instance);
        manager.Add(new SqlParam("id", 7, DbType.Int32));
        var binding = new SqlFragmentParameterBinding(new SqlParameterKey("request", "id"), manager, "id");
        var clone = (IAdvancedParameterManager)manager.Clone();

        Assert.True(binding.IsCurrent(manager, "@ID"));
        Assert.True(binding.IsCurrent(clone, "id"));
        clone.Add(new SqlParam("id", 7, DbType.Int32));

        Assert.True(binding.IsCurrent(manager, "id"));
        Assert.False(binding.IsCurrent(clone, "id"));
    }

    /// <summary>参数限额失败不会删除原键，清除并重加相同值则使原键失效。</summary>
    [Fact]
    public void IsCurrent_WhenLimitedManagerClearsAndReusesName_ShouldInvalidateBinding()
    {
        var manager = new AdvancedParameterLimitManager(new ParameterManager(TestDialect.Instance), 1, "test");
        manager.Add(new SqlParam("id", 7, DbType.Int32));
        var binding = new SqlFragmentParameterBinding(new SqlParameterKey("request", "id"), manager, "id");

        Assert.Throws<InvalidOperationException>(() => manager.Add("other", 3));
        Assert.True(binding.IsCurrent(manager, "id"));
        manager.Clear();
        manager.Add(new SqlParam("id", 7, DbType.Int32));

        Assert.False(binding.IsCurrent(manager, "id"));
    }
}
