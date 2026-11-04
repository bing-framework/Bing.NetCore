using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 对象图比较选项。
/// </summary>
public sealed class ChangeGraphOptions
{
    /// <summary>
    /// 保存按标量类型登记的值比较器。
    /// </summary>
    private readonly Dictionary<Type, Func<object, object, bool>> _scalarComparers =
        new Dictionary<Type, Func<object, object, bool>>();
    /// <summary>
    /// 保存按标量类型登记的哈希函数。
    /// </summary>
    private readonly Dictionary<Type, Func<object, int>> _scalarHashes = new Dictionary<Type, Func<object, int>>();
    /// <summary>
    /// 保存按类型登记的键相等比较器。
    /// </summary>
    private readonly Dictionary<Type, IEqualityComparer<object>> _keyComparers = new Dictionary<Type, IEqualityComparer<object>>();
    /// <summary>
    /// 保存按类型登记的显式适配器模型。
    /// </summary>
    private readonly Dictionary<Type, ChangeGraphModel> _adapters = new Dictionary<Type, ChangeGraphModel>();

    /// <summary>
    /// 获取默认选项。
    /// </summary>
    public static ChangeGraphOptions Default => new ChangeGraphOptions();

    /// <summary>
    /// 获取或设置最大递归深度。
    /// </summary>
    /// <remarks>默认值为 64。</remarks>
    public int MaxDepth { get; set; } = 64;
    /// <summary>
    /// 获取或设置最大节点数。
    /// </summary>
    /// <remarks>默认值为 10000。</remarks>
    public int MaxNodes { get; set; } = 10000;
    /// <summary>
    /// 获取或设置单个集合最大元素数。
    /// </summary>
    /// <remarks>默认值为 10000。</remarks>
    public int MaxCollectionItems { get; set; } = 10000;
    /// <summary>
    /// 获取或设置最大遍历步数。
    /// </summary>
    /// <remarks>默认值为 100000。</remarks>
    public int MaxTraversalSteps { get; set; } = 100000;
    /// <summary>
    /// 获取或设置是否报告有稳定键集合中的相对移动。
    /// </summary>
    /// <remarks>默认值为 false。</remarks>
    public bool TrackMoves { get; set; }

    /// <summary>
    /// 创建包含当前设置和注册规则的变更图选项副本。
    /// </summary>
    /// <returns>独立的选项副本。</returns>
    internal ChangeGraphOptions Clone()
    {
        var clone = new ChangeGraphOptions
        {
            MaxDepth = MaxDepth,
            MaxNodes = MaxNodes,
            MaxCollectionItems = MaxCollectionItems,
            MaxTraversalSteps = MaxTraversalSteps,
            TrackMoves = TrackMoves
        };
        foreach (var rule in _scalarComparers)
            clone._scalarComparers[rule.Key] = rule.Value;
        foreach (var hash in _scalarHashes)
            clone._scalarHashes[hash.Key] = hash.Value;
        foreach (var comparer in _keyComparers)
            clone._keyComparers[comparer.Key] = comparer.Value;
        foreach (var adapter in _adapters) clone._adapters[adapter.Key] = adapter.Value;
        return clone;
    }

    /// <summary>
    /// 创建设置集合移动跟踪状态的变更图选项副本。
    /// </summary>
    /// <param name="enabled">是否跟踪集合项移动。</param>
    /// <returns>启用或禁用移动跟踪的新选项。</returns>
    public ChangeGraphOptions WithTrackMoves(bool enabled = true)
    {
        var clone = Clone();
        clone.TrackMoves = enabled;
        return clone;
    }

    /// <summary>
    /// 创建配置指定标量相等比较器的变更图选项副本。
    /// </summary>
    /// <param name="comparer">比较 T 类型标量值的相等比较器。</param>
    /// <typeparam name="T">要比较的标量类型。</typeparam>
    /// <returns>包含指定标量相等比较器及其哈希实现的新选项。</returns>
    public ChangeGraphOptions WithComparer<T>(IEqualityComparer<T> comparer)
    {
        if (comparer == null)
            throw new ArgumentNullException(nameof(comparer));
        var clone = Clone();
        clone._scalarComparers[typeof(T)] = (left, right) =>
            comparer.Equals(left == null ? default(T) : (T)left, right == null ? default(T) : (T)right);
        clone._scalarHashes[typeof(T)] = value => value == null ? 0 : comparer.GetHashCode((T)value);
        return clone;
    }

    /// <summary>
    /// 创建配置指定身份键比较器的变更图选项副本。
    /// </summary>
    /// <param name="comparer">比较 T 类型身份键的相等比较器。</param>
    /// <typeparam name="T">身份键的类型。</typeparam>
    /// <returns>包含指定身份键比较器的新选项。</returns>
    public ChangeGraphOptions WithKeyComparer<T>(IEqualityComparer<T> comparer)
    {
        if (comparer == null) throw new ArgumentNullException(nameof(comparer));
        var clone = Clone();
        clone._keyComparers[typeof(T)] = new TypedKeyComparer<T>(comparer);
        return clone;
    }

    /// <summary>
    /// 创建注册指定类型适配器模型的变更图选项副本。
    /// </summary>
    /// <param name="model">用于捕获 T 类型值的适配器模型。</param>
    /// <typeparam name="T">适配器模型处理的类型。</typeparam>
    /// <returns>包含指定适配器模型的新选项。</returns>
    public ChangeGraphOptions WithAdapter<T>(ChangeGraphModel<T> model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        var clone = Clone();
        clone._adapters[typeof(T)] = model;
        return clone;
    }

    /// <summary>
    /// 查找指定类型的已注册适配器模型。
    /// </summary>
    /// <param name="type">要查找适配器模型的类型。</param>
    /// <param name="model">接收找到的适配器模型。</param>
    /// <returns>找到模型时返回 true，否则返回 false。</returns>
    internal bool TryAdapter(Type type, out ChangeGraphModel model) => _adapters.TryGetValue(type, out model);
    /// <summary>
    /// 检查模型实例是否为该类型注册的适配器。
    /// </summary>
    /// <param name="model">待检查的变更图模型。</param>
    /// <returns>该实例与模型类型对应的已注册模型相同时返回 true，否则返回 false。</returns>
    internal bool IsAdapter(ChangeGraphModel model) => _adapters.TryGetValue(model.ModelType, out var registered) && ReferenceEquals(model, registered);

    /// <summary>
    /// 创建用于比较图节点身份键的比较器。
    /// </summary>
    /// <param name="scalarSet">是否按照标量集合项的规则比较键。</param>
    /// <returns>用于比较身份键的比较器。</returns>
    internal IEqualityComparer<object> KeyComparer(bool scalarSet = false, bool ignoreOverrides = false) => new GraphKeyComparer(this, scalarSet, ignoreOverrides);

    /// <summary>
    /// 将强类型键比较器适配为对象键比较器。
    /// </summary>
    /// <typeparam name="T">强类型键比较器处理的键值类型。</typeparam>
    private sealed class TypedKeyComparer<T> : IEqualityComparer<object>
    {
        /// <summary>
        /// 保存包装的泛型相等比较器。
        /// </summary>
        private readonly IEqualityComparer<T> _comparer;
        /// <summary>
        /// 初始化一个 <see cref="TypedKeyComparer{T}" /> 类型的实例。
        /// </summary>
        /// <param name="comparer">用于比较键值的相等比较器。</param>
        internal TypedKeyComparer(IEqualityComparer<T> comparer) => _comparer = comparer;
        /// <inheritdoc />
        public new bool Equals(object x, object y) => _comparer.Equals((T)x, (T)y);
        /// <inheritdoc />
        public int GetHashCode(object value) => _comparer.GetHashCode((T)value);
    }

    /// <summary>
    /// 按复合键和配置规则比较变更图键值的比较器。
    /// </summary>
    private sealed class GraphKeyComparer : IEqualityComparer<object>
    {
        /// <summary>
        /// 保存用于解析标量比较规则的变更图配置。
        /// </summary>
        private readonly ChangeGraphOptions _options;
        /// <summary>
        /// 指示比较键是否已按标量集合处理。
        /// </summary>
        private readonly bool _scalarSet;
        /// <summary>
        /// 指示是否忽略调用方键比较器，改用领域默认相等语义。
        /// </summary>
        private readonly bool _ignoreOverrides;
        /// <summary>
        /// 初始化一个 <see cref="GraphKeyComparer" /> 类型的实例。
        /// </summary>
        /// <param name="options">用于初始化实例的配置选项。</param>
        /// <param name="scalarSet">指示当前集合是否使用标量值语义。</param>
        /// <param name="ignoreOverrides">是否忽略调用方键比较器覆盖。</param>
        internal GraphKeyComparer(ChangeGraphOptions options, bool scalarSet, bool ignoreOverrides)
        {
            _options = options;
            _scalarSet = scalarSet;
            _ignoreOverrides = ignoreOverrides;
        }
        /// <inheritdoc />
        public new bool Equals(object x, object y)
        {
            if (x == null || y == null) return x == null && y == null;
            if (x is ChangeGraphKey a && y is ChangeGraphKey b)
                return a.Components.Count == b.Components.Count && a.Components.Zip(b.Components, Equals).All(equal => equal);
            if (x.GetType() != y.GetType()) return false;
            if (!_ignoreOverrides && _options._keyComparers.TryGetValue(x.GetType(), out var comparer)) return comparer.Equals(x, y);
            return _scalarSet && !_ignoreOverrides ? _options.ScalarsEqual(x, y) : object.Equals(x, y);
        }
        /// <inheritdoc />
        public int GetHashCode(object value)
        {
            if (value == null) return 0;
            if (value is ChangeGraphKey composite)
                return composite.Components.Aggregate(17, (hash, part) => unchecked(hash * 31 + GetHashCode(part)));
            if (!_ignoreOverrides && _options._keyComparers.TryGetValue(value.GetType(), out var comparer)) return comparer.GetHashCode(value);
            if (_scalarSet && !_ignoreOverrides && _options._scalarHashes.TryGetValue(value.GetType(), out var hashFunction)) return hashFunction(value);
            return value.GetHashCode();
        }
    }

    /// <summary>
    /// 兼容生成代码的集合规则标识；身份由实体自己的模型提供，父实体不再覆盖。
    /// </summary>
    /// <param name="model">待验证的变更图模型。</param>
    /// <param name="ignored">判断成员是否被忽略的函数。</param>
    internal void ValidateModel(ChangeGraphModel model, Func<string, bool> ignored) { }

    /// <summary>
    /// 兼容生成代码的集合项校验；身份由实体自己的模型提供，父实体不再覆盖。
    /// </summary>
    /// <param name="memberName">集合成员名称。</param>
    /// <param name="model">集合项使用的变更图模型。</param>
    /// <param name="ignored">判断成员是否被忽略的函数。</param>
    /// <typeparam name="TOwner">包含集合成员的所有者类型。</typeparam>
    /// <typeparam name="TItem">集合项的类型。</typeparam>
    internal void ValidateCollectionItem<TOwner, TItem>(string memberName, ChangeGraphModel<TItem> model,
        Func<string, bool> ignored) { }

    /// <summary>
    /// 获取实体自身模型提供的身份选择函数；父级覆盖入口已删除。
    /// </summary>
    /// <param name="fallback">实体自身模型提供的选择函数。</param>
    /// <typeparam name="T">身份选择函数处理的类型。</typeparam>
    /// <returns>始终返回实体自身的 fallback。</returns>
    internal Func<T, object> ResolveIdentity<T>(Func<T, object> fallback) => fallback;

    /// <summary>
    /// 获取集合元素自身模型提供的身份选择函数；父级覆盖入口已删除。
    /// </summary>
    /// <param name="memberName">集合成员名称。</param>
    /// <param name="fallback">元素自身模型提供的选择函数。</param>
    /// <typeparam name="TOwner">包含集合成员的所有者类型。</typeparam>
    /// <typeparam name="T">集合项的类型。</typeparam>
    /// <returns>始终返回元素自身的 fallback。</returns>
    internal Func<T, object> ResolveCollectionKey<TOwner, T>(string memberName, Func<T, object> fallback) => fallback;

    /// <summary>
    /// 比较两个标量值是否相等。
    /// </summary>
    /// <param name="left">第一个标量值。</param>
    /// <param name="right">第二个标量值。</param>
    /// <returns>两个值均为 null，或类型相同且按对应比较器相等时返回 true；否则返回 false。</returns>
    internal bool ScalarsEqual(object left, object right)
    {
        if (left == null || right == null)
            return left == null && right == null;
        if (left.GetType() != right.GetType())
            return false;
        if (_scalarComparers.TryGetValue(left.GetType(), out var comparer))
            return comparer(left, right);
        return Equals(left, right);
    }
}
