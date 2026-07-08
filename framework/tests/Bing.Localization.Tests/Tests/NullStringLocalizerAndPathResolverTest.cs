using Bing.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Bing.Localization.Tests;

/// <summary>
/// <see cref="NullStringLocalizer"/> 及 <see cref="PathResolver"/> 单元测试
/// </summary>
public class NullStringLocalizerTest
{
    private readonly IStringLocalizer _localizer = NullStringLocalizer.Instance;

    // ═══════════════════════════════════════════════════════════
    // Instance 单例
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：NullStringLocalizer.Instance 是单例，多次访问返回同一引用，避免重复实例化开销。
    /// </summary>
    [Fact]
    public void Instance_ShouldBeSingleton()
    {
        // Arrange & Act
        var a = NullStringLocalizer.Instance;
        var b = NullStringLocalizer.Instance;

        // Assert
        a.ShouldNotBeNull();
        ReferenceEquals(a, b).ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // this[string name] 索引器
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：按名称索引时，返回的 LocalizedString.Name 和 Value 应等于传入的 name，
    /// ResourceNotFound 应为 true（空本地化器不存储任何资源）。
    /// </summary>
    [Fact]
    public void Indexer_ByName_ShouldReturnNameAsValueWithResourceNotFound()
    {
        // Arrange & Act
        var result = _localizer["Hello"];

        // Assert
        result.Name.ShouldBe("Hello");
        result.Value.ShouldBe("Hello");
        result.ResourceNotFound.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：空 name 应正常处理，返回空字符串，不抛异常。
    /// </summary>
    [Fact]
    public void Indexer_EmptyName_ShouldReturnEmptyWithoutThrowing()
    {
        // Arrange & Act
        var result = _localizer[string.Empty];

        // Assert
        result.Name.ShouldBe(string.Empty);
        result.Value.ShouldBe(string.Empty);
    }

    // ═══════════════════════════════════════════════════════════
    // this[string name, params object[] arguments] 索引器
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：带参数索引器应对格式字符串 + 参数做 string.Format，Name 和 Value 均为格式化结果。
    /// </summary>
    [Fact]
    public void Indexer_WithArguments_ShouldFormatNameTemplate()
    {
        // Arrange & Act
        var result = _localizer["用户 {0} 的订单 {1}", "u-001", 42];

        // Assert
        result.Name.ShouldBe("用户 u-001 的订单 42");
        result.Value.ShouldBe("用户 u-001 的订单 42");
        result.ResourceNotFound.ShouldBeTrue();
    }

    /// <summary>
    /// 测试目的：无参数时带参数索引器应与普通索引器等效，返回原始 name。
    /// </summary>
    [Fact]
    public void Indexer_WithNoArguments_ShouldReturnOriginalName()
    {
        // Arrange & Act
        var result = _localizer["Welcome", Array.Empty<object>()];

        // Assert
        result.Name.ShouldBe("Welcome");
        result.Value.ShouldBe("Welcome");
    }

    // ═══════════════════════════════════════════════════════════
    // GetAllStrings
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：GetAllStrings(false) 应返回空集合，空本地化器没有任何已知资源。
    /// </summary>
    [Fact]
    public void GetAllStrings_WithoutParentCultures_ShouldReturnEmpty()
    {
        // Arrange & Act
        var result = _localizer.GetAllStrings(includeParentCultures: false).ToList();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    /// <summary>
    /// 测试目的：GetAllStrings(true) 也应返回空集合（即使请求父文化资源）。
    /// </summary>
    [Fact]
    public void GetAllStrings_IncludingParentCultures_ShouldAlsoReturnEmpty()
    {
        // Arrange & Act
        var result = _localizer.GetAllStrings(includeParentCultures: true).ToList();

        // Assert
        result.Count.ShouldBe(0);
    }
}

/// <summary>
/// <see cref="PathResolver"/> 单元测试
/// </summary>
public class PathResolverTest
{
    private readonly PathResolver _resolver = new();

    // ═══════════════════════════════════════════════════════════
    // GetRootNamespace
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：null 程序集传入时应抛出 ArgumentNullException（CheckNull 防御）。
    /// </summary>
    [Fact]
    public void GetRootNamespace_WithNullAssembly_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => _resolver.GetRootNamespace(null));
    }

    /// <summary>
    /// 测试目的：无 RootNamespaceAttribute 时，应返回程序集的 GetName().Name。
    /// </summary>
    [Fact]
    public void GetRootNamespace_WithoutAttribute_ShouldReturnAssemblySimpleName()
    {
        // Arrange
        var assembly = typeof(PathResolverTest).Assembly;

        // Act
        var result = _resolver.GetRootNamespace(assembly);

        // Assert
        result.ShouldBe(assembly.GetName().Name);
    }

    // ═══════════════════════════════════════════════════════════
    // GetResourcesRootPath
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：assembly 为 null 时，直接返回 rootPath，不抛异常。
    /// </summary>
    [Fact]
    public void GetResourcesRootPath_WithNullAssembly_ShouldReturnRootPath()
    {
        // Arrange & Act
        var result = _resolver.GetResourcesRootPath(null, "Resources");

        // Assert
        result.ShouldBe("Resources");
    }

    /// <summary>
    /// 测试目的：程序集无 ResourceLocationAttribute 时，应返回传入的 rootPath。
    /// </summary>
    [Fact]
    public void GetResourcesRootPath_WithoutAttribute_ShouldReturnProvidedRootPath()
    {
        // Arrange
        var assembly = typeof(PathResolverTest).Assembly;

        // Act
        var result = _resolver.GetResourcesRootPath(assembly, "i18n");

        // Assert
        result.ShouldBe("i18n");
    }

    // ═══════════════════════════════════════════════════════════
    // GetResourcesBaseName
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：typeFullName 包含 rootNamespace 前缀时，应去掉前缀及分隔符 "."。
    /// </summary>
    [Fact]
    public void GetResourcesBaseName_ShouldStripRootNamespacePrefix()
    {
        // Arrange
        var assembly = typeof(PathResolverTest).Assembly;
        var rootNamespace = _resolver.GetRootNamespace(assembly);
        var typeFullName = $"{rootNamespace}.Models.Product";

        // Act
        var result = _resolver.GetResourcesBaseName(assembly, typeFullName);

        // Assert
        result.ShouldBe("Models.Product");
    }

    /// <summary>
    /// 测试目的：仅会移除“rootNamespace.”前缀；当 typeFullName 与 rootNamespace 完全相同时，应原样返回。
    /// </summary>
    [Fact]
    public void GetResourcesBaseName_WhenTypeIsRootNamespace_ShouldReturnRootNamespace()
    {
        // Arrange
        var assembly = typeof(PathResolverTest).Assembly;
        var rootNamespace = _resolver.GetRootNamespace(assembly);

        // Act
        var result = _resolver.GetResourcesBaseName(assembly, rootNamespace);

        // Assert
        result.ShouldBe(rootNamespace);
    }

    // ═══════════════════════════════════════════════════════════
    // GetJsonResourcePath
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 测试目的：有效 baseName 时，路径应包含 "{baseName}.{culture}.json" 片段。
    /// </summary>
    [Fact]
    public void GetJsonResourcePath_WithBaseName_ShouldContainBaseNameAndCulture()
    {
        // Arrange
        var culture = new System.Globalization.CultureInfo("zh-CN");

        // Act
        var result = _resolver.GetJsonResourcePath("Resources", "Models.Product", culture);

        // Assert
        result.ShouldContain("Models.Product.zh-CN.json");
    }

    /// <summary>
    /// 测试目的：baseName 为空时，路径应仅包含 "{culture}.json"（不带基名称前缀）。
    /// </summary>
    [Fact]
    public void GetJsonResourcePath_WithEmptyBaseName_ShouldContainOnlyCulture()
    {
        // Arrange
        var culture = new System.Globalization.CultureInfo("en-US");

        // Act
        var result = _resolver.GetJsonResourcePath("Resources", string.Empty, culture);

        // Assert
        result.ShouldEndWith("en-US.json");
        result.ShouldNotContain(".en-US.json");
    }

    /// <summary>
    /// 测试目的：baseName 含内部类分隔符 '+' 时，应被替换为 '.'，生成正确的文件名。
    /// </summary>
    [Fact]
    public void GetJsonResourcePath_WithInnerClassSeparator_ShouldReplacePlusWithDot()
    {
        // Arrange
        var culture = new System.Globalization.CultureInfo("zh-CN");

        // Act
        var result = _resolver.GetJsonResourcePath("Resources", "Models+Product", culture);

        // Assert
        result.ShouldContain("Models.Product.zh-CN.json");
        result.ShouldNotContain("+");
    }
}
