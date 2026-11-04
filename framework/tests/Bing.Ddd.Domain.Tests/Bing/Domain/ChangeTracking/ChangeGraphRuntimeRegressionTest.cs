using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Bing.Domain.ChangeTracking;

/// <summary>
/// 运行时审计问题的行为回归，不依赖生成器输出文本。
/// </summary>
public class ChangeGraphRuntimeRegressionTest
{
    /// <summary>
    /// 验证共享对象使用不同集合标识时保留未变标识。
    /// </summary>
    [Fact]
    public void SharedObject_DifferentCollectionIdentity_DoesNotReplaceUnchangedIdentity()
    {
        var before = new Item { Id = 1, ClientKey = 2, Name = "old" };
        var after = new Item { Id = 1, ClientKey = 3, Name = "new" };
        var tracker = new ChangeGraphTracker();
        var result = tracker.Compare(new Root { Left = new[] { before }, Right = new[] { before } },
            new Root { Left = new[] { after }, Right = new[] { after } });
        Assert.Equal(new[] { "Left[1].ClientKey:Modified", "Left[1].Name:Modified", "Right[2]:Removed", "Right[3]:Added" },
            result.Changes.Select(x => x.Path.Value + ":" + x.Kind));
        Assert.Equal("old", result.Changes[1].OldValue.GetScalar<string>());
        Assert.Equal("new", result.Changes[1].NewValue.GetScalar<string>());
    }

    /// <summary>
    /// 验证共享容器在一次快照捕获中只枚举一次。
    /// </summary>
    [Fact]
    public void SharedContainer_IsEnumeratedOncePerCapture()
    {
        var values = new Once<Item>(new[] { new Item { Id = 1, ClientKey = 2 } });
        new ChangeGraphTracker().Capture(new Root { Left = values, Right = values });
        Assert.Equal(1, values.Enumerations);
    }

    /// <summary>
    /// 验证字典捕获遵循集合元素数量限制。
    /// </summary>
    /// <param name="objects">是否测试对象字典；否则测试标量字典。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dictionary_RespectsCollectionLimit(bool objects)
    {
        var root = new Root();
        if (objects)
            root.ObjectMap = new Dictionary<string, Item> { ["a"] = new Item(), ["b"] = new Item() };
        else
            root.Map = new Dictionary<string, string> { ["a"] = "a", ["b"] = "b" };
        var error = Assert.Throws<ChangeGraphCaptureException>(() => new ChangeGraphTracker(new ChangeGraphOptions { MaxCollectionItems = 1 }).Capture(root));
        Assert.Equal(objects ? "ObjectMap" : "Map", error.Path);
        Assert.Contains("集合元素数超过限制 1", error.InnerException.Message);
    }

    /// <summary>
    /// 验证不支持的查询及异步可枚举在枚举前被拒绝。
    /// </summary>
    [Fact]
    public void UnsupportedQueryableAndAsyncEnumerable_AreRejectedBeforeEnumeration()
    {
        var queryableError = Assert.Throws<ChangeGraphCaptureException>(() => new ChangeGraphTracker()
            .Capture(new Root { Left = new[] { new Item { Id = 1 } }.AsQueryable() }));
        Assert.Equal("Left", queryableError.Path);
        Assert.Contains("IQueryable", queryableError.InnerException.Message);

        var asyncEnumerableError = Assert.Throws<ChangeGraphCaptureException>(() => new ChangeGraphTracker()
            .Capture(new Root { Left = new DualEnumerable<Item>(new[] { new Item { Id = 1 } }) }));
        Assert.Equal("Left", asyncEnumerableError.Path);
        Assert.Contains("异步流", asyncEnumerableError.InnerException.Message);
    }

    /// <summary>
    /// 验证嵌套容器捕获遵循深度限制。
    /// </summary>
    [Fact]
    public void NestedContainer_RespectsDepthLimit()
    {
        var error = Assert.Throws<ChangeGraphCaptureException>(() => new ChangeGraphTracker(new ChangeGraphOptions { MaxDepth = 1 })
            .Capture(new Root { Nested = new[] { new[] { new Item() } } }));
        Assert.Equal("Nested[0]", error.Path);
        Assert.Contains("深度超过限制 1", error.InnerException.Message);
    }

    /// <summary>
    /// 验证节点和枚举失败保留精确路径及原始原因。
    /// </summary>
    [Fact]
    public void NodeAndEnumerationFailures_KeepPrecisePathAndOriginalCause()
    {
        var nodeError = Assert.Throws<ChangeGraphCaptureException>(() => new ChangeGraphTracker(new ChangeGraphOptions { MaxNodes = 1 })
            .Capture(new Dag { Left = new Dag() }));
        Assert.Equal("Left", nodeError.Path);
        Assert.Contains("节点数超过限制 1", nodeError.InnerException.Message);
        IEnumerable<Item> FailsDuringEnumeration()
        {
            yield return new Item { Id = 1 };
            throw new InvalidOperationException("enumeration failed");
        }
        var enumerationError = Assert.Throws<ChangeGraphCaptureException>(() => new ChangeGraphTracker().Capture(new Root { Left = FailsDuringEnumeration() }));
        Assert.Equal("Left", enumerationError.Path);
        Assert.IsType<InvalidOperationException>(enumerationError.InnerException);
        Assert.Equal("enumeration failed", enumerationError.InnerException.Message);
    }

    /// <summary>
    /// 验证共享有向无环图的差异及载荷受遍历预算限制。
    /// </summary>
    [Fact]
    public void DiffAndPayload_RespectTraversalBudgetForSharedDag()
    {
        Dag Build(string name)
        {
            var node = new Dag { Name = name };
            for (var i = 0; i < 10; i++) node = new Dag { Left = node, Right = node };
            return node;
        }
        var tracker = new ChangeGraphTracker(new ChangeGraphOptions { MaxTraversalSteps = 180 });
        var diffError = Assert.Throws<ChangeGraphCaptureException>(() => tracker.Compare(Build("old"), Build("new")));
        var payloadError = Assert.Throws<ChangeGraphCaptureException>(() => tracker.Compare<Dag>(null, Build("new")));
        Assert.Contains("遍历步数超过限制 180", diffError.InnerException.Message);
        Assert.Contains("遍历步数超过限制 180", payloadError.InnerException.Message);
        Assert.Equal("$payload", payloadError.Path);
    }

    /// <summary>
    /// 验证比较的两次捕获及差异共用总遍历预算。
    /// </summary>
    [Fact]
    public void Compare_UsesOneTotalBudgetAcrossBothCapturesAndDiff()
    {
        var tracker = new ChangeGraphTracker(new ChangeGraphOptions { MaxTraversalSteps = 10 });
        tracker.Capture(new Root());
        var error = Assert.Throws<ChangeGraphCaptureException>(() => tracker.Compare(new Root(), new Root()));
        Assert.Contains("遍历步数超过限制 10", error.InnerException.Message);
    }

    /// <summary>
    /// 验证未知派生类型被拒绝以避免静默丢失成员。
    /// </summary>
    [Fact]
    public void UnknownDerivedType_IsRejectedInsteadOfSilentlyLosingMembers()
    {
        Assert.ThrowsAny<Exception>(() => new ChangeGraphTracker().Capture(new Root
        {
            Left = new Item[] { new DerivedItem { Id = 1, Extra = "lost" } }
        }));
    }

    /// <summary>
    /// 验证嵌套集合在无实体身份时按元素模型索引配对（DEC-04 删除调用方身份覆盖）。
    /// </summary>
    [Fact]
    public void NestedCollection_ComparesByElementModelIndex()
    {
        var tracker = new ChangeGraphTracker();
        var result = tracker.Compare(new Root { Nested = new[] { new[] { new Item { ClientKey = 2, Name = "old" } } } },
            new Root { Nested = new[] { new[] { new Item { ClientKey = 2, Name = "new" } } } });
        var change = Assert.Single(result.Changes);
        Assert.Equal("Nested[0][0].Name", change.Path.Value);
        Assert.Equal(ChangeKind.Modified, change.Kind);
        Assert.Equal("old", change.OldValue.GetScalar<string>());
        Assert.Equal("new", change.NewValue.GetScalar<string>());
    }

    /// <summary>
    /// 验证可变字典键在可能与快照共享前被拒绝。
    /// </summary>
    [Fact]
    public void MutableDictionaryKey_IsRejectedBeforeSnapshotCanAliasIt()
    {
        var root = new MutableKeyRoot { Values = new Dictionary<Item, string> { [new Item()] = "value" } };
        Assert.ThrowsAny<Exception>(() => new ChangeGraphTracker().Capture(root));
    }

    /// <summary>
    /// 验证共享对象使用不同键时不会复用首个节点标识。
    /// </summary>
    [Fact]
    public void SharedObject_DifferentKeys_DoNotReuseFirstNodeIdentity()
    {
        var oldItem = new Item { Id = 1, ClientKey = 2, Name = "old" };
        var newItem = new Item { Id = 3, ClientKey = 2, Name = "new" };
        var beforeValues = new[] { oldItem }; var afterValues = new[] { newItem };
        var result = new ChangeGraphTracker().Compare(new Root { Left = beforeValues, Right = beforeValues },
            new Root { Left = afterValues, Right = afterValues });
        Assert.Equal(new[] { "Left[1]:Removed", "Left[3]:Added", "Right[2].Id:Modified", "Right[2].Name:Modified" },
            result.Changes.Select(x => x.Path.Value + ":" + x.Kind));
    }

    /// <summary>
    /// 验证相同值的共享及重复对象拓扑不产生变更。
    /// </summary>
    [Fact]
    public void SharedAndDuplicatedTopology_WithSameValuesDoesNotProduceChanges()
    {
        var item = new Item { Id = 1, ClientKey = 2, Name = "same" };
        var before = new Root { Left = new[] { item }, Right = new[] { item } };
        var after = new Root { Left = new[] { new Item { Id = 1, ClientKey = 2, Name = "same" } }, Right = new[] { new Item { Id = 1, ClientKey = 2, Name = "same" } } };
        Assert.Empty(new ChangeGraphTracker().Compare(before, after).Changes);
    }

    /// <summary>
    /// 验证相互循环引用报告回边前的全部叶节点变更。
    /// </summary>
    [Fact]
    public void MutualCycle_ReportsAllLeavesBeforeTheBackEdge()
    {
        Dag Build(string a, string b)
        {
            var root = new Dag { Name = a, Left = new Dag { Name = b } };
            root.Left.Right = root;
            return root;
        }
        var result = new ChangeGraphTracker().Compare(Build("old-a", "old-b"), Build("new-a", "new-b"));
        Assert.Equal(new[] { "Left.Name", "Name" }, result.Changes.Select(change => change.Path.Value));
        Assert.Equal(new object[] { "old-b", "old-a" }, result.Changes.Select(change => change.OldValue.Value));
        Assert.Equal(new object[] { "new-b", "new-a" }, result.Changes.Select(change => change.NewValue.Value));
        Assert.All(result.Changes, change => Assert.Equal(ChangeKind.Modified, change.Kind));
    }

    /// <summary>
    /// 验证字典比较跟踪增删改并忽略枚举顺序。
    /// </summary>
    [Fact]
    public void Dictionary_TracksAddRemoveModifyAndIgnoresEnumerationOrder()
    {
        var before = new Root { Map = new Dictionary<string, string> { ["a"] = "old", ["b"] = "removed" } };
        var after = new Root { Map = new Dictionary<string, string> { ["c"] = "added", ["a"] = "new" } };
        var result = new ChangeGraphTracker().Compare(before, after);
        Assert.Equal(new[] { "Map{a}", "Map{b}", "Map{c}" }, result.Changes.Select(change => change.Path.Value));
        Assert.Equal(new[] { ChangeKind.Modified, ChangeKind.Removed, ChangeKind.Added }, result.Changes.Select(change => change.Kind));
        Assert.Equal("old", result.Changes[0].OldValue.Value); Assert.Equal("new", result.Changes[0].NewValue.Value);
        Assert.Equal("removed", result.Changes[1].OldValue.Value); Assert.Equal(ChangeValueKind.Missing, result.Changes[1].NewValue.Kind);
        Assert.Equal(ChangeValueKind.Missing, result.Changes[2].OldValue.Kind); Assert.Equal("added", result.Changes[2].NewValue.Value);
        Assert.Empty(new ChangeGraphTracker().Compare(before, new Root { Map = new Dictionary<string, string> { ["b"] = "removed", ["a"] = "old" } }).Changes);
    }

    /// <summary>
    /// 验证路径保留类型化片段并按数值排序索引。
    /// </summary>
    [Fact]
    public void Paths_KeepTypedSegmentsAndSortIndexesNumerically()
    {
        var oldValues = Enumerable.Repeat("old", 12).ToArray();
        var newValues = (string[])oldValues.Clone(); newValues[2] = "two"; newValues[10] = "ten";
        var result = new ChangeGraphTracker().Compare(new ScalarRoot { Values = oldValues }, new ScalarRoot { Values = newValues });
        Assert.Equal(new[] { "Values[2]", "Values[10]" }, result.Changes.Select(x => x.Path.Value));
        var path = result.Changes[0].Path;
        Assert.Equal(2, path.Segments.Count);
        Assert.Equal(ChangePathSegmentKind.Member, path.Segments[0].Kind);
        Assert.Equal(ChangePathSegmentKind.Index, path.Segments[1].Kind);
        Assert.Equal(2, path.Segments[1].Index);
    }

    /// <summary>
    /// 验证字典路径转义特殊键并保留键类型。
    /// </summary>
    [Fact]
    public void DictionaryPaths_EscapeSpecialKeysAndKeepKeyType()
    {
        var map = new Dictionary<string, string> { ["a}.b[\"x"] = "new", ["1"] = "new" };
        var result = new ChangeGraphTracker().Compare(new Root { Map = new Dictionary<string, string>() }, new Root { Map = map });
        Assert.Equal(2, result.Changes.Count);
        Assert.Equal(2, result.Changes.Select(x => x.Path.Value).Distinct().Count());
        foreach (var change in result.Changes)
        {
            Assert.Equal(ChangePathSegmentKind.DictionaryKey, change.Path.Segments[1].Kind);
            Assert.IsType<string>(change.Path.Segments[1].Key);
            Assert.Contains("{\"", change.Path.Value);
            Assert.Equal(ChangeKind.Added, change.Kind);
            Assert.Equal(ChangeValueKind.Missing, change.OldValue.Kind);
            Assert.Equal("new", change.NewValue.GetScalar<string>());
        }
    }

    /// <summary>
    /// 验证显示相同的异类型键及亚秒日期键不发生冲突。
    /// </summary>
    [Fact]
    public void TypedKeys_WithEqualDisplayOrSubsecondDates_DoNotCollide()
    {
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var keys = new object[] { 1, "1", 1L, date, date.AddTicks(1), ']', "]", ChangeGraphKey.Create("a,b", "c"), ChangeGraphKey.Create("a", "b,c") };
        var root = new MixedKeyRoot { Values = keys.ToDictionary(key => key, key => "new") };
        var result = new ChangeGraphTracker().Compare(new MixedKeyRoot { Values = new Dictionary<object, string>() }, root);
        Assert.Equal(keys.Length, result.Changes.Count);
        Assert.Equal(keys.Length, result.Changes.Select(x => x.Path.Value).Distinct().Count());
        Assert.Equal(keys.Length, result.Changes.Select(x => x.Path).Distinct().Count());
        Assert.All(result.Changes, change => Assert.Equal(ChangePathSegmentKind.DictionaryKey, change.Path.Segments[1].Kind));
    }

    /// <summary>
    /// 验证集合键比较器与标量比较器独立生效。
    /// </summary>
    [Fact]
    public void KeyComparer_IsSeparateFromScalarComparer()
    {
        var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithKeyComparer(StringComparer.OrdinalIgnoreCase));
        var change = Assert.Single(tracker.Compare(new Root { Map = new Dictionary<string, string> { ["KEY"] = "old" } },
            new Root { Map = new Dictionary<string, string> { ["key"] = "new" } }).Changes);
        Assert.Equal(ChangeKind.Modified, change.Kind);
        Assert.Equal("Map{key}", change.Path.Value);
        Assert.Equal("old", change.OldValue.GetScalar<string>());
        Assert.Equal("new", change.NewValue.GetScalar<string>());
    }

    /// <summary>
    /// 验证标量集合使用标量相等及哈希规则。
    /// </summary>
    /// <param name="keyComparer">是否额外配置键比较器。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScalarSet_UsesScalarEqualityAndHashing(bool keyComparer)
    {
        var options = keyComparer ? ChangeGraphOptions.Default.WithKeyComparer(StringComparer.OrdinalIgnoreCase) :
            ChangeGraphOptions.Default.WithComparer(StringComparer.OrdinalIgnoreCase);
        var tracker = new ChangeGraphTracker(options);
        Assert.Empty(tracker.Compare(new ScalarRoot { Set = new HashSet<string> { "VALUE" } },
            new ScalarRoot { Set = new HashSet<string> { "value" } }).Changes);
    }

    /// <summary>
    /// 验证循环载荷的引用指向各自独立的节点定义。
    /// </summary>
    [Fact]
    public void CyclePayload_ReferencesDistinctNodeDefinitions()
    {
        var a = new Dag { Name = "a" }; var b = new Dag { Name = "b" }; a.Left = b; b.Right = a;
        var value = Assert.Single(new ChangeGraphTracker().Compare<Dag>(null, a).Changes).NewValue;
        var child = value.Members["Left"];
        Assert.NotNull(value.NodeId); Assert.NotNull(child.NodeId); Assert.NotEqual(value.NodeId, child.NodeId);
        Assert.Equal(value.NodeId, child.Members["Right"].ReferenceId);
        a.Name = "later-a"; b.Name = "later-b";
        Assert.Equal("a", value.Members["Name"].Value);
        Assert.Equal("b", child.Members["Name"].Value);
    }

    /// <summary>
    /// 验证移除的循环载荷被冻结且引用自身节点定义。
    /// </summary>
    [Fact]
    public void RemovedCyclePayload_IsFrozenAndReferencesItsOwnNodeDefinition()
    {
        var before = new Dag { Name = "old" };
        before.Left = before;

        var change = Assert.Single(new ChangeGraphTracker().Compare<Dag>(before, null).Changes);

        Assert.Equal(ChangeKind.Removed, change.Kind);
        Assert.Equal(ChangeValueKind.Object, change.OldValue.Kind);
        Assert.Equal(change.OldValue.NodeId, change.OldValue.Members["Left"].ReferenceId);
        before.Name = "mutated";
        Assert.Equal("old", change.OldValue.Members["Name"].Value);
    }

    /// <summary>
    /// 验证显式适配器无需反射即可捕获未知接口及根对象。
    /// </summary>
    [Fact]
    public void ExplicitAdapter_CapturesUnknownInterfaceAndRootWithoutReflection()
    {
        var adapter = new ChangeGraphModel<IExternal>("external", (w, value) => w.Scalar(nameof(IExternal.Value), value.Value));
        var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(adapter));
        IExternal before = new External { Value = "old" }, after = new External { Value = "new" };
        var change = Assert.Single(tracker.Compare(before, after).Changes);
        Assert.Equal("Value", change.Path.Value);
        Assert.Equal(ChangeKind.Modified, change.Kind);
        var nested = Assert.Single(tracker.Compare(new AdapterRoot { Value = before }, new AdapterRoot { Value = after }).Changes);
        Assert.Equal("Value.Value", nested.Path.Value);
        Assert.Equal("old", nested.OldValue.GetScalar<string>());
        Assert.Equal("new", nested.NewValue.GetScalar<string>());
        Assert.ThrowsAny<Exception>(() => new ChangeGraphTracker().Capture(new AdapterRoot { Value = before }));
    }

    /// <summary>
    /// 验证已注册派生模型保留派生成员。
    /// </summary>
    [Fact]
    public void RegisteredDerivedModel_PreservesDerivedMembers()
    {
        var adapter = new ChangeGraphModel<DerivedItem>("derived", (w, value) => w.Scalar(nameof(DerivedItem.Extra), value.Extra));
        var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(adapter));
        var change = Assert.Single(tracker.Compare(new Root { Left = new Item[] { new DerivedItem { Id = 1, Extra = "old" } } },
            new Root { Left = new Item[] { new DerivedItem { Id = 1, Extra = "new" } } }).Changes);
        Assert.Equal("Left[1].Extra", change.Path.Value);
        var replacement = Assert.Single(tracker.Compare(new Root { Left = new Item[] { new Item { Id = 1 } } },
            new Root { Left = new Item[] { new DerivedItem { Id = 1, Extra = "new" } } }).Changes);
        Assert.Equal("Left[1]", replacement.Path.Value);
        Assert.Equal(ChangeKind.Replaced, replacement.Kind);
        Assert.Equal("new", replacement.NewValue.Members["Extra"].Value);
        Assert.Equal("old", change.OldValue.GetScalar<string>());
        Assert.Equal("new", change.NewValue.GetScalar<string>());
    }

    /// <summary>
    /// 验证旧投影保留完整路径及确定的原始值。
    /// </summary>
    [Fact]
    public void LegacyProjection_PreservesFullPathAndDeterministicOriginalValues()
    {
        var result = new ChangeGraphTracker().Compare(new Root { Map = new Dictionary<string, string> { ["x"] = "old" } },
            new Root { Map = new Dictionary<string, string> { ["x"] = "new" } });
        var descriptor = Assert.Single(result.ToLegacyDescriptors());
        Assert.Equal("Map{x}", descriptor.PropertyName);
        Assert.Equal("\"old\"", descriptor.OldValue);
        Assert.Equal("\"new\"", descriptor.NewValue);
        Assert.Equal("Map", descriptor.Description);
        Assert.Equal("$", Assert.Single(new ChangeGraphTracker().Compare<Root>(null, new Root()).ToLegacyDescriptors()).PropertyName);
    }

    /// <summary>
    /// 验证不透明容器适配器终止自身循环并引用其定义。
    /// </summary>
    [Fact]
    public void OpaqueContainerAdapter_SelfCycleTerminatesAndReferencesItsDefinition()
    {
        var element = ChangeGraphModel<object>.CreateAdapterModel<object>("opaque");
        var model = ChangeGraphModel<List<object>>.CreateSequenceModel<List<object>, object>("opaque-list", value => value, () => element);
        var tracker = new ChangeGraphTracker(ChangeGraphOptions.Default.WithAdapter(model));
        var before = new List<object>(); before.Add(before);
        var after = new List<object>(); after.Add(after);
        Assert.Empty(tracker.Compare(before, after).Changes);
        var payload = Assert.Single(tracker.Compare<List<object>>(null, after).Changes).NewValue;
        Assert.Equal(ChangeValueKind.Collection, payload.Kind);
        Assert.NotNull(payload.NodeId);
        Assert.Equal(payload.NodeId, Assert.Single(payload.Items).Value.ReferenceId);
    }

    /// <summary>
    /// 验证同一跟踪器上的并行比较保持独立会话。
    /// </summary>
    [Fact]
    public void ParallelComparisons_OnSameTrackerKeepIndependentSessions()
    {
        var tracker = new ChangeGraphTracker();
        System.Threading.Tasks.Parallel.For(1, 33, id =>
        {
            var result = tracker.Compare(new Root { Left = new[] { new Item { Id = id, Name = "old" } } },
                new Root { Left = new[] { new Item { Id = id, Name = "new" } } });
            var change = Assert.Single(result.Changes);
            Assert.Equal("Left[" + id + "].Name", change.Path.Value);
            Assert.Equal("old", change.OldValue.GetScalar<string>());
            Assert.Equal("new", change.NewValue.GetScalar<string>());
        });
    }

    /// <summary>
    /// 验证生成的忽略规则裁剪子树且不读取、枚举或计算键。
    /// </summary>
    [Fact]
    public void GeneratedIgnore_PrunesLargeSubtreeWithoutGetterOrEnumerationOrKeyWork()
    {
        PrunedGraph.Reads = 0;
        PrunedGraph.Enumerated = 0;
        // DEC-04 删除调用方身份覆盖入口；忽略子树零访问由 getter/枚举计数直接断言。
        var tracker = new ChangeGraphTracker();
        var source = new PrunedGraph();
        var snapshot = tracker.Capture(source);
        Assert.Empty(tracker.Compare(new PrunedGraph(), source).Changes);
        Assert.Empty(tracker.CompareSnapshot(snapshot, source).Changes);
        var payload = Assert.Single(tracker.Compare<PrunedGraph>(null, source).Changes).NewValue;
        Assert.Empty(payload.Members);
        Assert.Equal(0, PrunedGraph.Reads);
        Assert.Equal(0, PrunedGraph.Enumerated);
        Assert.DoesNotContain("Ignored", Assert.Single(tracker.Compare<PrunedGraph>(null, source).ToLegacyDescriptors()).NewValue);
    }

    /// <summary>
    /// 验证可空复杂结构体快照不保留可变列表引用。
    /// </summary>
    [Fact]
    public void NullableComplexStruct_SnapshotDoesNotRetainMutableList()
    {
        var source = new NullableValueGraph { Value = new MutableValue { Names = new List<string> { "old" } } };
        var tracker = new ChangeGraphTracker();
        var snapshot = tracker.Capture(source);
        source.Value.Value.Names[0] = "new";
        var change = Assert.Single(tracker.CompareSnapshot(snapshot, source).Changes);
        Assert.Equal("Value.Names[0]", change.Path.Value);
        Assert.Equal("old", change.OldValue.GetScalar<string>());
        Assert.Equal("new", change.NewValue.GetScalar<string>());
        Assert.Empty(tracker.Compare(new NullableValueGraph(), new NullableValueGraph()).Changes);
    }

    /// <summary>
    /// 包含多个候选匹配键的对象图测试节点。
    /// </summary>

    public class Item
    {
        /// <summary>
        /// 获取或设置主键。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置业务匹配键。
        /// </summary>
        public int ClientKey { get; set; }
        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 保存条目类型的变更图模型。
        /// </summary>
        public static readonly ChangeGraphModel<Item> Model = new ChangeGraphModel<Item>("item", (w, v) =>
        {
            w.Scalar(nameof(Id), v.Id); w.Scalar(nameof(ClientKey), v.ClientKey); w.Scalar(nameof(Name), v.Name);
        }, members: new[] { nameof(Id), nameof(ClientKey), nameof(Name) });
    }
    /// <summary>
    /// 验证派生模型成员捕获的测试节点。
    /// </summary>
    public sealed class DerivedItem : Item {
        /// <summary>
        /// 获取或设置派生节点的附加值。
        /// </summary>
        public string Extra { get; set; } }
    /// <summary>
    /// 验证集合与字典捕获的对象图根节点。
    /// </summary>
    public sealed class Root : IChangeGraphNode
    {
        /// <summary>
        /// 获取或设置按主键匹配的左侧节点序列。
        /// </summary>
        public IEnumerable<Item> Left { get; set; }
        /// <summary>
        /// 获取或设置按业务键匹配的右侧节点序列。
        /// </summary>
        public IEnumerable<Item> Right { get; set; }
        /// <summary>
        /// 获取或设置标量值字典。
        /// </summary>
        public Dictionary<string, string> Map { get; set; }
        /// <summary>
        /// 获取或设置按键索引的对象字典。
        /// </summary>
        public Dictionary<string, Item> ObjectMap { get; set; }
        /// <summary>
        /// 获取或设置节点交错数组。
        /// </summary>
        public Item[][] Nested { get; set; }
        /// <summary>
        /// 保存根序列类型的变更图模型。
        /// </summary>
        private static readonly ChangeGraphModel<Item[]> ArrayModel = ChangeGraphModel<Root>.CreateSequenceModel<Item[], Item>("array", x => x, () => Item.Model);
        /// <summary>
        /// 保存根类型的变更图模型。
        /// </summary>
        private static readonly ChangeGraphModel<Root> Model = new ChangeGraphModel<Root>("root", (w, v) =>
        {
            w.Sequence(nameof(Left), v.Left, Item.Model, x => x.Id);
            w.Sequence(nameof(Right), v.Right, Item.Model, x => x.ClientKey);
            w.DictionaryScalar(nameof(Map), v.Map);
            w.Dictionary(nameof(ObjectMap), v.ObjectMap, Item.Model);
            w.Sequence(nameof(Nested), v.Nested, ArrayModel);
        }, members: new[] { nameof(Left), nameof(Right), nameof(Map), nameof(ObjectMap), nameof(Nested) },
            collectionMembers: new Dictionary<string, Type> { [nameof(Left)] = typeof(Item), [nameof(Right)] = typeof(Item), [nameof(Nested)] = typeof(Item[]) });
        /// <inheritdoc />
        public ChangeGraphModel ChangeGraphModel => Model;
    }
    /// <summary>
    /// 以可变对象为字典键的对象图根节点。
    /// </summary>
    public sealed class MutableKeyRoot : IChangeGraphNode
    {
        /// <summary>
        /// 获取或设置以可变节点为键的文本字典。
        /// </summary>
        public Dictionary<Item, string> Values { get; set; }
        /// <summary>
        /// 保存可变键根类型的变更图模型。
        /// </summary>
        private static readonly ChangeGraphModel<MutableKeyRoot> Model = new ChangeGraphModel<MutableKeyRoot>("mutable-key", (w, v) => w.DictionaryScalar(nameof(Values), v.Values));
        /// <inheritdoc />
        public ChangeGraphModel ChangeGraphModel => Model;
    }
    /// <summary>
    /// 以混合类型对象为字典键的对象图根节点。
    /// </summary>
    public sealed class MixedKeyRoot : IChangeGraphNode
    {
        /// <summary>
        /// 获取或设置以混合类型对象为键的文本字典。
        /// </summary>
        public Dictionary<object, string> Values { get; set; }
        /// <summary>
        /// 保存混合键根类型的变更图模型。
        /// </summary>
        private static readonly ChangeGraphModel<MixedKeyRoot> Model = new ChangeGraphModel<MixedKeyRoot>("mixed-key", (w, value) => w.DictionaryScalar(nameof(Values), value.Values));
        /// <inheritdoc />
        public ChangeGraphModel ChangeGraphModel => Model;
    }
    /// <summary>
    /// 用于共享引用与循环引用测试的对象图节点。
    /// </summary>
    public sealed class Dag : IChangeGraphNode
    {
        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 获取或设置左侧节点。
        /// </summary>
        public Dag Left { get; set; }
        /// <summary>
        /// 获取或设置右侧节点。
        /// </summary>
        public Dag Right { get; set; }
        /// <summary>
        /// 保存有向无环图类型的变更图模型。
        /// </summary>
        private static readonly ChangeGraphModel<Dag> Model = new ChangeGraphModel<Dag>("dag", (w, v) =>
        {
            w.Scalar(nameof(Name), v.Name); w.Object(nameof(Left), v.Left, Model); w.Object(nameof(Right), v.Right, Model);
        });
        /// <inheritdoc />
        public ChangeGraphModel ChangeGraphModel => Model;
    }
    /// <summary>
    /// 仅允许一次枚举的可枚举对象。
    /// </summary>
    /// <typeparam name="T">枚举序列中的元素类型。</typeparam>
    private sealed class Once<T> : IEnumerable<T>
    {
        /// <summary>
        /// 保存只能枚举一次的测试数据源。
        /// </summary>
        private readonly IEnumerable<T> _items;
        /// <summary>
        /// 初始化一个 <see cref="Once{T}" /> 类型的实例。
        /// </summary>
        /// <param name="items">当前实例处理或保存的项集合。</param>
        public Once(IEnumerable<T> items) => _items = items;
        /// <summary>
        /// 获取枚举序列的累计次数。
        /// </summary>
        public int Enumerations { get; private set; }
        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            if (++Enumerations > 1) throw new InvalidOperationException("Second enumeration");
            return _items.GetEnumerator();
        }
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 同时提供同步和异步枚举的测试集合。
    /// </summary>
    /// <typeparam name="T">枚举序列中的元素类型。</typeparam>
    private sealed class DualEnumerable<T> : IEnumerable<T>, IAsyncEnumerable<T>
    {
        /// <summary>
        /// 保存可重复枚举的测试数据源。
        /// </summary>
        private readonly IReadOnlyList<T> _items;

        /// <summary>
        /// 初始化一个 <see cref="DualEnumerable{T}" /> 类型的实例。
        /// </summary>
        /// <param name="items">当前实例处理或保存的项集合。</param>
        public DualEnumerable(IEnumerable<T> items) => _items = items.ToList();

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new Enumerator(_items.GetEnumerator());

        /// <summary>
        /// 仅允许一次异步枚举的异步枚举器。
        /// </summary>
        private sealed class Enumerator : IAsyncEnumerator<T>
        {
            /// <summary>
            /// 保存被包装的枚举器。
            /// </summary>
            private readonly IEnumerator<T> _inner;

            /// <summary>
            /// 初始化一个 <see cref="Enumerator" /> 类型的实例。
            /// </summary>
            /// <param name="inner">由当前包装器保留或转发的内部对象或异常。</param>
            public Enumerator(IEnumerator<T> inner) => _inner = inner;

            /// <inheritdoc />
            public T Current => _inner.Current;

            /// <inheritdoc />
            public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());

            /// <inheritdoc />
            public ValueTask DisposeAsync()
            {
                _inner.Dispose();
                return default;
            }
        }
    }
    /// <summary>
    /// 包含标量序列与集合的对象图根节点。
    /// </summary>
    public sealed class ScalarRoot : IChangeGraphNode
    {
        /// <summary>
        /// 获取或设置文本数组。
        /// </summary>
        public string[] Values { get; set; }
        /// <summary>
        /// 获取或设置标量集合。
        /// </summary>
        public HashSet<string> Set { get; set; }
        /// <summary>
        /// 保存标量集合根类型的变更图模型。
        /// </summary>
        private static readonly ChangeGraphModel<ScalarRoot> Model = new ChangeGraphModel<ScalarRoot>("scalar", (w, v) =>
        {
            w.SequenceScalar(nameof(Values), v.Values); w.SetScalar(nameof(Set), v.Set);
        });
        /// <inheritdoc />
        public ChangeGraphModel ChangeGraphModel => Model;
    }
    /// <summary>
    /// 提供外部变更模型测试契约。
    /// </summary>
    public interface IExternal { /// <summary>
    /// 获取测试模型的成员值。
    /// </summary>
    string Value { get; } }
    /// <summary>
    /// 通过接口适配器捕获的外部数据模型。
    /// </summary>
    public sealed class External : IExternal {
        /// <inheritdoc />
        public string Value { get; set; } }
    /// <summary>
    /// 包含外部接口数据的对象图根节点。
    /// </summary>
    public sealed class AdapterRoot : IChangeGraphNode
    {
        /// <summary>
        /// 获取或设置外部接口数据。
        /// </summary>
        public IExternal Value { get; set; }
        /// <summary>
        /// 保存适配器根类型的变更图模型。
        /// </summary>
        private static readonly ChangeGraphModel<AdapterRoot> Model = new ChangeGraphModel<AdapterRoot>("adapter-root", (w, v) => w.Adapted(nameof(Value), v.Value));
        /// <inheritdoc />
        public ChangeGraphModel ChangeGraphModel => Model;
    }
}

/// <summary>
/// 验证忽略分支无需读取或枚举的对象图模型。
/// </summary>
public partial class PrunedGraph : Bing.Domain.Entities.DomainObjectBase<PrunedGraph>
{
    /// <summary>
    /// 记录被裁剪图属性的读取次数。
    /// </summary>
    public static int Reads;
    /// <summary>
    /// 记录被裁剪图的枚举次数。
    /// </summary>
    public static int Enumerated;
    /// <summary>
    /// 获取应被忽略的节点序列。
    /// </summary>
    /// <remarks>读取与枚举分别递增计数，用于确认忽略分支未被访问。</remarks>
    [ChangeIgnore]
    public IEnumerable<ChangeGraphRuntimeRegressionTest.Item> Ignored
    {
        get
        {
            Reads++;
            return Enumerable.Range(1, 1000000).Select(id =>
            {
                Enumerated++;
                return new ChangeGraphRuntimeRegressionTest.Item { Id = id };
            });
        }
    }
}

/// <summary>
/// 包含可空复杂值类型的对象图模型。
/// </summary>
public partial class NullableValueGraph : Bing.Domain.Entities.DomainObjectBase<NullableValueGraph>
{
    /// <summary>
    /// 获取或设置可空的复杂值类型数据。
    /// </summary>
    public MutableValue? Value { get; set; }
}

/// <summary>
/// 包含可变集合的值类型测试数据。
/// </summary>
public struct MutableValue
{
    /// <summary>
    /// 获取或设置名称列表。
    /// </summary>
    public List<string> Names { get; set; }
}
