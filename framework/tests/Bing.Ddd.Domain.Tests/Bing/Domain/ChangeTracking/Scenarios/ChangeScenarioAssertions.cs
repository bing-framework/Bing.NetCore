using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Bing.Domain.ChangeTracking.Scenarios;

/// <summary>
/// 按完整四元组验证差异，保留重复记录以便发现漏报和重复报告。
/// </summary>
internal static class ChangeScenarioAssertions
{
    /// <summary>
    /// 验证数量、路径、描述和新旧值；结果顺序不属于本测试约定。
    /// </summary>
    internal static void AssertChanges(
        IEnumerable<ChangedValueDescriptor> actual,
        params (string Path, string Description, string OldValue, string NewValue)[] expected)
    {
        var actualItems = actual
            .Select(change => (change.PropertyName, change.Description, change.OldValue, change.NewValue))
            .OrderBy(change => change.PropertyName, StringComparer.Ordinal)
            .ThenBy(change => change.Description, StringComparer.Ordinal)
            .ThenBy(change => change.OldValue, StringComparer.Ordinal)
            .ThenBy(change => change.NewValue, StringComparer.Ordinal)
            .ToArray();
        var expectedItems = expected
            .OrderBy(change => change.Path, StringComparer.Ordinal)
            .ThenBy(change => change.Description, StringComparer.Ordinal)
            .ThenBy(change => change.OldValue, StringComparer.Ordinal)
            .ThenBy(change => change.NewValue, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedItems, actualItems);
    }
}
