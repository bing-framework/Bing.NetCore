using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NuGet.Versioning;

namespace Bing.Core.Modularity;

internal static partial class BingPluginLoader
{
    /// <summary>
    /// 处理插件目录中的依赖程序集解析。
    /// </summary>
    private sealed class PluginAssemblyResolver
    {
        /// <summary>
        /// 当前注册批次的程序集候选表。
        /// </summary>
        private readonly IReadOnlyDictionary<string, AssemblyCandidate> _candidates;

        /// <summary>
        /// 是否只在当前可回收上下文中查找已加载程序集。
        /// </summary>
        private readonly bool _isolated;

        /// <summary>
        /// 初始化一个 <see cref="PluginAssemblyResolver" /> 类型的实例。
        /// </summary>
        /// <param name="candidates">本次注册的程序集候选表。</param>
        public PluginAssemblyResolver(IReadOnlyDictionary<string, AssemblyCandidate> candidates) : this(candidates, false) { }

        /// <summary>
        /// 初始化一个 <see cref="PluginAssemblyResolver" /> 类型的实例。
        /// </summary>
        /// <param name="candidates">本次注册的程序集候选表。</param>
        /// <param name="isolated">是否在独立上下文中解析。</param>
        public PluginAssemblyResolver(IReadOnlyDictionary<string, AssemblyCandidate> candidates, bool isolated)
        {
            _candidates = candidates;
            _isolated = isolated;
        }

        /// <summary>
        /// 解析当前插件批次引用的程序集。
        /// </summary>
        /// <param name="context">发起解析的加载上下文。</param>
        /// <param name="name">请求的程序集标识。</param>
        /// <returns>匹配的程序集；候选不存在时返回 null。</returns>
        public Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            if (name == null || string.IsNullOrWhiteSpace(name.Name))
                return null;
            if (_isolated)
            {
                var contracts = new[]
                {
                    typeof(BingModule).Assembly,
                    typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly
                };
                foreach (var contract in contracts)
                {
                    if (!string.Equals(contract.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!HasSameAssemblyIdentity(contract.GetName(), name) &&
                        !IsCompatibleDependencyInjectionContract(contract, name))
                        throw new FileLoadException("插件引用的宿主契约程序集版本不匹配: " + name.FullName);
                    return contract;
                }
            }
            var loadedAssemblies = _isolated
                ? AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                    ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), context))
                : AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                    ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), AssemblyLoadContext.Default));
            var loaded = loadedAssemblies.FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
            if (loaded != null)
            {
                var loadedName = loaded.GetName();
                if (!HasSameAssemblyIdentity(loadedName, name))
                {
                    var error = new FileLoadException("插件依赖程序集与已加载程序集版本不匹配: " + name.FullName +
                        "，已加载为 " + loadedName.FullName);
                    error.Data[PluginAssemblyDataKey] = name.FullName;
                    throw error;
                }
                return loaded;
            }
            if (!_candidates.TryGetValue(name.Name, out var candidate))
                return null;
            if (!HasSameAssemblyIdentity(candidate.Name, name))
            {
                var error = new FileLoadException("插件依赖程序集版本不匹配: " + name.FullName + "，候选为 " + candidate.Name.FullName);
                error.Data[PluginAssemblyDataKey] = candidate.Name.FullName;
                throw error;
            }
            return context.LoadFromAssemblyPath(candidate.Path);
        }

        /// <summary>
        /// 判断插件引用的 DI 抽象契约是否可绑定到当前宿主版本。
        /// </summary>
        /// <param name="contract">宿主已加载的契约程序集。</param>
        /// <param name="requested">插件请求的程序集标识。</param>
        /// <returns>若请求版本不高于宿主版本，则返回 <see langword="true"/>。</returns>
        private static bool IsCompatibleDependencyInjectionContract(Assembly contract, AssemblyName requested)
        {
            const string dependencyInjectionAbstractions = "Microsoft.Extensions.DependencyInjection.Abstractions";
            var contractName = contract.GetName();
            return string.Equals(contractName.Name, dependencyInjectionAbstractions, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(requested.Name, dependencyInjectionAbstractions, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(contractName.CultureName ?? string.Empty, requested.CultureName ?? string.Empty,
                       StringComparison.OrdinalIgnoreCase) &&
                   PublicKeyTokensEqual(contractName, requested) &&
                   requested.Version != null && contractName.Version != null &&
                   requested.Version <= contractName.Version;
        }

        /// <summary>
        /// 比较程序集名称、版本、区域性和公钥标记。
        /// </summary>
        /// <param name="left">已加载程序集标识。</param>
        /// <param name="right">请求程序集标识。</param>
        /// <returns>身份完全一致时返回 <see langword="true" />。</returns>
        internal static bool HasSameAssemblyIdentity(AssemblyName left, AssemblyName right)
        {
            if (left == null || right == null ||
                !string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) ||
                left.Version != right.Version ||
                !string.Equals(left.CultureName ?? string.Empty, right.CultureName ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return PublicKeyTokensEqual(left, right);
        }

        /// <summary>
        /// 比较程序集公钥标记。
        /// </summary>
        /// <param name="left">左侧程序集标识。</param>
        /// <param name="right">右侧程序集标识。</param>
        /// <returns>公钥标记一致时返回 <see langword="true" />。</returns>
        private static bool PublicKeyTokensEqual(AssemblyName left, AssemblyName right)
        {
            var leftToken = left.GetPublicKeyToken() ?? Array.Empty<byte>();
            var rightToken = right.GetPublicKeyToken() ?? Array.Empty<byte>();
            return leftToken.SequenceEqual(rightToken);
        }
    }
}
