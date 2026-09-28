using System.Collections.ObjectModel;

namespace Bing.Core.Modularity;

/// <summary>
/// 模块依赖关系图。图中的类型按根节点首次 DFS 访问顺序保存。
/// </summary>
internal sealed class ModuleDependencyGraph
{
    /// <summary>
    /// 依赖路径在异常数据中的键名。
    /// </summary>
    private const string DependencyPathDataKey = "Bing.ModuleDependencyPath";

    /// <summary>
    /// 初始化模块依赖图。
    /// </summary>
    /// <param name="types">按首次访问顺序排列的模块类型。</param>
    /// <param name="dependencies">模块到直接依赖类型的映射。</param>
    private ModuleDependencyGraph(IReadOnlyList<Type> types, IReadOnlyDictionary<Type, Type[]> dependencies)
    {
        Types = types;
        Dependencies = dependencies;
    }

    /// <summary>
    /// 获取模块首次访问顺序。
    /// </summary>
    public IReadOnlyList<Type> Types { get; }

    /// <summary>
    /// 获取每个模块的直接依赖。
    /// </summary>
    public IReadOnlyDictionary<Type, Type[]> Dependencies { get; }

    /// <summary>
    /// 构建模块依赖图。
    /// </summary>
    /// <param name="roots">模块图的根类型。</param>
    /// <param name="excluded">禁止加载的模块类型。</param>
    /// <returns>已校验的模块依赖图。</returns>
    /// <exception cref="ArgumentNullException">根模块集合为空时抛出。</exception>
    /// <exception cref="ArgumentException">模块类型无效时抛出。</exception>
    /// <exception cref="InvalidOperationException">检测到循环依赖或被排除的依赖时抛出。</exception>
    public static ModuleDependencyGraph Build(IEnumerable<Type> roots, IEnumerable<Type> excluded = null)
    {
        if (roots == null)
            throw new ArgumentNullException(nameof(roots));

        var excludedTypes = new HashSet<Type>();
        if (excluded != null)
        {
            foreach (var type in excluded)
            {
                ValidateModuleType(type, "excluded", new[] { type });
                excludedTypes.Add(type);
            }
        }

        var rootTypes = roots.ToList();
        foreach (var root in rootTypes)
            ValidateModuleType(root, "root", new[] { root });

        var types = new List<Type>();
        var dependencies = new Dictionary<Type, Type[]>();
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();
        var activePath = new List<Type>();

        foreach (var root in rootTypes)
        {
            if (excludedTypes.Contains(root))
                throw CreateExcludedException(new[] { root });
            Visit(root);
        }

        return new ModuleDependencyGraph(
            new ReadOnlyCollection<Type>(types),
            new ReadOnlyDictionary<Type, Type[]>(dependencies));

        void Visit(Type root)
        {
            if (visited.Contains(root))
                return;

            var stack = new Stack<VisitFrame>();
            stack.Push(new VisitFrame(root));
            while (stack.Count > 0)
            {
                var frame = stack.Peek();
                if (!frame.Entered)
                {
                    if (visited.Contains(frame.Type))
                    {
                        stack.Pop();
                        continue;
                    }

                    if (excludedTypes.Contains(frame.Type))
                    {
                        var excludedPath = activePath.Concat(new[] { frame.Type }).ToArray();
                        throw CreateExcludedException(excludedPath);
                    }

                    var framePath = activePath.Concat(new[] { frame.Type }).ToArray();
                    ValidateModuleType(frame.Type, "dependency", framePath);
                    if (visiting.Contains(frame.Type))
                    {
                        throw CreateCycleException(framePath);
                    }

                    visiting.Add(frame.Type);
                    activePath.Add(frame.Type);
                    types.Add(frame.Type);
                    frame.Dependencies = ReadDependencies(frame.Type, framePath);
                    dependencies.Add(frame.Type, frame.Dependencies);
                    frame.Entered = true;
                }

                if (frame.NextDependencyIndex < frame.Dependencies.Length)
                {
                    var dependency = frame.Dependencies[frame.NextDependencyIndex++];
                    if (visiting.Contains(dependency))
                    {
                        var cyclePath = activePath.Concat(new[] { dependency }).ToArray();
                        throw CreateCycleException(cyclePath);
                    }
                    if (excludedTypes.Contains(dependency))
                    {
                        var excludedPath = activePath.Concat(new[] { dependency }).ToArray();
                        throw CreateExcludedException(excludedPath);
                    }
                    if (!visited.Contains(dependency))
                        stack.Push(new VisitFrame(dependency));
                    continue;
                }

                stack.Pop();
                visiting.Remove(frame.Type);
                activePath.RemoveAt(activePath.Count - 1);
                visited.Add(frame.Type);
            }
        }

        static Type[] ReadDependencies(Type moduleType, Type[] path)
        {
            var result = new List<Type>();
            object[] attributes;
            try
            {
                attributes = moduleType.GetCustomAttributes(true);
            }
            catch (Exception error)
            {
                AttachPath(error, path);
                throw;
            }

            foreach (var provider in attributes.OfType<IDependedTypesProvider>())
            {
                Type[] provided;
                try
                {
                    provided = provider.GetDependedTypes();
                }
                catch (Exception error)
                {
                    AttachPath(error, path);
                    throw;
                }
                if (provided == null)
                    throw AttachPath(new InvalidOperationException(
                        "模块依赖提供程序返回了 null: " + FormatPath(path)), path);

                foreach (var dependency in provided)
                {
                    if (dependency == null)
                    {
                        var nullDependencyPath = path.Concat(new Type[] { null }).ToArray();
                        throw AttachPath(new ArgumentException(
                            "模块依赖不能为 null: " + FormatPath(nullDependencyPath), nameof(moduleType)),
                            nullDependencyPath);
                    }
                    if (!result.Contains(dependency))
                        result.Add(dependency);
                }
            }
            return result.ToArray();
        }
    }

    /// <summary>
    /// 验证模块类型及其构造条件。
    /// </summary>
    /// <param name="type">待验证的模块类型。</param>
    /// <param name="role">类型在依赖图中的角色名称。</param>
    /// <param name="path">当前依赖路径。</param>
    /// <exception cref="ArgumentNullException">模块类型为空时抛出。</exception>
    /// <exception cref="ArgumentException">模块类型不符合构造条件时抛出。</exception>
    private static void ValidateModuleType(Type type, string role, IEnumerable<Type> path)
    {
        if (type == null)
            throw AttachPath(new ArgumentNullException(
                role, "模块类型不能为 null。依赖路径: " + FormatPath(path)), path);
        if (!type.IsClass || type.IsAbstract || type.IsGenericType || !typeof(BingModule).IsAssignableFrom(type))
            throw AttachPath(new ArgumentException(
                "给定类型不是有效的 BingModule: " + FormatPath(path), role), path);
        if (type.GetConstructor(Type.EmptyTypes) == null)
            throw AttachPath(new ArgumentException(
                "BingModule 必须具有公共无参构造函数: " + FormatPath(path), role), path);
        // 仅实现 IAsyncDisposable 的模块可以进入异步生命周期；同步配置或构造失败时，
        // 所有权记录会同步等待 DisposeAsync，以免异常路径遗留异步资源。
    }

    /// <summary>
    /// 创建包含被排除模块路径的异常。
    /// </summary>
    /// <param name="path">发生冲突的依赖路径。</param>
    /// <returns>描述排除冲突的异常。</returns>
    private static InvalidOperationException CreateExcludedException(IEnumerable<Type> path) =>
        AttachPath(new InvalidOperationException(
            "模块依赖链包含被排除的模块: " + FormatPath(path)), path);

    /// <summary>
    /// 创建包含循环路径的异常。
    /// </summary>
    /// <param name="path">检测到的循环路径。</param>
    /// <returns>描述循环依赖的异常。</returns>
    private static InvalidOperationException CreateCycleException(IEnumerable<Type> path) =>
        AttachPath(new InvalidOperationException(
            "Bing 模块依赖存在环: " + FormatPath(path)), path);

    /// <summary>
    /// 将依赖路径附加到异常数据。
    /// </summary>
    /// <typeparam name="TException">异常类型。</typeparam>
    /// <param name="exception">需要附加路径的异常。</param>
    /// <param name="path">依赖路径。</param>
    /// <returns>已附加路径的原异常。</returns>
    private static TException AttachPath<TException>(TException exception, IEnumerable<Type> path)
        where TException : Exception
    {
        exception.Data[DependencyPathDataKey] = path.ToArray();
        return exception;
    }

    /// <summary>
    /// 格式化依赖路径。
    /// </summary>
    /// <param name="path">依赖路径。</param>
    /// <returns>使用箭头连接的类型名称。</returns>
    private static string FormatPath(IEnumerable<Type> path) => string.Join(" -> ", path.Select(FormatType));

    /// <summary>
    /// 获取模块类型的显示名称。
    /// </summary>
    /// <param name="type">模块类型。</param>
    /// <returns>完整类型名称、简单名称或 <c>&lt;null&gt;</c>。</returns>
    private static string FormatType(Type type) => type?.FullName ?? type?.Name ?? "<null>";

    /// <summary>
    /// 保存依赖图遍历过程中的模块状态。
    /// </summary>
    private sealed class VisitFrame
    {
        /// <summary>
        /// 初始化遍历帧。
        /// </summary>
        /// <param name="type">当前模块类型。</param>
        public VisitFrame(Type type) => Type = type;

        /// <summary>
        /// 获取当前模块类型。
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// 获取或设置当前帧是否已完成进入处理。
        /// </summary>
        public bool Entered { get; set; }

        /// <summary>
        /// 获取或设置当前模块的直接依赖。
        /// </summary>
        public Type[] Dependencies { get; set; }

        /// <summary>
        /// 获取或设置下一个待处理的依赖索引。
        /// </summary>
        public int NextDependencyIndex { get; set; }
    }
}
