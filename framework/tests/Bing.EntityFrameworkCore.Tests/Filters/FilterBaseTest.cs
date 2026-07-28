using System.Linq.Expressions;
using Bing.Data.Filters;

namespace Bing.EntityFrameworkCore.Tests.Filters;

/// <summary>
/// <see cref="FilterBase{TFilterType}"/> 单元测试。
/// </summary>
public class FilterBaseTest
{
    /// <summary>
    /// 测试目的：禁用过滤器后应保持禁用，直到最外层禁用作用域释放。
    /// </summary>
    [Fact]
    public void Disable_WhenNestedScopesDisposed_ShouldRestoreOnlyAfterOuterScope()
    {
        // Arrange
        var filter = new TestFilter();

        // Act
        using var outerScope = filter.Disable();
        using var innerScope = filter.Disable();

        // Assert
        Assert.False(filter.IsEnabled);

        // Act
        innerScope.Dispose();

        // Assert
        Assert.False(filter.IsEnabled);

        // Act
        outerScope.Dispose();

        // Assert
        Assert.True(filter.IsEnabled);
    }

    /// <summary>
    /// 测试目的：过滤器类型可赋值给实体时应启用实体过滤，否则不应启用。
    /// </summary>
    [Fact]
    public void IsEntityEnabled_WhenEntityImplementsFilterContract_ShouldReturnExpectedValue()
    {
        // Arrange
        var filter = new TestFilter();

        // Act and Assert
        Assert.True(filter.IsEntityEnabled<FilteredEntity>());
        Assert.False(filter.IsEntityEnabled<UnfilteredEntity>());
    }

    /// <summary>
    /// 测试过滤器。
    /// </summary>
    private sealed class TestFilter : FilterBase<ITestFilterMarker>
    {
        /// <inheritdoc />
        public override Expression<Func<TEntity, bool>> GetExpression<TEntity>() => entity => true;
    }

    /// <summary>
    /// 测试过滤器标记接口。
    /// </summary>
    private interface ITestFilterMarker
    {
    }

    /// <summary>
    /// 应被过滤的测试实体。
    /// </summary>
    private sealed class FilteredEntity : ITestFilterMarker
    {
    }

    /// <summary>
    /// 不应被过滤的测试实体。
    /// </summary>
    private sealed class UnfilteredEntity
    {
    }
}