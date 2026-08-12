using Bing.Data.Filters;
using Xunit;

namespace Bing.Data.Tests.Filters;

/// <summary>
/// <see cref="DataFilter"/> 异步作用域状态单元测试。
/// </summary>
public class DataFilterTest
{
    /// <summary>
    /// 测试 - 未设置覆盖时，过滤器应默认启用。
    /// </summary>
    [Fact]
    public void IsEnabled_WhenNoScopeExists_ShouldReturnTrue()
    {
        // Arrange
        var filter = new DataFilter();

        // Act
        var result = filter.IsEnabled<TestFilter>();

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// 测试 - 嵌套禁用和启用作用域释放后，应按最近有效覆盖恢复状态。
    /// </summary>
    [Fact]
    public void Enable_WhenNestedAfterDisable_ShouldRestoreOuterStateOnDispose()
    {
        // Arrange
        var filter = new DataFilter();

        // Act and Assert
        using (filter.Disable<TestFilter>())
        {
            Assert.False(filter.IsEnabled<TestFilter>());
            using (filter.Enable<TestFilter>())
                Assert.True(filter.IsEnabled<TestFilter>());
            Assert.False(filter.IsEnabled<TestFilter>());
        }
        Assert.True(filter.IsEnabled<TestFilter>());
    }

    /// <summary>
    /// 测试 - 同类型作用域以非 LIFO 顺序释放时，只应移除自身覆盖。
    /// </summary>
    [Fact]
    public void Disable_WhenScopesDisposedOutOfOrder_ShouldKeepLatestRemainingOverride()
    {
        // Arrange
        var filter = new DataFilter();
        var disabled = filter.Disable<TestFilter>();
        var enabled = filter.Enable<TestFilter>();

        // Act
        disabled.Dispose();

        // Assert
        Assert.True(filter.IsEnabled<TestFilter>());
        enabled.Dispose();
        Assert.True(filter.IsEnabled<TestFilter>());
    }

    /// <summary>
    /// 测试 - using 作用域因异常退出时，过滤状态必须恢复默认值。
    /// </summary>
    [Fact]
    public void Disable_WhenScopeExitsWithException_ShouldRestoreState()
    {
        // Arrange
        var filter = new DataFilter();

        // Act
        Assert.Throws<InvalidOperationException>(() => ThrowFromDisabledScope(filter));

        // Assert
        Assert.True(filter.IsEnabled<TestFilter>());
    }

    /// <summary>
    /// 测试 - 禁用作用域经过 await 后仍应在同一异步执行流中生效。
    /// </summary>
    [Fact]
    public async Task Disable_WhenAwaited_ShouldFlowWithinCurrentAsyncExecution()
    {
        // Arrange
        var filter = new DataFilter();

        // Act and Assert
        using (filter.Disable<TestFilter>())
        {
            await Task.Yield();
            Assert.False(filter.IsEnabled<TestFilter>());
        }
        Assert.True(filter.IsEnabled<TestFilter>());
    }

    /// <summary>
    /// 测试 - 并行异步执行流中的过滤覆盖必须相互隔离。
    /// </summary>
    [Fact]
    public async Task Disable_WhenParallelTasksUseDifferentScopes_ShouldKeepStatesIsolated()
    {
        // Arrange
        var filter = new DataFilter();

        // Act
        var disabled = Task.Run(async () =>
        {
            using (filter.Disable<TestFilter>())
            {
                await Task.Yield();
                return filter.IsEnabled<TestFilter>();
            }
        });
        var enabled = Task.Run(async () =>
        {
            using (filter.Enable<TestFilter>())
            {
                await Task.Yield();
                return filter.IsEnabled<TestFilter>();
            }
        });
        var results = await Task.WhenAll(disabled, enabled);

        // Assert
        Assert.Equal(new[] { false, true }, results);
        Assert.True(filter.IsEnabled<TestFilter>());
    }

    /// <summary>
    /// 用于隔离过滤状态的测试标识类型。
    /// </summary>
    private sealed class TestFilter
    {
    }

    /// <summary>
    /// 在禁用作用域中抛出同步异常，用于验证释放行为。
    /// </summary>
    /// <param name="filter">待验证的过滤状态管理器。</param>
    private static void ThrowFromDisabledScope(DataFilter filter)
    {
        using (filter.Disable<TestFilter>())
        {
            Assert.False(filter.IsEnabled<TestFilter>());
            throw new InvalidOperationException();
        }
    }
}
