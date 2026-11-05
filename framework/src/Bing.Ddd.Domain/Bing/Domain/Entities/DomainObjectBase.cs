using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Bing.Domain.ChangeTracking;
using Bing.Domain.ChangeTracking.Internal;
using Bing.Validation;
using Bing.Validation.Strategies;

namespace Bing.Domain.Entities;

/// <summary>
/// 为领域对象提供验证、变更跟踪和描述输出的基础实现。
/// </summary>
public abstract class DomainObjectBase : IDomainObject, IChangeTrackable
{
    /// <summary>
    /// 存储当前对象与其他对象比较后生成的变更描述。
    /// </summary>
    protected internal readonly ChangeTrackingContext ChangeTrackingContext;

    /// <summary>
    /// 收集当前对象的文本描述片段。
    /// </summary>
    protected internal readonly DescriptionContext DescriptionContext;

    /// <summary>
    /// 初始化一个 <see cref="DomainObjectBase"/> 类型的实例。
    /// </summary>
    protected DomainObjectBase()
    {
        ChangeTrackingContext = new ChangeTrackingContext();
        DescriptionContext = new DescriptionContext();
    }

    /// <inheritdoc />
    public abstract IValidationResult Validate();

    #region ChangeTracking(变更跟踪)

    /// <inheritdoc />
    /// <remarks>
    /// 最外层调用建立独立结果 frame；嵌套比较复用会话但各自拥有结果缓冲。
    /// 返回集合脱离本次会话缓冲，后续调用不会清空或修改它。
    /// </remarks>
    public ChangedValueDescriptorCollection GetChanges(object otherObject)
    {
        if (otherObject == null)
            return new ChangedValueDescriptorCollection();
        var frame = ChangeTrackingSession.Begin(ChangeTrackingContext, this, otherObject);
        try
        {
            // 使用内部方法，保证对象类型正确
            AddChangesInternal(otherObject);
            var result = new ChangedValueDescriptorCollection();
            foreach (var descriptor in frame.Result)
                result.Add(descriptor);
            return result;
        }
        finally
        {
            ChangeTrackingSession.End(frame);
        }
    }

    /// <summary>
    /// 将对象转换为具体领域类型并记录其变更。
    /// </summary>
    /// <param name="newObj">作为比较目标的新对象实例。</param>
    protected abstract void AddChangesInternal(object newObj);

    /// <summary>
    /// 记录领域对象变更。
    /// </summary>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="propertyName">发生变更的属性名称。</param>
    /// <param name="description">属性的显示描述。</param>
    /// <param name="oldValue">变更前的属性值。</param>
    /// <param name="newValue">变更后的属性值。</param>
    protected void AddChange<TValue>(string propertyName, string description, TValue oldValue, TValue newValue) =>
        ChangeTrackingContext.Add(propertyName, description, oldValue, newValue);

    /// <summary>
    /// 比较并汇总单个领域关系对象。
    /// </summary>
    /// <typeparam name="TValue">关系对象的静态类型。</typeparam>
    /// <param name="propertyName">关系属性路径。</param>
    /// <param name="description">关系描述。</param>
    /// <param name="oldValue">变更前的关系对象。</param>
    /// <param name="newValue">变更后的关系对象。</param>
    /// <remarks>新对象为 null 时直接记录关系删除，不读取旧对象成员或调用其比较钩子。</remarks>
    protected void AddObjectChanges<TValue>(string propertyName, string description, TValue oldValue, TValue newValue)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        CompareValue(frame, propertyName, description, oldValue, newValue);
    }

    /// <summary>
    /// 比较并汇总集合关系；有序集合按索引或实体身份匹配，Set 按元素身份或容器相等语义匹配。
    /// </summary>
    /// <typeparam name="TItem">集合元素类型。</typeparam>
    /// <param name="propertyName">集合关系路径。</param>
    /// <param name="description">集合关系描述。</param>
    /// <param name="oldItems">变更前集合。</param>
    /// <param name="newItems">变更后集合。</param>
    /// <remarks>null 集合等同空集合；旧明细逐项删除，不推断导航加载状态。</remarks>
    protected void AddCollectionChanges<TItem>(string propertyName, string description,
        IEnumerable<TItem> oldItems, IEnumerable<TItem> newItems) =>
        AddCollectionChanges(propertyName, description, oldItems, newItems, false);

    /// <summary>按静态集合类型补充 Set 语义；只读 Set 不一定实现运行时的 ISet 接口。</summary>
    /// <param name="propertyName">集合关系路径。</param>
    /// <param name="description">集合关系描述。</param>
    /// <param name="oldItems">变更前集合。</param>
    /// <param name="newItems">变更后集合。</param>
    /// <param name="declaredAsSet">静态类型为 Set 时为 true。</param>
    /// <typeparam name="TItem">集合元素类型。</typeparam>
    protected void AddCollectionChanges<TItem>(string propertyName, string description,
        IEnumerable<TItem> oldItems, IEnumerable<TItem> newItems, bool declaredAsSet)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        var isSet = declaredAsSet || oldItems is ISet<TItem> || newItems is ISet<TItem>;
        IEqualityComparer<TItem> equality = null;
        IComparer<TItem> ordering = null;
        if (isSet)
            ResolveSetComparers(propertyName, oldItems, newItems, out equality, out ordering);
        var before = oldItems?.ToList() ?? new List<TItem>();
        var after = newItems?.ToList() ?? new List<TItem>();
        if (isSet && IsScalar(typeof(TItem)))
        {
            CompareScalarSet(frame, propertyName, description, before, after, equality, ordering);
            return;
        }
        CompareCollection(frame, propertyName, description, before, after, isSet);
    }

    /// <summary>
    /// 比较并汇总字典关系；键按容器自身比较器匹配，值按自身领域或标量语义比较。
    /// </summary>
    /// <typeparam name="TKey">字典键类型。</typeparam>
    /// <typeparam name="TValue">字典值类型。</typeparam>
    /// <param name="propertyName">字典关系路径。</param>
    /// <param name="description">字典关系描述。</param>
    /// <param name="oldValues">变更前字典条目。</param>
    /// <param name="newValues">变更后字典条目。</param>
    protected void AddDictionaryChanges<TKey, TValue>(string propertyName, string description,
        IEnumerable<KeyValuePair<TKey, TValue>> oldValues, IEnumerable<KeyValuePair<TKey, TValue>> newValues)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        foreach (var pair in GetNestedDictionaryPairs(propertyName, description, oldValues, newValues))
            CompareValue(frame, pair.Path, description, pair.OldValue, pair.NewValue);
    }

    /// <summary>表示已按集合索引或字典键配对的两个非空值。</summary>
    /// <typeparam name="TValue">配对值的静态类型。</typeparam>
    protected readonly struct ChangeTrackingPair<TValue>
    {
        /// <summary>初始化一个带路径的配对。</summary>
        internal ChangeTrackingPair(string path, TValue oldValue, TValue newValue)
        {
            Path = path;
            OldValue = oldValue;
            NewValue = newValue;
        }

        /// <summary>获取配对项的完整关系路径。</summary>
        public string Path { get; }
        /// <summary>获取旧值。</summary>
        public TValue OldValue { get; }
        /// <summary>获取新值。</summary>
        public TValue NewValue { get; }
    }

    /// <summary>
    /// 按索引或领域身份配对嵌套集合，直接记录新增、删除与空值变化。
    /// </summary>
    /// <remarks>生成器可在配对后按元素的静态类型继续调用固定辅助方法，无需反射或比较委托。</remarks>
    protected IEnumerable<ChangeTrackingPair<TItem>> GetNestedCollectionPairs<TItem>(string propertyName,
        string description, IEnumerable<TItem> oldItems, IEnumerable<TItem> newItems) =>
        GetNestedCollectionPairs(propertyName, description, oldItems, newItems, false);

    /// <summary>按静态集合类型补充 Set 语义后配对嵌套值。</summary>
    protected IEnumerable<ChangeTrackingPair<TItem>> GetNestedCollectionPairs<TItem>(string propertyName,
        string description, IEnumerable<TItem> oldItems, IEnumerable<TItem> newItems, bool declaredAsSet)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return Array.Empty<ChangeTrackingPair<TItem>>();
        var isSet = declaredAsSet || oldItems is ISet<TItem> || newItems is ISet<TItem>;
        if (isSet)
            ResolveSetComparers(propertyName, oldItems, newItems, out _, out _);
        var before = oldItems?.ToList() ?? new List<TItem>();
        var after = newItems?.ToList() ?? new List<TItem>();
        return EnumerateCollectionPairs(frame, propertyName, description, before, after, isSet);
    }

    /// <summary>
    /// 按字典自身键比较器配对嵌套值，直接记录新增、删除与空值变化。
    /// </summary>
    protected IEnumerable<ChangeTrackingPair<TValue>> GetNestedDictionaryPairs<TKey, TValue>(string propertyName,
        string description, IEnumerable<KeyValuePair<TKey, TValue>> oldValues,
        IEnumerable<KeyValuePair<TKey, TValue>> newValues)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return Array.Empty<ChangeTrackingPair<TValue>>();
        var beforeHash = oldValues is Dictionary<TKey, TValue> oldMap ? oldMap.Comparer : null;
        var afterHash = newValues is Dictionary<TKey, TValue> newMap ? newMap.Comparer : null;
        var beforeOrder = GetDictionaryOrderComparer(oldValues);
        var afterOrder = GetDictionaryOrderComparer(newValues);
        if ((beforeOrder != null || afterOrder != null) && (beforeHash != null || afterHash != null))
            throw new InvalidOperationException($"容器比较器不兼容，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
        RejectUnknownDictionary(oldValues, beforeHash, beforeOrder, propertyName);
        RejectUnknownDictionary(newValues, afterHash, afterOrder, propertyName);
        var ordering = beforeOrder == null && afterOrder == null
            ? null : ResolveOrderComparer(beforeOrder, afterOrder, propertyName);
        var equality = ordering == null ? ResolveComparer(beforeHash, afterHash, propertyName) : null;
        var before = ToDictionary(oldValues, propertyName, equality, ordering);
        var after = ToDictionary(newValues, propertyName, equality, ordering);
        return EnumerateDictionaryPairs(frame, propertyName, description, before, after);
    }

    /// <summary>枚举匹配的字典值，使用旧侧键作为匹配项的路径。</summary>
    private static IEnumerable<ChangeTrackingPair<TValue>> EnumerateDictionaryPairs<TKey, TValue>(
        ChangeTrackingFrame frame, string propertyName, string description,
        IDictionary<TKey, TValue> before, IDictionary<TKey, TValue> after)
    {
        foreach (var pair in before)
        {
            var path = propertyName + "{" + FormatKey(pair.Key) + "}";
            if (!after.TryGetValue(pair.Key, out var newValue))
            {
                AddStructural(frame, path, description, pair.Value, true, default, false);
                continue;
            }
            if (pair.Value == null || newValue == null)
                CompareValue(frame, path, description, pair.Value, newValue);
            else
                yield return new ChangeTrackingPair<TValue>(path, pair.Value, newValue);
        }
        foreach (var pair in after)
        {
            if (!before.ContainsKey(pair.Key))
                AddStructural(frame, propertyName + "{" + FormatKey(pair.Key) + "}", description, default, false, pair.Value, true);
        }
    }

    /// <summary>
    /// 比较单个值并按关系路径合并差异。
    /// </summary>
    private static void CompareValue<TValue>(ChangeTrackingFrame frame, string path, string description,
        TValue oldValue, TValue newValue, bool hasOld = true, bool hasNew = true)
    {
        if (!hasOld && !hasNew)
            return;
        if (!hasOld)
        {
            AddStructural(frame, path, description, default, false, newValue, true);
            return;
        }
        if (!hasNew)
        {
            AddStructural(frame, path, description, oldValue, true, default, false);
            return;
        }
        if (oldValue == null && newValue == null)
            return;
        if (oldValue == null)
        {
            AddStructural(frame, path, description, default, false, newValue, true);
            return;
        }
        if (newValue == null)
        {
            AddStructural(frame, path, description, oldValue, true, default, false);
            return;
        }
        if (oldValue is IChangeTrackable trackable)
        {
            if (oldValue.GetType() != newValue.GetType())
            {
                AddStructural(frame, path, description, oldValue, true, newValue, true);
                return;
            }
            if (TryGetIdentity(oldValue, path, out var oldKey) && TryGetIdentity(newValue, path, out var newKey) &&
                !KeyEquals(oldKey, newKey))
            {
                AddStructural(frame, path, description, oldValue, true, newValue, true);
                return;
            }
            if (ChangeTrackingSession.IsInAncestorChain(oldValue, newValue))
                return;
            MergeTrackableChanges(frame, path, trackable, newValue);
            return;
        }
        if (CompareContainer(frame, path, description, oldValue, newValue))
            return;
        if (EqualityComparer<TValue>.Default.Equals(oldValue, newValue))
            return;
        ChangeTrackingSession.Add(frame, path, description,
            ScalarValueFormatter.Format(oldValue), ScalarValueFormatter.Format(newValue));
    }

    /// <summary>
    /// 比较集合；领域元素有身份契约时按身份匹配，否则按索引配对。
    /// </summary>
    private static void CompareCollection<TItem>(ChangeTrackingFrame frame, string propertyName, string description,
        IReadOnlyList<TItem> before, IReadOnlyList<TItem> after, bool isSet)
    {
        foreach (var pair in EnumerateCollectionPairs(frame, propertyName, description, before, after, isSet))
            CompareValue(frame, pair.Path, description, pair.OldValue, pair.NewValue);
    }

    /// <summary>枚举匹配的集合项，并直接记录结构性变化。</summary>
    private static IEnumerable<ChangeTrackingPair<TItem>> EnumerateCollectionPairs<TItem>(ChangeTrackingFrame frame,
        string propertyName, string description, IReadOnlyList<TItem> before, IReadOnlyList<TItem> after, bool isSet)
    {
        if (!IsScalar(typeof(TItem)) &&
            TryBuildIdentityMaps(before, after, propertyName, out var beforeMap, out var afterMap))
        {
            foreach (var pair in beforeMap)
            {
                var path = propertyName + "[" + FormatKey(pair.Key) + "]";
                if (afterMap.TryGetValue(pair.Key, out var newItem))
                    yield return new ChangeTrackingPair<TItem>(path, pair.Value, newItem);
                else
                    AddStructural(frame, path, description, pair.Value, true, default, false);
            }
            foreach (var pair in afterMap)
            {
                if (beforeMap.ContainsKey(pair.Key))
                    continue;
                AddStructural(frame, propertyName + "[" + FormatKey(pair.Key) + "]", description, default, false, pair.Value, true);
            }
            yield break;
        }
        if (isSet)
            throw new InvalidOperationException($"无序集合的复杂元素必须具有身份，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
        for (var index = 0; index < before.Count || index < after.Count; index++)
        {
            var path = propertyName + "[" + index + "]";
            var hasOld = index < before.Count;
            var hasNew = index < after.Count;
            var oldItem = hasOld ? before[index] : default;
            var newItem = hasNew ? after[index] : default;
            if (!hasOld || !hasNew || oldItem == null || newItem == null)
                CompareValue(frame, path, description, oldItem, newItem, hasOld, hasNew);
            else
                yield return new ChangeTrackingPair<TItem>(path, oldItem, newItem);
        }
    }

    /// <summary>取得并核对标准 Set 的比较器，拒绝无法读取的自定义容器。</summary>
    private static void ResolveSetComparers<TItem>(string propertyName, IEnumerable<TItem> oldItems,
        IEnumerable<TItem> newItems, out IEqualityComparer<TItem> equality, out IComparer<TItem> ordering)
    {
        var beforeHash = oldItems is HashSet<TItem> oldHash ? oldHash.Comparer : null;
        var afterHash = newItems is HashSet<TItem> newHash ? newHash.Comparer : null;
        var beforeOrder = oldItems is SortedSet<TItem> oldSorted ? oldSorted.Comparer : null;
        var afterOrder = newItems is SortedSet<TItem> newSorted ? newSorted.Comparer : null;
        if ((beforeOrder != null || afterOrder != null) && (beforeHash != null || afterHash != null))
            throw new InvalidOperationException($"容器比较器不兼容，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
        if ((oldItems != null && beforeHash == null && beforeOrder == null) ||
            (newItems != null && afterHash == null && afterOrder == null))
            throw new InvalidOperationException($"无法获取自定义 Set 的比较器，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");

        ordering = beforeOrder != null || afterOrder != null
            ? ResolveOrderComparer(beforeOrder, afterOrder, propertyName) : null;
        equality = ordering == null ? ResolveComparer(beforeHash, afterHash, propertyName) : null;
    }

    /// <summary>按已核对的 Set 比较器报告标量成员新增和删除。</summary>
    private static void CompareScalarSet<TItem>(ChangeTrackingFrame frame, string propertyName, string description,
        IReadOnlyList<TItem> before, IReadOnlyList<TItem> after, IEqualityComparer<TItem> equality,
        IComparer<TItem> ordering)
    {
        ISet<TItem> beforeSet;
        ISet<TItem> afterSet;
        if (ordering != null)
        {
            beforeSet = new SortedSet<TItem>(before, ordering);
            afterSet = new SortedSet<TItem>(after, ordering);
        }
        else
        {
            beforeSet = new HashSet<TItem>(before, equality);
            afterSet = new HashSet<TItem>(after, equality);
        }
        foreach (var item in before)
        {
            if (afterSet.Contains(item))
                continue;
            ChangeTrackingSession.Add(frame, propertyName + "[" + ScalarValueFormatter.Format(item) + "]", description,
                ScalarValueFormatter.Format(item), "<missing>");
        }
        foreach (var item in after)
        {
            if (beforeSet.Contains(item))
                continue;
            ChangeTrackingSession.Add(frame, propertyName + "[" + ScalarValueFormatter.Format(item) + "]", description,
                "<missing>", ScalarValueFormatter.Format(item));
        }
    }

    /// <summary>
    /// 尝试按实体身份建立新旧元素索引；没有身份契约的有序元素返回 false。
    /// </summary>
    private static bool TryBuildIdentityMaps<TItem>(IReadOnlyList<TItem> before, IReadOnlyList<TItem> after,
        string path, out Dictionary<object, TItem> beforeMap, out Dictionary<object, TItem> afterMap)
    {
        beforeMap = new Dictionary<object, TItem>();
        afterMap = new Dictionary<object, TItem>();
        var hasIdentity = typeof(IEntity).IsAssignableFrom(typeof(TItem));
        string nullPath = null;
        string unkeyedPath = null;
        IndexIdentityItems(before, path, beforeMap, ref hasIdentity, ref nullPath, ref unkeyedPath);
        IndexIdentityItems(after, path, afterMap, ref hasIdentity, ref nullPath, ref unkeyedPath);
        if (!hasIdentity)
            return false;
        if (nullPath != null)
            throw new InvalidOperationException($"带身份集合不允许 null 元素，路径: {ChangeTrackingSession.ErrorPath(nullPath)}");
        if (unkeyedPath != null)
            throw new InvalidOperationException($"带身份集合不能混入无身份元素，路径: {ChangeTrackingSession.ErrorPath(unkeyedPath)}");
        return true;
    }

    /// <summary>
    /// 为一侧集合建立身份索引，并保留首个无身份或空元素的位置。
    /// </summary>
    private static void IndexIdentityItems<TItem>(IReadOnlyList<TItem> items, string path,
        Dictionary<object, TItem> map, ref bool hasIdentity, ref string nullPath, ref string unkeyedPath)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var itemPath = path + "[" + index + "]";
            if (item == null)
            {
                nullPath ??= itemPath;
                continue;
            }
            if (!TryGetIdentity(item, itemPath, out var key))
            {
                unkeyedPath ??= itemPath;
                continue;
            }
            hasIdentity = true;
            ValidateCollectionIdentity(key, itemPath);
            if (map.ContainsKey(key))
                throw new InvalidOperationException($"集合元素身份键重复，路径: {ChangeTrackingSession.ErrorPath(path)}");
            map.Add(key, item);
        }
    }

    /// <summary>
    /// 拒绝没有分配的单键身份；复合键中允许业务上有意义的默认分量。
    /// </summary>
    private static void ValidateCollectionIdentity(object key, string path)
    {
        var isUnassigned = key == null || key is string text && text.Length == 0 ||
                           key is Guid guid && guid == Guid.Empty ||
                           key is int intKey && intKey == 0 || key is long longKey && longKey == 0 ||
                           key is short shortKey && shortKey == 0 || key is byte byteKey && byteKey == 0 ||
                           key is uint uintKey && uintKey == 0 || key is ulong ulongKey && ulongKey == 0 ||
                           key is ushort ushortKey && ushortKey == 0 || key is sbyte sbyteKey && sbyteKey == 0 ||
                           key is decimal decimalKey && decimalKey == 0;
        if (isUnassigned)
            throw new InvalidOperationException($"集合元素身份键未分配，路径: {ChangeTrackingSession.ErrorPath(path)}");
    }

    /// <summary>
    /// 递归比较非领域容器；元素到达领域对象时回到其 GetChanges。
    /// </summary>
    private static bool CompareContainer(ChangeTrackingFrame frame, string path, string description,
        object oldValue, object newValue)
    {
        if (oldValue is string || newValue is string)
            return false;
        if (oldValue == null && newValue == null)
            return true;
        if (!ChangeTrackingSession.TryEnterPair(oldValue, newValue))
            return true;
        try
        {
            var oldMap = oldValue as IDictionary;
            var newMap = newValue as IDictionary;
            if (oldMap != null || newMap != null)
            {
                CompareNonGenericDictionary(frame, path, description, oldMap, newMap);
                return true;
            }
            var oldItems = oldValue as IEnumerable;
            var newItems = newValue as IEnumerable;
            if (oldItems != null || newItems != null)
            {
                var before = ToObjectList(oldItems);
                var after = ToObjectList(newItems);
                // 嵌套容器中的领域元素同样优先按实体身份匹配。
                if (TryBuildIdentityMaps(before, after, path, out var beforeMap, out var afterMap))
                {
                    foreach (var pair in beforeMap)
                    {
                        var itemPath = path + "[" + FormatKey(pair.Key) + "]";
                        if (afterMap.TryGetValue(pair.Key, out var newItem))
                            CompareObjectValue(frame, itemPath, description, pair.Value, newItem, true, true);
                        else
                            AddStructural(frame, itemPath, description, pair.Value, true, null, false);
                    }
                    foreach (var pair in afterMap)
                    {
                        if (beforeMap.ContainsKey(pair.Key))
                            continue;
                        AddStructural(frame, path + "[" + FormatKey(pair.Key) + "]", description, null, false, pair.Value, true);
                    }
                    return true;
                }
                for (var index = 0; index < before.Count || index < after.Count; index++)
                {
                    var itemPath = path + "[" + index + "]";
                    var hasOld = index < before.Count;
                    var hasNew = index < after.Count;
                    CompareObjectValue(frame, itemPath, description,
                        hasOld ? before[index] : null, hasNew ? after[index] : null, hasOld, hasNew);
                }
                return true;
            }
            return false;
        }
        finally
        {
            ChangeTrackingSession.ExitPair(oldValue, newValue);
        }
    }

    /// <summary>
    /// 比较非泛型字典条目。
    /// </summary>
    private static void CompareNonGenericDictionary(ChangeTrackingFrame frame, string path, string description,
        IDictionary before, IDictionary after)
    {
        // 以各容器自身的 Contains 匹配，不能先用 object.Equals 合并键。
        // 若两侧对当前键集的匹配数不一致，不能选择某一侧语义继续比较。
        var oldMatches = 0;
        var newMatches = 0;
        if (before != null && after != null)
        {
            foreach (var key in before.Keys) if (after.Contains(key)) oldMatches++;
            foreach (var key in after.Keys) if (before.Contains(key)) newMatches++;
        }
        if (oldMatches != newMatches)
            throw new InvalidOperationException($"容器键比较语义不一致，路径: {ChangeTrackingSession.ErrorPath(path)}");
        if (before != null) foreach (var key in before.Keys)
        {
            var itemPath = path + "{" + FormatKey(key) + "}";
            var hasNew = after != null && after.Contains(key);
            CompareObjectValue(frame, itemPath, description,
                before[key], hasNew ? after[key] : null, true, hasNew);
        }
        if (after != null) foreach (var key in after.Keys)
            if (before == null || !before.Contains(key))
                CompareObjectValue(frame, path + "{" + FormatKey(key) + "}", description,
                    null, after[key], false, true);
    }

    /// <summary>
    /// 比较单个 object 值并分派领域对象、容器与标量。
    /// </summary>
    private static void CompareObjectValue(ChangeTrackingFrame frame, string path, string description,
        object oldValue, object newValue, bool hasOld, bool hasNew)
    {
        if (!hasOld)
        {
            AddStructural(frame, path, description, null, false, newValue, true);
            return;
        }
        if (!hasNew)
        {
            AddStructural(frame, path, description, oldValue, true, null, false);
            return;
        }
        if (oldValue == null && newValue == null)
            return;
        if (oldValue == null || newValue == null)
        {
            AddStructural(frame, path, description, oldValue, true, newValue, true);
            return;
        }
        if (oldValue is IChangeTrackable trackable)
        {
            if (oldValue.GetType() != newValue.GetType())
            {
                AddStructural(frame, path, description, oldValue, true, newValue, true);
                return;
            }
            if (ChangeTrackingSession.IsInAncestorChain(oldValue, newValue))
                return;
            MergeTrackableChanges(frame, path, trackable, newValue);
            return;
        }
        if (CompareContainer(frame, path, description, oldValue, newValue))
            return;
        if (Equals(oldValue, newValue))
            return;
        ChangeTrackingSession.Add(frame, path, description,
            ScalarValueFormatter.Format(oldValue), ScalarValueFormatter.Format(newValue));
    }

    /// <summary>
    /// 将可枚举对象物化为 object 列表。
    /// </summary>
    private static List<object> ToObjectList(IEnumerable values)
    {
        var result = new List<object>();
        if (values == null)
            return result;
        foreach (var value in values)
            result.Add(value);
        return result;
    }

    /// <summary>调用子对象自身的比较，并在异常时保留关系路径、恢复线程上下文。</summary>
    private static void MergeTrackableChanges(ChangeTrackingFrame frame, string path, IChangeTrackable trackable,
        object newValue)
    {
        var previous = ChangeTrackingSession.PushErrorPath(path);
        try
        {
            ChangeTrackingSession.Merge(frame, path, trackable.GetChanges(newValue));
        }
        finally
        {
            ChangeTrackingSession.RestoreErrorPath(previous);
        }
    }

    /// <summary>
    /// 通过实体自己的 GetKeys 读取身份；普通领域对象不推断主键，按有序位置配对。
    /// </summary>
    private static bool TryGetIdentity(object value, string path, out object key)
    {
        if (value is IEntity entity)
        {
            var keys = entity.GetKeys();
            if (keys == null || keys.Length == 0)
                throw new InvalidOperationException($"实体身份键为空，路径: {ChangeTrackingSession.ErrorPath(path)}，实体类型: {value.GetType().FullName}");
            key = keys.Length == 1 ? keys[0] : ChangeGraphKey.Create(keys);
            return true;
        }
        key = null;
        return false;
    }

    /// <summary>
    /// 比较两个身份键；复合键按分量逐项比较。
    /// </summary>
    private static bool KeyEquals(object left, object right) =>
        left is ChangeGraphKey a && right is ChangeGraphKey b
            ? a.Equals(b)
            : Equals(left, right);

    /// <summary>
    /// 判断类型是否按标量比较。
    /// </summary>
    private static bool IsScalar(Type type) =>
        type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) ||
        type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
        type == typeof(TimeSpan) || type == typeof(Uri) || type == typeof(Version) ||
        Nullable.GetUnderlyingType(type) is { } inner && IsScalar(inner);

    /// <summary>
    /// 判断新旧容器比较器是否兼容；缺失一侧时使用存在侧语义。
    /// </summary>
    private static IEqualityComparer<T> ResolveComparer<T>(IEqualityComparer<T> before,
        IEqualityComparer<T> after, string propertyName)
    {
        if (before == null)
            return after ?? EqualityComparer<T>.Default;
        if (after == null)
            return before;
        if (ReferenceEquals(before, after))
            return before;
        var beforeDefault = ReferenceEquals(before, EqualityComparer<T>.Default);
        var afterDefault = ReferenceEquals(after, EqualityComparer<T>.Default);
        if (beforeDefault && afterDefault)
            return EqualityComparer<T>.Default;
        if (beforeDefault || afterDefault)
            throw new InvalidOperationException($"容器比较器不兼容，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
        if (before.Equals(after))
            return before;
        throw new InvalidOperationException($"容器比较器不一致，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
    }

    /// <summary>解析排序容器比较器，不将排序等价关系降级为默认相等比较。</summary>
    private static IComparer<T> ResolveOrderComparer<T>(IComparer<T> before, IComparer<T> after, string propertyName)
    {
        if (before == null) return after;
        if (after == null) return before;
        if (ReferenceEquals(before, after) || before.Equals(after)) return before;
        throw new InvalidOperationException($"容器比较器不一致，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
    }

    /// <summary>读取标准排序字典的比较器。</summary>
    private static IComparer<TKey> GetDictionaryOrderComparer<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values) =>
        values is SortedDictionary<TKey, TValue> sorted ? sorted.Comparer :
        values is SortedList<TKey, TValue> list ? list.Comparer : null;

    /// <summary>不可读取比较器的字典不能静默降级到默认比较语义。</summary>
    private static void RejectUnknownDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values,
        IEqualityComparer<TKey> equality, IComparer<TKey> ordering, string propertyName)
    {
        if (values != null && equality == null && ordering == null &&
            (values is IDictionary<TKey, TValue> || values is IReadOnlyDictionary<TKey, TValue>))
            throw new InvalidOperationException($"无法获取自定义字典的比较器，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
    }

    /// <summary>
    /// 将字典条目序列转换为按容器键索引的字典。
    /// </summary>
    private static IDictionary<TKey, TValue> ToDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> values, string propertyName, IEqualityComparer<TKey> comparer,
        IComparer<TKey> ordering)
    {
        IDictionary<TKey, TValue> result = ordering == null
            ? new Dictionary<TKey, TValue>(comparer) : new SortedDictionary<TKey, TValue>(ordering);
        if (values == null)
            return result;
        foreach (var pair in values)
        {
            if (result.ContainsKey(pair.Key))
                throw new InvalidOperationException($"字典键重复，路径: {ChangeTrackingSession.ErrorPath(propertyName)}");
            result.Add(pair.Key, pair.Value);
        }
        return result;
    }

    /// <summary>
    /// 记录关系的结构性新增或删除。
    /// </summary>
    private static void AddStructural<TValue>(ChangeTrackingFrame frame, string path, string description,
        TValue oldValue, bool hasOld, TValue newValue, bool hasNew)
    {
        var oldText = hasOld ? FormatPayload(oldValue) : "<missing>";
        var newText = hasNew ? FormatPayload(newValue) : "<missing>";
        ChangeTrackingSession.Add(frame, path, description, oldText, newText);
    }

    /// <summary>
    /// 格式化结构事件载荷；不调用领域对象 ToString 或反射读取成员。
    /// </summary>
    private static string FormatPayload(object value)
    {
        if (value == null)
            return "null";
        if (value is IChangeTrackable)
            return "<object>";
        if (value is IEnumerable && value is not string)
            return "<collection>";
        return ScalarValueFormatter.Format(value);
    }

    /// <summary>
    /// 使用标量格式化规则格式化容器键。
    /// </summary>
    private static string FormatKey(object key) => GraphCaptureContext.FormatKey(key);

    #endregion

    #region Descriptin(描述)

    /// <summary>
    /// 向描述上下文添加当前对象的描述信息。
    /// </summary>
    /// <remarks>基类不添加描述；派生类可重写该方法写入自身属性。</remarks>
    protected virtual void AddDescriptions() { }

    /// <summary>
    /// 添加对象描述信息。
    /// </summary>
    /// <param name="description">要添加的描述文本。</param>
    protected void AddDescription(string description) => DescriptionContext.Add(description);

    /// <summary>
    /// 添加对象描述信息。
    /// </summary>
    /// <typeparam name="TValue">属性值类型。</typeparam>
    /// <param name="name">属性显示名称。</param>
    /// <param name="value">要描述的属性值。</param>
    protected void AddDescription<TValue>(string name, TValue value) => DescriptionContext.Add(name, value);

    #endregion

    /// <inheritdoc />
    /// <remarks>
    /// 输出当前对象的描述信息。
    /// </remarks>
    public override string ToString()
    {
        DescriptionContext.FlushCache();
        AddDescriptions();
        return DescriptionContext.Output();
    }
}

/// <summary>
/// 为指定领域对象类型提供强类型验证和变更跟踪支持。
/// </summary>
/// <typeparam name="TObject">具体领域对象类型。</typeparam>
public abstract class DomainObjectBase<TObject> : DomainObjectBase, IVerifyModel<TObject>
    where TObject : class, IDomainObject, IVerifyModel<TObject>
{
    /// <summary>
    /// 延迟创建的强类型验证上下文。
    /// </summary>
    private readonly Lazy<ValidationContext<TObject>> _validationContext;

    /// <summary>
    /// 初始化一个 <see cref="DomainObjectBase{TObject}"/> 类型的实例。
    /// </summary>
    protected DomainObjectBase()
    {
        _validationContext = new Lazy<ValidationContext<TObject>>(() => new ValidationContext<TObject>(AssignableType(this)));
    }

    #region Validation(验证)

    /// <inheritdoc />
    public void SetValidationCallback(IValidationCallbackHandler handler) => 
        _validationContext.Value.SetHandler(op => op.HandleAll(handler));

    /// <inheritdoc />
    /// <exception cref="NotImplementedException">当前版本尚未实现全局验证规则配置时抛出。</exception>
    public void UseValidationRules()
    {
        // 全局规则的注册入口尚未实现，保留现有失败契约以避免静默跳过验证。
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void UseStrategy(IValidationStrategy<TObject> strategy) => _validationContext.Value.AddStrategy(strategy);

    /// <inheritdoc />
    public void UseStrategyList(IEnumerable<IValidationStrategy<TObject>> strategies) => _validationContext.Value.AddStrategyList(strategies);

    /// <inheritdoc />
    /// <remarks>
    /// 执行验证并返回结果集合。
    /// </remarks>
    public override IValidationResult Validate()
    {
        _validationContext.Value.Validate(Validate);
        return _validationContext.Value.GetValidationResultCollection();
    }

    /// <summary>
    /// 执行派生对象定义的自定义验证逻辑。
    /// </summary>
    /// <param name="results">用于收集验证结果的集合。</param>
    protected virtual void Validate(ValidationResultCollection results) { }

    #endregion

    #region ChangeTracking(变更跟踪)

    /// <inheritdoc />
    protected override void AddChangesInternal(object newObj)
    {
        if (newObj is not TObject typedObj)
            throw new InvalidOperationException($"对象类型不匹配: {newObj.GetType().FullName} ≠ {typeof(TObject).FullName}");
        AddChanges(typedObj);
    }

    /// <summary>
    /// 添加当前对象与指定对象之间的变更信息。
    /// </summary>
    /// <param name="newObj">作为比较目标的新对象实例。</param>
    protected virtual void AddChanges(TObject newObj) { }

    /// <summary>
    /// 记录领域对象变更。
    /// </summary>
    /// <typeparam name="TProperty">目标属性类型。</typeparam>
    /// <typeparam name="TValue">新值类型。</typeparam>
    /// <param name="expression">用于选择待比较属性的成员访问表达式。</param>
    /// <param name="newValue">属性变更后的值。</param>
    protected void AddChange<TProperty, TValue>(Expression<Func<TObject, TProperty>> expression, TValue newValue) => 
        ChangeTrackingContext.Add(expression, AssignableType(this), newValue);

    /// <summary>
    /// 记录领域对象变更。
    /// </summary>
    /// <typeparam name="TDomainObject">变更后的领域对象类型。</typeparam>
    /// <param name="beforeChange">变更前的可跟踪对象。</param>
    /// <param name="afterChange">变更后的领域对象。</param>
    protected void AddChange<TDomainObject>(IChangeTrackable beforeChange, TDomainObject afterChange) 
        where TDomainObject : IDomainObject =>
        ChangeTrackingContext.Add(beforeChange, afterChange);

    /// <summary>
    /// 记录领域对象变更。
    /// </summary>
    /// <typeparam name="TDomainObject">变更后领域对象的类型。</typeparam>
    /// <param name="leftObjs">变更前的可跟踪对象集合。</param>
    /// <param name="rightObjs">变更后的领域对象集合。</param>
    protected void AddChange<TDomainObject>(IEnumerable<IChangeTrackable> leftObjs, IEnumerable<TDomainObject> rightObjs) 
        where TDomainObject : IDomainObject =>
        ChangeTrackingContext.Add(leftObjs, rightObjs);

    #endregion

    #region Description(描述)

    /// <summary>
    /// 添加对象描述信息。
    /// </summary>
    /// <typeparam name="TProperty">属性值类型。</typeparam>
    /// <param name="expression">用于选择描述属性的成员访问表达式。</param>
    protected void AddDescription<TProperty>(Expression<Func<TObject, TProperty>> expression) =>
        DescriptionContext.Add(expression);

    #endregion

    #region Misc(杂项)

    /// <summary>
    /// 将当前基类实例转换为具体领域对象类型。
    /// </summary>
    /// <param name="me">要转换的领域对象基类实例。</param>
    /// <returns>具体领域对象实例。</returns>
    /// <exception cref="InvalidCastException">实例无法转换为 <typeparamref name="TObject"/> 时抛出。</exception>
    private TObject AssignableType(DomainObjectBase<TObject> me) => me as TObject ?? throw new InvalidCastException($"无法转换为 {typeof(TObject).FullName}");

    #endregion
}
