using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 标识对象图路径段的定位方式。
/// </summary>
/// <remarks>
/// 成员、位置、实体键和字典键分别按不同规则解析。
/// </remarks>
public enum ChangePathSegmentKind
{
    /// <summary>
    /// 按对象的成员名称定位值。
    /// </summary>
    Member,
    /// <summary>
    /// 按集合中的位置索引定位元素。
    /// </summary>
    Index,
    /// <summary>
    /// 按实体身份键定位集合元素。
    /// </summary>
    EntityKey,
    /// <summary>
    /// 按字典键定位对应值。
    /// </summary>
    DictionaryKey
}

/// <summary>
/// 保留原始键类型的不可变对象图路径段。
/// </summary>
/// <remarks>一个不可变的路径段，键保留原始标量类型。</remarks>
public sealed class ChangePathSegment : IEquatable<ChangePathSegment>
{
    /// <summary>
    /// 初始化一个 <see cref="ChangePathSegment" /> 类型的实例。
    /// </summary>
    /// <param name="kind">当前对象或操作的种类。</param>
    /// <param name="name">实例使用的名称。</param>
    /// <param name="index">当前值在所属序列中的索引。</param>
    /// <param name="key">对象在容器内的键。</param>
    internal ChangePathSegment(ChangePathSegmentKind kind, string name = null, int? index = null, object key = null)
    { Kind = kind; Name = name; Index = index; Key = key; }
    /// <summary>
    /// 获取对象图路径段的类别。
    /// </summary>
    /// <remarks>段类别。</remarks>
    public ChangePathSegmentKind Kind { get; }
    /// <summary>
    /// 获取成员路径段对应的成员名称。
    /// </summary>
    /// <remarks>成员名。非成员路径段为 null。</remarks>
    public string Name { get; }
    /// <summary>
    /// 获取位置路径段对应的集合下标。
    /// </summary>
    /// <remarks>位置索引。非位置路径段为 null。</remarks>
    public int? Index { get; }
    /// <summary>
    /// 获取身份键或字典键路径段的冻结键。
    /// </summary>
    /// <remarks>冻结键。非身份键或字典键路径段为 null。</remarks>
    public object Key { get; }
    /// <inheritdoc />
    public bool Equals(ChangePathSegment other) => other != null && Kind == other.Kind && Name == other.Name &&
        Index == other.Index && Equals(Key, other.Key);
    /// <inheritdoc />
    public override bool Equals(object value) => Equals(value as ChangePathSegment);
    /// <inheritdoc />
    public override int GetHashCode() => ((int)Kind * 397) ^ (Name?.GetHashCode() ?? 0) ^ (Index ?? 0) ^ (Key?.GetHashCode() ?? 0);
}

/// <summary>
/// 保留类型化路径段的结构化变更路径。
/// </summary>
/// <remarks>
/// 结构化差异路径。生成的路径保留类型化段，展示文本不参与集合匹配。
/// </remarks>
public sealed class ChangePath : IEquatable<ChangePath>, IComparable<ChangePath>
{
    /// <summary>
    /// 初始化一个 <see cref="ChangePath" /> 类型的实例。
    /// </summary>
    /// <param name="value">用于初始化实例的数据值。</param>
    /// <remarks>从旧调用方的展示文本构造路径；生成器产生的结果使用类型化段。</remarks>
    public ChangePath(string value)
    { Value = value ?? string.Empty; Segments = Array.AsReadOnly(Array.Empty<ChangePathSegment>()); }

    /// <summary>
    /// 初始化一个 <see cref="ChangePath" /> 类型的实例。
    /// </summary>
    /// <param name="parent">当前节点的父对象。</param>
    /// <param name="segment">表示路径中单个组成部分的片段。</param>
    private ChangePath(ChangePath parent, ChangePathSegment segment)
    {
        Segments = Array.AsReadOnly(parent.Segments.Concat(new[] { segment }).ToArray());
        switch (segment.Kind)
        {
            case ChangePathSegmentKind.Member:
                Value = parent.Value + (parent.Value.Length == 0 ? "" : ".") + segment.Name;
                break;
            case ChangePathSegmentKind.Index:
                Value = parent.Value + "[" + segment.Index.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
                break;
            case ChangePathSegmentKind.EntityKey:
                Value = parent.Value + "[" + GraphCaptureContext.FormatKey(segment.Key) + "]";
                break;
            default:
                Value = parent.Value + "{" + GraphCaptureContext.FormatKey(segment.Key) + "}";
                break;
        }
    }
    /// <summary>
    /// 获取路径的确定性展示文本。
    /// </summary>
    /// <remarks>特殊字符串键带引号并进行转义。</remarks>
    public string Value { get; }
    /// <summary>
    /// 获取不可修改的路径段集合。
    /// </summary>
    /// <remarks>文本兼容构造函数不推断不存在的键类型。</remarks>
    public IReadOnlyList<ChangePathSegment> Segments { get; }
    /// <summary>
    /// 创建表示对象成员的变更路径。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <returns>追加该成员段后的变更路径。</returns>
    internal ChangePath Member(string name) => new ChangePath(this, new ChangePathSegment(ChangePathSegmentKind.Member, name));
    /// <summary>
    /// 创建表示集合索引的变更路径。
    /// </summary>
    /// <param name="index">集合项索引。</param>
    /// <returns>追加该索引段后的变更路径。</returns>
    internal ChangePath At(int index) => new ChangePath(this, new ChangePathSegment(ChangePathSegmentKind.Index, index: index));
    /// <summary>
    /// 创建表示键控项的变更路径。
    /// </summary>
    /// <param name="key">集合项或字典项的键。</param>
    /// <param name="dictionary">是否将键表示为字典键段。</param>
    /// <returns>追加实体键或字典键段后的变更路径。</returns>
    internal ChangePath ForKey(object key, bool dictionary = false) => new ChangePath(this,
        new ChangePathSegment(dictionary ? ChangePathSegmentKind.DictionaryKey : ChangePathSegmentKind.EntityKey, key: key));
    /// <inheritdoc />
    public int CompareTo(ChangePath other)
    {
        if (other == null) return 1;
        for (var i = 0; i < Math.Min(Segments.Count, other.Segments.Count); i++)
        {
            var left = Segments[i]; var right = other.Segments[i];
            var order = left.Kind.CompareTo(right.Kind);
            if (order == 0 && left.Kind == ChangePathSegmentKind.Index) order = left.Index.Value.CompareTo(right.Index.Value);
            if (order == 0 && left.Kind == ChangePathSegmentKind.Member) order = string.CompareOrdinal(left.Name, right.Name);
            if (order == 0 && left.Kind >= ChangePathSegmentKind.EntityKey)
                order = string.CompareOrdinal(GraphCaptureContext.FormatKey(left.Key), GraphCaptureContext.FormatKey(right.Key));
            if (order != 0) return order;
        }
        return Segments.Count != 0 && other.Segments.Count != 0
            ? Segments.Count.CompareTo(other.Segments.Count) : string.CompareOrdinal(Value, other.Value);
    }
    /// <inheritdoc />
    public bool Equals(ChangePath other) => other != null && (Segments.Count == 0 && other.Segments.Count == 0
        ? string.Equals(Value, other.Value, StringComparison.Ordinal) : Segments.SequenceEqual(other.Segments));
    /// <inheritdoc />
    public override bool Equals(object value) => Equals(value as ChangePath);
    /// <inheritdoc />
    public override int GetHashCode() => Segments.Count == 0 ? StringComparer.Ordinal.GetHashCode(Value) :
        Segments.Aggregate(17, (hash, segment) => unchecked(hash * 31 + segment.GetHashCode()));
    /// <inheritdoc />
    public override string ToString() => Value;
}
