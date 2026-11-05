using System.Collections.Generic;

namespace Bing.Domain.ChangeTracking.Internal;

/// <summary>
/// 表示一次 GetChanges 调用拥有的结果 frame。
/// </summary>
internal sealed class ChangeTrackingFrame
{
    /// <summary>
    /// 初始化一个 <see cref="ChangeTrackingFrame" /> 类型的实例。
    /// </summary>
    /// <param name="parent">外层调用 frame；最外层为 null。</param>
    /// <param name="before">本次比较的旧对象引用。</param>
    /// <param name="after">本次比较的新对象引用。</param>
    internal ChangeTrackingFrame(ChangeTrackingFrame parent, ChangeTrackingContext owner, object before, object after)
    {
        Parent = parent;
        Owner = owner;
        Before = before;
        After = after;
        Result = new ChangedValueDescriptorCollection();
    }

    /// <summary>获取该 frame 所属的变更上下文。</summary>
    internal ChangeTrackingContext Owner { get; }

    /// <summary>获取外层调用 frame。</summary>
    internal ChangeTrackingFrame Parent { get; }
    /// <summary>获取本次比较的旧对象引用。</summary>
    internal object Before { get; }
    /// <summary>获取本次比较的新对象引用。</summary>
    internal object After { get; }
    /// <summary>获取当前 frame 独立的结果缓冲。</summary>
    internal ChangedValueDescriptorCollection Result { get; }
}

/// <summary>
/// 维护同步 GetChanges 组合调用链的 frame 栈与引用对防环。
/// </summary>
/// <remarks>
/// 使用线程本地栈隔离并发比较；每次 GetChanges 拥有独立结果 frame，嵌套调用在 finally 中恢复外层。
/// 防环只检查当前祖先链中的同一新旧引用对，兄弟路径再次到达共享对象仍会正常比较。
/// </remarks>
internal static class ChangeTrackingSession
{
    /// <summary>
    /// 保存当前线程正在比较的 frame 栈顶。
    /// </summary>
    [System.ThreadStatic]
    private static ChangeTrackingFrame _current;

    /// <summary>
    /// 保存普通结构/容器的引用对作用域栈。
    /// </summary>
    [System.ThreadStatic]
    private static List<PairScope> _valuePairs;

    /// <summary>保存当前领域关系链，仅用于异常中定位完整路径。</summary>
    [System.ThreadStatic]
    private static string _errorPathPrefix;

    /// <summary>进入子对象比较并返回原有异常路径前缀。</summary>
    internal static string PushErrorPath(string path)
    {
        var previous = _errorPathPrefix;
        _errorPathPrefix = Prefix(previous, path);
        return previous;
    }

    /// <summary>恢复调用子对象前的异常路径前缀。</summary>
    internal static void RestoreErrorPath(string previous) => _errorPathPrefix = previous;

    /// <summary>把当前对象中的局部路径转换成面向调用方的完整路径。</summary>
    internal static string ErrorPath(string localPath) => Prefix(_errorPathPrefix, localPath);

    /// <summary>
    /// 引用对作用域。
    /// </summary>
    private readonly struct PairScope
    {
        /// <summary>初始化引用对作用域。</summary>
        internal PairScope(object before, object after) { Before = before; After = after; }
        /// <summary>获取旧引用。</summary>
        internal object Before { get; }
        /// <summary>获取新引用。</summary>
        internal object After { get; }
    }

    /// <summary>
    /// 进入普通结构/容器的引用对作用域；同一引用对已在祖先链时返回 false。
    /// </summary>
    /// <param name="before">旧引用。</param>
    /// <param name="after">新引用。</param>
    /// <returns>首次进入返回 true；回边返回 false。</returns>
    internal static bool TryEnterPair(object before, object after)
    {
        _valuePairs ??= new List<PairScope>();
        for (var index = 0; index < _valuePairs.Count; index++)
        {
            if (ReferenceEquals(_valuePairs[index].Before, before) && ReferenceEquals(_valuePairs[index].After, after))
                return false;
        }
        _valuePairs.Add(new PairScope(before, after));
        return true;
    }

    /// <summary>
    /// 退出最近进入的引用对作用域。
    /// </summary>
    /// <param name="before">旧引用。</param>
    /// <param name="after">新引用。</param>
    internal static void ExitPair(object before, object after)
    {
        if (_valuePairs == null || _valuePairs.Count == 0)
            return;
        var index = _valuePairs.Count - 1;
        if (ReferenceEquals(_valuePairs[index].Before, before) && ReferenceEquals(_valuePairs[index].After, after))
            _valuePairs.RemoveAt(index);
    }

    /// <summary>
    /// 获取当前 frame；没有进行中的比较时为 null。
    /// </summary>
    internal static ChangeTrackingFrame Current => _current;

    /// <summary>
    /// 开始一次新的比较 frame。
    /// </summary>
    /// <param name="owner">该 frame 所属的实体变更上下文。</param>
    /// <param name="before">本次比较的旧对象引用。</param>
    /// <param name="after">本次比较的新对象引用。</param>
    /// <returns>新建并已入栈的 frame。</returns>
    internal static ChangeTrackingFrame Begin(ChangeTrackingContext owner, object before, object after)
    {
        var frame = new ChangeTrackingFrame(_current, owner, before, after);
        _current = frame;
        return frame;
    }

    /// <summary>
    /// 结束指定 frame 并恢复外层 frame。
    /// </summary>
    /// <param name="frame">要结束的 frame。</param>
    internal static void End(ChangeTrackingFrame frame)
    {
        if (ReferenceEquals(_current, frame))
            _current = frame.Parent;
    }

    /// <summary>
    /// 判断指定新旧引用对是否已位于当前递归祖先链中。
    /// </summary>
    /// <param name="before">待检查的旧对象引用。</param>
    /// <param name="after">待检查的新对象引用。</param>
    /// <returns>祖先链中已存在同一引用对时为 true，否则为 false。</returns>
    internal static bool IsInAncestorChain(object before, object after)
    {
        for (var frame = _current; frame != null; frame = frame.Parent)
        {
            if (ReferenceEquals(frame.Before, before) && ReferenceEquals(frame.After, after))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 把子结果按关系路径前缀合并到当前 frame。
    /// </summary>
    /// <param name="frame">接收合并结果的 frame。</param>
    /// <param name="prefix">关系路径前缀。</param>
    /// <param name="descriptors">子对象返回的独立结果。</param>
    internal static void Merge(ChangeTrackingFrame frame, string prefix, IEnumerable<ChangedValueDescriptor> descriptors)
    {
        if (frame == null || descriptors == null)
            return;
        foreach (var descriptor in descriptors)
        {
            frame.Result.Add(new ChangedValueDescriptor(Prefix(prefix, descriptor.PropertyName),
                descriptor.Description, descriptor.OldValue, descriptor.NewValue));
        }
    }

    /// <summary>
    /// 直接向当前 frame 写入一条描述符。
    /// </summary>
    /// <param name="frame">接收结果的 frame。</param>
    /// <param name="propertyName">属性或关系路径。</param>
    /// <param name="description">描述文本。</param>
    /// <param name="oldValue">旧值文本。</param>
    /// <param name="newValue">新值文本。</param>
    internal static void Add(ChangeTrackingFrame frame, string propertyName, string description, string oldValue, string newValue)
    {
        frame?.Result.Add(new ChangedValueDescriptor(propertyName, description, oldValue, newValue));
    }

    /// <summary>
    /// 将关系路径前缀与子路径组合。
    /// </summary>
    /// <param name="prefix">关系路径前缀。</param>
    /// <param name="path">子对象内部路径。</param>
    /// <returns>完整路径。</returns>
    internal static string Prefix(string prefix, string path)
    {
        // 子结果根路径使用 "$" 表示；关系本身即可作为路径，不能拼成 "关系.$"。
        if (string.IsNullOrEmpty(path) || path == "$")
            return prefix;
        if (string.IsNullOrEmpty(prefix))
            return path;
        // 索引/字典段直接接在前缀后，不插入分隔点。
        if (path[0] == '[' || path[0] == '{')
            return prefix + path;
        return prefix + "." + path;
    }

    /// <summary>
    /// 组合关系路径与成员名。
    /// </summary>
    /// <param name="prefix">关系路径；为空时直接返回成员名。</param>
    /// <param name="member">成员名。</param>
    /// <returns>组合后的路径。</returns>
    internal static string Combine(string prefix, string member) =>
        string.IsNullOrEmpty(prefix) ? member : prefix + "." + member;
}
