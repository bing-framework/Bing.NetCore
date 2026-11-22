namespace Bing.Data.Sql.Tests;

/// <summary>参数值快照的独立职责测试。</summary>
public sealed class SqlParameterSnapshotTest
{
    /// <summary>值类型和字符串数组应保留数组类型并与原始容器隔离。</summary>
    [Fact]
    public void SnapshotValue_WhenArrayContainsValueTypesOrStrings_ShouldCloneContainer()
    {
        var numbers = new[] { 1, 2 };
        var strings = new[] { "first", "second" };

        var numberSnapshot = Assert.IsType<int[]>(SqlParameterSnapshot.SnapshotValue(numbers));
        var stringSnapshot = Assert.IsType<string[]>(SqlParameterSnapshot.SnapshotValue(strings));
        numbers[0] = 9;
        strings[0] = "changed";

        Assert.Equal(new[] { 1, 2 }, numberSnapshot);
        Assert.Equal(new[] { "first", "second" }, stringSnapshot);
    }

    /// <summary>引用类型数组中的嵌套可变容器仍应逐层生成独立快照。</summary>
    [Fact]
    public void SnapshotValue_WhenArrayContainsNestedArrays_ShouldIsolateNestedValues()
    {
        var inner = new byte[] { 2, 3 };
        var source = new object[] { inner, new object[] { new[] { 4, 5 } } };

        var snapshot = Assert.IsType<object[]>(SqlParameterSnapshot.SnapshotValue(source));
        inner[0] = 8;
        ((int[])((object[])source[1])[0])[0] = 9;

        Assert.Equal(new byte[] { 2, 3 }, Assert.IsType<byte[]>(snapshot[0]));
        var nested = Assert.IsType<object[]>(snapshot[1]);
        Assert.Equal(new[] { 4, 5 }, Assert.IsType<int[]>(nested[0]));
    }

    /// <summary>字符串键类型化字典数组应使用可容纳快照值的数组，并隔离嵌套字典。</summary>
    [Fact]
    public void SnapshotValue_WhenArrayContainsTypedStringDictionaries_ShouldCloneNestedValues()
    {
        var source = new[]
        {
            new Dictionary<string, int> { ["tenant"] = 7 }
        };

        var snapshot = Assert.IsType<object[]>(SqlParameterSnapshot.SnapshotValue(source));
        source[0]["tenant"] = 99;

        var nested = Assert.IsAssignableFrom<IDictionary<string, object>>(snapshot[0]);
        Assert.NotSame(source[0], snapshot[0]);
        Assert.Equal(7, nested["tenant"]);
    }
}
