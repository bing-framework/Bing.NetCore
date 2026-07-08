using Bing.Logging.ExtraSupports;
using Shouldly;
using Xunit;

namespace Bing.Logging.Tests.ExtraSupports;

/// <summary>
/// <see cref="ContextData"/> 单元测试
/// </summary>
public class ContextDataTest
{
    // ═══════════════════════════════════════════════════════════
    // AddItem
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AddItem 在 value 为 null 时应静默忽略，不向集合添加任何元素。
    /// </summary>
    [Fact]
    public void AddItem_WhenValueIsNull_ShouldNotAdd()
    {
        // Arrange
        var ctx = new ContextData();

        // Act
        ctx.AddItem("key", null!);

        // Assert
        ctx.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：AddItem 在 key 不存在时应正确添加一条新记录。
    /// </summary>
    [Fact]
    public void AddItem_WhenKeyNotExists_ShouldAddSuccessfully()
    {
        // Arrange
        var ctx = new ContextData();

        // Act
        ctx.AddItem("name", "Alice");

        // Assert
        ctx.Count.ShouldBe(1);
        ctx["name"].Value.ShouldBe("Alice");
    }

    /// <summary>
    /// 测试目的：AddItem 在 key 已存在时应抛出 ArgumentException，不允许重复键。
    /// </summary>
    [Fact]
    public void AddItem_WhenKeyAlreadyExists_ShouldThrowArgumentException()
    {
        // Arrange
        var ctx = new ContextData();
        ctx.AddItem("name", "Alice");

        // Act & Assert
        Should.Throw<ArgumentException>(() => ctx.AddItem("name", "Bob"));
    }

    /// <summary>
    /// 测试目的：AddItem 键大小写不敏感（OrdinalIgnoreCase），重复大小写不同的键也应抛出异常。
    /// </summary>
    [Fact]
    public void AddItem_IsCaseInsensitive_ShouldThrowOnDuplicateDifferentCase()
    {
        // Arrange
        var ctx = new ContextData();
        ctx.AddItem("Name", "Alice");

        // Act & Assert
        Should.Throw<ArgumentException>(() => ctx.AddItem("name", "Bob"));
    }

    /// <summary>
    /// 测试目的：AddItem 传入 ContextDataItem 值时，应直接以 item.Name 作为键插入。
    /// </summary>
    [Fact]
    public void AddItem_WhenValueIsContextDataItem_ShouldUseItemName()
    {
        // Arrange
        var ctx = new ContextData();
        var item = new ContextDataItem("itemKey", typeof(string), "itemValue");

        // Act
        ctx.AddItem("otherKey", item);

        // Assert
        ctx.ContainsKey("itemKey").ShouldBeTrue();
        ctx["itemKey"].Value.ShouldBe("itemValue");
    }

    // ═══════════════════════════════════════════════════════════
    // AddOrUpdateItem
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：AddOrUpdateItem 在 value 为 null 时应静默忽略，不修改集合。
    /// </summary>
    [Fact]
    public void AddOrUpdateItem_WhenValueIsNull_ShouldNotAdd()
    {
        // Arrange
        var ctx = new ContextData();

        // Act
        ctx.AddOrUpdateItem("key", null!);

        // Assert
        ctx.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：AddOrUpdateItem 在 name 为空白字符串时应静默忽略。
    /// </summary>
    [Fact]
    public void AddOrUpdateItem_WhenNameIsWhiteSpace_ShouldNotAdd()
    {
        // Arrange
        var ctx = new ContextData();

        // Act
        ctx.AddOrUpdateItem("   ", "value");

        // Assert
        ctx.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：AddOrUpdateItem 在键不存在时应新增。
    /// </summary>
    [Fact]
    public void AddOrUpdateItem_WhenKeyNotExists_ShouldAdd()
    {
        // Arrange
        var ctx = new ContextData();

        // Act
        ctx.AddOrUpdateItem("age", 30);

        // Assert
        ctx.ContainsKey("age").ShouldBeTrue();
        ctx["age"].Value.ShouldBe(30);
    }

    /// <summary>
    /// 测试目的：AddOrUpdateItem 在键已存在时应更新旧值，而不是抛出异常。
    /// </summary>
    [Fact]
    public void AddOrUpdateItem_WhenKeyExists_ShouldUpdateValue()
    {
        // Arrange
        var ctx = new ContextData();
        ctx.AddOrUpdateItem("status", "pending");

        // Act
        ctx.AddOrUpdateItem("status", "done");

        // Assert
        ctx["status"].Value.ShouldBe("done");
        ctx.Count.ShouldBe(1); // 仍然只有一条
    }

    // ═══════════════════════════════════════════════════════════
    // Copy（克隆）
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：Copy() 返回一个内容相同的新实例，修改副本不影响原始对象。
    /// </summary>
    [Fact]
    public void Copy_ShouldReturnIndependentCopy()
    {
        // Arrange
        var ctx = new ContextData();
        ctx.AddItem("x", 1);

        // Act
        var copy = ctx.Copy();
        copy.AddOrUpdateItem("x", 99); // 修改副本

        // Assert
        ctx["x"].Value.ShouldBe(1);   // 原始对象不受影响
        copy["x"].Value.ShouldBe(99);
    }

    // ═══════════════════════════════════════════════════════════
    // ToString
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：空 ContextData 的 ToString() 应返回空字符串，不是 "[]"。
    /// </summary>
    [Fact]
    public void ToString_WhenEmpty_ShouldReturnEmptyString()
    {
        // Arrange
        var ctx = new ContextData();

        // Act
        var result = ctx.ToString();

        // Assert
        result.ShouldBe(string.Empty);
    }

    /// <summary>
    /// 测试目的：有可输出项时，ToString() 结果应以 "[" 开头、"]" 结尾，并包含键名。
    /// </summary>
    [Fact]
    public void ToString_WhenHasOutputItems_ShouldContainKeyName()
    {
        // Arrange
        var ctx = new ContextData();
        ctx.AddItem("userId", "user-001");

        // Act
        var result = ctx.ToString();

        // Assert
        result.ShouldStartWith("[");
        result.ShouldEndWith("]");
        result.ShouldContain("userId");
    }

    /// <summary>
    /// 测试目的：output=false 的条目不应出现在 ToString() 结果中。
    /// </summary>
    [Fact]
    public void ToString_WhenItemOutputIsFalse_ShouldNotIncludeItem()
    {
        // Arrange
        var ctx = new ContextData();
        ctx.AddItem("secret", "password", output: false);

        // Act
        var result = ctx.ToString();

        // Assert
        // 所有条目 output=false → 结果为空字符串或只有括号
        result.ShouldNotContain("secret");
    }
}
