using Shouldly;
using Xunit;

namespace Bing.TextTemplating;

// =========================================================================
//  TemplateDefinitionContext Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 TemplateDefinitionContext 的增删查全逻辑。
/// </summary>
public class TemplateDefinitionContextTest
{
    private static TemplateDefinitionContext CreateEmpty() =>
        new(new Dictionary<string, TemplateDefinition>());

    // -----------------------------------------------------------------
    //  GetOrNull
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：空上下文 GetOrNull 应返回 null，不抛异常。
    /// </summary>
    [Fact]
    public void GetOrNull_EmptyContext_ShouldReturnNull()
    {
        var ctx = CreateEmpty();
        ctx.GetOrNull("invoice").ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：Add 后可通过 GetOrNull 按名称找到对应定义。
    /// </summary>
    [Fact]
    public void GetOrNull_AfterAdd_ShouldReturnDefinition()
    {
        // Arrange
        var ctx = CreateEmpty();
        var def = new TemplateDefinition("invoice");

        // Act
        ctx.Add(def);

        // Assert
        ctx.GetOrNull("invoice").ShouldNotBeNull();
        ctx.GetOrNull("invoice").ShouldBeSameAs(def);
    }

    /// <summary>
    /// 测试目的：Add 不存在名称时，GetOrNull 仍返回 null。
    /// </summary>
    [Fact]
    public void GetOrNull_NotExistName_ShouldReturnNull()
    {
        var ctx = CreateEmpty();
        ctx.Add(new TemplateDefinition("invoice"));

        ctx.GetOrNull("email").ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：同名模板 Add 两次，第二次应覆盖第一次。
    /// </summary>
    [Fact]
    public void GetOrNull_SameName_ShouldReturnLatest()
    {
        var ctx = CreateEmpty();
        var first = new TemplateDefinition("report");
        var second = new TemplateDefinition("report");
        ctx.Add(first);
        ctx.Add(second);

        ctx.GetOrNull("report").ShouldBeSameAs(second);
    }

    // -----------------------------------------------------------------
    //  GetAll (no param)
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：空上下文 GetAll() 应返回空列表，不抛异常。
    /// </summary>
    [Fact]
    public void GetAll_EmptyContext_ShouldReturnEmptyList()
    {
        var ctx = CreateEmpty();
        ctx.GetAll().ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：Add 多个定义后，GetAll() 应包含所有已添加的定义。
    /// </summary>
    [Fact]
    public void GetAll_AfterAddMultiple_ShouldReturnAll()
    {
        var ctx = CreateEmpty();
        ctx.Add(new TemplateDefinition("a"), new TemplateDefinition("b"), new TemplateDefinition("c"));

        var all = ctx.GetAll();
        all.Count.ShouldBe(3);
        all.Select(x => x.Name).ShouldContain("a");
        all.Select(x => x.Name).ShouldContain("b");
        all.Select(x => x.Name).ShouldContain("c");
    }

    // -----------------------------------------------------------------
    //  GetAll (name param) — implementation ignores name, returns all
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：GetAll(name) 当前实现忽略 name 参数，返回全部定义。
    /// </summary>
    [Fact]
    public void GetAllByName_ShouldReturnAllDefinitions()
    {
        var ctx = CreateEmpty();
        ctx.Add(new TemplateDefinition("a"), new TemplateDefinition("b"));

        // 实现：返回全部，name 参数被忽略
        ctx.GetAll("whatever").Count.ShouldBe(2);
    }

    // -----------------------------------------------------------------
    //  Add edge cases
    // -----------------------------------------------------------------

    /// <summary>
    /// 测试目的：Add 传入空数组时不应抛出异常，上下文保持不变。
    /// </summary>
    [Fact]
    public void Add_EmptyArray_ShouldNotThrowAndContextUnchanged()
    {
        var ctx = CreateEmpty();
        var ex = Record.Exception(() => ctx.Add());
        ex.ShouldBeNull();
        ctx.GetAll().ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：Add 传入 null 数组时不应抛出异常（源码: null→return）。
    /// </summary>
    [Fact]
    public void Add_NullArray_ShouldNotThrow()
    {
        var ctx = CreateEmpty();
        var ex = Record.Exception(() => ctx.Add(null!));
        ex.ShouldBeNull();
    }
}

// =========================================================================
//  BingTextTemplatingOptions Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 BingTextTemplatingOptions 构造后各集合正确初始化。
/// </summary>
public class BingTextTemplatingOptionsTest
{
    /// <summary>
    /// 测试目的：构造后 DefinitionProviders 不为 null。
    /// </summary>
    [Fact]
    public void Constructor_DefinitionProviders_ShouldNotBeNull()
    {
        var options = new BingTextTemplatingOptions();
        options.DefinitionProviders.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：构造后 ContentContributors 不为 null。
    /// </summary>
    [Fact]
    public void Constructor_ContentContributors_ShouldNotBeNull()
    {
        var options = new BingTextTemplatingOptions();
        options.ContentContributors.ShouldNotBeNull();
    }

    /// <summary>
    /// 测试目的：构造后 RenderingEngines 不为 null 且为空字典。
    /// </summary>
    [Fact]
    public void Constructor_RenderingEngines_ShouldBeEmptyDictionary()
    {
        var options = new BingTextTemplatingOptions();
        options.RenderingEngines.ShouldNotBeNull();
        options.RenderingEngines.ShouldBeEmpty();
    }

    /// <summary>
    /// 测试目的：构造后 DefaultRenderingEngine 默认为 null。
    /// </summary>
    [Fact]
    public void Constructor_DefaultRenderingEngine_ShouldBeNull()
    {
        var options = new BingTextTemplatingOptions();
        options.DefaultRenderingEngine.ShouldBeNull();
    }

    /// <summary>
    /// 测试目的：可以正常设置并读取 DefaultRenderingEngine。
    /// </summary>
    [Fact]
    public void DefaultRenderingEngine_SetValue_ShouldReturnSameValue()
    {
        var options = new BingTextTemplatingOptions();
        options.DefaultRenderingEngine = "Liquid";
        options.DefaultRenderingEngine.ShouldBe("Liquid");
    }

    /// <summary>
    /// 测试目的：可向 RenderingEngines 字典添加条目并正常读取。
    /// </summary>
    [Fact]
    public void RenderingEngines_AddEntry_ShouldBeRetrievable()
    {
        var options = new BingTextTemplatingOptions();
        options.RenderingEngines["Liquid"] = typeof(object);

        options.RenderingEngines["Liquid"].ShouldBe(typeof(object));
    }
}

// =========================================================================
//  TemplateContentContributorContext Tests
// =========================================================================

/// <summary>
/// 测试目的：验证 TemplateContentContributorContext 构造参数校验及属性正确性。
/// </summary>
public class TemplateContentContributorContextTest
{
    private static IServiceProvider CreateFakeProvider() =>
        new FakeServiceProvider();

    /// <summary>
    /// 测试目的：所有参数合法时，构造成功并可正确读取各属性。
    /// </summary>
    [Fact]
    public void Constructor_ValidArgs_ShouldSetProperties()
    {
        // Arrange
        var def = new TemplateDefinition("invoice");
        var sp = CreateFakeProvider();

        // Act
        var ctx = new TemplateContentContributorContext(def, sp, "zh-CN");

        // Assert
        ctx.TemplateDefinition.ShouldBeSameAs(def);
        ctx.ServiceProvider.ShouldBeSameAs(sp);
        ctx.Culture.ShouldBe("zh-CN");
    }

    /// <summary>
    /// 测试目的：templateDefinition 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_NullTemplateDefinition_ShouldThrowArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TemplateContentContributorContext(null!, CreateFakeProvider(), "zh-CN"));
        ex.ParamName.ShouldBe("templateDefinition");
    }

    /// <summary>
    /// 测试目的：serviceProvider 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Constructor_NullServiceProvider_ShouldThrowArgumentNullException()
    {
        var def = new TemplateDefinition("invoice");
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new TemplateContentContributorContext(def, null!, "zh-CN"));
        ex.ParamName.ShouldBe("serviceProvider");
    }

    /// <summary>
    /// 测试目的：Culture 允许为 null，不应抛出异常。
    /// </summary>
    [Fact]
    public void Constructor_NullCulture_ShouldBeAllowed()
    {
        var ctx = new TemplateContentContributorContext(
            new TemplateDefinition("invoice"), CreateFakeProvider(), null);
        ctx.Culture.ShouldBeNull();
    }

    /// <summary>
    /// 简单 IServiceProvider 桩，仅用于测试。
    /// </summary>
    private class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
