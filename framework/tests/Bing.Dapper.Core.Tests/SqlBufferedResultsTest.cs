using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>缓冲结果复用列表，且不能为集合转换引入 Dapper 的静态初始化。</summary>
public sealed class SqlBufferedResultsTest
{
    [Fact]
    public void AsBufferedList_ShouldReuseExistingList()
    {
        IEnumerable<int> rows = new List<int> { 1, 2 };

        Assert.Same(rows, rows.AsBufferedList());
    }

    [Fact]
    public void AsBufferedList_ShouldMaterializeEnumerableOnce()
    {
        var enumerations = 0;
        IEnumerable<int> Rows()
        {
            enumerations++;
            yield return 1;
            yield return 2;
        }

        var rows = Rows().AsBufferedList();

        Assert.Equal(new[] { 1, 2 }, rows);
        Assert.Equal(1, enumerations);
        Assert.Same(rows, rows.AsBufferedList());
        Assert.Equal(1, enumerations);
    }

    [Fact]
    public void AsBufferedList_ShouldPreserveNullResult()
    {
        IEnumerable<int> rows = null;

        Assert.Null(rows.AsBufferedList());
    }
}
