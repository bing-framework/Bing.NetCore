using Shouldly;
using Xunit;

namespace Bing.TextTemplating;

/// <summary>
/// 测试目的：验证 TemplateDefinition 的构造行为、属性读写及流式 API 正确性。
/// </summary>
public class TemplateDefinitionTest
{
    // =====================================================================
    //  Constructor & readonly properties
    // =====================================================================

    /// <summary>
    /// 测试目的：构造时 Name 被正确赋值，IsLayout 默认为 false。
    /// </summary>
    [Fact]
    public void Constructor_WithNameOnly_ShouldSetNameAndDefaultIsLayout()
    {
        // Arrange & Act
        var def = new TemplateDefinition("invoice");

        // Assert
        def.Name.ShouldBe("invoice");
        def.IsLayout.ShouldBeFalse();
        def.Layout.ShouldBeNull();
        def.RenderEngine.ShouldBeNull();
        def.Properties.ShouldNotBeNull();
        def.Properties.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：构造时指定 isLayout=true，IsLayout 应为 true。
    /// </summary>
    [Fact]
    public void Constructor_WithIsLayoutTrue_ShouldSetIsLayoutTrue()
    {
        // Arrange & Act
        var def = new TemplateDefinition("base-layout", isLayout: true);

        // Assert
        def.IsLayout.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：构造时指定 layout 名称，Layout 属性应正确读取。
    /// </summary>
    [Fact]
    public void Constructor_WithLayout_ShouldSetLayout()
    {
        // Arrange & Act
        var def = new TemplateDefinition("report", layout: "base-layout");

        // Assert
        def.Layout.ShouldBe("base-layout");
    }

    // =====================================================================
    //  Mutable properties
    // =====================================================================

    /// <summary>
    /// 测试目的：Layout 属性支持赋值后再读取正确值。
    /// </summary>
    [Fact]
    public void Layout_SetValue_ShouldReturnSameValue()
    {
        // Arrange
        var def = new TemplateDefinition("email");

        // Act
        def.Layout = "base";

        // Assert
        def.Layout.ShouldBe("base");
    }

    /// <summary>
    /// 测试目的：RenderEngine 属性支持赋值后再读取正确值。
    /// </summary>
    [Fact]
    public void RenderEngine_SetValue_ShouldReturnSameValue()
    {
        // Arrange
        var def = new TemplateDefinition("sms");

        // Act
        def.RenderEngine = "Liquid";

        // Assert
        def.RenderEngine.ShouldBe("Liquid");
    }

    // =====================================================================
    //  Indexer & Properties dictionary
    // =====================================================================

    /// <summary>
    /// 测试目的：通过索引器写入属性后，可用索引器及 Properties 字典读取。
    /// </summary>
    [Fact]
    public void Indexer_SetAndGet_ShouldStoreInDictionary()
    {
        // Arrange
        var def = new TemplateDefinition("report");

        // Act
        def["subject"] = "Monthly Report";

        // Assert
        def["subject"].ShouldBe("Monthly Report");
        def.Properties["subject"].ShouldBe("Monthly Report");
    }

    /// <summary>
    /// 测试目的：索引器读取不存在的 key 时应返回 null（GetOrDefault 语义）。
    /// </summary>
    [Fact]
    public void Indexer_GetNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var def = new TemplateDefinition("report");

        // Act
        var value = def["nonexistent"];

        // Assert
        value.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：多次写入同一 key，应以最新值覆盖。
    /// </summary>
    [Fact]
    public void Indexer_Overwrite_ShouldUseLatestValue()
    {
        // Arrange
        var def = new TemplateDefinition("report");
        def["key"] = "first";

        // Act
        def["key"] = "second";

        // Assert
        def["key"].ShouldBe("second");
        def.Properties.Count.ShouldBe(1);
    }

    // =====================================================================
    //  WithProperty fluent API
    // =====================================================================

    /// <summary>
    /// 测试目的：WithProperty 应将属性存入字典并返回同一实例（支持链式调用）。
    /// </summary>
    [Fact]
    public void WithProperty_ShouldStoreValueAndReturnSameInstance()
    {
        // Arrange
        var def = new TemplateDefinition("report");

        // Act
        var result = def.WithProperty("author", "Bing");

        // Assert
        result.ShouldBeSameAs(def);
        def.Properties["author"].ShouldBe("Bing");
    }

    /// <summary>
    /// 测试目的：WithProperty 支持链式调用写入多个属性。
    /// </summary>
    [Fact]
    public void WithProperty_Chained_ShouldStoreAllProperties()
    {
        // Arrange
        var def = new TemplateDefinition("report");

        // Act
        def.WithProperty("a", 1).WithProperty("b", 2).WithProperty("c", 3);

        // Assert
        def.Properties.Count.ShouldBe(3);
        def.Properties["a"].ShouldBe(1);
        def.Properties["b"].ShouldBe(2);
        def.Properties["c"].ShouldBe(3);
    }

    // =====================================================================
    //  WithRenderEngine fluent API
    // =====================================================================

    /// <summary>
    /// 测试目的：WithRenderEngine 应设置 RenderEngine 并返回同一实例。
    /// </summary>
    [Fact]
    public void WithRenderEngine_ShouldSetRenderEngineAndReturnSameInstance()
    {
        // Arrange
        var def = new TemplateDefinition("report");

        // Act
        var result = def.WithRenderEngine("Scriban");

        // Assert
        result.ShouldBeSameAs(def);
        def.RenderEngine.ShouldBe("Scriban");
    }

    /// <summary>
    /// 测试目的：WithRenderEngine 可与 WithProperty 链式组合使用。
    /// </summary>
    [Fact]
    public void WithRenderEngine_Chained_WithProperty_ShouldSetBoth()
    {
        // Arrange
        var def = new TemplateDefinition("report");

        // Act
        def.WithRenderEngine("Liquid").WithProperty("version", "2.0");

        // Assert
        def.RenderEngine.ShouldBe("Liquid");
        def.Properties["version"].ShouldBe("2.0");
    }
}
