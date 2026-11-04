using System.ComponentModel;
using Bing.Domain.ChangeTracking;
using DomainAotFixtures;

/// <summary>补齐原生门禁：完整结果集、空值、冻结、预算、规则视图和外部模型。</summary>
public static class ReviewMatrixScenarios
{
    public static void Run()
    {
        VerifyNullAndIdentity();
        VerifyBudgets();
        VerifyExternalAndDescriptions();
        VerifyRuntimeTypes();
        VerifyScalarsAndContainerBoundaries();
    }

    private static void VerifyNullAndIdentity()
    {
        var tracker = new ChangeGraphTracker();
        Require(tracker.Compare<ReviewMatrixRoot>(null, null).Changes.Count == 0, "空根双方无变更");
        var root = new ReviewMatrixRoot { Child = new ExternalEntity { Id = 1, Name = " old " } };
        var added = tracker.Compare<ReviewMatrixRoot>(null, root).Changes.Single();
        var removed = tracker.Compare(root, (ReviewMatrixRoot?)null).Changes.Single();
        Require(added.Path.Value == "" && added.Kind == ChangeKind.Added && added.OldValue.Kind == ChangeValueKind.Null &&
            removed.Path.Value == "" && removed.Kind == ChangeKind.Removed && removed.NewValue.Kind == ChangeValueKind.Null &&
            added.NewValue.Members["Child"].Members["Name"].GetScalar<string>() == " old " &&
            removed.OldValue.Members["Child"].Members["Id"].GetScalar<int>() == 1, "空根增删的种类和冻结载荷");
        var empty = new ReviewMatrixRoot();
        Require(tracker.Compare(empty, new ReviewMatrixRoot()).Changes.Count == 0, "空子对象双方无变更");
        added = tracker.Compare(empty, root).Changes.Single();
        removed = tracker.Compare(root, empty).Changes.Single();
        Require(added.Path.Value == "Child" && added.Kind == ChangeKind.Added && added.OldValue.Kind == ChangeValueKind.Null &&
            removed.Path.Value == "Child" && removed.Kind == ChangeKind.Removed && removed.NewValue.Kind == ChangeValueKind.Null &&
            added.NewValue.Members["Name"].GetScalar<string>() == " old " &&
            removed.OldValue.Members["Name"].GetScalar<string>() == " old ", "空子对象增删载荷");
        var next = new ReviewMatrixRoot { Child = new ExternalEntity { Id = 2, Name = "new" } };
        var replaced = tracker.Compare(root, next).Changes.Single();
        Require(replaced.Path.Value == "Child" && replaced.Kind == ChangeKind.Replaced &&
            replaced.OldValue.Members["Id"].GetScalar<int>() == 1 && replaced.NewValue.Members["Id"].GetScalar<int>() == 2 &&
            replaced.OldValue.Members["Name"].GetScalar<string>() == " old " && replaced.NewValue.Members["Name"].GetScalar<string>() == "new",
            "子对象身份替换只报告完整节点");
        var snapshot = tracker.Capture(root);
        var priorPayload = tracker.Compare<ReviewMatrixRoot>(null, root).Changes.Single().NewValue;
        root.Child!.Name = "new";
        var frozen = tracker.CompareSnapshot(snapshot, root).Changes.Single();
        Require(frozen.Path.Value == "Child.Name" && frozen.Kind == ChangeKind.Modified && frozen.Description == "外部名称" &&
            frozen.OldValue.GetScalar<string>() == " old " && frozen.NewValue.GetScalar<string>() == "new" &&
            priorPayload.Members["Child"].Members["Name"].GetScalar<string>() == " old ", "外部模型快照和既有结果均冻结");
        Require(tracker.Compare(root, new ReviewMatrixRoot { Child = new ExternalEntity { Id = 1, Name = "new" } }).Changes.Count == 0,
            "同身份同值外部实体无变更");
        foreach (var set in new[] { false, true })
        {
            var oldValue = new ReviewNullableRoot { Value = set ? new ReviewNullableValue { Name = "nullable", Count = 3 } : null };
            var newValue = new ReviewNullableRoot { Value = set ? null : new ReviewNullableValue { Name = "nullable", Count = 3 } };
            var change = tracker.Compare(oldValue, newValue).Changes.Single();
            Require(change.Path.Value == "Value" && change.Kind == (set ? ChangeKind.Removed : ChangeKind.Added) &&
                (set ? change.NewValue : change.OldValue).Kind == ChangeValueKind.Null &&
                (set ? change.OldValue : change.NewValue).Members["Count"].GetScalar<int>() == 3, "可空结构体有值与空值双向转换");
        }
    }

    private static void VerifyBudgets()
    {
        ExpectFailure(() => new ChangeGraphTracker(new ChangeGraphOptions { MaxNodes = 1 }).Capture(
            new ReviewDag { Left = new ReviewDag() }), "Left", "节点数超过限制 1");
        ExpectFailure(() => new ChangeGraphTracker(new ChangeGraphOptions { MaxCollectionItems = 1 }).Capture(
            new ReviewBudgetRoot { Children = new() { new() { Name = "one" }, new() { Name = "two" } } }), "Children", "集合元素数超过限制 1");
        ExpectFailure(() => new ChangeGraphTracker(new ChangeGraphOptions { MaxDepth = 0 }).Capture(
            new ReviewDag { Left = new ReviewDag() }), "Left", "深度超过限制 0");
        static ReviewDag Dag(string text)
        {
            var node = new ReviewDag { Name = text };
            for (var i = 0; i < 10; i++) node = new ReviewDag { Left = node, Right = node };
            return node;
        }
        var limited = new ChangeGraphTracker(new ChangeGraphOptions { MaxTraversalSteps = 180 });
        ExpectFailure(() => limited.Compare<ReviewDag>(null, Dag("new")), "$payload", "遍历步数超过限制 180");
        // 比较先成功捕获，再在展开共享 DAG 的叶子差异时耗尽统一预算。
        ExpectFailure(() => limited.Compare(Dag("old"), Dag("new")),
            "Left.Left.Left.Left.Left.Left.Right.Left.Left", "遍历步数超过限制 180");
        Require(new ChangeGraphTracker().Compare(new ReviewDag(), new ReviewDag()).Changes.Count == 0,
            "预算失败后独立会话仍可正常比较");
    }

    private static void VerifyExternalAndDescriptions()
    {
        var oldRoot = new ReviewMatrixRoot { Batches = new() { ["main"] = new() { new() { Id = 5, Name = "old" } } } };
        var newRoot = new ReviewMatrixRoot { Batches = new() { ["main"] = new() { new() { Id = 5, Name = "new" } } } };
        var change = new ChangeGraphTracker().Compare(oldRoot, newRoot).Changes.Single();
        Require(change.Path.Value == "Batches{main}[5].Name" && change.Description == "外部名称" && change.Kind == ChangeKind.Modified &&
            change.OldValue.GetScalar<string>() == "old" && change.NewValue.GetScalar<string>() == "new", "跨程序集自治模型经过字典和列表调用");
        var before = new ReviewDescriptionEntity { Inherited = "old", Overridden = "old", Hidden = "old", Text = "old", Plain = "old" };
        var after = new ReviewDescriptionEntity { Inherited = "new", Overridden = "new", Hidden = "new", Text = "new", Plain = "new" };
        var descriptions = before.GetChanges(after).ToArray();
        Require(descriptions.Select(c => c.PropertyName).SequenceEqual(new[] { "Hidden", "Inherited", "Overridden", "Plain", "Text" }) &&
            descriptions.Select(c => c.Description).SequenceEqual(new[] { "隐藏说明", "继承说明", "基类虚属性", "Plain", ReviewDescriptionEntity.Label }) &&
            descriptions.All(c => c.OldValue == "\"old\"" && c.NewValue == "\"new\""),
            "外部继承、override、new、partial、类描述隔离与常量转义");
    }

    private static void VerifyRuntimeTypes()
    {
        var options = ChangeGraphOptions.Default
            .WithAdapter(new ChangeGraphModel<ReviewRuntimeA>("runtime-a", (w, v) => w.Scalar("Name", v.Name)))
            .WithAdapter(new ChangeGraphModel<ReviewRuntimeB>("runtime-b", (w, v) => w.Scalar("Name", v.Name)));
        var tracker = new ChangeGraphTracker(options);
        var change = tracker.Compare(new ReviewRuntimeRoot { Value = new ReviewRuntimeA { Name = "old" } },
            new ReviewRuntimeRoot { Value = new ReviewRuntimeB { Name = "new" } }).Changes.Single();
        Require(change.Path.Value == "Value" && change.Kind == ChangeKind.Replaced &&
            change.OldValue.Members["Name"].GetScalar<string>() == "old" && change.NewValue.Members["Name"].GetScalar<string>() == "new",
            "有限显式适配器的运行时类型替换");
        ExpectFailure(() => new ChangeGraphTracker().Capture(new ReviewRuntimeRoot { Value = new ReviewRuntimeA() }), "Value", "必须通过 WithAdapter");
    }

    private static void VerifyScalarsAndContainerBoundaries()
    {
        var tracker = new ChangeGraphTracker();
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var changes = tracker.Compare(new ReviewScalarRoot { Date = date, Number = 1m, State = DayOfWeek.Monday, Text = " A " },
            new ReviewScalarRoot { Date = date.AddTicks(1), Number = 2m, State = DayOfWeek.Tuesday, Text = "a" }).Changes;
        Require(changes.Select(c => c.Path.Value).SequenceEqual(new[] { "Date", "Number", "State", "Text" }) &&
            changes.All(c => c.Kind == ChangeKind.Modified) && changes[0].OldValue.GetScalar<DateTime>() == date &&
            changes[0].NewValue.GetScalar<DateTime>() == date.AddTicks(1) && changes[1].OldValue.GetScalar<decimal>() == 1m &&
            changes[1].NewValue.GetScalar<decimal>() == 2m && changes[2].OldValue.GetScalar<DayOfWeek>() == DayOfWeek.Monday &&
            changes[2].NewValue.GetScalar<DayOfWeek>() == DayOfWeek.Tuesday && changes[3].OldValue.GetScalar<string>() == " A " &&
            changes[3].NewValue.GetScalar<string>() == "a", "标量完整差异保留时间精度、枚举、decimal 和原始文本");
        var blank = tracker.Compare(new ReviewScalarRoot(), new ReviewScalarRoot { Text = "" }).Changes.Single();
        Require(blank.Path.Value == "Text" && blank.Kind == ChangeKind.Added && blank.OldValue.Kind == ChangeValueKind.Null &&
            blank.NewValue.GetScalar<string>() == "", "标量空值与空字符串严格区分");
        foreach (var reverse in new[] { false, true })
        {
            var shortValue = new ReviewPositionRoot { Items = new string?[] { "same", "same" } };
            var longValue = new ReviewPositionRoot { Items = new string?[] { "same", "same", "tail" } };
            var item = tracker.Compare(reverse ? longValue : shortValue, reverse ? shortValue : longValue).Changes.Single();
            Require(item.Path.Value == "Items[2]" && item.Kind == (reverse ? ChangeKind.Removed : ChangeKind.Added) &&
                (reverse ? item.OldValue : item.NewValue).GetScalar<string>() == "tail" &&
                (reverse ? item.NewValue : item.OldValue).Kind == ChangeValueKind.Missing, "位置集合重复标量的尾部增删");
        }
        var empty = tracker.Compare(new ReviewPositionRoot(), new ReviewPositionRoot { Items = Array.Empty<string>() }).Changes.Single();
        Require(empty.Path.Value == "Items" && empty.Kind == ChangeKind.Added && empty.OldValue.Kind == ChangeValueKind.Null &&
            empty.NewValue.Items.Count == 0, "空集合与 null 区分");
        var nestedNull = tracker.Compare(new ReviewPositionRoot { Items = new string?[] { null } },
            new ReviewPositionRoot { Items = new[] { "" } }).Changes.Single();
        Require(nestedNull.Path.Value == "Items[0]" && nestedNull.Kind == ChangeKind.Added && nestedNull.OldValue.Kind == ChangeValueKind.Null &&
            nestedNull.NewValue.GetScalar<string>() == "", "集合元素 null 与空文本区分");
    }

    private static void ExpectFailure(Action action, string path, string cause)
    {
        // 若调用意外返回（即使返回半结果），必须失败，不能把已采集片段当成功。
        try { action(); }
        catch (ChangeGraphCaptureException error)
        {
            Require(error.Path == path && error.InnerException is InvalidOperationException &&
                error.InnerException.Message.Contains(cause, StringComparison.Ordinal),
                $"失败边界 {cause} @ {path}（实际 {error.Path}）");
            return;
        }
        throw new InvalidOperationException("未抛出预期失败: " + cause);
    }

    private static void Require(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("原生矩阵失败: " + description);
        Console.WriteLine("PASS | " + description);
    }
}

public partial class ReviewMatrixRoot : Bing.Domain.Entities.DomainObjectBase<ReviewMatrixRoot>
{
    public ExternalEntity? Child { get; set; }
    public Dictionary<string, List<ExternalEntity>> Batches { get; set; } = new();
}
public partial class ReviewNullableRoot : Bing.Domain.Entities.DomainObjectBase<ReviewNullableRoot> { public ReviewNullableValue? Value { get; set; } }
public partial class ReviewDag : Bing.Domain.Entities.DomainObjectBase<ReviewDag> { public ReviewDag? Left { get; set; } public string Name { get; set; } = ""; public ReviewDag? Right { get; set; } }

[Description("类说明不覆盖字段")]
public partial class ReviewDescriptionEntity : ExternalLegacyBase
{
    public const string Label = "中文\"引号\\斜线\n换行\t制表";
    [Description("隐藏说明")] public new string Hidden { get; set; } = "";
    public override string Overridden { get; set; } = "";
    [Description(Label)] public string Text { get; set; } = "";
}
public partial class ReviewDescriptionEntity { public string Plain { get; set; } = ""; }
public interface IReviewRuntime { }
public class ReviewRuntimeA : IReviewRuntime { public string Name { get; set; } = ""; }
public class ReviewRuntimeB : IReviewRuntime { public string Name { get; set; } = ""; }
public partial class ReviewRuntimeRoot : Bing.Domain.Entities.DomainObjectBase<ReviewRuntimeRoot> { public IReviewRuntime? Value { get; set; } }
public partial class ReviewScalarRoot : Bing.Domain.Entities.DomainObjectBase<ReviewScalarRoot>
{
    public DateTime Date { get; set; }
    public decimal Number { get; set; }
    public DayOfWeek State { get; set; }
    public string? Text { get; set; }
}
public partial class ReviewPositionRoot : Bing.Domain.Entities.DomainObjectBase<ReviewPositionRoot> { public string?[]? Items { get; set; } }
