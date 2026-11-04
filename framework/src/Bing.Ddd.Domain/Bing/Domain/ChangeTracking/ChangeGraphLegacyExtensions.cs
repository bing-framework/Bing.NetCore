using System;
using System.Globalization;
using System.Linq;
using Bing.Domain.ChangeTracking.Internal;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 将结构化变更图投影为旧文本描述符的兼容扩展。
/// </summary>
/// <remarks>
/// 对象图结果到旧文本描述符的显式有损投影，不参与匹配或判等。
/// </remarks>
public static class ChangeGraphLegacyExtensions
{
    /// <summary>
    /// 将变更图结果转换为旧版变更描述集合。
    /// </summary>
    /// <param name="result">要转换的变更图结果。</param>
    /// <returns>包含路径、描述及新旧值文本的旧版描述集合。</returns>
    /// <remarks>
    /// 根路径使用 $ 表示；未指定描述时使用路径作为描述。
    /// </remarks>
    public static ChangedValueDescriptorCollection ToLegacyDescriptors(this ChangeGraphResult result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        var descriptors = new ChangedValueDescriptorCollection();
        foreach (var change in result.Changes)
        {
            var path = change.Path.Value.Length == 0 ? "$" : change.Path.Value;
            descriptors.Add(new ChangedValueDescriptor(path,
                string.IsNullOrWhiteSpace(change.Description) ? path : change.Description,
                Format(change.OldValue), Format(change.NewValue)));
        }
        return descriptors;
    }

    /// <summary>
    /// 将快照根值格式化为与标量描述一致的展示文本。
    /// </summary>
    /// <typeparam name="T">快照根类型。</typeparam>
    /// <param name="snapshot">要格式化的冻结快照。</param>
    /// <returns>快照根值的结构化文本表示。</returns>
    internal static string FormatSnapshot<T>(ChangeGraphSnapshot<T> snapshot) =>
        snapshot == null ? "null" : FormatGraphValue(snapshot.Root);

    /// <summary>
    /// 将内部图值格式化为结构化文本。
    /// </summary>
    /// <param name="value">要格式化的内部图值。</param>
    /// <returns>内部图值的结构化文本表示。</returns>
    private static string FormatGraphValue(GraphValue value)
    {
        if (value == null || value.Kind == ChangeValueKind.Null) return "null";
        if (value.Kind == ChangeValueKind.Scalar)
            return ScalarValueFormatter.Format(value.ScalarValue);
        if (value.Node != null)
            return "{" + string.Join(",", value.Node.Members.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => ScalarValueFormatter.Quote(pair.Key) + ":" + FormatGraphValue(pair.Value.Value))) + "}";
        return "[" + string.Join(",", value.Items.Select(item =>
            (value.Kind == ChangeValueKind.Dictionary ? GraphCaptureContext.FormatKey(item.Key) + ":" : "") +
            FormatGraphValue(item.Value))) + "]";
    }

    /// <summary>
    /// 将公开变更值格式化为旧版文本表示。
    /// </summary>
    /// <param name="value">要格式化的公开变更值。</param>
    /// <returns>缺失值、null、标量、引用、对象或集合的文本表示。</returns>
    internal static string Format(ChangeValue value)
    {
        if (value.Kind == ChangeValueKind.Missing) return "<missing>";
        if (value.Kind == ChangeValueKind.Null) return "null";
        if (value.Kind == ChangeValueKind.Scalar)
            return ScalarValueFormatter.Format(value.Value);
        if (value.ReferenceId.HasValue) return "ref(" + value.ReferenceId.Value.ToString(CultureInfo.InvariantCulture) + ")";
        if (value.Members != null)
            return "{" + string.Join(",", value.Members.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => ScalarValueFormatter.Quote(pair.Key) + ":" + Format(pair.Value))) + "}";
        return "[" + string.Join(",", value.Items.Select(item =>
            (value.Kind == ChangeValueKind.Dictionary ? GraphCaptureContext.FormatKey(item.Key) + ":" : "") + Format(item.Value))) + "]";
    }
}
