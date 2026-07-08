using System.Security.Claims;
using Shouldly;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// ScopedDictionary Scoped 生命周期字典测试
/// </summary>
public class ScopedDictionaryTest
{
    // ==================== 基本字典操作 ====================

    /// <summary>
    /// 测试目的：新建字典后为空，DataAuthValidRoleNames 默认为空数组。
    /// </summary>
    [Fact]
    public void NewInstance_IsEmpty_And_DefaultRoleNamesIsEmpty()
    {
        // Arrange & Act
        var dict = new ScopedDictionary();

        // Assert
        dict.Count.ShouldBe(0);
        dict.DataAuthValidRoleNames.ShouldNotBeNull();
        dict.DataAuthValidRoleNames.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：添加键值对后，可以通过键正确读取值。
    /// </summary>
    [Fact]
    public void AddKeyValue_CanBeRetrievedByKey()
    {
        // Arrange
        var dict = new ScopedDictionary();

        // Act
        dict["name"] = "Alice";
        dict["age"] = 30;

        // Assert
        dict["name"].ShouldBe("Alice");
        dict["age"].ShouldBe(30);
    }

    /// <summary>
    /// 测试目的：TryGetValue 对存在的键返回 true 和正确值；不存在的键返回 false。
    /// </summary>
    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrueAndValue()
    {
        // Arrange
        var dict = new ScopedDictionary();
        dict["key"] = "value";

        // Act
        var found = dict.TryGetValue("key", out var value);
        var notFound = dict.TryGetValue("missing", out var missing);

        // Assert
        found.ShouldBeTrue();
        value.ShouldBe("value");
        notFound.ShouldBeFalse();
        missing.ShouldBeNull();
    }

    // ==================== Identity 属性 ====================

    /// <summary>
    /// 测试目的：Identity 属性默认为 null，可以被设置。
    /// </summary>
    [Fact]
    public void Identity_DefaultIsNull_CanBeSet()
    {
        // Arrange
        var dict = new ScopedDictionary();
        var identity = new ClaimsIdentity("test");

        // Assert before
        dict.Identity.ShouldBeNull();

        // Act
        dict.Identity = identity;

        // Assert after
        dict.Identity.ShouldBeSameAs(identity);
    }

    // ==================== DataAuthValidRoleNames ====================

    /// <summary>
    /// 测试目的：DataAuthValidRoleNames 可以被设置，并保存正确值。
    /// </summary>
    [Fact]
    public void DataAuthValidRoleNames_CanBeSet()
    {
        // Arrange
        var dict = new ScopedDictionary();
        var roles = new[] { "Admin", "Manager" };

        // Act
        dict.DataAuthValidRoleNames = roles;

        // Assert
        dict.DataAuthValidRoleNames.ShouldBe(roles);
    }

    // ==================== Dispose ====================

    /// <summary>
    /// 测试目的：Dispose 后所有键值对应被清空（Count = 0）。
    /// </summary>
    [Fact]
    public void Dispose_ClearsAllItems()
    {
        // Arrange
        var dict = new ScopedDictionary();
        dict["a"] = 1;
        dict["b"] = 2;

        // Act
        dict.Dispose();

        // Assert
        dict.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：Dispose 后 Identity 应被清为 null。
    /// </summary>
    [Fact]
    public void Dispose_ClearsIdentity()
    {
        // Arrange
        var dict = new ScopedDictionary();
        dict.Identity = new ClaimsIdentity("test");

        // Act
        dict.Dispose();

        // Assert
        dict.Identity.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：多次 Dispose 不应抛出异常（幂等性）。
    /// </summary>
    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        // Arrange
        var dict = new ScopedDictionary();
        dict["key"] = "value";

        // Act & Assert
        Should.NotThrow(() =>
        {
            dict.Dispose();
            dict.Dispose();
        });
    }
}
