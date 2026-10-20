using Bing.Caching;

namespace Bing.Tests;

/// <summary>
/// 测试本地内存缓存基类的并发、快照和集合操作语义。
/// </summary>
public class LocalMemoryCacheBaseTest
{
    /// <summary>
    /// 验证字典缓存并发添加同一键只成功一次。
    /// </summary>
    /// <remarks>
    /// 其余并发调用不得抛出重复键异常，缓存最终只保留一个条目。
    /// </remarks>
    [Fact]
    public async Task SingleType_AddSameKeyConcurrently_ShouldSucceedOnce()
    {
        var cache = new TestDictionaryCache();

        var results = await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(index => Task.Run(() => cache.Add("shared", index))));

        results.Count(result => result).ShouldBe(1);
        cache.Count.ShouldBe(1);
        cache.Exists("shared").ShouldBeTrue();
    }

    /// <summary>
    /// 验证字典缓存读取返回独立快照。
    /// </summary>
    /// <remarks>
    /// 调用方修改快照不得改变缓存内部字典。
    /// </remarks>
    [Fact]
    public void SingleType_Reader_ShouldReturnSnapshot()
    {
        var cache = new TestDictionaryCache();
        cache.Add("one", 1);

        var snapshot = cache.Reader();
        snapshot["one"] = 2;
        snapshot.Add("two", 2);
        snapshot.Remove("one");

        cache.Count.ShouldBe(1);
        cache.Get("one").ShouldBe(1);
        cache.Exists("two").ShouldBeFalse();
    }

    /// <summary>
    /// 验证字典缓存 Set、Update 和批量 Remove 的返回语义。
    /// </summary>
    /// <remarks>
    /// Update 只更新已有键，批量 Remove 仅在至少删除一个键时返回 true。
    /// </remarks>
    [Fact]
    public void SingleType_SetUpdateAndRemoveArray_ShouldReportCorrectResults()
    {
        var cache = new TestDictionaryCache();

        cache.Set("one", 1).ShouldBeTrue();
        cache.Update("one", 2).ShouldBeTrue();
        cache.Get("one").ShouldBe(2);
        cache.Update("missing", 3).ShouldBeFalse();

        cache.Remove(new[] { "missing", "one" }).ShouldBeTrue();
        cache.Remove(new[] { "missing" }).ShouldBeFalse();
    }

    /// <summary>
    /// 验证列表缓存并发添加同一项目只成功一次。
    /// </summary>
    /// <remarks>
    /// 其余并发调用不得产生重复项目，列表最终只保留一个条目。
    /// </remarks>
    [Fact]
    public async Task List_AddSameItemConcurrently_ShouldSucceedOnce()
    {
        var cache = new TestListCache();

        var results = await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => cache.Add("shared"))));

        results.Count(result => result).ShouldBe(1);
        cache.Count.ShouldBe(1);
        cache.ReaderAll().ShouldBe(new[] { "shared" });
    }

    /// <summary>
    /// 验证列表缓存读取返回快照并保持删除后的项目顺序。
    /// </summary>
    /// <remarks>
    /// 调用方修改快照不得影响缓存，删除项目后剩余项目保持原有顺序。
    /// </remarks>
    [Fact]
    public void List_ReaderAllAndRemove_ShouldPreserveOrderAndIsolation()
    {
        var cache = new TestListCache();
        cache.Add("one");
        cache.Add("two");
        cache.Add("three");

        var snapshot = cache.ReaderAll();
        snapshot.RemoveAt(0);
        snapshot.Add("snapshot-only");

        cache.ReaderAll().ShouldBe(new[] { "one", "two", "three" });
        cache.Remove("two").ShouldBeTrue();
        cache.ReaderAll().ShouldBe(new[] { "one", "three" });
        cache.Remove("missing").ShouldBeFalse();
    }

    /// <summary>
    /// 提供字典缓存测试实现。
    /// </summary>
    private sealed class TestDictionaryCache : SingleTypeLocalMemoryBase<string, int>
    {
        /// <summary>
        /// 测试使用的字典缓存容器。
        /// </summary>
        private readonly IDictionary<string, int> _cache = new Dictionary<string, int>();
        /// <summary>
        /// 保护字典缓存操作的同步对象。
        /// </summary>
        private readonly object _sync = new();

        /// <inheritdoc />
        protected override IDictionary<string, int> GetCache() => _cache;

        /// <inheritdoc />
        protected override object GetSyncCache() => _sync;
    }

    /// <summary>
    /// 提供列表缓存测试实现。
    /// </summary>
    private sealed class TestListCache : ListLocalMemoryBase<string>
    {
        /// <summary>
        /// 测试使用的列表缓存容器。
        /// </summary>
        private readonly IList<string> _cache = new List<string>();
        /// <summary>
        /// 保护列表缓存操作的同步对象。
        /// </summary>
        private readonly object _sync = new();

        /// <inheritdoc />
        protected override IList<string> GetCache() => _cache;

        /// <inheritdoc />
        protected override object GetSyncCache() => _sync;
    }
}
