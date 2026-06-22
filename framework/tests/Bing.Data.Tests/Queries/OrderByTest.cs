using Bing.Data.Queries;
using Shouldly;
using Xunit;

namespace Bing.Data.Tests;

/// <summary>
/// 测试目的：验证 <see cref="OrderByItem"/> 的属性存储与 <see cref="OrderByItem.Generate"/> 输出格式。
/// </summary>
public class OrderByItemTest
{
    // ── Generate（升序）────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：Desc=false 时，Generate 应返回属性名本身（不附加 " desc"）。
    /// </summary>
    [Fact]
    public void Generate_Asc_ShouldReturnNameOnly()
    {
        // Arrange
        var item = new OrderByItem("Name", false);

        // Act
        var result = item.Generate();

        // Assert
        result.ShouldBe("Name");
    }

    /// <summary>
    /// 测试目的：Desc=true 时，Generate 应返回 "{Name} desc"。
    /// </summary>
    [Fact]
    public void Generate_Desc_ShouldReturnNameWithDescSuffix()
    {
        // Arrange
        var item = new OrderByItem("CreationTime", true);

        // Act
        var result = item.Generate();

        // Assert
        result.ShouldBe("CreationTime desc");
    }

    // ── 属性读写 ───────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：构造后 Name 和 Desc 属性应与传入参数一致。
    /// </summary>
    [Fact]
    public void Properties_ShouldMatchConstructorArgs()
    {
        // Arrange & Act
        var item = new OrderByItem("Age", true);

        // Assert
        item.Name.ShouldBe("Age");
        item.Desc.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：通过属性修改 Name 和 Desc 后，Generate 应反映最新值。
    /// </summary>
    [Fact]
    public void Generate_AfterPropertyChange_ShouldReflectNewValues()
    {
        // Arrange
        var item = new OrderByItem("Name", false);

        // Act
        item.Name = "Score";
        item.Desc = true;

        // Assert
        item.Generate().ShouldBe("Score desc");
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 测试目的：验证 <see cref="OrderByBuilder"/> 的 Add / Generate 逻辑，
/// 涵盖空输入、升降序混合、多字段组合等场景。
/// </summary>
public class OrderByBuilderTest
{
    // ── 空输入 ────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：未 Add 任何项时，Generate 应返回 null 或空字符串，不抛异常。
    /// </summary>
    [Fact]
    public void Generate_WithNoItems_ShouldReturnNullOrEmpty()
    {
        // Arrange
        var builder = new OrderByBuilder();

        // Act
        var result = builder.Generate();

        // Assert
        (result == null || result == string.Empty).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：Add null 名称时应被忽略，Generate 结果应为空。
    /// </summary>
    [Fact]
    public void Add_NullName_ShouldBeIgnored()
    {
        // Arrange
        var builder = new OrderByBuilder();

        // Act
        builder.Add(null);
        var result = builder.Generate();

        // Assert
        (result == null || result == string.Empty).ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：Add 空白字符串时应被忽略，Generate 结果应为空。
    /// </summary>
    [Fact]
    public void Add_WhitespaceName_ShouldBeIgnored()
    {
        // Arrange
        var builder = new OrderByBuilder();

        // Act
        builder.Add("   ");
        var result = builder.Generate();

        // Assert
        (result == null || result == string.Empty).ShouldBeTrue();
    }

    // ── 单字段 ────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：单个升序字段，Generate 应返回该字段名。
    /// </summary>
    [Fact]
    public void Generate_SingleAscField_ShouldReturnName()
    {
        // Arrange
        var builder = new OrderByBuilder();
        builder.Add("Name");

        // Act
        var result = builder.Generate();

        // Assert
        result.ShouldBe("Name");
    }

    /// <summary>
    /// 测试目的：单个降序字段，Generate 应返回 "{Name} desc"。
    /// </summary>
    [Fact]
    public void Generate_SingleDescField_ShouldReturnNameDesc()
    {
        // Arrange
        var builder = new OrderByBuilder();
        builder.Add("CreationTime", true);

        // Act
        var result = builder.Generate();

        // Assert
        result.ShouldBe("CreationTime desc");
    }

    // ── 多字段 ────────────────────────────────────────────────────────

    /// <summary>
    /// 测试目的：多个字段（升降序混合）Generate 应用逗号分隔，顺序与 Add 顺序一致。
    /// </summary>
    [Fact]
    public void Generate_MultipleFields_ShouldJoinWithComma()
    {
        // Arrange
        var builder = new OrderByBuilder();
        builder.Add("Name");
        builder.Add("Age", true);
        builder.Add("CreationTime");

        // Act
        var result = builder.Generate();

        // Assert
        result.ShouldContain("Name");
        result.ShouldContain("Age desc");
        result.ShouldContain("CreationTime");
        // 确保顺序：Name 在 Age 前
        result.IndexOf("Name").ShouldBeLessThan(result.IndexOf("Age desc"));
    }

    /// <summary>
    /// 测试目的：Add 两个纯升序字段，Generate 应包含正确的逗号分隔符。
    /// </summary>
    [Fact]
    public void Generate_TwoAscFields_ShouldContainComma()
    {
        // Arrange
        var builder = new OrderByBuilder();
        builder.Add("Id");
        builder.Add("Name");

        // Act
        var result = builder.Generate();

        // Assert
        result.ShouldContain(",");
        result.ShouldContain("Id");
        result.ShouldContain("Name");
    }
}
