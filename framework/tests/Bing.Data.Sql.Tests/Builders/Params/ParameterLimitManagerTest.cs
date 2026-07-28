using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>
/// 参数数量上限管理器测试。
/// </summary>
public class ParameterLimitManagerTest
{
    /// <summary>
    /// 测试 - 未超过上限时应允许添加和替换同名参数，Clear 后应重新计数。
    /// </summary>
    [Fact]
    public void Add_WhenWithinLimitOrReplacingExisting_ShouldPreserveCountAndAllowClear()
    {
        // Arrange
        var manager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 2, "TestProvider");

        // Act
        manager.Add("first", 1);
        manager.Add("second", 2);
        manager.Add("first", 3);
        var countBeforeClear = manager.GetParams().Count;
        var replacedValue = manager.GetValue("first");
        manager.Clear();
        manager.Add("third", 3);

        // Assert
        Assert.Equal(2, countBeforeClear);
        Assert.Equal(3, replacedValue);
        Assert.Single(manager.GetParams());
        Assert.Equal(3, manager.GetValue("third"));
    }

    /// <summary>
    /// 测试 - 超过参数上限时应保留源状态并报告 Provider 和完整计数上下文。
    /// </summary>
    [Fact]
    public void Add_WhenLimitExceeded_ShouldThrowWithProviderAndCountsWithoutMutatingState()
    {
        // Arrange
        var manager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 2, "TestProvider");
        manager.Add("first", 1);
        manager.Add("second", 2);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => manager.Add("third", 3));

        // Assert
        Assert.Contains("TestProvider", exception.Message);
        Assert.Contains("当前参数数量: 2", exception.Message);
        Assert.Contains("尝试添加后数量: 3", exception.Message);
        Assert.Contains("最大参数数量: 2", exception.Message);
        Assert.Equal(2, manager.GetParams().Count);
        Assert.False(manager.Contains("third"));
    }

    /// <summary>
    /// 测试 - Clone 和 CreateEmpty 应保留限制并与来源参数状态隔离。
    /// </summary>
    [Fact]
    public void CloneAndCreateEmpty_ShouldRetainLimitAndKeepParameterStateIsolated()
    {
        // Arrange
        var manager = new ParameterLimitManager(new ParameterManager(TestDialect.Instance), 2, "TestProvider");
        manager.Add("first", 1);

        // Act
        var clone = manager.Clone();
        clone.Add("second", 2);
        var empty = ((IParameterManagerLifecycle)manager).CreateEmpty();
        empty.Add("only", 1);
        empty.Add("another", 2);

        // Assert
        Assert.Single(manager.GetParams());
        Assert.Equal(2, clone.GetParams().Count);
        Assert.Equal(2, empty.GetParams().Count);
        Assert.Throws<InvalidOperationException>(() => clone.Add("third", 3));
        Assert.Throws<InvalidOperationException>(() => empty.Add("other", 3));
    }

    /// <summary>
    /// 测试 - 内部管理器 Clone 返回 null 或自身时应拒绝生成共享状态副本。
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Clone_WhenInnerManagerReturnsInvalidResult_ShouldThrow(bool returnNull)
    {
        // Arrange
        var inner = new InvalidCloneParameterManager(returnNull);
        var manager = new ParameterLimitManager(inner, 1, "TestProvider");

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => manager.Clone());
    }

    /// <summary>
    /// 为异常路径提供可控 Clone 结果的测试参数管理器。
    /// </summary>
    private sealed class InvalidCloneParameterManager : ParameterManager
    {
        private readonly bool _returnNull;

        public InvalidCloneParameterManager(bool returnNull) : base(TestDialect.Instance) => _returnNull = returnNull;

        public override IParameterManager Clone() => _returnNull ? null : this;
    }
}