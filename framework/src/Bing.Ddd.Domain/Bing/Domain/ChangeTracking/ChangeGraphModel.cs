using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 非泛型模型基类。
/// </summary>
public abstract class ChangeGraphModel
{
    /// <summary>
    /// 获取实际采集的模型类型。
    /// </summary>
    /// <remarks>不通过反射遍历成员。</remarks>
    public abstract Type ModelType { get; }
    /// <summary>
    /// 获取采集模型的成员名称集合。
    /// </summary>
    internal abstract IReadOnlyCollection<string> MemberNames { get; }
    /// <summary>
    /// 获取集合成员及其元素类型映射。
    /// </summary>
    internal abstract IReadOnlyDictionary<string, Type> CollectionMembers { get; }
    /// <summary>
    /// 获取模型是否仅为运行时适配器占位。
    /// </summary>
    internal virtual bool IsAdapterPlaceholder => false;
    /// <summary>
    /// 获取模型是否表达领域实体，从而忽略调用方比较器覆盖。
    /// </summary>
    internal virtual bool IsDomainEntity => false;
    /// <summary>
    /// 获取采集模型标识。
    /// </summary>
    internal abstract string ModelId { get; }
    /// <summary>
    /// 获取忽略采集的成员路径集合。
    /// </summary>
    internal abstract IReadOnlyCollection<string> IgnorePaths { get; }
    /// <summary>
    /// 采集指定路径的模型值快照。
    /// </summary>
    /// <param name="value">待处理的值。</param>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="path">当前采集成员的路径。</param>
    /// <param name="depth">当前采集深度。</param>
    /// <returns>采集得到的模型值快照。</returns>
    internal abstract GraphValue CaptureValue(object value, GraphCaptureContext context, string path, int depth);
}

/// <summary>
/// 类型化对象图模型。
/// </summary>
/// <typeparam name="T">模型根类型。</typeparam>
public sealed class ChangeGraphModel<T> : ChangeGraphModel
{
    /// <summary>
    /// 保存把实体写入变更图写入器的捕获委托。
    /// </summary>
    private readonly Action<ChangeGraphWriter<T>, T> _capture;
    /// <summary>
    /// 保存从实体提取集合身份键的委托。
    /// </summary>
    private readonly Func<T, object> _keySelector;
    /// <summary>
    /// 保存用于形状比较的捕获委托。
    /// </summary>
    private readonly Func<T, GraphCaptureContext, string, int, GraphValue> _shapeCapture;
    /// <summary>
    /// 指示该模型是否为显式适配器占位模型。
    /// </summary>
    private readonly bool _adapterPlaceholder;

    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphModel{T}" /> 类型的实例。
    /// </summary>
    /// <param name="modelId">模型身份。</param>
    /// <param name="capture">类型化采集委托。</param>
    /// <param name="keySelector">可选的实体键选择器。</param>
    /// <param name="members">生成或手工声明的可采集成员名，用于验证显式键依赖。</param>
    /// <param name="collectionMembers">顺序集合及 Set 成员的静态元素类型，用于验证成员规则。</param>
    public ChangeGraphModel(string modelId, Action<ChangeGraphWriter<T>, T> capture, Func<T, object> keySelector = null,
        IReadOnlyCollection<string> members = null, IReadOnlyDictionary<string, Type> collectionMembers = null)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("模型身份不能为空。", nameof(modelId));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        ModelId = modelId;
        _keySelector = keySelector;
        MemberNames = members == null ? null : Array.AsReadOnly(members.ToArray());
        CollectionMembers = collectionMembers == null ? null :
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, Type>(collectionMembers.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphModel{T}" /> 类型的实例。
    /// </summary>
    /// <param name="modelId">用于区分对象图模型定义的标识。</param>
    /// <param name="shapeCapture">用于捕获对象图模型值的委托。</param>
    /// <param name="adapterPlaceholder">指示当前对象图模型是否为适配器占位模型。</param>
    private ChangeGraphModel(string modelId, Func<T, GraphCaptureContext, string, int, GraphValue> shapeCapture, bool adapterPlaceholder = false)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("模型身份不能为空。", nameof(modelId));
        _shapeCapture = shapeCapture ?? throw new ArgumentNullException(nameof(shapeCapture));
        _adapterPlaceholder = adapterPlaceholder;
        ModelId = modelId;
    }

    /// <summary>
    /// 创建捕获对象序列的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <param name="valuesSelector">从集合值中读取对象序列的函数。</param>
    /// <param name="itemModelResolver">获取序列项变更图模型的函数。</param>
    /// <param name="keySelector">可选的序列项身份键选择函数。</param>
    /// <typeparam name="TCollection">集合的类型。</typeparam>
    /// <typeparam name="TItem">集合元素的类型。</typeparam>
    /// <returns>用于捕获集合对象序列的变更图模型。</returns>
    public static ChangeGraphModel<TCollection> CreateSequenceModel<TCollection, TItem>(string modelId,
        Func<TCollection, IEnumerable<TItem>> valuesSelector,
        Func<ChangeGraphModel<TItem>> itemModelResolver,
        Func<TItem, object> keySelector = null, Func<Func<TItem, object>> keySelectorResolver = null)
    {
        if (valuesSelector == null)
            throw new ArgumentNullException(nameof(valuesSelector));
        if (itemModelResolver == null)
            throw new ArgumentNullException(nameof(itemModelResolver));
        return new ChangeGraphModel<TCollection>(modelId, (value, context, path, depth) =>
        {
            if (ReferenceEquals(value, null))
                return GraphValue.Null;
            return context.CaptureSequence(valuesSelector(value), itemModelResolver(), context.ResolveIdentity(
                keySelectorResolver == null ? keySelector : keySelectorResolver()), path, depth);
        });
    }

    /// <summary>
    /// 创建捕获标量序列的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <param name="valuesSelector">从集合值中读取标量序列的函数。</param>
    /// <typeparam name="TCollection">集合的类型。</typeparam>
    /// <typeparam name="TItem">集合元素的类型。</typeparam>
    /// <returns>用于捕获标量序列的变更图模型。</returns>
    public static ChangeGraphModel<TCollection> CreateScalarSequenceModel<TCollection, TItem>(string modelId,
        Func<TCollection, IEnumerable<TItem>> valuesSelector)
    {
        if (valuesSelector == null)
            throw new ArgumentNullException(nameof(valuesSelector));
        return new ChangeGraphModel<TCollection>(modelId, (value, context, path, depth) =>
        {
            if (ReferenceEquals(value, null))
                return GraphValue.Null;
            return context.CaptureScalarSequence(valuesSelector(value), path, depth);
        });
    }

    /// <summary>
    /// 创建捕获字典值的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <param name="valuesSelector">从字典值中读取键值对序列的函数。</param>
    /// <param name="valueModelResolver">获取字典值变更图模型的函数。</param>
    /// <typeparam name="TDictionary">字典的类型。</typeparam>
    /// <typeparam name="TKey">字典键的类型。</typeparam>
    /// <typeparam name="TValue">字典值的类型。</typeparam>
    /// <returns>用于捕获字典值的变更图模型。</returns>
    public static ChangeGraphModel<TDictionary> CreateDictionaryModel<TDictionary, TKey, TValue>(string modelId,
        Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> valuesSelector,
        Func<ChangeGraphModel<TValue>> valueModelResolver)
    {
        if (valuesSelector == null)
            throw new ArgumentNullException(nameof(valuesSelector));
        if (valueModelResolver == null)
            throw new ArgumentNullException(nameof(valueModelResolver));
        return new ChangeGraphModel<TDictionary>(modelId, (value, context, path, depth) =>
        {
            if (ReferenceEquals(value, null))
                return GraphValue.Null;
            return context.CaptureDictionary(valuesSelector(value), valueModelResolver(), path, depth);
        });
    }

    /// <summary>
    /// 创建捕获标量字典值的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <param name="valuesSelector">从字典值中读取键值对序列的函数。</param>
    /// <typeparam name="TDictionary">字典的类型。</typeparam>
    /// <typeparam name="TKey">字典键的类型。</typeparam>
    /// <typeparam name="TValue">字典值的类型。</typeparam>
    /// <returns>用于捕获标量字典值的变更图模型。</returns>
    public static ChangeGraphModel<TDictionary> CreateScalarDictionaryModel<TDictionary, TKey, TValue>(string modelId,
        Func<TDictionary, IEnumerable<KeyValuePair<TKey, TValue>>> valuesSelector)
    {
        if (valuesSelector == null)
            throw new ArgumentNullException(nameof(valuesSelector));
        return new ChangeGraphModel<TDictionary>(modelId, (value, context, path, depth) =>
        {
            if (ReferenceEquals(value, null))
                return GraphValue.Null;
            return context.CaptureScalarDictionary(valuesSelector(value), path, depth);
        });
    }

    /// <summary>
    /// 创建捕获集合对象项的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <param name="valuesSelector">从集合值中读取项序列的函数。</param>
    /// <param name="itemModelResolver">获取集合项变更图模型的函数。</param>
    /// <param name="keySelector">可选的集合项身份键选择函数。</param>
    /// <typeparam name="TSet">集合的类型。</typeparam>
    /// <typeparam name="TItem">集合元素的类型。</typeparam>
    /// <returns>用于捕获集合对象项的变更图模型。</returns>
    public static ChangeGraphModel<TSet> CreateSetModel<TSet, TItem>(string modelId,
        Func<TSet, IEnumerable<TItem>> valuesSelector,
        Func<ChangeGraphModel<TItem>> itemModelResolver,
        Func<TItem, object> keySelector = null, Func<Func<TItem, object>> keySelectorResolver = null)
    {
        if (valuesSelector == null)
            throw new ArgumentNullException(nameof(valuesSelector));
        if (itemModelResolver == null)
            throw new ArgumentNullException(nameof(itemModelResolver));
        return new ChangeGraphModel<TSet>(modelId, (value, context, path, depth) =>
        {
            if (ReferenceEquals(value, null))
                return GraphValue.Null;
            return context.CaptureSet(valuesSelector(value), itemModelResolver(), context.ResolveIdentity(
                keySelectorResolver == null ? keySelector : keySelectorResolver()), path, depth);
        });
    }

    /// <summary>
    /// 创建捕获标量集合项的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <param name="valuesSelector">从集合值中读取标量项序列的函数。</param>
    /// <typeparam name="TSet">集合的类型。</typeparam>
    /// <typeparam name="TItem">集合元素的类型。</typeparam>
    /// <returns>用于捕获标量集合项的变更图模型。</returns>
    public static ChangeGraphModel<TSet> CreateScalarSetModel<TSet, TItem>(string modelId,
        Func<TSet, IEnumerable<TItem>> valuesSelector)
    {
        if (valuesSelector == null)
            throw new ArgumentNullException(nameof(valuesSelector));
        return new ChangeGraphModel<TSet>(modelId, (value, context, path, depth) =>
        {
            if (ReferenceEquals(value, null))
                return GraphValue.Null;
            return context.CaptureScalarSet(valuesSelector(value), path, depth);
        });
    }

    /// <summary>
    /// 创建使用适配器捕获未知类型的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <typeparam name="TUnknown">要适配的未知类型。</typeparam>
    /// <returns>用于捕获未知类型值的适配器模型。</returns>
    public static ChangeGraphModel<TUnknown> CreateAdapterModel<TUnknown>(string modelId) =>
        new ChangeGraphModel<TUnknown>(modelId, (value, context, path, depth) => context.CaptureAdapted(value, path, depth), true);

    /// <summary>
    /// 创建捕获可空值类型的变更图模型。
    /// </summary>
    /// <param name="modelId">模型标识。</param>
    /// <param name="valueModelResolver">获取底层值变更图模型的函数。</param>
    /// <typeparam name="TValue">可空值的底层值类型。</typeparam>
    /// <returns>用于捕获可空值的变更图模型。</returns>
    public static ChangeGraphModel<TValue?> CreateNullableModel<TValue>(string modelId,
        Func<ChangeGraphModel<TValue>> valueModelResolver) where TValue : struct
    {
        if (valueModelResolver == null) throw new ArgumentNullException(nameof(valueModelResolver));
        return new ChangeGraphModel<TValue?>(modelId, (value, context, path, depth) => value.HasValue
            ? valueModelResolver().CaptureValue(value.Value, context, path, depth) : GraphValue.Null);
    }

    /// <summary>
    /// 获取模型身份。
    /// </summary>
    public string Id => ModelId;

    /// <summary>获取本实体声明的身份键选择器；未配置键时为 null。</summary>
    /// <remarks>供生成的关系代码复用，父实体不重新生成子实体的键读取逻辑。</remarks>
    public Func<T, object> KeySelector => _keySelector;

    /// <inheritdoc />
    public override Type ModelType => typeof(T);
    /// <inheritdoc />
    internal override bool IsDomainEntity => typeof(IChangeTrackable).IsAssignableFrom(typeof(T));

    /// <inheritdoc />
    internal override IReadOnlyCollection<string> MemberNames { get; }
    /// <inheritdoc />
    internal override IReadOnlyDictionary<string, Type> CollectionMembers { get; }
    /// <inheritdoc />
    internal override bool IsAdapterPlaceholder => _adapterPlaceholder;

    /// <inheritdoc />
    internal override string ModelId { get; }

    /// <inheritdoc />
    internal override IReadOnlyCollection<string> IgnorePaths => Array.Empty<string>();

    /// <summary>
    /// 获取模型值的身份键。
    /// </summary>
    /// <param name="value">要获取身份键的模型值。</param>
    /// <param name="key">接收身份键的输出参数。</param>
    /// <returns>模型配置了键选择函数时返回 true，并将其结果写入 key；未配置时返回 false，key 为 null。</returns>
    internal bool TryGetKey(T value, out object key)
    {
        key = _keySelector == null ? null : _keySelector(value);
        return _keySelector != null;
    }

    /// <inheritdoc />
    internal override GraphValue CaptureValue(object value, GraphCaptureContext context, string path, int depth)
    {
        return CaptureValue(value, context, path, depth, null);
    }

    /// <summary>
    /// 采集指定路径的模型值快照。
    /// </summary>
    /// <param name="value">待捕获的对象值。</param>
    /// <param name="context">用于捕获和验证图数据的上下文。</param>
    /// <param name="path">对象在图中的路径。</param>
    /// <param name="depth">对象在图中的遍历深度。</param>
    /// <param name="keyOverride">可选的身份键选择函数；为 null 时使用模型配置。</param>
    /// <returns>由对象生成的内部图值；value 为 null 时返回空值节点。</returns>
    /// <remarks>
    /// 值类型与模型不匹配时抛出异常；具有派生运行时类型且未配置适配器时使用适配器捕获。
    /// </remarks>
    internal GraphValue CaptureValue(object value, GraphCaptureContext context, string path, int depth,
        Func<T, object> keyOverride)
    {
        if (value == null)
            return GraphValue.Null;
        context.Visit(path, depth);
        if (!(value is T typed))
            throw new InvalidOperationException($"模型 {ModelId} 收到不兼容的值类型: {value.GetType().FullName}");
        if (_shapeCapture != null)
            return _shapeCapture(typed, context, path, depth);
        context.ValidateModel(this, path);
        if (value.GetType() != typeof(T) && Nullable.GetUnderlyingType(typeof(T)) != value.GetType() && !context.IsAdapter(this))
            return context.CaptureAdapted(value, path, depth);
        return context.CaptureObject(typed, this, path, depth, _capture, keyOverride ?? _keySelector, keyOverride == null);
    }
}
