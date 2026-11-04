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
    /// 比较并汇总单个关系对象。
    /// </summary>
    /// <typeparam name="TValue">关系对象的静态类型。</typeparam>
    /// <param name="propertyName">关系属性路径。</param>
    /// <param name="description">关系描述。</param>
    /// <param name="oldValue">变更前的关系对象。</param>
    /// <param name="newValue">变更后的关系对象。</param>
    /// <param name="structureModel">关系对象为普通结构时使用的冻结模型；领域对象忽略该参数。</param>
    /// <param name="keySelector">关系对象的领域身份选择器；无身份时为 null。</param>
    protected void AddObjectChanges<TValue>(string propertyName, string description, TValue oldValue, TValue newValue,
        ChangeGraphModel<TValue> structureModel = null, Func<TValue, object> keySelector = null)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        CompareValue(frame, propertyName, description, oldValue, newValue, structureModel, keySelector);
    }

    /// <summary>
    /// 记录结构性新增或删除；供生成的结构比较方法在单侧缺失时使用。
    /// </summary>
    /// <typeparam name="TValue">关系对象类型。</typeparam>
    /// <param name="propertyName">关系路径。</param>
    /// <param name="description">关系描述。</param>
    /// <param name="oldValue">变更前的对象。</param>
    /// <param name="newValue">变更后的对象。</param>
    protected void AddStructuralChange<TValue>(string propertyName, string description, TValue oldValue, TValue newValue)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        AddStructural(frame, propertyName, description, oldValue, true, newValue, true, null);
    }

    /// <summary>
    /// 组合关系路径与成员名。
    /// </summary>
    /// <param name="path">关系路径；为空时直接返回成员名。</param>
    /// <param name="member">成员名。</param>
    /// <returns>组合后的路径。</returns>
    protected static string CombinePath(string path, string member) => ChangeTrackingSession.Combine(path, member);

    /// <summary>
    /// 进入普通结构/容器的引用对作用域；回边返回 false。
    /// </summary>
    /// <param name="oldValue">旧值。</param>
    /// <param name="newValue">新值。</param>
    /// <returns>首次进入返回 true；已在祖先作用域时返回 false。</returns>
    protected static bool TryEnterShape(object oldValue, object newValue) =>
        ChangeTrackingSession.TryEnterPair(oldValue, newValue);

    /// <summary>
    /// 退出普通结构/容器的引用对作用域。
    /// </summary>
    /// <param name="oldValue">旧值。</param>
    /// <param name="newValue">新值。</param>
    protected static void ExitShape(object oldValue, object newValue) =>
        ChangeTrackingSession.ExitPair(oldValue, newValue);

    /// <summary>
    /// 比较并汇总有序集合关系。
    /// </summary>
    /// <typeparam name="TItem">集合元素类型。</typeparam>
    /// <param name="propertyName">集合关系路径。</param>
    /// <param name="description">集合关系描述。</param>
    /// <param name="oldItems">变更前集合。</param>
    /// <param name="newItems">变更后集合。</param>
    /// <param name="keySelector">元素身份选择器；为 null 时按索引配对。</param>
    /// <param name="itemModel">元素为普通结构时使用的冻结模型。</param>
    /// <param name="compare">元素自身比较委托；由生成器按元素形状提供。</param>
    protected void AddSequenceChanges<TItem>(string propertyName, string description, IEnumerable<TItem> oldItems,
        IEnumerable<TItem> newItems, Func<TItem, object> keySelector, ChangeGraphModel<TItem> itemModel = null,
        Action<TItem, TItem, string, string> compare = null)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        var before = oldItems?.ToList() ?? new List<TItem>();
        var after = newItems?.ToList() ?? new List<TItem>();
        if (keySelector == null)
        {
            for (var index = 0; index < before.Count || index < after.Count; index++)
            {
                var path = propertyName + "[" + index + "]";
                if (index < before.Count && index < after.Count)
                    CompareElement(frame, path, description, before[index], after[index], itemModel, compare);
                else if (index < before.Count)
                    AddStructural(frame, path, description, before[index], true, default, false, itemModel);
                else
                    AddStructural(frame, path, description, default, false, after[index], true, itemModel);
            }
            return;
        }
        CompareKeyed(frame, propertyName, description, before, after, keySelector, itemModel, compare);
    }

    /// <summary>
    /// 比较并汇总无序对象或标量集合关系。
    /// </summary>
    /// <typeparam name="TItem">集合元素类型。</typeparam>
    /// <param name="propertyName">集合关系路径。</param>
    /// <param name="description">集合关系描述。</param>
    /// <param name="oldItems">变更前集合。</param>
    /// <param name="newItems">变更后集合。</param>
    /// <param name="keySelector">元素身份选择器；标量集合为 null。</param>
    /// <param name="itemModel">元素为普通结构时使用的冻结模型。</param>
    /// <param name="compare">元素自身比较委托；由生成器按元素形状提供。</param>
    protected void AddSetChanges<TItem>(string propertyName, string description, IEnumerable<TItem> oldItems,
        IEnumerable<TItem> newItems, Func<TItem, object> keySelector, ChangeGraphModel<TItem> itemModel = null,
        Action<TItem, TItem, string, string> compare = null)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        if (keySelector == null)
        {
            var beforeComparer = oldItems is HashSet<TItem> oldSet ? oldSet.Comparer : null;
            var afterComparer = newItems is HashSet<TItem> newSet ? newSet.Comparer : null;
            var comparer = ResolveContainerComparer(beforeComparer, afterComparer, propertyName);
            CompareScalarSet(frame, propertyName, description,
                oldItems?.ToList() ?? new List<TItem>(), newItems?.ToList() ?? new List<TItem>(), comparer);
            return;
        }
        CompareKeyed(frame, propertyName, description,
            oldItems?.ToList() ?? new List<TItem>(), newItems?.ToList() ?? new List<TItem>(),
            keySelector, itemModel, compare);
    }

    /// <summary>
    /// 比较并汇总字典关系。
    /// </summary>
    /// <typeparam name="TKey">字典键类型。</typeparam>
    /// <typeparam name="TValue">字典值类型。</typeparam>
    /// <param name="propertyName">字典关系路径。</param>
    /// <param name="description">字典关系描述。</param>
    /// <param name="oldValues">变更前字典条目。</param>
    /// <param name="newValues">变更后字典条目。</param>
    /// <param name="valueModel">字典值为普通结构时使用的冻结模型。</param>
    /// <param name="keySelector">字典值的领域身份选择器；无身份时为 null。</param>
    /// <param name="compare">字典值自身比较委托；由生成器按值形状提供。</param>
    protected void AddDictionaryChanges<TKey, TValue>(string propertyName, string description,
        IEnumerable<KeyValuePair<TKey, TValue>> oldValues, IEnumerable<KeyValuePair<TKey, TValue>> newValues,
        ChangeGraphModel<TValue> valueModel = null, Func<TValue, object> keySelector = null,
        Action<TValue, TValue, string, string> compare = null)
    {
        var frame = ChangeTrackingSession.Current;
        if (frame == null)
            return;
        var beforeComparer = oldValues is Dictionary<TKey, TValue> oldMap ? oldMap.Comparer : null;
        var afterComparer = newValues is Dictionary<TKey, TValue> newMap ? newMap.Comparer : null;
        var comparer = ResolveContainerComparer(beforeComparer, afterComparer, propertyName);
        var before = ToDictionary(oldValues, propertyName, comparer);
        var after = ToDictionary(newValues, propertyName, comparer);
        foreach (var pair in before)
        {
            var path = propertyName + "{" + GraphCaptureContext.FormatKey(pair.Key) + "}";
            if (after.TryGetValue(pair.Key, out var newValue))
                CompareElement(frame, path, description, pair.Value, newValue, valueModel, compare);
            else
                AddStructural(frame, path, description, pair.Value, true, default, false, valueModel);
        }
        foreach (var pair in after)
        {
            if (before.ContainsKey(pair.Key))
                continue;
            var path = propertyName + "{" + GraphCaptureContext.FormatKey(pair.Key) + "}";
            AddStructural(frame, path, description, default, false, pair.Value, true, valueModel);
        }
    }

    /// <summary>
    /// 比较单个元素；优先使用生成器提供的元素比较委托。
    /// </summary>
    private static void CompareElement<TItem>(ChangeTrackingFrame frame, string path, string description,
        TItem oldValue, TItem newValue, ChangeGraphModel<TItem> itemModel,
        Action<TItem, TItem, string, string> compare)
    {
        if (oldValue == null && newValue == null)
            return;
        if (compare != null)
        {
            compare(oldValue, newValue, path, description);
            return;
        }
        CompareValue(frame, path, description, oldValue, newValue, itemModel, null);
    }

    /// <summary>
    /// 比较单个值并按关系路径合并差异。
    /// </summary>
    private static void CompareValue<TValue>(ChangeTrackingFrame frame, string path, string description,
        TValue oldValue, TValue newValue, ChangeGraphModel<TValue> structureModel,
        Func<TValue, object> keySelector, bool hasOld = true, bool hasNew = true)
    {
        if (!hasOld && !hasNew)
            return;
        if (!hasOld)
        {
            AddStructural(frame, path, description, default, false, newValue, true, structureModel);
            return;
        }
        if (!hasNew)
        {
            AddStructural(frame, path, description, oldValue, true, default, false, structureModel);
            return;
        }
        // 两侧槽位都存在时，双 null 表示无变化；单侧 null 才是关系新增或删除。
        if (oldValue == null && newValue == null)
            return;
        if (oldValue == null)
        {
            AddStructural(frame, path, description, default, false, newValue, true, structureModel);
            return;
        }
        if (newValue == null)
        {
            AddStructural(frame, path, description, oldValue, true, default, false, structureModel);
            return;
        }
        if (oldValue is IChangeTrackable trackable)
        {
            if (oldValue.GetType() != newValue.GetType())
            {
                AddStructural(frame, path, description, oldValue, true, newValue, true, structureModel);
                return;
            }
            // 已具备领域身份的关系先判配对：不同身份是删除旧实体并新增新实体。
            if (keySelector != null && !Equals(keySelector(oldValue), keySelector(newValue)))
            {
                AddStructural(frame, path, description, oldValue, true, newValue, true, structureModel);
                return;
            }
            if (ChangeTrackingSession.IsInAncestorChain(oldValue, newValue))
                return;
            var childChanges = trackable.GetChanges(newValue);
            ChangeTrackingSession.Merge(frame, path, childChanges);
            return;
        }
        // 任意层容器在到达领域对象前继续递归，避免用结构模型替代子实体 hook。
        if (CompareContainer(frame, path, description, oldValue, newValue))
            return;
        if (structureModel != null)
        {
            var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(structureModel));
            ChangeTrackingSession.Merge(frame, path, tracker.Compare(oldValue, newValue).ToLegacyDescriptors());
            return;
        }
        if (EqualityComparer<TValue>.Default.Equals(oldValue, newValue))
            return;
        ChangeTrackingSession.Add(frame, path, description,
            ScalarValueFormatter.Format(oldValue), ScalarValueFormatter.Format(newValue));
    }

    /// <summary>
    /// 递归比较非领域容器；元素递归到领域对象时回到 GetChanges。
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
                for (var index = 0; index < before.Count || index < after.Count; index++)
                {
                    var itemPath = path + "[" + index + "]";
                    var hasOld = index < before.Count;
                    var hasNew = index < after.Count;
                    if (hasOld && hasNew)
                        CompareObjectValue(frame, itemPath, description, before[index], after[index], true, true);
                    else if (hasOld)
                        AddStructural(frame, itemPath, description, before[index], true, null, false, (ChangeGraphModel<object>)null);
                    else
                        AddStructural(frame, itemPath, description, null, false, after[index], true, (ChangeGraphModel<object>)null);
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
        var keys = new List<object>();
        if (before != null) foreach (var key in before.Keys) if (!keys.Contains(key)) keys.Add(key);
        if (after != null) foreach (var key in after.Keys) if (!keys.Contains(key)) keys.Add(key);
        foreach (var key in keys)
        {
            var itemPath = path + "{" + GraphCaptureContext.FormatKey(key) + "}";
            var hasOld = before != null && before.Contains(key);
            var hasNew = after != null && after.Contains(key);
            CompareObjectValue(frame, itemPath, description,
                hasOld ? before[key] : null, hasNew ? after[key] : null, hasOld, hasNew);
        }
    }

    /// <summary>
    /// 比较单个 object 值并分派领域对象、容器与结构/标量。
    /// </summary>
    private static void CompareObjectValue(ChangeTrackingFrame frame, string path, string description,
        object oldValue, object newValue, bool hasOld, bool hasNew)
    {
        if (!hasOld)
        {
            AddStructural(frame, path, description, null, false, newValue, true, (ChangeGraphModel<object>)null);
            return;
        }
        if (!hasNew)
        {
            AddStructural(frame, path, description, oldValue, true, null, false, (ChangeGraphModel<object>)null);
            return;
        }
        if (oldValue == null && newValue == null)
            return;
        if (oldValue == null || newValue == null)
        {
            AddStructural(frame, path, description, oldValue, true, newValue, true, (ChangeGraphModel<object>)null);
            return;
        }
        if (oldValue is IChangeTrackable trackable)
        {
            if (oldValue.GetType() == newValue.GetType() &&
                !ChangeTrackingSession.IsInAncestorChain(oldValue, newValue))
            {
                ChangeTrackingSession.Merge(frame, path, trackable.GetChanges(newValue));
            }
            else
            {
                AddStructural(frame, path, description, oldValue, true, newValue, true, (ChangeGraphModel<object>)null);
            }
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

    /// <summary>
    /// 按身份键匹配并比较集合元素。
    /// </summary>
    private static void CompareKeyed<TItem>(ChangeTrackingFrame frame, string propertyName, string description,
        IReadOnlyList<TItem> before, IReadOnlyList<TItem> after, Func<TItem, object> keySelector,
        ChangeGraphModel<TItem> itemModel, Action<TItem, TItem, string, string> compare)
    {
        var beforeMap = new Dictionary<object, TItem>();
        foreach (var item in before)
            AddKeyedItem(beforeMap, RequireKey(keySelector, item, propertyName), item, propertyName);
        var afterMap = new Dictionary<object, TItem>();
        foreach (var item in after)
            AddKeyedItem(afterMap, RequireKey(keySelector, item, propertyName), item, propertyName);
        foreach (var pair in beforeMap)
        {
            var path = propertyName + "[" + GraphCaptureContext.FormatKey(pair.Key) + "]";
            if (afterMap.TryGetValue(pair.Key, out var newItem))
                CompareElement(frame, path, description, pair.Value, newItem, itemModel, compare);
            else
                AddStructural(frame, path, description, pair.Value, true, default, false, itemModel);
        }
        foreach (var pair in afterMap)
        {
            if (beforeMap.ContainsKey(pair.Key))
                continue;
            var path = propertyName + "[" + GraphCaptureContext.FormatKey(pair.Key) + "]";
            AddStructural(frame, path, description, default, false, pair.Value, true, itemModel);
        }
    }

    /// <summary>
    /// 比较标量集合，按容器自身相等语义报告新增和删除。
    /// </summary>
    private static void CompareScalarSet<TItem>(ChangeTrackingFrame frame, string propertyName, string description,
        IReadOnlyList<TItem> before, IReadOnlyList<TItem> after, IEqualityComparer<TItem> comparer)
    {
        var beforeSet = new HashSet<TItem>(before, comparer);
        var afterSet = new HashSet<TItem>(after, comparer);
        foreach (var item in before)
        {
            if (afterSet.Contains(item))
                continue;
            var path = propertyName + "[" + ScalarValueFormatter.Format(item) + "]";
            ChangeTrackingSession.Add(frame, path, description, ScalarValueFormatter.Format(item), "<missing>");
        }
        foreach (var item in after)
        {
            if (beforeSet.Contains(item))
                continue;
            var path = propertyName + "[" + ScalarValueFormatter.Format(item) + "]";
            ChangeTrackingSession.Add(frame, path, description, "<missing>", ScalarValueFormatter.Format(item));
        }
    }

    /// <summary>
    /// 判断新旧容器比较器是否兼容；缺失一侧时使用存在侧语义。
    /// </summary>
    private static IEqualityComparer<T> ResolveContainerComparer<T>(IEqualityComparer<T> before,
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
            throw new InvalidOperationException($"容器比较器不兼容，路径: {propertyName}");
        if (before.Equals(after))
            return before;
        throw new InvalidOperationException($"容器比较器不一致，路径: {propertyName}");
    }

    /// <summary>
    /// 读取元素身份键；缺失或重复时抛出带关系路径的异常。
    /// </summary>
    private static object RequireKey<TItem>(Func<TItem, object> keySelector, TItem item, string propertyName)
    {
        var key = keySelector(item);
        if (key == null)
            throw new InvalidOperationException($"集合元素身份键为空，路径: {propertyName}");
        return key;
    }

    /// <summary>
    /// 登记元素身份键；重复时抛出带关系路径的异常。
    /// </summary>
    private static void AddKeyedItem<TItem>(Dictionary<object, TItem> map, object key, TItem item, string propertyName)
    {
        if (map.ContainsKey(key))
            throw new InvalidOperationException($"集合元素身份键重复，路径: {propertyName}");
        map.Add(key, item);
    }

    /// <summary>
    /// 将字典条目序列转换为按容器键索引的字典。
    /// </summary>
    private static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> values, string propertyName, IEqualityComparer<TKey> comparer)
    {
        var result = new Dictionary<TKey, TValue>(comparer);
        if (values == null)
            return result;
        foreach (var pair in values)
        {
            if (result.ContainsKey(pair.Key))
                throw new InvalidOperationException($"字典键重复，路径: {propertyName}");
            result.Add(pair.Key, pair.Value);
        }
        return result;
    }

    /// <summary>
    /// 记录关系的结构性新增或删除。
    /// </summary>
    private static void AddStructural<TValue>(ChangeTrackingFrame frame, string path, string description,
        TValue oldValue, bool hasOld, TValue newValue, bool hasNew, ChangeGraphModel<TValue> structureModel)
    {
        var oldText = hasOld ? FormatPayload(oldValue, structureModel) : "<missing>";
        var newText = hasNew ? FormatPayload(newValue, structureModel) : "<missing>";
        ChangeTrackingSession.Add(frame, path, description, oldText, newText);
    }

    /// <summary>
    /// 使用冻结模型格式化关系载荷；没有模型时退回类型值展示。
    /// </summary>
    private static string FormatPayload<TValue>(TValue value, ChangeGraphModel<TValue> structureModel)
    {
        if (structureModel == null)
            return ScalarValueFormatter.Format(value);
        var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(structureModel));
        return ChangeGraphLegacyExtensions.FormatSnapshot(tracker.Capture(value));
    }

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
