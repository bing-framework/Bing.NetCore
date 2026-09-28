using System.Text.Json;
using System.Runtime.Loader;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 串行执行启动期插件测试，避免默认加载上下文和夹具静态状态互相影响。
/// </summary>
[CollectionDefinition("Bing启动期插件", DisableParallelization = true)]
public sealed class BingPluginTestCollectionDefinition { }

/// <summary>
/// 插件测试使用的应用根模块。
/// </summary>
public sealed class PluginHostModule : BingModule { }

/// <summary>
/// 创建临时插件目录并生成测试清单。
/// </summary>
internal sealed class PluginTestWorkspace : IDisposable
{
    /// <summary>
    /// 不带 BOM 的 UTF-8 编码。
    /// </summary>
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    /// <summary>
    /// 第一版本夹具程序集路径。
    /// </summary>
    private readonly string _fixtureAssemblyPath;

    /// <summary>
    /// 第二版本夹具程序集路径。
    /// </summary>
    private readonly string _fixtureV2AssemblyPath;

    /// <summary>
    /// 初始化插件测试工作区。
    /// </summary>
    public PluginTestWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "bing-plugin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        _fixtureAssemblyPath = Path.Combine(AppContext.BaseDirectory, "PluginFixtures", "Bing.Core.PluginFixtures.dll");
        _fixtureV2AssemblyPath = Path.Combine(AppContext.BaseDirectory, "PluginFixtures", "V2", "Bing.Core.PluginFixtures.dll");
        if (!File.Exists(_fixtureAssemblyPath))
            throw new FileNotFoundException("插件测试夹具程序集未复制到测试输出目录。", _fixtureAssemblyPath);
    }

    /// <summary>
    /// 获取临时插件父目录。
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// 获取未被测试项目直接引用的夹具程序集路径。
    /// </summary>
    public string FixtureAssemblyPath => _fixtureAssemblyPath;

    /// <summary>
    /// 获取独立的插件附加服务程序集路径。
    /// </summary>
    public string FixtureScanAssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "PluginFixtures", "Bing.Core.PluginScanFixtures.dll");

    /// <summary>
    /// 获取第二版本的附加服务程序集路径。
    /// </summary>
    public string FixtureScanV2AssemblyPath =>
        Path.Combine(AppContext.BaseDirectory, "PluginFixtures", "V2", "Bing.Core.PluginScanFixtures.dll");

    /// <summary>
    /// 获取附加服务程序集测试依赖的路径。
    /// </summary>
    public string FixtureScanDependencyPath =>
        Path.Combine(AppContext.BaseDirectory, "PluginFixtures", "Bing.Core.PluginScanDependency.dll");

    /// <summary>
    /// 获取用于程序集身份冲突测试的第二版本夹具路径。
    /// </summary>
    public string FixtureV2AssemblyPath => _fixtureV2AssemblyPath;

    /// <summary>
    /// 获取已经加载的夹具程序集，未加载时从测试输出目录加载一次。
    /// </summary>
    /// <returns>夹具程序集。</returns>
    public Assembly LoadFixtureAssembly() =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), AssemblyLoadContext.Default) &&
            string.Equals(assembly.GetName().Name, "Bing.Core.PluginFixtures", StringComparison.Ordinal)) ??
        AssemblyLoadContext.Default.LoadFromAssemblyPath(_fixtureAssemblyPath);

    /// <summary>
    /// 创建包含夹具程序集和清单的插件目录。
    /// </summary>
    /// <param name="id">插件 ID。</param>
    /// <param name="version">插件版本。</param>
    /// <param name="startupModules">启动模块完整名称。</param>
    /// <param name="dependencies">插件直接依赖。</param>
    /// <param name="directoryName">目录名称；为空时使用插件 ID。</param>
    /// <returns>插件目录路径。</returns>
    public string CreatePlugin(string id, string version, IEnumerable<string> startupModules,
        IEnumerable<(string Id, string Version)> dependencies = null, string directoryName = null)
    {
        return CreatePluginFromAssembly(id, version, startupModules, _fixtureAssemblyPath, dependencies, directoryName);
    }

    /// <summary>
    /// 使用指定入口程序集创建插件目录。
    /// </summary>
    /// <param name="id">插件 ID。</param>
    /// <param name="version">插件版本。</param>
    /// <param name="startupModules">启动模块完整名称。</param>
    /// <param name="assemblyPath">入口程序集路径。</param>
    /// <param name="dependencies">插件直接依赖。</param>
    /// <param name="directoryName">目录名称；为空时使用插件 ID。</param>
    /// <returns>插件目录路径。</returns>
    public string CreatePluginFromAssembly(string id, string version, IEnumerable<string> startupModules,
        string assemblyPath, IEnumerable<(string Id, string Version)> dependencies = null, string directoryName = null)
    {
        var directory = Path.Combine(RootPath, directoryName ?? id);
        Directory.CreateDirectory(directory);
        File.Copy(assemblyPath, Path.Combine(directory, "Bing.Core.PluginFixtures.dll"));
        WriteManifest(directory, id, version, "Bing.Core.PluginFixtures.dll", startupModules, dependencies);
        return directory;
    }

    /// <summary>
    /// 写入插件清单。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    /// <param name="id">插件 ID。</param>
    /// <param name="version">插件版本。</param>
    /// <param name="entryAssembly">入口程序集相对路径。</param>
    /// <param name="startupModules">启动模块完整名称。</param>
    /// <param name="dependencies">插件直接依赖。</param>
    public static void WriteManifest(string directory, string id, string version, string entryAssembly,
        IEnumerable<string> startupModules, IEnumerable<(string Id, string Version)> dependencies = null)
    {
        var manifest = new Dictionary<string, object>
        {
            ["id"] = id,
            ["version"] = version,
            ["entryAssembly"] = entryAssembly,
            ["startupModules"] = startupModules?.ToArray(),
            ["dependencies"] = (dependencies ?? Array.Empty<(string Id, string Version)>())
                .Select(dependency => new { id = dependency.Id, version = dependency.Version })
                .ToArray()
        };
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(directory, "bing-plugin.json"), json, Utf8WithoutBom);
    }

    /// <summary>
    /// 重置已加载夹具程序集中的静态测试状态。
    /// </summary>
    /// <param name="assembly">插件夹具程序集。</param>
    public static void ResetFixtureState(Assembly assembly)
    {
        var stateType = assembly.GetType("Bing.Core.PluginFixtures.PluginFixtureState", true);
        stateType.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    }

    /// <summary>
    /// 创建只包含指定清单的插件目录。
    /// </summary>
    /// <param name="directoryName">目录名称。</param>
    /// <param name="manifestContent">清单文本。</param>
    /// <param name="copyFixture">是否复制夹具入口程序集。</param>
    /// <returns>插件目录路径。</returns>
    public string CreateRawPlugin(string directoryName, string manifestContent, bool copyFixture = true)
    {
        var directory = Path.Combine(RootPath, directoryName);
        Directory.CreateDirectory(directory);
        if (copyFixture)
            File.Copy(_fixtureAssemblyPath, Path.Combine(directory, "Bing.Core.PluginFixtures.dll"));
        File.WriteAllText(Path.Combine(directory, "bing-plugin.json"), manifestContent, Utf8WithoutBom);
        return directory;
    }

    /// <summary>
    /// 释放临时插件目录。
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>
/// 返回固定插件目录的自定义来源。
/// </summary>
internal sealed class FixedPluginSource : IBingPluginSource
{
    /// <summary>
    /// 来源提供的插件目录。
    /// </summary>
    private readonly string[] _directories;

    /// <summary>
    /// 初始化自定义插件来源。
    /// </summary>
    /// <param name="directories">插件目录。</param>
    public FixedPluginSource(params string[] directories) => _directories = directories ?? Array.Empty<string>();

    /// <summary>
    /// 获取来源被调用的次数。
    /// </summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public IEnumerable<string> GetPluginDirectories()
    {
        CallCount++;
        return _directories;
    }
}
