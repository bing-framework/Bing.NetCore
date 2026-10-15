namespace Bing.Core.Modularity;

/// <summary>
/// 模块及其直接依赖的只读描述。
/// </summary>
/// <remarks>
/// 描述符中的模块实例由框架在加载成功后负责释放。
/// </remarks>
public sealed class BingModuleDescriptor
{
    /// <summary>
    /// 模块实例。
    /// </summary>
    private BingModule _instance;

    /// <summary>
    /// 模块类型。
    /// </summary>
    private readonly Type _moduleType;

    /// <summary>
    /// 模块所属程序集。
    /// </summary>
    private readonly Assembly _assembly;

    /// <summary>
    /// 初始化一个 <see cref="BingModuleDescriptor" /> 类型的实例。
    /// </summary>
    /// <param name="instance">模块实例。</param>
    /// <param name="dependencies">模块直接依赖的类型集合。</param>
    /// <param name="executionOrder">模块在当前模块图中的执行序号，从零开始。</param>
    /// <exception cref="ArgumentNullException">模块实例或依赖集合为空时抛出。</exception>
    public BingModuleDescriptor(BingModule instance, IEnumerable<Type> dependencies, int executionOrder)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _moduleType = instance.GetType();
        _assembly = _moduleType.Assembly;
        Assemblies = Array.AsReadOnly(new[] { _assembly });
        Dependencies = Array.AsReadOnly((dependencies ?? throw new ArgumentNullException(nameof(dependencies))).ToArray());
        ExecutionOrder = executionOrder;
    }

    /// <summary>
    /// 获取模块实例。
    /// </summary>
    internal BingModule Instance => _instance;

    /// <summary>
    /// 清除已释放的模块实例引用。
    /// </summary>
    internal void ClearInstance() => _instance = null;

    /// <summary>
    /// 获取模块类型。
    /// </summary>
    public Type ModuleType => _moduleType;

    /// <summary>
    /// 获取模块所属程序集。
    /// </summary>
    public Assembly Assembly => _assembly;

    /// <summary>
    /// 获取模块本体及其声明的附加程序集。
    /// </summary>
    public IReadOnlyList<Assembly> Assemblies { get; private set; }

    /// <summary>
    /// 冻结模块的程序集声明。
    /// </summary>
    internal void SetAssemblies(IEnumerable<Assembly> assemblies) =>
        Assemblies = Array.AsReadOnly(assemblies.ToArray());

    /// <summary>
    /// 获取模块直接依赖的类型集合。
    /// </summary>
    public IReadOnlyList<Type> Dependencies { get; }

    /// <summary>
    /// 获取模块在当前模块图中的执行序号。
    /// </summary>
    public int ExecutionOrder { get; }
}
