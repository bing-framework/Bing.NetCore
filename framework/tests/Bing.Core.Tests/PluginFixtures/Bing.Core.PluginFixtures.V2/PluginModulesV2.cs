using Bing.Core.Modularity;
using Bing.Core.PluginScanFixtures;

namespace Bing.Core.PluginFixtures.V2;

/// <summary>
/// 用于验证同名不同程序集身份冲突的第二版本模块。
/// </summary>
public sealed class FixtureV2Module : BingModule { }

/// <summary>
/// 声明第二版本附加程序集的模块。
/// </summary>
[BingModuleAssembly(typeof(PluginScanMarker))]
public sealed class FixtureV2AdditionalAssemblyModule : BingModule { }
