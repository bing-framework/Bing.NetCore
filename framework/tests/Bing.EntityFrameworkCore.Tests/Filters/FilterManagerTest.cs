using Bing.Data.Filters;

namespace Bing.EntityFrameworkCore.Tests.Filters;

/// <summary>
/// <see cref="FilterManager"/> 单元测试。
/// </summary>
public class FilterManagerTest
{
    /// <summary>
    /// 测试目的：未注册过滤器时应返回 null，查询启用状态应返回 false。
    /// </summary>
    [Fact]
    public void GetFilterAndIsEnabled_WhenFilterServiceMissing_ShouldReturnNullAndFalse()
    {
        // Arrange
        using var provider = new ServiceCollection().BuildServiceProvider();
        var manager = new FilterManager(provider);

        // Act
        var filter = manager.GetFilter<ITestFilterMarker>();
        var isEnabled = manager.IsEnabled<ITestFilterMarker>();

        // Assert
        Assert.Null(filter);
        Assert.False(isEnabled);
    }

    /// <summary>
    /// 测试目的：注册过滤器时应缓存解析结果，并支持禁用和重新启用。
    /// </summary>
    [Fact]
    public void EnableAndDisableFilter_WhenRegistered_ShouldChangeFilterState()
    {
        // Arrange
        var filter = new TestFilter();
        var services = new ServiceCollection();
        services.AddSingleton<IFilter<ITestFilterMarker>>(filter);
        using var provider = services.BuildServiceProvider();
        var manager = new FilterManager(provider);

        // Act
        var first = manager.GetFilter<ITestFilterMarker>();
        var second = manager.GetFilter<ITestFilterMarker>();
        using var scope = manager.DisableFilter<ITestFilterMarker>();

        // Assert
        Assert.Same(first, second);
        Assert.False(manager.IsEnabled<ITestFilterMarker>());

        // Act
        scope.Dispose();

        // Assert
        Assert.True(manager.IsEnabled<ITestFilterMarker>());
    }

    /// <summary>
    /// 测试过滤器。
    /// </summary>
    private sealed class TestFilter : FilterBase<ITestFilterMarker>
    {
        /// <inheritdoc />
        public override System.Linq.Expressions.Expression<Func<TEntity, bool>> GetExpression<TEntity>() => entity => true;
    }

    /// <summary>
    /// 测试过滤器标记接口。
    /// </summary>
    private interface ITestFilterMarker
    {
    }
}