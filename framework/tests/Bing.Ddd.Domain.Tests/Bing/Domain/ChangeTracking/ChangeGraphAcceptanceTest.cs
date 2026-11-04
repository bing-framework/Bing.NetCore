using System;
using System.Collections.Generic;
using System.Linq;
using Bing.Domain.Entities;
using Xunit;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 使用真实生成模型，从公开入口验证完整结果集及快照载荷。
/// </summary>
public class ChangeGraphAcceptanceTest
{
    /// <summary>
    /// 验证子实体标识和空值转换产生不同变更种类及载荷。
    /// </summary>
    [Fact]
    public void ChildIdentityAndNullTransitions_HaveDistinctKindsAndPayloads()
    {
        var tracker = new ChangeGraphTracker();
        var before = new AcceptanceGraph { Child = new AcceptanceChild { Id = 1, Name = "old" } };
        var after = new AcceptanceGraph { Child = new AcceptanceChild { Id = 1, Name = "new" } };
        var changed = Assert.Single(tracker.Compare(before, after).Changes);
        AssertChange(changed, "Child.Name", ChangeKind.Modified, "old", "new");
        Assert.Empty(tracker.Compare(after, new AcceptanceGraph { Child = new AcceptanceChild { Id = 1, Name = "new" } }).Changes);
        after.Child.Id = 2;
        var replaced = Assert.Single(tracker.Compare(before, after).Changes);
        Assert.Equal("Child", replaced.Path.Value); Assert.Equal(ChangeKind.Replaced, replaced.Kind);
        Assert.Equal(1, replaced.OldValue.Members["Id"].GetScalar<int>());
        Assert.Equal(2, replaced.NewValue.Members["Id"].GetScalar<int>());
        var added = Assert.Single(tracker.Compare(new AcceptanceGraph(), before).Changes);
        Assert.Equal("Child", added.Path.Value); Assert.Equal(ChangeKind.Added, added.Kind);
        Assert.Equal(ChangeValueKind.Null, added.OldValue.Kind); Assert.Equal("old", added.NewValue.Members["Name"].Value);
        var removed = Assert.Single(tracker.Compare(before, new AcceptanceGraph()).Changes);
        Assert.Equal("Child", removed.Path.Value); Assert.Equal(ChangeKind.Removed, removed.Kind);
        Assert.Equal(ChangeValueKind.Null, removed.NewValue.Kind);
        Assert.Empty(tracker.Compare<AcceptanceGraph>(null, null).Changes);
        var rootAdded = Assert.Single(tracker.Compare<AcceptanceGraph>(null, new AcceptanceGraph()).Changes);
        Assert.Equal(ChangeKind.Added, rootAdded.Kind); Assert.Equal(ChangeValueKind.Null, rootAdded.OldValue.Kind);
        var rootRemoved = Assert.Single(tracker.Compare<AcceptanceGraph>(new AcceptanceGraph(), null).Changes);
        Assert.Equal(ChangeKind.Removed, rootRemoved.Kind); Assert.Equal(ChangeValueKind.Null, rootRemoved.NewValue.Kind);
    }

    /// <summary>
    /// 验证冻结快照与已有比较结果不受后续修改影响。
    /// </summary>
    [Fact]
    public void FrozenSnapshotAndPreviousResult_StayIndependentOfLaterMutations()
    {
        var source = new AcceptanceGraph
        {
            Child = new AcceptanceChild { Id = 1, Name = "old" },
            Children = new List<AcceptanceChild> { new AcceptanceChild { Id = 2, Name = "list-old" } },
            Map = new Dictionary<string, AcceptanceChild> { ["key"] = new AcceptanceChild { Id = 3, Name = "map-old" } }
        };
        var tracker = new ChangeGraphTracker();
        var snapshot = tracker.Capture(source);
        var initial = Assert.Single(tracker.Compare<AcceptanceGraph>(null, source).Changes).NewValue;
        source.Child.Name = "new"; source.Children[0].Name = "list-new"; source.Map["key"].Name = "map-new";
        var result = tracker.CompareSnapshot(snapshot, source);
        Assert.Equal(new[] { "Child.Name", "Children[2].Name", "Map{key}.Name" }, result.Changes.Select(x => x.Path.Value));
        Assert.Equal(new object[] { "old", "list-old", "map-old" }, result.Changes.Select(x => x.OldValue.Value));
        Assert.Equal(new object[] { "new", "list-new", "map-new" }, result.Changes.Select(x => x.NewValue.Value));
        source.Children.Clear(); source.Map.Clear(); source.Child.Name = "later";
        tracker.CompareSnapshot(snapshot, source);
        Assert.Equal("old", initial.Members["Child"].Members["Name"].Value);
        Assert.Equal("list-old", Assert.Single(initial.Members["Children"].Items).Value.Members["Name"].Value);
        Assert.Equal("map-old", Assert.Single(initial.Members["Map"].Items).Value.Members["Name"].Value);
        Assert.Equal(new object[] { "new", "list-new", "map-new" }, result.Changes.Select(x => x.NewValue.Value));
        var members = Assert.IsAssignableFrom<IDictionary<string, ChangeValue>>(initial.Members);
        Assert.Throws<NotSupportedException>(() => members.Add("injected", initial));
    }

    /// <summary>
    /// 验证标量值保留原始内容及类型。
    /// </summary>
    [Fact]
    public void ScalarValues_AreNotNormalizedAndKeepOriginalTypes()
    {
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = new ChangeGraphTracker().Compare(new AcceptanceGraph { Text = " A ", Date = date, State = AcceptanceState.Old, Number = 1 },
            new AcceptanceGraph { Text = "a", Date = date.AddTicks(1), State = AcceptanceState.New, Number = 2 });
        Assert.Equal(new[] { "Date", "Number", "State", "Text" }, result.Changes.Select(x => x.Path.Value));
        Assert.All(result.Changes, x => Assert.Equal(ChangeKind.Modified, x.Kind));
        Assert.Equal(date, result.Changes[0].OldValue.GetScalar<DateTime>());
        Assert.Equal(date.AddTicks(1), result.Changes[0].NewValue.GetScalar<DateTime>());
        Assert.Equal(1m, result.Changes[1].OldValue.GetScalar<decimal>());
        Assert.Equal(AcceptanceState.Old, result.Changes[2].OldValue.GetScalar<AcceptanceState>());
        Assert.Equal(" A ", result.Changes[3].OldValue.GetScalar<string>());
        Assert.Equal("a", result.Changes[3].NewValue.GetScalar<string>());
        var blank = Assert.Single(new ChangeGraphTracker().Compare(new AcceptanceGraph(), new AcceptanceGraph { Text = "" }).Changes);
        Assert.Equal(ChangeValueKind.Null, blank.OldValue.Kind); Assert.Equal("", blank.NewValue.Value);
    }

    /// <summary>
    /// 验证稳定标识集合在插入、删除和移动时保留元素身份。
    /// </summary>
    [Fact]
    public void StableEntityCollections_InsertDeleteAndMovesPreserveIdentity()
    {
        List<AcceptanceChild> Lines(params int[] ids) => ids.Select(id => new AcceptanceChild { Id = id, Name = "same" }).ToList();
        var before = new AcceptanceGraph { Children = Lines(1, 2, 3) };
        var after = new AcceptanceGraph { Children = Lines(4, 1, 2, 3, 5) };
        var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithTrackMoves());
        var inserted = tracker.Compare(before, after);
        Assert.Equal(new[] { "Children[4]", "Children[5]" }, inserted.Changes.Select(x => x.Path.Value));
        Assert.All(inserted.Changes, x => { Assert.Equal(ChangeKind.Added, x.Kind); Assert.Equal(ChangeValueKind.Missing, x.OldValue.Kind); });
        Assert.Equal(new int?[] { 0, 4 }, inserted.Changes.Select(x => x.NewIndex));
        var removed = tracker.Compare(after, before);
        Assert.Equal(new[] { "Children[4]", "Children[5]" }, removed.Changes.Select(x => x.Path.Value));
        Assert.All(removed.Changes, x => Assert.Equal(ChangeKind.Removed, x.Kind));
        var middle = new AcceptanceGraph { Children = Lines(1, 6, 2, 3) };
        var middleAdded = Assert.Single(tracker.Compare(before, middle).Changes);
        Assert.Equal("Children[6]", middleAdded.Path.Value); Assert.Equal(1, middleAdded.NewIndex); Assert.Equal(ChangeKind.Added, middleAdded.Kind);
        Assert.Equal(ChangeKind.Removed, Assert.Single(tracker.Compare(middle, before).Changes).Kind);
        after.Children = Lines(3, 1, 2); after.Children[0].Name = "changed";
        var moved = tracker.Compare(before, after);
        Assert.Equal(new[] { "Children[3]", "Children[3].Name" }, moved.Changes.Select(x => x.Path.Value));
        Assert.Equal(ChangeKind.Moved, moved.Changes[0].Kind);
        Assert.Equal(2, moved.Changes[0].OldIndex); Assert.Equal(0, moved.Changes[0].NewIndex);
        AssertChange(moved.Changes[1], "Children[3].Name", ChangeKind.Modified, "same", "changed");
        Assert.Equal(new[] { "Children[3].Name" }, new ChangeGraphTracker().Compare(before, after).Changes.Select(x => x.Path.Value));
        var tie = tracker.Compare(new AcceptanceGraph { Children = Lines(1, 2, 3, 4) }, new AcceptanceGraph { Children = Lines(2, 1, 4, 3) });
        Assert.Equal(new[] { "Children[2]", "Children[4]" }, tie.Changes.Select(x => x.Path.Value));
        Assert.All(tie.Changes, x => Assert.Equal(ChangeKind.Moved, x.Kind));
    }

    /// <summary>
    /// 验证键控集合拒绝无效键且字典允许零值键。
    /// </summary>
    [Fact]
    public void KeyedCollection_InvalidKeysFailAndDictionaryZeroIsAllowed()
    {
        var tracker = new ChangeGraphTracker();
        foreach (var values in new[]
        {
            new List<AcceptanceChild> { new AcceptanceChild { Id = 0 } },
            new List<AcceptanceChild> { new AcceptanceChild { Id = 1 }, new AcceptanceChild { Id = 1 } },
            new List<AcceptanceChild> { null }
        })
        {
            var error = Assert.Throws<ChangeGraphCaptureException>(() => tracker.Capture(new AcceptanceGraph { Children = values }));
            Assert.StartsWith("Children", error.Path);
        }
        var change = Assert.Single(tracker.Compare(new AcceptanceGraph { Numbers = new Dictionary<int, string> { [0] = "old" } },
            new AcceptanceGraph { Numbers = new Dictionary<int, string> { [0] = "new" } }).Changes);
        AssertChange(change, "Numbers{0}", ChangeKind.Modified, "old", "new");
    }

    /// <summary>
    /// 验证调用方身份/集合键覆盖入口已按 DEC-04 删除，实体身份由 IKey 决定。
    /// </summary>
    [Fact]
    public void CallerIdentityOverrides_AreRemovedAndEntityIdentityWins()
    {
        // DEC-04：父级不能为子实体另选键；旧覆盖入口必须不可用。
        Assert.Null(typeof(ChangeGraphOptions).GetMethod("WithIdentity"));
        Assert.Null(typeof(ChangeGraphOptions).GetMethod("WithCollectionKey"));

        var result = new ChangeGraphTracker().Compare(
            new AcceptanceGraph { Children = new List<AcceptanceChild> { new AcceptanceChild { Id = 1, Name = "key" } } },
            new AcceptanceGraph { Children = new List<AcceptanceChild> { new AcceptanceChild { Id = 2, Name = "key" } } });
        // 身份来自 AcceptanceChild.IKey<int>.Id，换身份是删除旧身份并新增新身份。
        Assert.Equal(new[] { "Children[1]:Removed", "Children[2]:Added" },
            result.Changes.Select(change => change.Path.Value + ":" + change.Kind));
    }

    /// <summary>
    /// 验证按位置比较的集合处理尾部、空值和重复标量。
    /// </summary>
    [Fact]
    public void PositionalCollection_HandlesTailNullEmptyAndDuplicateScalars()
    {
        var tracker = new ChangeGraphTracker();
        var change = Assert.Single(tracker.Compare(new AcceptanceGraph { Tags = new[] { "same", "same" } },
            new AcceptanceGraph { Tags = new[] { "same", "same", "tail" } }).Changes);
        Assert.Equal("Tags[2]", change.Path.Value); Assert.Equal(ChangeKind.Added, change.Kind);
        Assert.Equal(ChangeValueKind.Missing, change.OldValue.Kind); Assert.Equal("tail", change.NewValue.Value);
        var removed = Assert.Single(tracker.Compare(new AcceptanceGraph { Tags = new[] { "same", "same", "tail" } },
            new AcceptanceGraph { Tags = new[] { "same", "same" } }).Changes);
        Assert.Equal("Tags[2]", removed.Path.Value); Assert.Equal(ChangeKind.Removed, removed.Kind);
        Assert.Equal("tail", removed.OldValue.Value); Assert.Equal(ChangeValueKind.Missing, removed.NewValue.Kind);
        var empty = Assert.Single(tracker.Compare(new AcceptanceGraph(), new AcceptanceGraph { Tags = Array.Empty<string>() }).Changes);
        Assert.Equal(ChangeKind.Added, empty.Kind); Assert.Equal(ChangeValueKind.Null, empty.OldValue.Kind); Assert.Empty(empty.NewValue.Items);
        var nullable = Assert.Single(tracker.Compare(new AcceptanceGraph { Tags = new string[] { null } }, new AcceptanceGraph { Tags = new[] { "" } }).Changes);
        Assert.Equal(ChangeValueKind.Null, nullable.OldValue.Kind); Assert.Equal("", nullable.NewValue.Value);
    }

    /// <summary>
    /// 验证字典条目标识不会覆盖嵌套列表的元素标识。
    /// </summary>
    [Fact]
    public void DictionaryEntryIdentity_DoesNotOverrideNestedListIdentity()
    {
        var tracker = new ChangeGraphTracker();
        var before = new AcceptanceGraph { Groups = new Dictionary<string, List<AcceptanceChild>> { ["group"] = new List<AcceptanceChild> { new AcceptanceChild { Id = 1, Name = "old" } } } };
        var after = new AcceptanceGraph { Groups = new Dictionary<string, List<AcceptanceChild>> { ["group"] = new List<AcceptanceChild> { new AcceptanceChild { Id = 1, Name = "new" } } } };
        AssertChange(Assert.Single(tracker.Compare(before, after).Changes), "Groups{group}[1].Name", ChangeKind.Modified, "old", "new");
    }

    /// <summary>
    /// 验证旧子实体跟踪与生成的对象图保持各自契约。
    /// </summary>
    [Fact]
    public void ManualLegacyChildTrackingAndGeneratedGraph_KeepTheirOwnContracts()
    {
        var before = new LegacyContainer { Child = new LegacyNestedChild { Name = " Child OLD " }, Children = new List<LegacyNestedChild> { new LegacyNestedChild { Name = " List OLD " } } };
        var after = new LegacyContainer { Child = new LegacyNestedChild { Name = "Child NEW" }, Children = new List<LegacyNestedChild> { new LegacyNestedChild { Name = "List NEW" } } };
        var legacy = before.GetChanges(after).ToArray();
        Assert.Equal(new[] { "Name", "Name" }, legacy.Select(x => x.PropertyName));
        Assert.Equal(new[] { "\" Child OLD \"", "\" List OLD \"" }, legacy.Select(x => x.OldValue));
        Assert.Equal(new[] { "\"Child NEW\"", "\"List NEW\"" }, legacy.Select(x => x.NewValue));
        var graph = new ChangeGraphTracker().Compare(before, after);
        Assert.Equal(new[] { "Child.Name", "Children[0].Name" }, graph.Changes.Select(x => x.Path.Value));
        Assert.Equal(new object[] { " Child OLD ", " List OLD " }, graph.Changes.Select(x => x.OldValue.Value));
        Assert.Equal(new object[] { "Child NEW", "List NEW" }, graph.Changes.Select(x => x.NewValue.Value));
    }

    /// <summary>
    /// 验证多层交替容器保留完整的路径边界。
    /// </summary>
    [Fact]
    public void DeepAlternatingShapes_KeepAllContainerBoundaries()
    {
        AcceptanceGraph Build(string value) => new AcceptanceGraph
        {
            Branches = new List<AcceptanceBranch> { new AcceptanceBranch { Children = new List<AcceptanceChild> { new AcceptanceChild { Id = 2, Name = value } } } },
            Mixed = new Dictionary<string, List<Dictionary<string, AcceptanceChild>>>
            {
                ["outer"] = new List<Dictionary<string, AcceptanceChild>> { new Dictionary<string, AcceptanceChild> { ["inner"] = new AcceptanceChild { Id = 1, Name = value } } }
            }
        };
        var result = new ChangeGraphTracker().Compare(Build("old"), Build("new"));
        Assert.Equal(new[] { "Branches[0].Children[2].Name", "Mixed{outer}[0]{inner}.Name" }, result.Changes.Select(x => x.Path.Value));
        Assert.All(result.Changes, change => { Assert.Equal(ChangeKind.Modified, change.Kind); Assert.Equal("old", change.OldValue.Value); Assert.Equal("new", change.NewValue.Value); });
        Assert.Empty(new ChangeGraphTracker().Compare(Build("same"), Build("same")).Changes);
    }

    /// <summary>
    /// 验证键控集合跟踪成员增减及叶节点变更。
    /// </summary>
    [Fact]
    public void KeyedSet_TracksMembershipAndLeafChanges()
    {
        var before = new OrderGraph { LineSet = new HashSet<OrderLine> { new OrderLine { Id = 40, Amount = 1 }, new OrderLine { Id = 50, Amount = 2 } } };
        var after = new OrderGraph { LineSet = new HashSet<OrderLine> { new OrderLine { Id = 60, Amount = 3 }, new OrderLine { Id = 50, Amount = 4 } } };
        var result = new ChangeGraphTracker().Compare(before, after);
        Assert.Equal(new[] { "LineSet[40]", "LineSet[50].Amount", "LineSet[60]" }, result.Changes.Select(x => x.Path.Value));
        Assert.Equal(new[] { ChangeKind.Removed, ChangeKind.Modified, ChangeKind.Added }, result.Changes.Select(x => x.Kind));
        Assert.Equal(1m, result.Changes[0].OldValue.Members["Amount"].Value);
        Assert.Equal(2m, result.Changes[1].OldValue.Value); Assert.Equal(4m, result.Changes[1].NewValue.Value);
        Assert.Equal(3m, result.Changes[2].NewValue.Members["Amount"].Value);
    }

    /// <summary>
    /// 验证带用户键的审计聚合只忽略根及子实体的审计时间戳。
    /// </summary>
    [Fact]
    public void RealAuditedAggregateWithUserKey_IgnoresOnlyTimestampsAcrossRootAndChildren()
    {
        var date = new DateTime(2026, 1, 1);
        ActualAuditRoot Build(int offset) => new ActualAuditRoot(1)
        {
            CreationTime = date.AddDays(offset), LastModificationTime = date.AddDays(offset), DeletionTime = date.AddDays(offset),
            CreatorId = 10L + offset, LastModifierId = 20L + offset, DeleterId = 30L + offset,
            BusinessDate = date.AddDays(offset), Version = new[] { (byte)offset }, IsDeleted = offset != 0
        };
        var before = Build(0); var after = Build(1);
        var tracker = new ChangeGraphTracker();
        var expected = new[] { "BusinessDate", "CreatorId", "DeleterId", "LastModifierId" };
        var result = tracker.Compare(before, after);
        Assert.Equal(expected, result.Changes.Select(change => change.Path.Value));
        Assert.All(result.Changes, change => Assert.Equal(ChangeKind.Modified, change.Kind));
        Assert.Equal(10L, result.Changes[1].OldValue.GetScalar<long>());
        Assert.Equal(11L, result.Changes[1].NewValue.GetScalar<long>());
        var nested = tracker.Compare(new ActualAuditEnvelope { Child = before, Children = new List<ActualAuditRoot> { before } },
            new ActualAuditEnvelope { Child = after, Children = new List<ActualAuditRoot> { after } });
        Assert.Equal(expected.Select(path => "Child." + path).Concat(expected.Select(path => "Children[1]." + path)), nested.Changes.Select(change => change.Path.Value));
        var payload = Assert.Single(tracker.Compare<ActualAuditRoot>(null, after).Changes).NewValue;
        Assert.False(payload.Members.ContainsKey("CreationTime"));
        Assert.False(payload.Members.ContainsKey("LastModificationTime"));
        Assert.False(payload.Members.ContainsKey("DeletionTime"));
        Assert.False(payload.Members.ContainsKey("Version"));
        Assert.False(payload.Members.ContainsKey("IsDeleted"));
    }

    /// <summary>
    /// 验证基类型根及子节点分派到生成的具体派生模型。
    /// </summary>
    [Fact]
    public void ConcreteGeneratedDerivedModel_IsDispatchedForBaseTypedRootAndChild()
    {
        var tracker = new ChangeGraphTracker();
        var before = new PolymorphicBase { Name = "old" };
        var after = new PolymorphicChild { Name = "new", Extra = "derived" };
        var root = Assert.Single(tracker.Compare<PolymorphicBase>(before, after).Changes);
        Assert.Equal("", root.Path.Value); Assert.Equal(ChangeKind.Replaced, root.Kind);
        Assert.Equal("derived", root.NewValue.Members["Extra"].Value);
        var child = Assert.Single(tracker.Compare(new PolymorphicHolder { Child = before }, new PolymorphicHolder { Child = after }).Changes);
        Assert.Equal("Child", child.Path.Value); Assert.Equal(ChangeKind.Replaced, child.Kind);
        Assert.Equal("old", child.OldValue.Members["Name"].Value);
        Assert.Equal("new", child.NewValue.Members["Name"].Value);
        Assert.Equal("derived", child.NewValue.Members["Extra"].Value);
    }

    /// <summary>
    /// 验证生成的交错数组与列表形状均可编译并执行比较。
    /// </summary>
    [Fact]
    public void GeneratedJaggedArraysAndLists_CompileAndExecuteEveryShape()
    {
        AcceptanceGraph Build(string value) => new AcceptanceGraph
        {
            Jagged = new[] { new[] { new AcceptanceChild { Id = 1, Name = value } } },
            ArrayOfLists = new[] { new List<AcceptanceChild> { new AcceptanceChild { Id = 2, Name = value } } },
            ListOfArrays = new List<AcceptanceChild[]> { new[] { new AcceptanceChild { Id = 3, Name = value } } }
        };
        var tracker = new ChangeGraphTracker();
        var before = Build("old"); var after = Build("new");
        var result = tracker.Compare(before, after);
        var paths = new[] { "ArrayOfLists[0][2].Name", "Jagged[0][1].Name", "ListOfArrays[0][3].Name" };
        Assert.Equal(paths, result.Changes.Select(change => change.Path.Value));
        Assert.All(result.Changes, change => { Assert.Equal(ChangeKind.Modified, change.Kind); Assert.Equal("old", change.OldValue.Value); Assert.Equal("new", change.NewValue.Value); });
        var snapshot = tracker.Capture(before);
        before.Jagged[0][0].Name = "snapshot-new";
        before.ArrayOfLists[0][0].Name = "snapshot-new";
        before.ListOfArrays[0][0].Name = "snapshot-new";
        var frozen = tracker.CompareSnapshot(snapshot, before);
        Assert.Equal(paths, frozen.Changes.Select(change => change.Path.Value));
        Assert.All(frozen.Changes, change => { Assert.Equal("old", change.OldValue.Value); Assert.Equal("snapshot-new", change.NewValue.Value); });
    }

    /// <summary>
    /// 验证审计创建时间契约不隐藏同名业务日期。
    /// </summary>
    [Fact]
    public void CreationAuditContract_DoesNotHideSameNamedBusinessDates()
    {
        var before = new BusinessTimestampGraph { CreationTime = new DateTime(2026, 1, 1), LastModificationTime = new DateTime(2026, 1, 1), DeletionTime = new DateTime(2026, 1, 1) };
        var after = new BusinessTimestampGraph { CreationTime = new DateTime(2026, 1, 2), LastModificationTime = new DateTime(2026, 1, 2), DeletionTime = new DateTime(2026, 1, 2) };
        var result = new ChangeGraphTracker().Compare(before, after);
        Assert.Equal(new[] { "DeletionTime", "LastModificationTime" }, result.Changes.Select(change => change.Path.Value));
        Assert.All(result.Changes, change =>
        {
            Assert.Equal(ChangeKind.Modified, change.Kind);
            Assert.Equal(new DateTime(2026, 1, 1), change.OldValue.GetScalar<DateTime>());
            Assert.Equal(new DateTime(2026, 1, 2), change.NewValue.GetScalar<DateTime>());
        });
    }

    /// <summary>
    /// 验证变更的路径、种类及前后值。
    /// </summary>
    /// <param name="change">待校验的变更条目。</param>
    /// <param name="path">待校验的对象或树路径。</param>
    /// <param name="kind">预期的变更种类。</param>
    /// <param name="oldValue">预期的旧值。</param>
    /// <param name="newValue">预期的新值。</param>
    private static void AssertChange(ChangeEntry change, string path, ChangeKind kind, object oldValue, object newValue)
    {
        Assert.Equal(path, change.Path.Value); Assert.Equal(kind, change.Kind);
        Assert.Equal(oldValue, change.OldValue.Value); Assert.Equal(newValue, change.NewValue.Value);
        var lastSeparator = path.LastIndexOf('.');
        var expectedDescription = lastSeparator >= 0 ? path[(lastSeparator + 1)..] : path;
        var collectionMarker = expectedDescription.IndexOfAny(new[] { '[', '{' });
        if (collectionMarker >= 0)
        {
            expectedDescription = expectedDescription[..collectionMarker];
        }
        Assert.Equal(expectedDescription, change.Description);
    }
}

/// <summary>
/// 覆盖嵌套集合与标量的对象图验收模型。
/// </summary>
public partial class AcceptanceGraph : DomainObjectBase<AcceptanceGraph>
{
    /// <summary>
    /// 获取或设置子节点。
    /// </summary>
    public AcceptanceChild Child { get; set; }
    /// <summary>
    /// 获取或设置子节点集合。
    /// </summary>
    public List<AcceptanceChild> Children { get; set; }
    /// <summary>
    /// 获取或设置按文本键索引的子节点字典。
    /// </summary>
    public Dictionary<string, AcceptanceChild> Map { get; set; }
    /// <summary>
    /// 获取或设置嵌套节点分组。
    /// </summary>
    public Dictionary<string, List<AcceptanceChild>> Groups { get; set; }
    /// <summary>
    /// 获取或设置以整数为键的文本字典。
    /// </summary>
    public Dictionary<int, string> Numbers { get; set; }
    /// <summary>
    /// 获取或设置分支集合。
    /// </summary>
    public List<AcceptanceBranch> Branches { get; set; }
    /// <summary>
    /// 获取或设置包含字典与序列的嵌套节点集合。
    /// </summary>
    public Dictionary<string, List<Dictionary<string, AcceptanceChild>>> Mixed { get; set; }
    /// <summary>
    /// 获取或设置节点交错数组。
    /// </summary>
    public AcceptanceChild[][] Jagged { get; set; }
    /// <summary>
    /// 获取或设置节点列表组成的数组。
    /// </summary>
    public List<AcceptanceChild>[] ArrayOfLists { get; set; }
    /// <summary>
    /// 获取或设置节点数组组成的列表。
    /// </summary>
    public List<AcceptanceChild[]> ListOfArrays { get; set; }
    /// <summary>
    /// 获取或设置标签序列。
    /// </summary>
    public string[] Tags { get; set; }
    /// <summary>
    /// 获取或设置文本值。
    /// </summary>
    public string Text { get; set; }
    /// <summary>
    /// 获取或设置日期值。
    /// </summary>
    public DateTime Date { get; set; }
    /// <summary>
    /// 获取或设置数值。
    /// </summary>
    public decimal Number { get; set; }
    /// <summary>
    /// 获取或设置验收状态。
    /// </summary>
    public AcceptanceState State { get; set; }
}

/// <summary>
/// 标识变更图验收测试中用于对比的状态版本。
/// </summary>
public enum AcceptanceState
{
    /// <summary>
    /// 变更前的原始状态。
    /// </summary>
    Old,
    /// <summary>
    /// 变更后的目标状态。
    /// </summary>
    New
}
/// <summary>
/// 对象图验收使用的带键子节点。
/// </summary>
public class AcceptanceChild : IKey<int>
{
    /// <inheritdoc />
    public int Id { get; set; }
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; }
}
/// <summary>
/// 包含子节点集合的对象图分支。
/// </summary>
public class AcceptanceBranch {
    /// <summary>
    /// 获取或设置子节点集合。
    /// </summary>
    public List<AcceptanceChild> Children { get; set; } }

/// <summary>
/// 验证旧领域基类兼容性的对象图容器。
/// </summary>
public partial class LegacyContainer : DomainObjectBase<LegacyContainer>
{
    /// <summary>
    /// 获取或设置子节点。
    /// </summary>
    public LegacyNestedChild Child { get; set; }
    /// <summary>
    /// 获取或设置子节点集合。
    /// </summary>
    public List<LegacyNestedChild> Children { get; set; }
    /// <inheritdoc />
    protected override void AddChanges(LegacyContainer other)
    {
        AddChange(Child, other.Child);
        AddChange(Children, other.Children);
    }
}
/// <summary>
/// 继承旧领域基类的嵌套子节点。
/// </summary>
public partial class LegacyNestedChild : DomainObjectBase<LegacyNestedChild>
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    [System.ComponentModel.Description("name")]
    public string Name { get; set; }
    /// <inheritdoc />
    protected override void AddChanges(LegacyNestedChild other) => AddChange(value => value.Name, other.Name);
}

/// <summary>
/// 验证审计字段忽略规则的真实审计聚合根。
/// </summary>
public partial class ActualAuditRoot : Bing.Domain.Entities.Auditing.FullAuditedAggregateRootWithUserKey<ActualAuditRoot, int, long>
{
    /// <summary>
    /// 初始化一个 <see cref="ActualAuditRoot" /> 类型的实例。
    /// </summary>
    /// <param name="id">对象的标识。</param>
    public ActualAuditRoot(int id) : base(id) { }
    /// <summary>
    /// 获取或设置业务日期。
    /// </summary>
    public DateTime BusinessDate { get; set; }
}

/// <summary>
/// 包含真实审计聚合根的对象图容器。
/// </summary>
public partial class ActualAuditEnvelope : DomainObjectBase<ActualAuditEnvelope>
{
    /// <summary>
    /// 获取或设置子节点。
    /// </summary>
    public ActualAuditRoot Child { get; set; }
    /// <summary>
    /// 获取或设置子节点集合。
    /// </summary>
    public List<ActualAuditRoot> Children { get; set; }
}

/// <summary>
/// 多态对象图测试的节点基类。
/// </summary>
public partial class PolymorphicBase : DomainObjectBase<PolymorphicBase>
{
    /// <summary>
    /// 获取或设置名称。
    /// </summary>
    public string Name { get; set; } }
/// <summary>
/// 包含额外成员的多态子节点。
/// </summary>
public partial class PolymorphicChild : PolymorphicBase {
    /// <summary>
    /// 获取或设置派生节点的附加值。
    /// </summary>
    public string Extra { get; set; } }
/// <summary>
/// 通过基类引用保存多态节点的对象图容器。
/// </summary>
public partial class PolymorphicHolder : DomainObjectBase<PolymorphicHolder>
{
    /// <summary>
    /// 获取或设置通过基类引用保存的子节点。
    /// </summary>
    public PolymorphicBase Child { get; set; } }

/// <summary>
/// 区分审计时间与业务时间的对象图模型。
/// </summary>
public partial class BusinessTimestampGraph : DomainObjectBase<BusinessTimestampGraph>, Bing.Auditing.IHasCreationTime
{
    /// <inheritdoc />
    public DateTime? CreationTime { get; set; }
    /// <summary>
    /// 获取或设置业务修改时间。
    /// </summary>
    public DateTime? LastModificationTime { get; set; }
    /// <summary>
    /// 获取或设置业务删除时间。
    /// </summary>
    public DateTime? DeletionTime { get; set; }
}
