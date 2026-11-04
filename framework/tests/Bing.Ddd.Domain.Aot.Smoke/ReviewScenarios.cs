using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Domain.ChangeTracking;

/// <summary>扩展原生 Smoke 的容器、预算、适配器和闭合泛型验收场景。</summary>
public static class ReviewScenarios
{
    /// <summary>运行 AOT 发布中必须保留的边界断言。</summary>
    public static void Run()
    {
        VerifyContainersAndCompositeKeys();
        VerifyCaptureFailuresAndAdapters();
        VerifyClosedGenericModels();
        ReviewFixScenarios.Run();
        ReviewMatrixScenarios.Run();
        ReviewIgnoreScenarios.Run();
    }

    private static void VerifyContainersAndCompositeKeys()
    {
        static ReviewCollectionsRoot Build(bool updated) => new()
        {
            Ordered = updated
                ? new List<ReviewCompositeItem>
                {
                    new() { TenantId = 7, ItemId = 3, Name = "three" },
                    new() { TenantId = 7, ItemId = 1, Name = "one" },
                    new() { TenantId = 7, ItemId = 2, Name = "two-new" }
                }
                : new List<ReviewCompositeItem>
                {
                    new() { TenantId = 7, ItemId = 1, Name = "one" },
                    new() { TenantId = 7, ItemId = 2, Name = "two" },
                    new() { TenantId = 7, ItemId = 3, Name = "three" }
                },
            Map = updated
                ? new Dictionary<string, ReviewCompositeItem>
                {
                    ["keep"] = new() { TenantId = 8, ItemId = 1, Name = "map-new" },
                    ["added"] = new() { TenantId = 8, ItemId = 3, Name = "added" }
                }
                : new Dictionary<string, ReviewCompositeItem>
                {
                    ["keep"] = new() { TenantId = 8, ItemId = 1, Name = "map-old" },
                    ["removed"] = new() { TenantId = 8, ItemId = 2, Name = "removed" }
                },
            Members = updated
                ? new HashSet<ReviewCompositeItem>
                {
                    new() { TenantId = 9, ItemId = 2, Name = "set-new" },
                    new() { TenantId = 9, ItemId = 3, Name = "set-added" }
                }
                : new HashSet<ReviewCompositeItem>
                {
                    new() { TenantId = 9, ItemId = 1, Name = "set-removed" },
                    new() { TenantId = 9, ItemId = 2, Name = "set-old" }
                },
            Optional = new ReviewNullableValue { Name = updated ? "struct-new" : "struct-old", Count = updated ? 2 : 1 }
        };

        var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithTrackMoves());
        var result = tracker.Compare(Build(false), Build(true));
        var changes = result.Changes.ToDictionary(change => change.Path.Value, StringComparer.Ordinal);
        var expectedPaths = new[] { "Map{keep}.Name", "Map{removed}", "Map{added}", "Members[9,1]", "Members[9,2].Name", "Members[9,3]",
            "Ordered[7,3]", "Ordered[7,2].Name", "Optional.Name", "Optional.Count" };
        Require(changes.Count == expectedPaths.Length && expectedPaths.All(changes.ContainsKey), "容器比较的全部十项结果无多报漏报");
        Require(changes.ContainsKey("Map{keep}.Name") && changes["Map{keep}.Name"].Kind == ChangeKind.Modified &&
                changes.ContainsKey("Map{removed}") && changes["Map{removed}"].Kind == ChangeKind.Removed &&
                changes.ContainsKey("Map{added}") && changes["Map{added}"].Kind == ChangeKind.Added,
            "字典值修改与键增删");
        Require(changes.ContainsKey("Members[9,1]") && changes["Members[9,1]"].Kind == ChangeKind.Removed &&
                changes.ContainsKey("Members[9,2].Name") && changes["Members[9,2].Name"].Kind == ChangeKind.Modified &&
                changes.ContainsKey("Members[9,3]") && changes["Members[9,3]"].Kind == ChangeKind.Added,
            "Set 复合身份键的成员增删和叶子修改");
        foreach (var pair in new[] { ("Map{keep}.Name", "map-old", "map-new"), ("Members[9,2].Name", "set-old", "set-new"),
            ("Ordered[7,2].Name", "two", "two-new") })
            Require(changes[pair.Item1].Kind == ChangeKind.Modified && changes[pair.Item1].OldValue.GetScalar<string>() == pair.Item2 &&
                changes[pair.Item1].NewValue.GetScalar<string>() == pair.Item3, "容器叶子原值 " + pair.Item1);
        foreach (var pair in new[] { ("Map{removed}", "removed", 8, 2, false), ("Map{added}", "added", 8, 3, true),
            ("Members[9,1]", "set-removed", 9, 1, false), ("Members[9,3]", "set-added", 9, 3, true) })
        {
            var change = changes[pair.Item1];
            var payload = pair.Item5 ? change.NewValue : change.OldValue;
            Require(change.Kind == (pair.Item5 ? ChangeKind.Added : ChangeKind.Removed) &&
                (pair.Item5 ? change.OldValue : change.NewValue).Kind == ChangeValueKind.Missing &&
                payload.Members.Count == 3 && payload.Members["Name"].GetScalar<string>() == pair.Item2 &&
                payload.Members["TenantId"].GetScalar<int>() == pair.Item3 && payload.Members["ItemId"].GetScalar<int>() == pair.Item4,
                "容器增删完整载荷 " + pair.Item1);
        }
        var moved = result.Changes.Single(change => change.Kind == ChangeKind.Moved && change.Path.Value.StartsWith("Ordered[", StringComparison.Ordinal));
        var movedKey = (ChangeGraphKey)moved.Path.Segments.Single(segment => segment.Kind == ChangePathSegmentKind.EntityKey).Key;
        var modified = result.Changes.Single(change => change.Kind == ChangeKind.Modified && change.Path.Value.StartsWith("Ordered[", StringComparison.Ordinal));
        var modifiedKey = (ChangeGraphKey)modified.Path.Segments.Single(segment => segment.Kind == ChangePathSegmentKind.EntityKey).Key;
        Require(movedKey.Components.SequenceEqual(new object[] { 7, 3 }) && moved.OldIndex == 2 && moved.NewIndex == 0 &&
                modifiedKey.Components.SequenceEqual(new object[] { 7, 2 }) &&
                modified.Path.Segments.Last().Kind == ChangePathSegmentKind.Member &&
                modified.Path.Segments.Last().Name == nameof(ReviewCompositeItem.Name),
            "列表使用复合键报告移动及同轮修改");
        Require(changes["Optional.Name"].OldValue.GetScalar<string>() == "struct-old" &&
                changes["Optional.Name"].NewValue.GetScalar<string>() == "struct-new" &&
                changes["Optional.Count"].OldValue.GetScalar<int>() == 1 &&
                changes["Optional.Count"].NewValue.GetScalar<int>() == 2,
            "可空结构体采集成员差异");
    }

    private static void VerifyCaptureFailuresAndAdapters()
    {
        var budgetTracker = new ChangeGraphTracker(new ChangeGraphOptions { MaxDepth = 0 });
        var budgetFailure = CatchCaptureFailure(() => budgetTracker.Capture(new ReviewBudgetRoot
        {
            Children = new List<ReviewBudgetChild> { new() { Name = "child" } }
        }));
        Require(budgetFailure.Path == "Children" && budgetFailure.InnerException?.Message.Contains("深度超过限制 0", StringComparison.Ordinal) == true,
            "深度预算超限报告完整成员路径");

        IReviewUnknownValue beforeValue = new ReviewUnknownValue { Name = "old" };
        IReviewUnknownValue afterValue = new ReviewUnknownValue { Name = "new" };
        var before = new ReviewAdapterRoot { Value = beforeValue };
        var after = new ReviewAdapterRoot { Value = afterValue };
        var unknownFailure = CatchCaptureFailure(() => new ChangeGraphTracker().Capture(before));
        Require(unknownFailure.Path == "Value" &&
                unknownFailure.InnerException?.Message.Contains("必须通过 WithAdapter", StringComparison.Ordinal) == true,
            "未知接口实现失败时保留路径");

        var adapter = new ChangeGraphModel<IReviewUnknownValue>("review-interface-adapter",
            (writer, value) => writer.Scalar(nameof(IReviewUnknownValue.Name), value.Name), null,
            Array.Empty<string>());
        var adapted = new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(adapter)).Compare(before, after);
        var change = adapted.Changes.Single();
        Require(change.Path.Value == "Value.Name" && change.Kind == ChangeKind.Modified &&
                change.OldValue.GetScalar<string>() == "old" && change.NewValue.GetScalar<string>() == "new",
            "显式接口适配器捕获真实实现");
    }

    private static void VerifyClosedGenericModels()
    {
        var scalar = new ChangeGraphTracker().Compare(
            new ReviewGenericRoot<int> { Value = 10 }, new ReviewGenericRoot<int> { Value = 20 });
        Require(scalar.Changes.Count == 1 && scalar.Changes[0].Path.Value == "Value" &&
                scalar.Changes[0].OldValue.GetScalar<int>() == 10 && scalar.Changes[0].NewValue.GetScalar<int>() == 20,
            "NativeAOT 闭合值类型泛型模型");

        var reference = new ChangeGraphTracker().Compare(
            new ReviewGenericRoot<Customer> { Value = new Customer { Id = 41, Name = "generic-old" } },
            new ReviewGenericRoot<Customer> { Value = new Customer { Id = 41, Name = "generic-new" } });
        Require(reference.Changes.Count == 1 && reference.Changes[0].Path.Value == "Value.Name" &&
                reference.Changes[0].OldValue.GetScalar<string>() == "generic-old" &&
                reference.Changes[0].NewValue.GetScalar<string>() == "generic-new",
            "NativeAOT 闭合引用类型泛型模型");
    }

    private static ChangeGraphCaptureException CatchCaptureFailure(Action action)
    {
        try
        {
            action();
        }
        catch (ChangeGraphCaptureException exception)
        {
            return exception;
        }
        throw new InvalidOperationException("预期的对象图采集失败未发生。");
    }

    private static void Require(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException("Smoke 检查失败: " + description);
        Console.WriteLine("PASS | " + description);
    }
}

public partial class ReviewCollectionsRoot : Bing.Domain.Entities.DomainObjectBase<ReviewCollectionsRoot>
{
    public List<ReviewCompositeItem> Ordered { get; set; } = new();
    public Dictionary<string, ReviewCompositeItem> Map { get; set; } = new();
    public HashSet<ReviewCompositeItem> Members { get; set; } = new();
    public ReviewNullableValue? Optional { get; set; }
}

public partial class ReviewCompositeItem : Bing.Domain.Entities.DomainObjectBase<ReviewCompositeItem>, Bing.Domain.Entities.IEntity
{
    public int TenantId { get; set; }
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;

    public void Init() { }
    public object[] GetKeys() => new object[] { TenantId, ItemId };
}

public struct ReviewNullableValue
{
    public string Name { get; set; }
    public int Count { get; set; }
}

public partial class ReviewBudgetRoot : Bing.Domain.Entities.DomainObjectBase<ReviewBudgetRoot>
{
    public List<ReviewBudgetChild> Children { get; set; } = new();
}

public partial class ReviewBudgetChild : Bing.Domain.Entities.DomainObjectBase<ReviewBudgetChild>
{
    public string Name { get; set; } = string.Empty;
}

public interface IReviewUnknownValue
{
    string Name { get; }
}

public sealed class ReviewUnknownValue : IReviewUnknownValue
{
    public string Name { get; init; } = string.Empty;
}

public partial class ReviewAdapterRoot : Bing.Domain.Entities.DomainObjectBase<ReviewAdapterRoot>
{
    public IReviewUnknownValue Value { get; set; } = null!;
}

public partial class ReviewGenericRoot<T> : Bing.Domain.Entities.DomainObjectBase<ReviewGenericRoot<T>>
{
    public T Value { get; set; } = default!;
}
