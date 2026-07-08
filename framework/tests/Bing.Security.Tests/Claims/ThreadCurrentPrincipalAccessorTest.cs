using System.Security.Claims;
using Bing.Security.Claims;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Claims;

/// <summary>
/// <see cref="ThreadCurrentPrincipalAccessor"/> 单元测试
/// </summary>
public class ThreadCurrentPrincipalAccessorTest
{
    // ═══════════════════════════════════════════════════════════
    // Principal 获取
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：当 Thread.CurrentPrincipal 为有效 ClaimsPrincipal 时，Principal 应返回该主体。
    /// </summary>
    [Fact]
    public void Principal_WhenThreadCurrentPrincipalSet_ShouldReturnIt()
    {
        // Arrange
        var original = Thread.CurrentPrincipal;
        try
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test-user") }, "test");
            var principal = new ClaimsPrincipal(identity);
            Thread.CurrentPrincipal = principal;

            var accessor = new ThreadCurrentPrincipalAccessor();

            // Act
            var result = accessor.Principal;

            // Assert
            result.ShouldNotBeNull();
            result.Identity!.Name.ShouldBe("test-user");
        }
        finally
        {
            Thread.CurrentPrincipal = original;
        }
    }

    /// <summary>
    /// 测试目的：当 Thread.CurrentPrincipal 为 null 时，Principal 应返回 null，而不抛异常。
    /// </summary>
    [Fact]
    public void Principal_WhenThreadCurrentPrincipalIsNull_ShouldReturnNull()
    {
        // Arrange
        var original = Thread.CurrentPrincipal;
        try
        {
            Thread.CurrentPrincipal = null;
            var accessor = new ThreadCurrentPrincipalAccessor();

            // Act
            var result = accessor.Principal;

            // Assert
            result.ShouldBeNull();
        }
        finally
        {
            Thread.CurrentPrincipal = original;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Change() 作用域切换
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：调用 Change() 后，Principal 应返回新传入的主体（作用域内覆盖）。
    /// </summary>
    [Fact]
    public void Change_WithNewPrincipal_ShouldReturnNewPrincipal()
    {
        // Arrange
        var original = Thread.CurrentPrincipal;
        try
        {
            Thread.CurrentPrincipal = null;
            var accessor = new ThreadCurrentPrincipalAccessor();

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "new-user") }, "override");
            var newPrincipal = new ClaimsPrincipal(identity);

            // Act
            using (accessor.Change(newPrincipal))
            {
                var inScope = accessor.Principal;

                // Assert
                inScope.ShouldNotBeNull();
                inScope.Identity!.Name.ShouldBe("new-user");
            }
        }
        finally
        {
            Thread.CurrentPrincipal = original;
        }
    }

    /// <summary>
    /// 测试目的：Change() Dispose 后，Principal 应恢复到变更前的值（作用域退出后回滚）。
    /// </summary>
    [Fact]
    public void Change_AfterDispose_ShouldRestorePreviousPrincipal()
    {
        // Arrange
        var original = Thread.CurrentPrincipal;
        try
        {
            Thread.CurrentPrincipal = null;
            var accessor = new ThreadCurrentPrincipalAccessor();

            var newIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "temp-user") }, "temp");
            var newPrincipal = new ClaimsPrincipal(newIdentity);

            // Act
            using (accessor.Change(newPrincipal))
            {
                // scope：Principal = newPrincipal
            }

            var afterDispose = accessor.Principal;

            // Assert
            // Thread.CurrentPrincipal is null, AsyncLocal was reset → null
            afterDispose.ShouldBeNull();
        }
        finally
        {
            Thread.CurrentPrincipal = original;
        }
    }

    /// <summary>
    /// 测试目的：Change() 可嵌套使用，最内层作用域返回最新设置的主体，退出后逐层恢复。
    /// </summary>
    [Fact]
    public void Change_Nested_ShouldRestoreEachLayer()
    {
        // Arrange
        var original = Thread.CurrentPrincipal;
        try
        {
            Thread.CurrentPrincipal = null;
            var accessor = new ThreadCurrentPrincipalAccessor();

            var outer = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "outer") }, "a"));
            var inner = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "inner") }, "b"));

            // Act & Assert
            using (accessor.Change(outer))
            {
                accessor.Principal!.Identity!.Name.ShouldBe("outer");

                using (accessor.Change(inner))
                {
                    accessor.Principal!.Identity!.Name.ShouldBe("inner");
                }

                accessor.Principal!.Identity!.Name.ShouldBe("outer");
            }
        }
        finally
        {
            Thread.CurrentPrincipal = original;
        }
    }
}
