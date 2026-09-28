using Bing.Core.Modularity;
using Bing.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Builders;

/// <summary>
/// 兼容立即注册模式的模块构建器。
/// </summary>
/// <remarks>
/// 构建器只创建实际选中的模块，并保留旧入口的注册顺序。
/// </remarks>
public class BingBuilder : IBingBuilder
{
    /// <summary>
    /// 自动发现到的候选模块类型。
    /// </summary>
    private readonly Type[] _sourceTypes;

    /// <summary>
    /// 当前服务集合的模块注册状态。
    /// </summary>
    private readonly BingModuleRegistration _registration;

    /// <summary>
    /// 已创建的模块实例。
    /// </summary>
    private readonly Dictionary<Type, BingModule> _instances = new();

    /// <summary>
    /// 已执行服务注册的模块实例。
    /// </summary>
    private readonly List<BingModule> _registrationOrder = new();

    /// <summary>
    /// 当前已选中的模块列表。
    /// </summary>
    private List<BingModule> _modules = new();

    /// <summary>
    /// 是否正在执行模块注册回调。
    /// </summary>
    private bool _configuring;

    /// <summary>
    /// 初始化兼容模式模块构建器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> 为空时抛出。</exception>
    public BingBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        _registration = BingModuleRegistration.Register(services, true);
        var finder = services.GetOrAddTypeFinder<IBingModuleTypeFinder>(a => new BingModuleTypeFinder(a));
        _sourceTypes = finder.FindAll(true);
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }
    /// <inheritdoc />
    public IEnumerable<BingModule> Modules => _modules.AsReadOnly();

    /// <summary>
    /// 清除已释放模块的内部引用。
    /// </summary>
    internal void ClearModuleReferences()
    {
        _instances.Clear();
        _registrationOrder.Clear();
        _modules.Clear();
        _configuring = false;
    }

    /// <inheritdoc />
    public IBingBuilder AddModule<TModule>() where TModule : BingModule => AddModule(typeof(TModule));

    /// <summary>
    /// 添加指定模块及其依赖。
    /// </summary>
    /// <param name="type">根模块类型。</param>
    /// <returns>当前构建器。</returns>
    private IBingBuilder AddModule(Type type)
    {
        _registration.EnsureConfigurable();
        if (_configuring) throw new InvalidOperationException("AddServices 期间不能重入模块注册。");
        if (_registrationOrder.Any(m => m.GetType() == type)) return this;
        _configuring = true;
        try
        {
            var graph = ModuleDependencyGraph.Build(new[] { type });
            var added = new List<BingModule>();
            foreach (var moduleType in graph.Types)
            {
                if (_registrationOrder.Any(m => m.GetType() == moduleType)) continue;
                if (_instances.Keys.Any(t => t != moduleType && (t.BaseType == moduleType || moduleType.BaseType == t)))
                    throw new InvalidOperationException($"模块 {moduleType.FullName} 与已显式加载的基类/派生模块冲突，请选择一个实现。");
                if (!_instances.TryGetValue(moduleType, out var instance))
                {
                    instance = (BingModule)Activator.CreateInstance(moduleType);
                    _registration.Own(instance);
                    _instances.Add(moduleType, instance);
                }
                if (BingModuleHooks.HasNewServiceHooks(instance))
                    throw new InvalidOperationException($"模块 {moduleType.FullName} 使用了新服务配置钩子，请改用 AddBingApplication。");
                added.Add(instance);
            }
            // 旧入口同 Level/Order 的模块保留添加顺序，不暗改为拓扑顺序。
            _modules = _modules.Concat(added).OrderBy(m => m.Level).ThenBy(m => m.Order).ToList();
            var addedTypes = new HashSet<Type>(added.Select(m => m.GetType()));
            foreach (var module in _modules.Where(m => addedTypes.Contains(m.GetType())))
            {
                Services.AddSingleton(typeof(BingModule), module);
                _registrationOrder.Add(module);
                Services.LogInformation($"添加模块 {module.GetType().FullName} 的服务", typeof(BingBuilder).FullName);
                module.AddServices(Services);
            }
            UpdateDirectory();
            return this;
        }
        catch (Exception ex)
        {
            ex.Data["Bing.ModulePhase"] = "ConfigureServices";
            ex.Data["Bing.ModuleType"] = type.FullName;
            _registration.FailConfiguration(ex);
            throw;
        }
        finally { _configuring = false; }
    }

    /// <summary>
    /// 根据已注册模块刷新只读模块目录。
    /// </summary>
    private void UpdateDirectory() => _registration.SetModules(_registrationOrder.Select((m, index) =>
        new BingModuleDescriptor(m, BingModuleHelper.FindDependedModuleTypes(m.GetType()), index)));

    /// <inheritdoc />
    public IBingBuilder AddModules(params Type[] exceptModuleTypes)
    {
        _registration.EnsureConfigurable();
        if (_configuring) throw new InvalidOperationException("AddServices 期间不能重入模块注册。");
        _configuring = true;
        try
        {
            if (exceptModuleTypes == null) throw new ArgumentNullException(nameof(exceptModuleTypes));
            var roots = _sourceTypes.Where(t => !exceptModuleTypes.Contains(t)).ToArray();
            var graph = ModuleDependencyGraph.Build(roots, exceptModuleTypes);
            foreach (var type in graph.Types)
            {
                if (_instances.ContainsKey(type)) continue;
                var module = (BingModule)Activator.CreateInstance(type);
                _registration.Own(module);
                _instances.Add(type, module);
            }
            // 兼容旧全选模式：排序根模块后逐个纳入依赖，而非全图一起注册。
            var orderedRoots = roots.OrderBy(t => _instances[t].Level).ThenBy(t => _instances[t].Order)
                .ThenBy(t => t.FullName).ToArray();
            _configuring = false;
            foreach (var root in orderedRoots) AddModule(root);
            return this;
        }
        catch (Exception ex) { _registration.FailConfiguration(ex); throw; }
        finally { _configuring = false; }
    }
}
