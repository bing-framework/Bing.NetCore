using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 启动期插件清单和入口模块校验测试。
/// </summary>
[Collection("Bing启动期插件")]
public sealed class PluginManifestValidationTest
{
    /// <summary>
    /// 顶部模块完整名称。
    /// </summary>
    private const string TopModuleName = "Bing.Core.PluginFixtures.FixtureTopModule";

    /// <summary>
    /// 验证无效 JSON 在模块构造前失败。
    /// </summary>
    [Fact]
    public void AddBingApplication_InvalidManifest_ShouldFailBeforeModuleConstruction()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreateRawPlugin("invalid-json", "{ invalid json", copyFixture: false);

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件清单解析失败");
    }

    /// <summary>
    /// 验证插件版本必须使用三段或四段数字格式。
    /// </summary>
    [Fact]
    public void AddBingApplication_InvalidVersion_ShouldFailManifestValidation()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.InvalidVersion", "1.0", new[] { TopModuleName });

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件版本必须是三段或四段非负数字");
        exception.Data["Bing.PluginId"].ShouldBe("Contoso.InvalidVersion");
    }

    /// <summary>
    /// 验证非法依赖区间在加载模块前被拒绝。
    /// </summary>
    [Theory]
    [InlineData("[1.0.0,2.0.0")]
    [InlineData("(,)")]
    [InlineData("[1.0,2.0.0)")]
    [InlineData("[2.0.0,1.0.0)")]
    public void AddBingApplication_InvalidDependencyRange_ShouldFailManifestValidation(string requirement)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.InvalidRange", "1.0.0", new[] { TopModuleName },
            new[] { ("Contoso.Foundation", requirement) });

        var services = new ServiceCollection();
        var error = Should.Throw<InvalidOperationException>(() =>
            services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory)));

        error.Message.ShouldContain("插件依赖版本");
        error.Data["Bing.PluginId"].ShouldBe("Contoso.InvalidRange");
        error.Data["Bing.PluginPhase"].ShouldBe("ReadManifest");
    }

    /// <summary>
    /// 验证基础必填清单字段缺失时立即失败。
    /// </summary>
    /// <param name="manifest">缺少一个基础字段的清单文本。</param>
    /// <param name="field">缺失字段名称。</param>
    [Theory]
    [InlineData("{\"version\":\"1.0.0\",\"entryAssembly\":\"Bing.Core.PluginFixtures.dll\",\"startupModules\":[\"Bing.Core.PluginFixtures.FixtureTopModule\"]}", "id")]
    [InlineData("{\"id\":\"Contoso.MissingVersion\",\"entryAssembly\":\"Bing.Core.PluginFixtures.dll\",\"startupModules\":[\"Bing.Core.PluginFixtures.FixtureTopModule\"]}", "version")]
    [InlineData("{\"id\":\"Contoso.MissingEntry\",\"version\":\"1.0.0\",\"startupModules\":[\"Bing.Core.PluginFixtures.FixtureTopModule\"]}", "entryAssembly")]
    public void AddBingApplication_MissingRequiredManifestField_ShouldFail(string manifest, string field)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreateRawPlugin("missing-" + field, manifest);

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件清单字段不能为空");
        exception.Message.ShouldContain(field);
    }

    /// <summary>
    /// 验证缺少启动模块清单时立即失败。
    /// </summary>
    [Fact]
    public void AddBingApplication_MissingStartupModules_ShouldFailManifestValidation()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreateRawPlugin(
            "missing-startup-modules",
            "{\"id\":\"Contoso.MissingStartupModules\",\"version\":\"1.0.0\",\"entryAssembly\":\"Bing.Core.PluginFixtures.dll\"}");

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件必须至少声明一个 startupModules");
    }

    /// <summary>
    /// 验证依赖清单项的必填字段缺失时立即失败。
    /// </summary>
    /// <param name="dependency">缺少一个依赖字段的清单文本。</param>
    /// <param name="field">缺失字段名称。</param>
    [Theory]
    [InlineData("{\"id\":\"Contoso.DependencyField\",\"version\":\"1.0.0\",\"entryAssembly\":\"Bing.Core.PluginFixtures.dll\",\"startupModules\":[\"Bing.Core.PluginFixtures.FixtureTopModule\"],\"dependencies\":[{\"version\":\"1.0.0\"}]}", "dependencies.id")]
    [InlineData("{\"id\":\"Contoso.DependencyField\",\"version\":\"1.0.0\",\"entryAssembly\":\"Bing.Core.PluginFixtures.dll\",\"startupModules\":[\"Bing.Core.PluginFixtures.FixtureTopModule\"],\"dependencies\":[{\"id\":\"Contoso.Foundation\"}]}", "dependencies.version")]
    public void AddBingApplication_MissingPluginDependencyField_ShouldFail(string dependency, string field)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreateRawPlugin("missing-dependency-" + field.Replace('.', '-'), dependency);

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件清单字段不能为空");
        exception.Message.ShouldContain(field);
    }

    /// <summary>
    /// 验证入口程序集文件不存在时立即失败。
    /// </summary>
    [Fact]
    public void AddBingApplication_MissingEntryAssembly_ShouldFailManifestValidation()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreateRawPlugin(
            "missing-entry-assembly",
            "{\"id\":\"Contoso.MissingEntryAssembly\",\"version\":\"1.0.0\",\"entryAssembly\":\"Missing.Plugin.dll\",\"startupModules\":[\"Bing.Core.PluginFixtures.FixtureTopModule\"]}",
            copyFixture: false);

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件入口程序集不存在");
        exception.Message.ShouldContain("Missing.Plugin.dll");
    }

    /// <summary>
    /// 验证入口程序集路径不能逃逸插件目录。
    /// </summary>
    [Fact]
    public void AddBingApplication_EntryAssemblyOutsideDirectory_ShouldFailPathValidation()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreateRawPlugin(
            "outside-path",
            "{\"id\":\"Contoso.Path\",\"version\":\"1.0.0\",\"entryAssembly\":\"../outside.dll\",\"startupModules\":[\"" + TopModuleName + "\"]}",
            copyFixture: false);

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件入口程序集不能离开插件目录");
    }

    /// <summary>
    /// 验证入口模块必须是入口程序集中的公开 BingModule。
    /// </summary>
    [Fact]
    public void AddBingApplication_UnknownStartupModule_ShouldFailWithPluginContext()
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin(
            "Contoso.InvalidModule", "1.0.0", new[] { "Bing.Core.PluginFixtures.MissingModule" });

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件启动模块不是有效的公开 BingModule");
        exception.Data["Bing.PluginId"].ShouldBe("Contoso.InvalidModule");
        exception.Data["Bing.PluginPhase"].ShouldBe("LoadAssembly");
        ((string)exception.Data["Bing.PluginAssembly"]).ShouldEndWith("Bing.Core.PluginFixtures.dll");
        exception.Data["Bing.PluginType"].ShouldBe("Bing.Core.PluginFixtures.MissingModule");
    }

    /// <summary>
    /// 验证抽象、非公开、不可构造或普通类型不能作为启动模块。
    /// </summary>
    /// <param name="moduleName">启动模块完整名称。</param>
    [Theory]
    [InlineData("Bing.Core.PluginFixtures.FixtureAbstractModule")]
    [InlineData("Bing.Core.PluginFixtures.FixtureNoPublicConstructorModule")]
    [InlineData("Bing.Core.PluginFixtures.FixtureInternalModule")]
    [InlineData("Bing.Core.PluginFixtures.FixtureOrdinaryType")]
    public void AddBingApplication_InvalidStartupModuleType_ShouldFailWithPluginContext(string moduleName)
    {
        using var workspace = new PluginTestWorkspace();
        var directory = workspace.CreatePlugin("Contoso.InvalidModuleType", "1.0.0", new[] { moduleName });

        var exception = Should.Throw<InvalidOperationException>(() => Register(directory));

        exception.Message.ShouldContain("插件启动模块不是有效的公开 BingModule");
        exception.Data["Bing.PluginId"].ShouldBe("Contoso.InvalidModuleType");
        exception.Data["Bing.PluginPhase"].ShouldBe("LoadAssembly");
        ((string)exception.Data["Bing.PluginAssembly"]).ShouldEndWith("Bing.Core.PluginFixtures.dll");
        exception.Data["Bing.PluginType"].ShouldBe(moduleName);
    }

    /// <summary>
    /// 验证指定插件目录并完成应用注册。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    private static void Register(string directory)
    {
        var services = new ServiceCollection();
        services.AddBingApplication<PluginHostModule>(options => options.PluginSources.AddDirectory(directory));
    }
}
