using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 忽略属性及其整棵可达子树。
/// </summary>
/// <remarks>
/// 只能标记当前实体声明或继承得到的成员；父实体需要忽略子对象或集合时，应标记整个关系属性。
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class ChangeIgnoreAttribute : Attribute
{
}

/// <summary>
/// 获取当前类型独立生成的对象图模型。
/// </summary>
/// <remarks>
/// 继承 <c>DomainObjectBase&lt;TObject&gt;</c> 的类型由生成器实现此接口；类型不区分根或子对象角色。
/// </remarks>
public interface IChangeGraphNode
{
    /// <summary>
    /// 获取由源生成器生成的根模型。
    /// </summary>
    ChangeGraphModel ChangeGraphModel { get; }
}

/// <summary>
/// 标识对象图中的变更类型。
/// </summary>
public enum ChangeKind
{
    /// <summary>
    /// 对象图中新增了节点或集合元素。
    /// </summary>
    Added,
    /// <summary>
    /// 对象图中移除了节点或集合元素。
    /// </summary>
    Removed,
    /// <summary>
    /// 现有对象成员的值发生变化。
    /// </summary>
    Modified,
    /// <summary>
    /// 现有对象节点被另一节点替换。
    /// </summary>
    Replaced,
    /// <summary>
    /// 集合中的元素位置或键发生变化。
    /// </summary>
    Moved
}

/// <summary>
/// 标识变更值的结构形态。
/// </summary>
public enum ChangeValueKind
{
    /// <summary>
    /// 目标对象中不存在对应成员。
    /// </summary>
    Missing,
    /// <summary>
    /// 对应成员存在，但其值为 null。
    /// </summary>
    Null,
    /// <summary>
    /// 值为非复合的标量数据。
    /// </summary>
    Scalar,
    /// <summary>
    /// 值为可展开的对象节点。
    /// </summary>
    Object,
    /// <summary>
    /// 值为保持元素顺序的集合。
    /// </summary>
    Collection,
    /// <summary>
    /// 值为按键访问的映射。
    /// </summary>
    Dictionary,
    /// <summary>
    /// 值为不按位置索引的集合。
    /// </summary>
    Set
}

/// <summary>
/// 差异值的只读视图。
/// </summary>
public sealed class ChangeValue
{
    /// <summary>
    /// 初始化一个 <see cref="ChangeValue" /> 类型的实例。
    /// </summary>
    /// <param name="kind">当前对象或操作的种类。</param>
    /// <param name="value">用于初始化实例的数据值。</param>
    /// <param name="nodeId">当前节点的标识。</param>
    /// <param name="referenceId">用于引用对象图节点的标识。</param>
    internal ChangeValue(ChangeValueKind kind, object value, int? nodeId = null, int? referenceId = null)
    {
        Kind = kind;
        Value = value;
        NodeId = nodeId;
        ReferenceId = referenceId;
    }

    /// <summary>
    /// 获取值类别。
    /// </summary>
    public ChangeValueKind Kind { get; }

    /// <summary>
    /// 获取冻结值。
    /// </summary>
    /// <remarks>标量返回原值；对象返回只读成员字典；集合和字典返回只读条目列表。</remarks>
    public object Value { get; }

    /// <summary>
    /// 获取对象节点在本次快照中的编号。
    /// </summary>
    /// <remarks>获取本次冻结图内对象或容器定义的节点身份，不能跨快照比较。</remarks>
    public int? NodeId { get; }

    /// <summary>
    /// 获取引用目标的节点编号。
    /// </summary>
    /// <remarks>获取循环回边指向的节点身份；与同一载荷中的 NodeId 对应。</remarks>
    public int? ReferenceId { get; }

    /// <summary>
    /// 获取对象快照的只读成员。
    /// </summary>
    /// <remarks>非对象值返回空值。</remarks>
    public IReadOnlyDictionary<string, ChangeValue> Members => Value as IReadOnlyDictionary<string, ChangeValue>;

    /// <summary>
    /// 获取集合或字典快照的只读条目。
    /// </summary>
    /// <remarks>非容器值返回空值。</remarks>
    public IReadOnlyList<ChangeValueItem> Items => Value as IReadOnlyList<ChangeValueItem>;

    /// <summary>
    /// 获取标量值。
    /// </summary>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <returns>标量值。</returns>
    public T GetScalar<T>()
    {
        if (Kind != ChangeValueKind.Scalar)
            throw new InvalidOperationException("只有 Scalar 值可以调用 GetScalar。");
        if (Value == null)
            return default(T);
        if (Value is T typed)
            return typed;
        throw new InvalidCastException($"标量类型不匹配: {Value.GetType().FullName} ≠ {typeof(T).FullName}");
    }

    /// <inheritdoc />
    public override string ToString() => Kind == ChangeValueKind.Scalar ? Convert.ToString(Value) : Kind.ToString();
}

/// <summary>
/// 对象图集合或字典中的冻结条目。
/// </summary>
public sealed class ChangeValueItem
{
    /// <summary>
    /// 初始化一个 <see cref="ChangeValueItem" /> 类型的实例。
    /// </summary>
    /// <param name="key">对象在容器内的键。</param>
    /// <param name="index">当前值在所属序列中的索引。</param>
    /// <param name="value">用于初始化实例的数据值。</param>
    internal ChangeValueItem(object key, int index, ChangeValue value)
    {
        Key = key;
        Index = index;
        Value = value;
    }

    /// <summary>
    /// 获取集合身份键或字典键。
    /// </summary>
    public object Key { get; }

    /// <summary>
    /// 获取捕获时的集合下标。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 获取条目值。
    /// </summary>
    public ChangeValue Value { get; }
}

/// <summary>
/// 一条对象图差异。
/// </summary>
public sealed class ChangeEntry
{
    /// <summary>
    /// 初始化一个 <see cref="ChangeEntry" /> 类型的实例。
    /// </summary>
    /// <param name="path">要使用的路径。</param>
    /// <param name="kind">当前对象或操作的种类。</param>
    /// <param name="description">当前对象的说明文本。</param>
    /// <param name="oldValue">变更前的值。</param>
    /// <param name="newValue">变更后的值。</param>
    /// <param name="oldIndex">变更前在序列中的索引；不适用时为 null。</param>
    /// <param name="newIndex">变更后在序列中的索引；不适用时为 null。</param>
    internal ChangeEntry(ChangePath path, ChangeKind kind, string description, ChangeValue oldValue, ChangeValue newValue,
        int? oldIndex = null, int? newIndex = null)
    {
        Path = path;
        Kind = kind;
        Description = string.IsNullOrWhiteSpace(description) ? path.Value : description;
        OldValue = oldValue;
        NewValue = newValue;
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>
    /// 获取对象图差异所在的成员路径。
    /// </summary>
    /// <remarks>获取差异路径。</remarks>
    public ChangePath Path { get; }
    /// <summary>
    /// 获取差异类型。
    /// </summary>
    public ChangeKind Kind { get; }
    /// <summary>
    /// 获取发生差异成员的显示说明。
    /// </summary>
    /// <remarks>获取描述。</remarks>
    public string Description { get; }
    /// <summary>
    /// 获取比较前的冻结值。
    /// </summary>
    /// <remarks>获取旧值。</remarks>
    public ChangeValue OldValue { get; }
    /// <summary>
    /// 获取比较后的冻结值。
    /// </summary>
    /// <remarks>获取新值。</remarks>
    public ChangeValue NewValue { get; }
    /// <summary>
    /// 获取比较前快照中的集合下标。
    /// </summary>
    /// <remarks>获取旧下标。</remarks>
    public int? OldIndex { get; }
    /// <summary>
    /// 获取比较后快照中的集合下标。
    /// </summary>
    /// <remarks>获取新下标。</remarks>
    public int? NewIndex { get; }
}

/// <summary>
/// 对象图比较结果。
/// </summary>
public sealed class ChangeGraphResult
{
    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphResult" /> 类型的实例。
    /// </summary>
    /// <param name="changes">当前对象图中的变更条目集合。</param>
    internal ChangeGraphResult(IReadOnlyList<ChangeEntry> changes) => Changes = changes;

    /// <summary>
    /// 获取捕获的只读对象图差异列表。
    /// </summary>
    /// <remarks>获取只读差异列表。</remarks>
    public IReadOnlyList<ChangeEntry> Changes { get; }

    /// <summary>
    /// 获取是否存在对象图差异。
    /// </summary>
    /// <remarks>获取是否存在差异。</remarks>
    public bool HasChanges => Changes.Count != 0;
}

/// <summary>
/// 对象图采集失败时携带已解析路径的异常。
/// </summary>
public sealed class ChangeGraphCaptureException : InvalidOperationException
{
    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphCaptureException" /> 类型的实例。
    /// </summary>
    /// <param name="path">要使用的路径。</param>
    /// <param name="innerException">导致当前异常的内部异常。</param>
    /// <remarks>
    /// 初始化一个 <see cref="ChangeGraphCaptureException" /> 实例。
    /// </remarks>
    public ChangeGraphCaptureException(string path, Exception innerException)
        : base($"对象图采集失败，路径: {path}", innerException) => Path = path ?? string.Empty;

    /// <summary>
    /// 获取失败成员路径。
    /// </summary>
    public string Path { get; }
}

/// <summary>
/// 复合对象图键。
/// </summary>
/// <remarks>
/// 源生成器用它把领域身份契约 <see cref="Bing.Domain.Entities.IEntity.GetKeys" /> 返回的多个分量组合成一个稳定键。
/// </remarks>
public sealed class ChangeGraphKey : IEquatable<ChangeGraphKey>
{
    /// <summary>
    /// 保存构成键值的组件数组。
    /// </summary>
    private readonly object[] _components;
    /// <summary>
    /// 保存组件数组的只读视图，供调用方读取。
    /// </summary>
    private readonly IReadOnlyList<object> _componentsView;
    /// <summary>
    /// 保存构造时计算的键哈希值。
    /// </summary>
    private readonly int _hashCode;

    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphKey" /> 类型的实例。
    /// </summary>
    /// <param name="components">组合键的组成部分。</param>
    private ChangeGraphKey(object[] components)
    {
        _components = components ?? throw new ArgumentNullException(nameof(components));
        _componentsView = Array.AsReadOnly(_components);
        unchecked
        {
            var hash = 17;
            foreach (var component in _components)
                hash = hash * 31 + (component == null ? 0 : component.GetHashCode());
            _hashCode = hash;
        }
    }

    /// <summary>
    /// 根据组成部分创建变更图身份键。
    /// </summary>
    /// <param name="components">构成复合键的一个或多个组件。</param>
    /// <returns>包含指定组件副本的变更图键。</returns>
    /// <remarks>
    /// 组件数组为 null 或为空时抛出参数异常；每个组件都必须符合图键类型约束。
    /// </remarks>
    public static ChangeGraphKey Create(params object[] components)
    {
        if (components == null || components.Length == 0)
            throw new ArgumentException("复合键至少需要一个组成部分。", nameof(components));
        foreach (var component in components)
            GraphCaptureContext.ValidateKey(component, "$key");
        return new ChangeGraphKey((object[])components.Clone());
    }

    /// <summary>
    /// 获取组成部分的只读视图。
    /// </summary>
    public IReadOnlyList<object> Components => _componentsView;

    /// <inheritdoc />
    public bool Equals(ChangeGraphKey other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other == null || _components.Length != other._components.Length)
            return false;
        for (var index = 0; index < _components.Length; index++)
        {
            if (!Equals(_components[index], other._components[index]))
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as ChangeGraphKey);

    /// <inheritdoc />
    public override int GetHashCode() => _hashCode;

    /// <inheritdoc />
    public override string ToString() => string.Join(",", _components.Select(component =>
        component is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : Convert.ToString(component, CultureInfo.InvariantCulture)));
}

/// <summary>
/// 对象图快照。
/// </summary>
/// <typeparam name="T">根类型。</typeparam>
public sealed class ChangeGraphSnapshot<T>
{
    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphSnapshot{T}" /> 类型的实例。
    /// </summary>
    /// <param name="model">用于分析当前调用的语义模型。</param>
    /// <param name="root">当前对象图、工作单元或文件系统的根对象或根路径。</param>
    /// <param name="options">用于初始化实例的配置选项。</param>
    internal ChangeGraphSnapshot(ChangeGraphModel model, GraphValue root, ChangeGraphOptions options)
    {
        Model = model;
        Root = root;
        Options = options;
    }

    /// <summary>
    /// 获取捕获快照时使用的类型化对象图模型。
    /// </summary>
    internal ChangeGraphModel Model { get; }
    /// <summary>
    /// 获取捕获后冻结的对象图根值。
    /// </summary>
    internal GraphValue Root { get; }
    /// <summary>
    /// 获取捕获快照时使用的对象图比较选项。
    /// </summary>
    internal ChangeGraphOptions Options { get; }
}
