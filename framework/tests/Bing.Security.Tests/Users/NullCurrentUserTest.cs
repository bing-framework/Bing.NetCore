using Bing.Users;
using Shouldly;
using Xunit;

namespace Bing.Security.Tests.Users;

/// <summary>
/// <see cref="NullCurrentUser"/> 单元测试
/// </summary>
public class NullCurrentUserTest
{
    private readonly ICurrentUser _user = NullCurrentUser.Instance;

    /// <summary>
    /// 测试目的：NullCurrentUser 应始终报告未认证状态。
    /// </summary>
    [Fact]
    public void IsAuthenticated_ShouldBeFalse()
    {
        // Assert
        _user.IsAuthenticated.ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：NullCurrentUser 的 UserId 应返回空字符串，而不是 null。
    /// </summary>
    [Fact]
    public void UserId_ShouldReturnEmptyString()
    {
        _user.UserId.ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：NullCurrentUser 的 UserName 应返回空字符串。
    /// </summary>
    [Fact]
    public void UserName_ShouldReturnEmptyString()
    {
        _user.UserName.ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：NullCurrentUser 的 TenantId 应返回空字符串，不为 null（避免调用方 NullReferenceException）。
    /// </summary>
    [Fact]
    public void TenantId_ShouldReturnEmptyString()
    {
        _user.TenantId.ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：NullCurrentUser 的 Roles 应返回空数组，不为 null。
    /// </summary>
    [Fact]
    public void Roles_ShouldReturnEmptyArray_NotNull()
    {
        _user.Roles.ShouldNotBeNull();
        _user.Roles.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：FindClaim 对任意类型应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void FindClaim_WithAnyType_ShouldReturnNull()
    {
        _user.FindClaim("any_claim_type").ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：FindClaims 对任意类型应返回空数组，不为 null。
    /// </summary>
    [Fact]
    public void FindClaims_WithAnyType_ShouldReturnEmptyArray()
    {
        var claims = _user.FindClaims("any_claim_type");
        claims.ShouldNotBeNull();
        claims.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：GetAllClaims 应返回空数组，不为 null。
    /// </summary>
    [Fact]
    public void GetAllClaims_ShouldReturnEmptyArray()
    {
        var claims = _user.GetAllClaims();
        claims.ShouldNotBeNull();
        claims.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：IsInRole 对任何角色名都应返回 false。
    /// </summary>
    [Fact]
    public void IsInRole_WithAnyRole_ShouldReturnFalse()
    {
        _user.IsInRole("admin").ShouldBeFalse();
        _user.IsInRole("user").ShouldBeFalse();
        _user.IsInRole(string.Empty).ShouldBeFalse();
    }

    /// <summary>
    /// 测试目的：NullCurrentUser.Instance 应为单例，多次访问返回同一实例。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSingleton()
    {
        ReferenceEquals(NullCurrentUser.Instance, NullCurrentUser.Instance).ShouldBeTrue();
    }
}
