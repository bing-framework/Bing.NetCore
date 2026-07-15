using System.Security.Claims;
using Bing.Clients;
using Bing.Security.Claims;
using Moq;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Clients;

/// <summary>
/// <see cref="CurrentClient"/> 单元测试
/// </summary>
public class CurrentClientTest
{
    // ═══════════════════════════════════════════════════════════
    // Id / IsAuthenticated
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：当 Principal 为 null 时，Id 应返回 null，IsAuthenticated 应为 false。
    /// </summary>
    [Fact]
    public void Id_WhenPrincipalIsNull_ShouldReturnNull()
    {
        // Arrange
        var mockAccessor = new Mock<ICurrentPrincipalAccessor>();
        mockAccessor.Setup(a => a.Principal).Returns((ClaimsPrincipal)null);
        var client = new CurrentClient(mockAccessor.Object);

        // Act & Assert
        client.Id.ShouldBeNull();
        client.IsAuthenticated.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：当 Principal 不包含 ClientId claim 时，Id 应返回 null，IsAuthenticated 应为 false。
    /// </summary>
    [Fact]
    public void Id_WhenPrincipalHasNoClientIdClaim_ShouldReturnNull()
    {
        // Arrange
        var mockAccessor = new Mock<ICurrentPrincipalAccessor>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "user1") }, "test"));
        mockAccessor.Setup(a => a.Principal).Returns(principal);
        var client = new CurrentClient(mockAccessor.Object);

        // Act & Assert
        client.Id.ShouldBeNull();
        client.IsAuthenticated.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：当 Principal 包含有效 ClientId claim 时，Id 应返回该值，IsAuthenticated 应为 true。
    /// </summary>
    [Fact]
    public void Id_WhenPrincipalHasClientIdClaim_ShouldReturnClientId()
    {
        // Arrange
        var mockAccessor = new Mock<ICurrentPrincipalAccessor>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(BingClaimTypes.ClientId, "my-client") }, "test"));
        mockAccessor.Setup(a => a.Principal).Returns(principal);
        var client = new CurrentClient(mockAccessor.Object);

        // Act & Assert
        client.Id.ShouldBe("my-client");
        client.IsAuthenticated.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：当 ClientId claim 值为空字符串时，Id 应返回 null，IsAuthenticated 应为 false。
    /// </summary>
    [Fact]
    public void Id_WhenClientIdClaimIsEmpty_ShouldReturnNull()
    {
        // Arrange
        var mockAccessor = new Mock<ICurrentPrincipalAccessor>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(BingClaimTypes.ClientId, string.Empty) }, "test"));
        mockAccessor.Setup(a => a.Principal).Returns(principal);
        var client = new CurrentClient(mockAccessor.Object);

        // Act & Assert
        client.Id.ShouldBeNull();
        client.IsAuthenticated.ShouldBeFalse();
    }
}
