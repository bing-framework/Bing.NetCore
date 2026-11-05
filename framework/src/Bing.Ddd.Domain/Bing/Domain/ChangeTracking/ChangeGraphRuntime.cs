using System;
using System.Collections.Generic;
using System.Linq;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 对象图比较器。
/// </summary>
public sealed class ChangeGraphTracker
{
    /// <summary>
    /// 保存变更图跟踪使用的配置。
    /// </summary>
    private readonly ChangeGraphOptions _options;

    /// <summary>
    /// 初始化一个 <see cref="ChangeGraphTracker" /> 类型的实例。
    /// </summary>
    /// <param name="options">比较选项。</param>
    /// <remarks>
    /// 初始化一个 <see cref="ChangeGraphTracker" /> 实例。
    /// </remarks>
    public ChangeGraphTracker(ChangeGraphOptions options = null) => _options = (options ?? ChangeGraphOptions.Default).Clone();

    /// <summary>
    /// 捕获对象并创建变更图快照。
    /// </summary>
    /// <param name="source">要捕获的对象。</param>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <returns>包含捕获模型、根图值和当前选项的快照。</returns>
    /// <remarks>
    /// 模型按声明类型或运行时适配器解析。
    /// </remarks>
    public ChangeGraphSnapshot<T> Capture<T>(T source)
    {
        var model = ResolveModel(source);
        var domainRules = DomainRules(typeof(T), source);
        var context = new GraphCaptureContext(_options, null, domainRules);
        return new ChangeGraphSnapshot<T>(model, model.CaptureValue(source, context, string.Empty, 0), _options);
    }

    /// <summary>
    /// 捕获并比较两个对象以生成变更结果。
    /// </summary>
    /// <param name="before">比较前的对象。</param>
    /// <param name="after">比较后的对象。</param>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <returns>描述两个对象图之间差异的结果；两个对象均为 null 时返回空结果。</returns>
    /// <remarks>
    /// 比较两侧分别创建捕获上下文，并共用一次遍历预算。
    /// </remarks>
    public ChangeGraphResult Compare<T>(T before, T after)
    {
        if (ReferenceEquals(before, null) && ReferenceEquals(after, null))
            return EmptyResult();
        var runtimeValue = ReferenceEquals(after, null) ? before : after;
        var model = ResolveModel(runtimeValue);
        var budget = new GraphTraversalBudget(_options);
        var domainRules = DomainRules(typeof(T), runtimeValue);
        // before/after 必须使用独立上下文；同一业务对象被两边复用时也要分别形成快照。
        var oldValue = model.CaptureValue(before, new GraphCaptureContext(_options, budget, domainRules), string.Empty, 0);
        var newValue = model.CaptureValue(after, new GraphCaptureContext(_options, budget, domainRules), string.Empty, 0);
        return GraphDiffer.Diff(oldValue, newValue, _options, budget, domainRules);
    }

    /// <summary>
    /// 将当前对象与先前快照比较并生成变更结果。
    /// </summary>
    /// <param name="before">比较前的变更图快照。</param>
    /// <param name="current">要与快照比较的当前对象。</param>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <returns>描述快照与当前对象差异的结果。</returns>
    /// <remarks>
    /// 快照必须由当前运行时所使用的同一个 ChangeGraphOptions 实例创建。
    /// </remarks>
    public ChangeGraphResult CompareSnapshot<T>(ChangeGraphSnapshot<T> before, T current)
    {
        if (before == null)
            throw new ArgumentNullException(nameof(before));
        if (!ReferenceEquals(before.Options, _options))
            throw new InvalidOperationException("快照只能由创建它的同一套 ChangeGraphOptions 规则解释。");
        var budget = new GraphTraversalBudget(_options);
        var domainRules = DomainRules(typeof(T), current);
        var context = new GraphCaptureContext(_options, budget, domainRules);
        var currentValue = before.Model.CaptureValue(current, context, string.Empty, 0);
        return GraphDiffer.Diff(before.Root, currentValue, _options, budget, domainRules);
    }

    /// <summary>
    /// 解析指定对象类型对应的变更图模型。
    /// </summary>
    /// <param name="value">用于判断运行时类型的对象值。</param>
    /// <typeparam name="T">要解析模型的声明类型。</typeparam>
    /// <returns>注册的适配器模型、对象自身提供的模型，或运行时分派适配器模型。</returns>
    /// <remarks>
    /// 无法解析模型或适配器时抛出异常。
    /// </remarks>
    private ChangeGraphModel ResolveModel<T>(T value)
    {
        // 精确类型的领域对象直接使用自身模型；object/接口/基类入口统一走运行时分派，
        // 由 CaptureAdapted 的领域优先规则保证两侧各自使用自身模型且不被适配器覆盖。
        if (value is IChangeGraphNode node)
        {
            if (typeof(T) == value.GetType())
                return node.ChangeGraphModel;
            return ChangeGraphModel<T>.CreateAdapterModel<T>(typeof(T).FullName + ":runtime-dispatch");
        }
        if (value != null && _options.TryAdapter(value.GetType(), out var runtimeAdapter))
            return runtimeAdapter;
        if (_options.TryAdapter(typeof(T), out var declaredAdapter))
            return declaredAdapter;
        throw new InvalidOperationException($"类型 {typeof(T).FullName} 没有可用的 ChangeGraph 模型。请实现 IChangeGraphNode 或通过 WithAdapter 显式注册模型。");
    }

    /// <summary>
    /// 判断当前比较根是否为领域实体，从而忽略调用方标量/键比较器覆盖。
    /// </summary>
    /// <typeparam name="T">比较根类型。</typeparam>
    /// <returns>根类型实现变更跟踪契约时为 true。</returns>
    private static bool DomainRules(Type declaredType, object runtimeValue) =>
        typeof(IChangeTrackable).IsAssignableFrom(declaredType) || runtimeValue is IChangeTrackable;

    /// <summary>
    /// 创建不包含变更项的结果。
    /// </summary>
    /// <returns>包含空只读变更集合的结果。</returns>
    private static ChangeGraphResult EmptyResult() => new ChangeGraphResult(new List<ChangeEntry>().AsReadOnly());
}
