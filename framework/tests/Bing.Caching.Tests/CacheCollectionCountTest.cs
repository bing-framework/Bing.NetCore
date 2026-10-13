using Bing.Caching;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// 验证缓存工具可直接读取集合数量而不枚举输入。
/// </summary>
public sealed class CacheCollectionCountTest
{
    /// <summary>
    /// 从可枚举集合读取数量且不调用枚举器。
    /// </summary>
    [Fact]
    public void TryGetCount_ReadsCollectionCountWithoutEnumerating()
    {
        var source = new ThrowOnEnumerationCollection<string>(new[] { "one", "two" });

        Assert.True(CacheCollectionCount.TryGetCount(source, out var count));
        Assert.Equal(2, count);
        Assert.Equal(0, source.EnumerationAttempts);
    }

    /// <summary>
    /// 从只读集合读取数量且不调用枚举器。
    /// </summary>
    [Fact]
    public void TryGetCount_ReadsReadOnlyCollectionCountWithoutEnumerating()
    {
        var source = new ReadOnlyCountCollection<string>(3);

        Assert.True(CacheCollectionCount.TryGetCount(source, out var count));
        Assert.Equal(3, count);
        Assert.Equal(0, source.EnumerationAttempts);
    }

    /// <summary>
    /// 对未知数量的普通枚举返回未命中。
    /// </summary>
    [Fact]
    public void TryGetCount_ReturnsFalseForUnknownEnumerable()
    {
        var source = new SingleUseEnumerable<string>(new[] { "one", "two" });

        Assert.False(CacheCollectionCount.TryGetCount(source, out var count));
        Assert.Equal(0, count);
    }

    /// <summary>
    /// 提供已知数量但枚举时抛出异常的集合。
    /// </summary>
    /// <typeparam name="T">集合元素类型。</typeparam>
    private sealed class ThrowOnEnumerationCollection<T> : ICollection<T>
    {
        /// <summary>
        /// 保存集合包含的元素。
        /// </summary>
        private readonly T[] _items;

        /// <summary>
        /// 初始化一个 <see cref="ThrowOnEnumerationCollection{T}"/> 类型的实例。
        /// </summary>
        /// <param name="items">集合包含的元素。</param>
        public ThrowOnEnumerationCollection(T[] items) => _items = items;

        /// <summary>
        /// 获取或设置集合枚举器被请求的次数。
        /// </summary>
        public int EnumerationAttempts { get; private set; }
        /// <summary>
        /// 获取集合报告的元素数量。
        /// </summary>
        public int Count => _items.Length;
        /// <summary>
        /// 获取集合是否只读。
        /// </summary>
        public bool IsReadOnly => true;
        /// <inheritdoc />
        public void Add(T item) => throw new NotSupportedException();
        /// <inheritdoc />
        public void Clear() => throw new NotSupportedException();
        /// <inheritdoc />
        public bool Contains(T item) => _items.Contains(item);
        /// <inheritdoc />
        public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        /// <inheritdoc />
        public bool Remove(T item) => throw new NotSupportedException();
        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            EnumerationAttempts++;
            throw new InvalidOperationException("不应枚举已知集合长度。");
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 提供已知数量且枚举时抛出异常的只读集合。
    /// </summary>
    /// <typeparam name="T">集合元素类型。</typeparam>
    private sealed class ReadOnlyCountCollection<T> : IReadOnlyCollection<T>
    {
        /// <summary>
        /// 初始化一个 <see cref="ReadOnlyCountCollection{T}"/> 类型的实例。
        /// </summary>
        /// <param name="count">集合报告的元素数量。</param>
        public ReadOnlyCountCollection(int count) => Count = count;

        /// <summary>
        /// 获取集合报告的元素数量。
        /// </summary>
        public int Count { get; }
        /// <summary>
        /// 获取或设置集合枚举器被请求的次数。
        /// </summary>
        public int EnumerationAttempts { get; private set; }

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            EnumerationAttempts++;
            throw new InvalidOperationException("不应枚举已知集合长度。");
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 包装一个不声明数量的源序列。
    /// </summary>
    /// <typeparam name="T">序列元素类型。</typeparam>
    private sealed class SingleUseEnumerable<T> : IEnumerable<T>
    {
        /// <summary>
        /// 保存被包装的源序列。
        /// </summary>
        private readonly IEnumerable<T> _source;

        /// <summary>
        /// 初始化一个 <see cref="SingleUseEnumerable{T}"/> 类型的实例。
        /// </summary>
        /// <param name="source">要包装的源序列。</param>
        public SingleUseEnumerable(IEnumerable<T> source) => _source = source;

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => _source.GetEnumerator();
        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
