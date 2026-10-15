using Bing.FreeSQL;
using Xunit;

namespace Bing.FreeSQL.Tests.Core;

/// <summary>
/// 验证 FreeSQL 查询提供程序的同步 LINQ 异步包装行为。
/// </summary>
public sealed class FreeSqlAsyncQueryableProviderTest
{
    /// <summary>
    /// 被测的 FreeSQL 异步查询包装器。
    /// </summary>
    private readonly FreeSqlAsyncQueryableProvider _provider = new();

    /// <summary>
    /// 验证 FreeSQL 提供程序不抢占可由数据库提供程序执行的查询。
    /// </summary>
    [Fact]
    public void CanExecute_ShouldRemainFalse()
    {
        Assert.False(_provider.CanExecute(new[] { 1, 2, 3 }.AsQueryable()));
    }

    /// <summary>
    /// 验证元素、条件和数量查询保持 LINQ 的结果与异常语义。
    /// </summary>
    [Fact]
    public async Task ElementAndCountQueries_ShouldMatchLinqSemantics()
    {
        var query = new[] { 1, 2, 3, 4 }.AsQueryable();

        Assert.True(await _provider.ContainsAsync(query, 3));
        Assert.True(await _provider.AnyAsync(query, value => value > 3));
        Assert.True(await _provider.AllAsync(query, value => value > 0));
        Assert.Equal(4, await _provider.CountAsync(query));
        Assert.Equal(2, await _provider.CountAsync(query, value => value % 2 == 0));
        Assert.Equal(4L, await _provider.LongCountAsync(query));
        Assert.Equal(2L, await _provider.LongCountAsync(query, value => value % 2 == 0));
        Assert.Equal(1, await _provider.FirstAsync(query));
        Assert.Equal(4, await _provider.LastAsync(query));
        Assert.Equal(2, await _provider.SingleAsync(query.Where(value => value == 2)));
        Assert.Equal(0, await _provider.FirstOrDefaultAsync(query.Where(value => value == 99)));
        Assert.Equal(0, await _provider.LastOrDefaultAsync(query.Where(value => value == 99)));
        Assert.Equal(0, await _provider.SingleOrDefaultAsync(query.Where(value => value == 99)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _provider.SingleAsync(query.Where(value => value > 1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _provider.FirstAsync(query.Where(value => value == 99)));
    }

    /// <summary>
    /// 验证聚合、列表和数组查询使用同步 LINQ 的数值语义。
    /// </summary>
    [Fact]
    public async Task AggregateAndMaterializationQueries_ShouldReturnExpectedValues()
    {
        var query = new[] { 1, 2, 3, 4 }.AsQueryable();
        var decimals = new[] { 1.5m, 2.5m }.AsQueryable();
        var nullable = new int?[] { 1, null, 3 }.AsQueryable();

        Assert.Equal(1, await _provider.MinAsync(query));
        Assert.Equal(4, await _provider.MaxAsync(query));
        Assert.Equal(1, await _provider.MinAsync(query, value => value));
        Assert.Equal(4, await _provider.MaxAsync(query, value => value));
        Assert.Equal(10, await _provider.SumAsync(query));
        Assert.Equal(10L, await _provider.SumAsync(query.Select(value => (long)value)));
        Assert.Equal(10d, await _provider.SumAsync(query.Select(value => (double)value)));
        Assert.Equal(10f, await _provider.SumAsync(query.Select(value => (float)value)));
        Assert.Equal(10m, await _provider.SumAsync(query.Select(value => (decimal)value)));
        Assert.Equal(4, await _provider.SumAsync(nullable));
        Assert.Equal(2m, await _provider.AverageAsync(decimals));
        Assert.Equal(2.5d, await _provider.AverageAsync(query));
        Assert.Equal(2d, await _provider.AverageAsync(nullable));
        Assert.Equal(new[] { 1, 2, 3, 4 }, await _provider.ToArrayAsync(query));
        Assert.Equal(new[] { 1, 2, 3, 4 }, await _provider.ToListAsync(query));
    }

    /// <summary>
    /// 验证取消令牌在同步计算开始前生效，不会静默执行已取消查询。
    /// </summary>
    [Fact]
    public async Task QueryWithCancelledToken_ShouldThrowCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _provider.ToListAsync(new[] { 1, 2, 3 }.AsQueryable(), source.Token));
    }
}
