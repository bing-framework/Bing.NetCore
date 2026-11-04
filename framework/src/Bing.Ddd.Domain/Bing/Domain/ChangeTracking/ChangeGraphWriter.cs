using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 生成代码使用的类型化采集 Writer。
/// </summary>
/// <typeparam name="TOwner">当前节点类型。</typeparam>
public sealed class ChangeGraphWriter<TOwner>
{
    /// <summary>
    /// 保存写入变更图时使用的捕获上下文。
    /// </summary>
    private readonly GraphCaptureContext _context;
    /// <summary>
    /// 保存当前写入的图节点。
    /// </summary>
    private readonly GraphNode _node;
    /// <summary>
    /// 保存当前成员路径。
    /// </summary>
    private readonly string _path;
    /// <summary>
    /// 保存当前递归深度。
    /// </summary>
    private readonly int _depth;

    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphWriter{TOwner}" /> 类型的实例。
    /// </summary>
    /// <param name="context">当前操作关联的上下文或工作单元。</param>
    /// <param name="node">当前对象图中的节点。</param>
    /// <param name="path">要使用的路径。</param>
    /// <param name="depth">当前节点在对象图中的深度。</param>
    internal ChangeGraphWriter(GraphCaptureContext context, GraphNode node, string path, int depth)
    {
        _context = context;
        _node = node;
        _path = path;
        _depth = depth;
    }

    /// <summary>
    /// 检查当前成员路径是否被配置为忽略。
    /// </summary>
    /// <param name="name">当前成员名称。</param>
    /// <returns>当前路径被忽略时返回 true，否则返回 false。</returns>
    public bool IsIgnored(string name) => _context.IsIgnored(Join(_path, name));

    /// <summary>
    /// 捕获成员的适配器值并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="value">要适配的成员值。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TValue">成员值的类型。</typeparam>
    public void Adapted<TValue>(string name, TValue value, string description = null) =>
        Add(name, _context.CaptureAdapted(value, Join(_path, name), _depth + 1), description);

    /// <summary>
    /// 为成员捕获异常添加成员路径上下文。
    /// </summary>
    /// <param name="name">发生异常的成员名称。</param>
    /// <param name="exception">捕获成员时发生的异常。</param>
    /// <returns>包含成员路径的变更图捕获异常；传入的异常已是该类型时直接返回原异常。</returns>
    public Exception CreateCaptureException(string name, Exception exception)
    {
        if (exception is ChangeGraphCaptureException)
            return exception;
        return new ChangeGraphCaptureException(Join(_path, name), exception);
    }

    /// <summary>
    /// 将标量成员加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="value">标量成员值。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TValue">标量值的类型。</typeparam>
    public void Scalar<TValue>(string name, TValue value, string description = null) =>
        Add(name, GraphValue.Scalar(value), description);

    /// <summary>
    /// 使用指定模型捕获对象成员并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="value">对象成员值。</param>
    /// <param name="model">用于捕获对象成员的变更图模型。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TValue">对象成员的类型。</typeparam>
    public void Object<TValue>(string name, TValue value, ChangeGraphModel<TValue> model, string description = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        Add(name, model.CaptureValue(value, _context, Join(_path, name), _depth + 1), description);
    }

    /// <summary>
    /// 使用指定模型捕获序列成员并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="values">序列成员值。</param>
    /// <param name="model">用于捕获序列项的变更图模型。</param>
    /// <param name="keySelector">可选的序列项身份键选择函数。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TValue">序列元素的类型。</typeparam>
    public void Sequence<TValue>(string name, IEnumerable<TValue> values, ChangeGraphModel<TValue> model,
        Func<TValue, object> keySelector = null, string description = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        var effectiveKey = _context.ResolveCollectionKey<TOwner, TValue>(name,
            _context.ResolveIdentity(keySelector));
        _context.ValidateCollectionItem<TOwner, TValue>(name, model, Join(_path, name) + "[]");
        Add(name, _context.CaptureSequence(values, model, effectiveKey, Join(_path, name), _depth + 1,
            _context.CollectionRuleId<TOwner, TValue>(name), _node.IsDomainEntity), description);
    }

    /// <summary>
    /// 捕获标量序列成员并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="values">标量序列值。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TValue">序列元素的类型。</typeparam>
    public void SequenceScalar<TValue>(string name, IEnumerable<TValue> values, string description = null) =>
        Add(name, _context.CaptureScalarSequence(values, Join(_path, name), _depth + 1), description);

    /// <summary>
    /// 使用指定模型捕获字典成员并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="values">字典键值对序列。</param>
    /// <param name="model">用于捕获字典值的变更图模型。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TKey">字典键的类型。</typeparam>
    /// <typeparam name="TValue">字典值的类型。</typeparam>
    public void Dictionary<TKey, TValue>(string name, IEnumerable<KeyValuePair<TKey, TValue>> values,
        ChangeGraphModel<TValue> model, string description = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        Add(name, _context.CaptureDictionary(values, model, Join(_path, name), _depth + 1, _node.IsDomainEntity), description);
    }

    /// <summary>
    /// 捕获标量字典成员并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="values">字典键值对序列。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TKey">字典键的类型。</typeparam>
    /// <typeparam name="TValue">字典值的类型。</typeparam>
    public void DictionaryScalar<TKey, TValue>(string name, IEnumerable<KeyValuePair<TKey, TValue>> values,
        string description = null) => Add(name, _context.CaptureScalarDictionary(values, Join(_path, name), _depth + 1, _node.IsDomainEntity), description);

    /// <summary>
    /// 使用指定模型捕获集合成员并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="values">集合项序列。</param>
    /// <param name="model">用于捕获集合项的变更图模型。</param>
    /// <param name="keySelector">可选的集合项身份键选择函数。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TValue">集合元素的类型。</typeparam>
    public void Set<TValue>(string name, IEnumerable<TValue> values, ChangeGraphModel<TValue> model,
        Func<TValue, object> keySelector = null, string description = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        var effectiveKey = _context.ResolveCollectionKey<TOwner, TValue>(name,
            _context.ResolveIdentity(keySelector));
        _context.ValidateCollectionItem<TOwner, TValue>(name, model, Join(_path, name) + "[]");
        Add(name, _context.CaptureSet(values, model, effectiveKey, Join(_path, name), _depth + 1,
            _context.CollectionRuleId<TOwner, TValue>(name), _node.IsDomainEntity), description);
    }

    /// <summary>
    /// 捕获标量集合成员并加入变更图。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="values">标量集合项。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <typeparam name="TValue">集合元素的类型。</typeparam>
    public void SetScalar<TValue>(string name, IEnumerable<TValue> values, string description = null) =>
        Add(name, _context.CaptureScalarSet(values, Join(_path, name), _depth + 1, _node.IsDomainEntity), description);

    /// <summary>
    /// 报告当前成员类型不受变更图捕获支持。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <remarks>
    /// 该方法始终抛出异常，指出不支持成员的图路径。
    /// </remarks>
    public void Unsupported(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("成员名称不能为空。", nameof(name));
        throw new InvalidOperationException($"成员类型不支持对象图采集，路径: {Join(_path, name)}");
    }

    /// <summary>
    /// 将成员值加入当前对象的变更图节点。
    /// </summary>
    /// <param name="name">成员名称。</param>
    /// <param name="value">成员的内部图值。</param>
    /// <param name="description">可选的成员说明。</param>
    /// <remarks>
    /// 忽略路径命中忽略规则的成员；成员名称为空白时抛出参数异常。
    /// </remarks>
    private void Add(string name, GraphValue value, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("成员名称不能为空。", nameof(name));
        if (_context.IsIgnored(Join(_path, name)))
            return;
        _context.Visit(Join(_path, name), _depth + 1);
        _node.Members[name] = new GraphMember(value,
            string.IsNullOrWhiteSpace(description) ? name : description);
    }

    /// <summary>
    /// 将父路径和成员名称组合为成员路径。
    /// </summary>
    /// <param name="parent">父级路径。</param>
    /// <param name="member">成员名称。</param>
    /// <returns>父路径为空时返回成员名称，否则返回以点分隔的组合路径。</returns>
    internal static string Join(string parent, string member) => string.IsNullOrEmpty(parent) ? member : parent + "." + member;
}
