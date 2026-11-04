using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 限制变更图捕获与展开规模的硬预算。
/// </summary>
/// <remarks>
/// 捕获、差异展开和公开载荷都使用同一类硬预算，超限抛出而不返回半结果。
/// </remarks>
internal sealed class GraphTraversalBudget
{
    /// <summary>
    /// 保存限制图遍历的配置。
    /// </summary>
    private readonly ChangeGraphOptions _options;
    /// <summary>
    /// 记录已消耗的遍历步数。
    /// </summary>
    private int _steps;
    /// <summary>
    /// 初始化一个 <see cref="GraphTraversalBudget" /> 类型的实例。
    /// </summary>
    /// <param name="options">用于初始化实例的配置选项。</param>
    internal GraphTraversalBudget(ChangeGraphOptions options) => _options = options;
    /// <summary>
    /// 在当前路径遍历对象图并检查递归预算。
    /// </summary>
    /// <param name="path">当前成员或集合元素的图路径。</param>
    /// <param name="depth">当前递归深度。</param>
    /// <remarks>
    /// 超过深度或遍历步数上限时抛出 ChangeGraphCaptureException。
    /// </remarks>
    internal void Visit(string path, int depth)
    {
        if (depth > _options.MaxDepth)
            throw new ChangeGraphCaptureException(path, new InvalidOperationException($"对象图深度超过限制 {_options.MaxDepth}。"));
        if (++_steps > _options.MaxTraversalSteps)
            throw new ChangeGraphCaptureException(path, new InvalidOperationException($"对象图遍历步数超过限制 {_options.MaxTraversalSteps}。"));
    }
}

/// <summary>
/// 在变更图捕获期间解析对象身份和属性规则。
/// </summary>
internal sealed class GraphCaptureContext
{
    /// <summary>
    /// 保存捕获期间使用的变更图配置。
    /// </summary>
    private readonly ChangeGraphOptions _options;
    /// <summary>
    /// 指示是否忽略调用方键比较器，改用领域默认相等语义。
    /// </summary>
    private readonly bool _ignoreOverrides;
    /// <summary>
    /// 保存已捕获节点按视图键索引的映射。
    /// </summary>
    private readonly Dictionary<CaptureViewKey, GraphNode> _nodes = new Dictionary<CaptureViewKey, GraphNode>();
    /// <summary>
    /// 保存集合容器按视图键索引的映射。
    /// </summary>
    private readonly Dictionary<CaptureViewKey, GraphValue> _containerViews = new Dictionary<CaptureViewKey, GraphValue>();
    /// <summary>
    /// 保存对象到其容器视图映射的引用索引。
    /// </summary>
    private readonly Dictionary<object, Dictionary<Type, object>> _containers = new Dictionary<object, Dictionary<Type, object>>(ReferenceEqualityComparer.Instance);
    /// <summary>
    /// 保存本次捕获共享的遍历预算。
    /// </summary>
    private readonly GraphTraversalBudget _budget;
    /// <summary>
    /// 记录已捕获的节点数量。
    /// </summary>
    private int _nodeCount;
    /// <summary>
    /// 保存当前模型定义的标识，用于视图去重。
    /// </summary>
    private int _definitionId;

    /// <summary>
    /// 初始化一个 <see cref="GraphCaptureContext" /> 类型的实例。
    /// </summary>
    /// <param name="options">用于初始化实例的配置选项。</param>
    /// <param name="budget">限制对象图遍历范围的预算。</param>
    internal GraphCaptureContext(ChangeGraphOptions options, GraphTraversalBudget budget = null,
        bool ignoreOverrides = false)
    {
        _options = options ?? ChangeGraphOptions.Default;
        _budget = budget ?? new GraphTraversalBudget(_options);
        _ignoreOverrides = ignoreOverrides;
    }

    /// <summary>
    /// 解析对象身份键选择器。
    /// </summary>
    /// <param name="fallback">调用方提供的默认身份键选择器。</param>
    /// <typeparam name="T">需要解析身份键的对象类型。</typeparam>
    /// <returns>用于选择对象身份键的委托；未配置覆盖规则时使用 fallback。</returns>
    /// <remarks>
    /// 优先使用模型选项配置的选择器；未配置时回退到指定选择器。
    /// </remarks>
    internal Func<T, object> ResolveIdentity<T>(Func<T, object> fallback) => _options.ResolveIdentity(fallback);

    /// <summary>
    /// 判断模型是否为适配器模型。
    /// </summary>
    /// <param name="model">待检查的变更图模型。</param>
    /// <returns>model 已注册为适配器模型时为 true；否则为 false。</returns>
    /// <remarks>
    /// 按模型选项中的适配器规则返回 true 或 false。
    /// </remarks>
    internal bool IsAdapter(ChangeGraphModel model) => _options.IsAdapter(model);

    /// <summary>
    /// 为集合成员生成稳定的规则标识。
    /// </summary>
    /// <param name="name">集合成员名称。</param>
    /// <typeparam name="TOwner">声明集合成员的所有者类型。</typeparam>
    /// <typeparam name="TItem">集合中元素的类型。</typeparam>
    /// <returns>由所有者类型全名和成员名组成的集合键规则标识。</returns>
    /// <remarks>
    /// 标识由所有者完整类型名和成员名称组成。
    /// </remarks>
    internal string CollectionRuleId<TOwner, TItem>(string name) => typeof(TOwner).FullName + "." + name;

    /// <summary>
    /// 使用已注册适配器捕获值的变更图表示。
    /// </summary>
    /// <param name="value">待捕获的对象值。</param>
    /// <param name="path">对象在图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <typeparam name="T">待捕获对象的静态类型。</typeparam>
    /// <returns>按适配器模型捕获的图值；value 为 null 时返回空值，标量类型保留为标量图值。</returns>
    /// <remarks>
    /// 根据值类型或声明类型查找适配器；无适配器或模型类型不兼容时抛出捕获异常。
    /// </remarks>
    internal GraphValue CaptureAdapted<T>(T value, string path, int depth)
    {
        if (value == null) return GraphValue.Null;
        // 领域模型优先：接口、object 与开放属性访问不得用调用方适配器覆盖实体契约。
        ChangeGraphModel model;
        if (value is IChangeGraphNode node && node.ChangeGraphModel.ModelType == value.GetType())
            model = node.ChangeGraphModel;
        else if (!_options.TryAdapter(value.GetType(), out model) && !_options.TryAdapter(typeof(T), out model))
        {
            if (IsScalarRuntimeType(value.GetType())) return GraphValue.Scalar(value);
            throw CaptureFailure(path, $"未知类型 {value.GetType().FullName} 必须通过 WithAdapter 显式注册模型。");
        }
        if (model.IsAdapterPlaceholder)
            throw CaptureFailure(path, "WithAdapter 必须注册实际捕获模型，不能注册需再次解析的适配占位模型。");
        return model.CaptureValue(value, this, path, depth);
    }

    /// <summary>
    /// 按当前路径规则验证对象模型。
    /// </summary>
    /// <param name="model">待验证的对象成员模型。</param>
    /// <param name="path">模型所属对象的图路径。</param>
    internal void ValidateModel(ChangeGraphModel model, string path) => _options.ValidateModel(model,
        name => IsIgnored(string.IsNullOrEmpty(path) ? name : path + "." + name));

    /// <summary>
    /// 按所有者和成员路径规则验证集合元素模型。
    /// </summary>
    /// <param name="memberName">集合属性的成员名称。</param>
    /// <param name="model">集合元素模型。</param>
    /// <param name="path">集合属性的图路径。</param>
    /// <typeparam name="TOwner">声明集合属性的所有者类型。</typeparam>
    /// <typeparam name="TItem">集合元素的类型。</typeparam>
    internal void ValidateCollectionItem<TOwner, TItem>(string memberName, ChangeGraphModel<TItem> model, string path) =>
        _options.ValidateCollectionItem<TOwner, TItem>(memberName, model, name => IsIgnored(path + "." + name));

    /// <summary>
    /// 解析集合元素的稳定身份键选择器。
    /// </summary>
    /// <param name="memberName">集合属性名称。</param>
    /// <param name="fallback">调用方提供的默认元素键选择器。</param>
    /// <typeparam name="TOwner">声明集合属性的所有者类型。</typeparam>
    /// <typeparam name="T">集合元素的类型。</typeparam>
    /// <returns>按所有者类型和成员名解析的集合键选择委托；未配置覆盖规则时使用 fallback。</returns>
    /// <remarks>
    /// 使用模型配置的集合键规则；没有匹配配置时回退到指定选择器。
    /// </remarks>
    internal Func<T, object> ResolveCollectionKey<TOwner, T>(string memberName, Func<T, object> fallback) =>
        _options.ResolveCollectionKey<TOwner, T>(memberName, fallback);

    /// <summary>
    /// 判断指定成员路径是否命中忽略规则。
    /// </summary>
    /// <param name="path">待检查的对象成员路径。</param>
    /// <returns>path 等于忽略路径或位于其成员路径下方时为 true；否则为 false。</returns>
    /// <remarks>
    /// 规则按路径边界匹配：目标路径相等或位于被忽略成员的子树时返回 true；否则返回 false。
    /// </remarks>
    internal bool IsIgnored(string path) => false;

    /// <summary>
    /// 规范化对象图路径并清除键文本中的引号内容。
    /// </summary>
    /// <param name="path">待规范化的成员路径。</param>
    /// <returns>移除方括号和花括号内的索引或键内容，同时保留路径边界及其余文本。</returns>
    /// <remarks>
    /// 保留方括号和花括号分隔符，避免键中的标点被解析成额外路径段。
    /// </remarks>
    private static string NormalizePath(string path)
    {
        var builder = new System.Text.StringBuilder(path.Length);
        var square = false;
        var brace = false;
        var quoted = false;
        var escaped = false;
        foreach (var character in path)
        {
            if (square || brace)
            {
                if (escaped) { escaped = false; continue; }
                if (character == '\\' && quoted) { escaped = true; continue; }
                if (character == '"') { quoted = !quoted; continue; }
                if (quoted) continue;
            }
            if (character == '[')
            {
                square = true;
                builder.Append('[');
                continue;
            }
            if (character == ']')
            {
                square = false;
                builder.Append(']');
                continue;
            }
            if (character == '{')
            {
                brace = true;
                builder.Append('{');
                continue;
            }
            if (character == '}')
            {
                brace = false;
                builder.Append('}');
                continue;
            }
            if (!square && !brace)
                builder.Append(character);
        }
        return builder.ToString();
    }

    /// <summary>
    /// 按身份键捕获对象及其成员值。
    /// </summary>
    /// <param name="value">要捕获的对象；null 会转换为 Null 图值。</param>
    /// <param name="model">描述对象成员的变更图模型。</param>
    /// <param name="path">对象在图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <param name="capture">将对象成员写入图节点的回调。</param>
    /// <param name="keySelector">选择对象身份键的委托。</param>
    /// <param name="applyIdentity">是否应用模型配置的身份键规则。</param>
    /// <typeparam name="T">待捕获对象的静态类型。</typeparam>
    /// <returns>value 为 null 时返回空值；否则返回包含已捕获成员的对象图值。</returns>
    /// <remarks>
    /// 引用对象按对象、模型、键和忽略规则复用既有节点；节点数超限或身份键类型无效时抛出捕获异常。
    /// </remarks>
    internal GraphValue CaptureObject<T>(T value, ChangeGraphModel<T> model, string path, int depth,
        Action<ChangeGraphWriter<T>, T> capture, Func<T, object> keySelector, bool applyIdentity = true)
    {
        if (value == null)
            return GraphValue.Null;
        var effectiveKeySelector = applyIdentity ? ResolveIdentity(keySelector) : keySelector;
        var key = effectiveKeySelector == null ? null : effectiveKeySelector(value);
        ValidateKey(key, path);
        var view = new CaptureViewKey(value, model, key, RuleView(path));
        if (!typeof(T).IsValueType && _nodes.TryGetValue(view, out var existing))
            return GraphValue.Object(existing);
        if (++_nodeCount > _options.MaxNodes)
            throw CaptureFailure(path, $"对象图节点数超过限制 {_options.MaxNodes}。");
        var node = new GraphNode(model.ModelId, key, ++_definitionId, model.IsDomainEntity);
        // 在递归前登记；规则剩余状态有限，因此自环仍可终止，且不同入口不串用视图。
        if (!typeof(T).IsValueType)
            _nodes.Add(view, node);
        capture(new ChangeGraphWriter<T>(this, node, path, depth), value);
        return GraphValue.Object(node);
    }

    /// <summary>
    /// 捕获对象序列并按可选身份键生成集合图值。
    /// </summary>
    /// <param name="values">待枚举的元素集合；null 转换为 Null 图值。</param>
    /// <param name="model">集合元素的对象模型。</param>
    /// <param name="keySelector">可选的稳定元素键选择器。</param>
    /// <param name="path">集合在对象图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <param name="ruleId">可选的集合规则标识。</param>
    /// <typeparam name="T">集合元素的类型。</typeparam>
    /// <returns>按枚举顺序捕获的集合图值；values 为 null 时返回空值。</returns>
    /// <remarks>
    /// 重复集合键或无效身份键会导致捕获失败；项目数和遍历深度受配置限制。
    /// </remarks>
    internal GraphValue CaptureSequence<T>(IEnumerable<T> values, ChangeGraphModel<T> model, Func<T, object> keySelector,
        string path, int depth, string ruleId = null, bool ownerDomainRules = false)
    {
        if (values == null)
            return GraphValue.Null;
        Visit(path, depth);
        ValidateModel(model, path + "[]");
        var view = new CaptureViewKey(values, model, ruleId ?? (keySelector == null ? "position" : "identity"), RuleView(path));
        if (_containerViews.TryGetValue(view, out var cached)) return cached;
        var items = new List<GraphCollectionItem>();
        var result = GraphValue.Collection(items, keySelector != null).WithNodeId(++_definitionId);
        _containerViews.Add(view, result);
        var ignoreKeyOverrides = ownerDomainRules || model.IsDomainEntity;
        var seenKeys = keySelector == null ? null : new HashSet<object>(_options.KeyComparer(false, ignoreKeyOverrides));
        var index = 0;
        foreach (var value in Materialize(values, path))
        {
            Visit(path, 0);
            if (++index > _options.MaxCollectionItems)
                throw new InvalidOperationException($"集合元素数超过限制 {_options.MaxCollectionItems}，路径: {path}");
            var key = keySelector == null ? null : keySelector(value);
            ValidateKey(key, path);
            if (keySelector != null && IsInvalidIdentityKey(key))
                throw CaptureFailure(path + "[" + (index - 1) + "]", "集合键不能为 null 或默认值。");
            if (seenKeys != null && !seenKeys.Add(key))
                throw CaptureFailure(path, $"集合中存在重复键 {FormatKey(key)}。");
            var item = model.CaptureValue(value, this,
                path + "[" + (keySelector == null ? (index - 1).ToString() : FormatKey(key)) + "]", depth + 1, keySelector == null ? null : new Func<T, object>(_ => key));
            items.Add(new GraphCollectionItem(key, item, index - 1));
        }
        return result;
    }

    /// <summary>
    /// 捕获标量序列并保留元素顺序。
    /// </summary>
    /// <param name="values">待捕获的标量值集合；null 转换为 Null 图值。</param>
    /// <param name="path">集合在对象图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <typeparam name="T">标量元素的类型。</typeparam>
    /// <returns>按枚举顺序捕获的标量集合图值；values 为 null 时返回空值。</returns>
    /// <remarks>
    /// 枚举集合受最大元素数和遍历预算限制。
    /// </remarks>
    internal GraphValue CaptureScalarSequence<T>(IEnumerable<T> values, string path, int depth = 0)
    {
        if (values == null)
            return GraphValue.Null;
        Visit(path, depth);
        var items = new List<GraphCollectionItem>();
        var index = 0;
        foreach (var value in Materialize(values, path))
        {
            Visit(path, 0);
            if (++index > _options.MaxCollectionItems)
                throw new InvalidOperationException($"集合元素数超过限制 {_options.MaxCollectionItems}，路径: {path}");
            items.Add(new GraphCollectionItem(null, GraphValue.Scalar(value), index - 1));
        }
        return GraphValue.Collection(items, false).WithNodeId(++_definitionId);
    }

    /// <summary>
    /// 捕获集合语义的图值并按元素键去重。
    /// </summary>
    /// <param name="values">待捕获的元素集合；null 转换为 Null 图值。</param>
    /// <param name="model">集合元素的对象模型。</param>
    /// <param name="keySelector">可选的稳定元素键选择器。</param>
    /// <param name="path">集合在对象图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <param name="ruleId">可选的集合规则标识。</param>
    /// <typeparam name="T">集合元素的类型。</typeparam>
    /// <returns>将 values 捕获并标记为集合的图值；values 为 null 时保留空值语义。</returns>
    /// <remarks>
    /// 复杂 Set 元素没有身份键时抛出捕获异常。
    /// </remarks>
    internal GraphValue CaptureSet<T>(IEnumerable<T> values, ChangeGraphModel<T> model, Func<T, object> keySelector,
        string path, int depth, string ruleId = null, bool ownerDomainRules = false)
    {
        if (values == null)
            return GraphValue.Null;
        if (keySelector == null && !IsScalarRuntimeType(typeof(T)))
            throw CaptureFailure(path, "复杂 Set 元素必须提供 ChangeKey、实体 Id 或显式集合键。");
        return CaptureSequence(values, model, keySelector, path, depth, ruleId, ownerDomainRules).AsSet();
    }

    /// <summary>
    /// 将标量序列转换为集合图值并按键比较去重。
    /// </summary>
    /// <param name="values">待捕获的标量集合；null 转换为 Null 集合图值。</param>
    /// <param name="path">集合在对象图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <typeparam name="T">标量元素的类型。</typeparam>
    /// <returns>按标量值键去重后生成的集合图值；values 为 null 时保留空值语义。</returns>
    /// <remarks>
    /// 重复的标量值按当前键比较器保留首次出现项。
    /// </remarks>
    internal GraphValue CaptureScalarSet<T>(IEnumerable<T> values, string path, int depth = 0, bool ownerDomainRules = false)
    {
        var sequence = CaptureScalarSequence(values, path, depth);
        if (sequence.Kind == ChangeValueKind.Null)
            return sequence.AsSet();
        var seen = new HashSet<object>(_options.KeyComparer(true, ownerDomainRules));
        var keyed = sequence.Items.Select(item => new GraphCollectionItem(
            ChangeGraphKey.Create(item.Value.Kind == ChangeValueKind.Scalar ? item.Value.ScalarValue : null),
            item.Value,
            item.Index)).Where(item => seen.Add(item.Key)).ToList();
        return GraphValue.Collection(keyed, true).WithNodeId(sequence.NodeId.Value).AsSet();
    }

    /// <summary>
    /// 捕获以字典键标识的对象值集合。
    /// </summary>
    /// <param name="values">待捕获的键值对集合；null 转换为 Null 图值。</param>
    /// <param name="model">字典值的对象模型。</param>
    /// <param name="path">字典在对象图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <typeparam name="TKey">字典键的类型。</typeparam>
    /// <typeparam name="TValue">字典值的类型。</typeparam>
    /// <returns>以字典键和值模型捕获的字典图值；values 为 null 时返回空值。</returns>
    /// <remarks>
    /// 字典键须为非 null 的可冻结标量或复合键，重复键和超过集合上限时抛出捕获异常。
    /// </remarks>
    internal GraphValue CaptureDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values,
        ChangeGraphModel<TValue> model, string path, int depth, bool ownerDomainRules = false)
    {
        if (values == null)
            return GraphValue.Null;
        Visit(path, depth);
        var view = new CaptureViewKey(values, model, "dictionary", RuleView(path));
        if (_containerViews.TryGetValue(view, out var cached)) return cached;
        var items = new List<GraphCollectionItem>();
        var result = GraphValue.Dictionary(items).WithNodeId(++_definitionId);
        _containerViews.Add(view, result);
        var seenKeys = new HashSet<object>(_options.KeyComparer(false, ownerDomainRules));
        var index = 0;
        foreach (var pair in Materialize(values, path))
        {
            Visit(path, 0);
            var key = (object)pair.Key;
            ValidateKey(key, path);
            if (index >= _options.MaxCollectionItems)
                throw CaptureFailure(path, $"集合元素数超过限制 {_options.MaxCollectionItems}。");
            if (key == null)
                throw CaptureFailure(path, "字典键不能为 null。");
            if (!seenKeys.Add(key))
                throw CaptureFailure(path, $"字典中存在重复键 {FormatKey(key)}。");
            items.Add(new GraphCollectionItem(key,
                model.CaptureValue(pair.Value, this, path + "{" + FormatKey(key) + "}", depth + 1,
                    value => key), index++));
        }
        return result;
    }

    /// <summary>
    /// 捕获标量字典并记录每个键对应的标量值。
    /// </summary>
    /// <param name="values">待捕获的字典键值对集合；null 转换为 Null 图值。</param>
    /// <param name="path">字典在对象图中的成员路径。</param>
    /// <param name="depth">当前图递归深度。</param>
    /// <typeparam name="TKey">字典键的类型。</typeparam>
    /// <typeparam name="TValue">字典值的标量类型。</typeparam>
    /// <returns>以字典键和标量值捕获的字典图值；values 为 null 时返回空值。</returns>
    /// <remarks>
    /// 字典键须唯一且不能为 null；集合元素数受配置上限限制。
    /// </remarks>
    internal GraphValue CaptureScalarDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values, string path, int depth = 0, bool ownerDomainRules = false)
    {
        if (values == null)
            return GraphValue.Null;
        Visit(path, depth);
        var items = new List<GraphCollectionItem>();
        var seenKeys = new HashSet<object>(_options.KeyComparer(false, ownerDomainRules));
        var index = 0;
        foreach (var pair in Materialize(values, path))
        {
            Visit(path, 0);
            var key = (object)pair.Key;
            ValidateKey(key, path);
            if (index >= _options.MaxCollectionItems)
                throw CaptureFailure(path, $"集合元素数超过限制 {_options.MaxCollectionItems}。");
            if (key == null)
                throw new InvalidOperationException($"字典键不能为 null，路径: {path}");
            if (!seenKeys.Add(key))
                throw CaptureFailure(path, $"字典中存在重复键 {FormatKey(key)}。");
            items.Add(new GraphCollectionItem(key, GraphValue.Scalar(pair.Value), index++));
        }
        return GraphValue.Dictionary(items).WithNodeId(++_definitionId);
    }

    /// <summary>
    /// 将对象路径和递归深度交由捕获预算检查。
    /// </summary>
    /// <param name="path">当前成员或集合元素的图路径。</param>
    /// <param name="depth">当前递归深度。</param>
    internal void Visit(string path, int depth) => _budget.Visit(path, depth);

    /// <summary>
    /// 将可枚举容器物化为可重复读取的列表。
    /// </summary>
    /// <param name="values">待物化的枚举容器。</param>
    /// <param name="path">容器在对象图中的成员路径。</param>
    /// <typeparam name="T">容器元素的类型。</typeparam>
    /// <returns>由 values 物化并按集合实例缓存的只读元素列表。</returns>
    /// <remarks>
    /// 按容器及元素类型缓存物化结果；默认拒绝 IQueryable 和 IAsyncEnumerable。
    /// </remarks>
    private IReadOnlyList<T> Materialize<T>(IEnumerable<T> values, string path)
    {
        if (_containers.TryGetValue(values, out var shapes) && shapes.TryGetValue(typeof(T), out var cached))
            return (IReadOnlyList<T>)cached;
        if (values is IQueryable)
            throw CaptureFailure(path, "默认不支持 IQueryable；请先物化为 IEnumerable 或注册显式适配器。");
        if (values is IAsyncEnumerable<T>)
            throw CaptureFailure(path, "默认不支持异步流；请先物化为 IEnumerable 或注册显式适配器。");
        var items = new List<T>();
        ContainerId(values, path);
        try
        {
            foreach (var value in values)
            {
                Visit(path, 0);
                if (items.Count >= _options.MaxCollectionItems)
                    throw CaptureFailure(path, $"集合元素数超过限制 {_options.MaxCollectionItems}。");
                items.Add(value);
            }
        }
        catch (ChangeGraphCaptureException) { throw; }
        catch (Exception exception) { throw new ChangeGraphCaptureException(path, exception); }
        if (shapes == null)
            _containers.Add(values, shapes = new Dictionary<Type, object>());
        shapes.Add(typeof(T), items);
        return items;
    }

    /// <summary>
    /// 保存集合容器引用到标识的映射。
    /// </summary>
    private readonly Dictionary<object, int> _containerIds = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
    /// <summary>
    /// 获取或分配容器的稳定图节点编号。
    /// </summary>
    /// <param name="container">待登记的集合或字典对象。</param>
    /// <param name="path">容器在对象图中的成员路径。</param>
    /// <returns>container 对应的稳定节点编号；相同容器实例复用已有编号。</returns>
    /// <remarks>
    /// 达到对象节点上限时抛出捕获异常。
    /// </remarks>
    private int ContainerId(object container, string path)
    {
        if (_containerIds.TryGetValue(container, out var id)) return id;
        if (++_nodeCount > _options.MaxNodes)
            throw CaptureFailure(path, $"对象图节点数超过限制 {_options.MaxNodes}。");
        _containerIds.Add(container, _nodeCount);
        return _nodeCount;
    }

    /// <summary>
    /// 生成当前路径下仍适用的忽略规则视图。
    /// </summary>
    /// <param name="path">待匹配忽略规则的对象路径。</param>
    /// <returns>当前路径下适用的忽略规则相对视图字符串。</returns>
    /// <remarks>
    /// 返回相对于当前路径的规范化规则，并按序连接以用于捕获视图去重。
    /// </remarks>
    private static string RuleView(string path) => string.Empty;

    /// <summary>
    /// 验证对象身份键是否为可冻结的标量或复合键。
    /// </summary>
    /// <param name="key">待检查的对象身份键。</param>
    /// <param name="path">键所处的对象图路径。</param>
    /// <remarks>
    /// 递归验证复合键的全部组成项；不支持的运行时类型会产生包含路径的捕获异常。
    /// </remarks>
    internal static void ValidateKey(object key, string path)
    {
        if (key is ChangeGraphKey composite)
        {
            foreach (var part in composite.Components) ValidateKey(part, path);
        }
        else if (key != null && !IsScalarRuntimeType(key.GetType()))
            throw CaptureFailure(path, $"键类型 {key.GetType().FullName} 不是可冻结的标量或 ChangeGraphKey。");
    }

    /// <summary>
    /// 标识一次对象实例对应的变更图捕获视图。
    /// </summary>
    private readonly struct CaptureViewKey : IEquatable<CaptureViewKey>
    {
        /// <summary>
        /// 保存视图键引用的对象实例。
        /// </summary>
        private readonly object _reference;
        /// <summary>
        /// 保存视图键对应的变更图模型。
        /// </summary>
        private readonly ChangeGraphModel _model;
        /// <summary>
        /// 保存视图键使用的身份键。
        /// </summary>
        private readonly object _key;
        /// <summary>
        /// 保存视图键包含的规则标识。
        /// </summary>
        private readonly string _rules;
        /// <summary>
        /// 初始化一个 <see cref="CaptureViewKey" /> 类型的实例。
        /// </summary>
        /// <param name="reference">对象图中捕获的引用对象。</param>
        /// <param name="model">用于分析当前调用的语义模型。</param>
        /// <param name="key">对象在容器内的键。</param>
        /// <param name="rules">用于匹配路径的规则集合。</param>
        internal CaptureViewKey(object reference, ChangeGraphModel model, object key, string rules)
        { _reference = reference; _model = model; _key = key; _rules = rules; }
        /// <inheritdoc />
        public bool Equals(CaptureViewKey other) => ReferenceEquals(_reference, other._reference) &&
            ReferenceEquals(_model, other._model) && Equals(_key, other._key) && _rules == other._rules;
        /// <inheritdoc />
        public override bool Equals(object value) => value is CaptureViewKey other && Equals(other);
        /// <inheritdoc />
        public override int GetHashCode() => ((System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_reference) * 397) ^
            _model.GetHashCode()) * 397 ^ (_key?.GetHashCode() ?? 0) ^ _rules.GetHashCode();
    }

    /// <summary>
    /// 将身份键格式化为可读且无歧义的路径文本。
    /// </summary>
    /// <param name="key">待格式化的身份键；null 表示空键。</param>
    /// <returns>按图键类型规则生成的稳定文本表示。</returns>
    /// <remarks>
    /// 格式化复合键、字符串、数值和其他标量时会转义分隔符。
    /// </remarks>
    internal static string FormatKey(object key)
    {
        if (key == null)
            return "null";
        if (key is ChangeGraphKey composite)
        {
            var formatted = string.Join(",", composite.Components.Select(part => part is ChangeGraphKey
                ? "(" + FormatKey(part) + ")" : FormatKey(part)));
            return composite.Components.Count == 1 ? "(" + formatted + ")" : formatted;
        }
        if (key is string text)
        {
            if (text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') &&
                text.All(character => char.IsLetterOrDigit(character) || character == '_' || character == '-') &&
                text != "null" && text != "true" && text != "false")
                return text;
            return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
        }
        if (key is int number)
            return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var format = key is DateTime || key is DateTimeOffset ? "O" : key is double || key is float ? "R" : null;
        var rendered = key is Uri uri ? uri.OriginalString : key is IFormattable formattable
            ? formattable.ToString(format, System.Globalization.CultureInfo.InvariantCulture)
            : Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture);
        // 类型名和值均在引号内，嵌套类型名和 char 等也不会引入额外路径段。
        return "@(" + QuoteKey(key.GetType().FullName) + "," + QuoteKey(rendered) + ")";
    }

    /// <summary>
    /// 对身份键文本中的路径特殊字符进行转义。
    /// </summary>
    /// <param name="text">要引用的键文本。</param>
    /// <returns>转义反斜杠、引号和控制字符后加引号的键文本。</returns>
    /// <remarks>
    /// 转义反斜杠、引号、回车、换行及制表符。
    /// </remarks>
    private static string QuoteKey(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    /// <summary>
    /// 判断集合元素身份键是否为空或为数值默认值。
    /// </summary>
    /// <param name="key">待检查的元素身份键。</param>
    /// <returns>key 为 null、Guid.Empty、受检查整数类型的零值，或复合键包含无效分量时为 true；否则为 false。</returns>
    /// <remarks>
    /// null、空复合键组成项及支持数值类型的零值返回 true；其他值返回 false。
    /// </remarks>
    private static bool IsInvalidIdentityKey(object key)
    {
        if (key == null)
            return true;
        if (key is ChangeGraphKey composite)
            return composite.Components.Any(IsInvalidIdentityKey);
        if (key is Guid guid)
            return guid == Guid.Empty;
        if (key is int intValue)
            return intValue == 0;
        if (key is long longValue)
            return longValue == 0L;
        if (key is short shortValue)
            return shortValue == 0;
        if (key is byte byteValue)
            return byteValue == 0;
        if (key is uint uintValue)
            return uintValue == 0U;
        if (key is ulong ulongValue)
            return ulongValue == 0UL;
        if (key is ushort ushortValue)
            return ushortValue == 0;
        if (key is sbyte sbyteValue)
            return sbyteValue == 0;
        return false;
    }

    /// <summary>
    /// 判断 CLR 类型是否为支持捕获的标量类型。
    /// </summary>
    /// <param name="type">待检查的 CLR 类型。</param>
    /// <returns>type 的底层运行时类型可作为图标量直接捕获时为 true；否则为 false。</returns>
    /// <remarks>
    /// 识别可空基础类型、枚举、原始值、字符串、日期时间、时长、Guid、Uri 和 Version。
    /// </remarks>
    private static bool IsScalarRuntimeType(Type type)
    {
        if (type == null)
            return false;
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable != null)
            type = nullable;
        return type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) ||
            type == typeof(Guid) || type == typeof(Uri) || type == typeof(Version);
    }

    /// <summary>
    /// 创建包含图路径和原始异常的捕获异常。
    /// </summary>
    /// <param name="path">捕获失败的对象图路径。</param>
    /// <param name="message">用于构造内部异常的错误说明。</param>
    /// <returns>封装 path 和指定原因的 ChangeGraphCaptureException。</returns>
    private static ChangeGraphCaptureException CaptureFailure(string path, string message) =>
        new ChangeGraphCaptureException(path, new InvalidOperationException(message));
}
