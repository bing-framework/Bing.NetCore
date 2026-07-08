using Bing.Domain.Entities;

namespace Bing.Domain.Entities;

/// <summary>
/// 键列表比较器 测试
/// </summary>
public class KeyListComparatorTest
{
    private readonly KeyListComparator<int> _comparator;

    /// <summary>
    /// 初始化
    /// </summary>
    public KeyListComparatorTest()
    {
        _comparator = new KeyListComparator<int>();
    }

    #region 参数校验

    /// <summary>
    /// 测试目的：newList 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Compare_NullNewList_ShouldThrowArgumentNullException()
    {
        // Arrange
        var oldList = new List<int> { 1, 2 };

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _comparator.Compare(null, oldList));
    }

    /// <summary>
    /// 测试目的：oldList 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Compare_NullOldList_ShouldThrowArgumentNullException()
    {
        // Arrange
        var newList = new List<int> { 1, 2 };

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _comparator.Compare(newList, null));
    }

    #endregion

    #region 空集合场景

    /// <summary>
    /// 测试目的：新旧集合均为空时，三个结果集合均应为空。
    /// </summary>
    [Fact]
    public void Compare_BothEmpty_ShouldReturnEmptyLists()
    {
        // Arrange & Act
        var result = _comparator.Compare(new List<int>(), new List<int>());

        // Assert
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.ShouldBeEmpty();
    }

    #endregion

    #region 全新增场景

    /// <summary>
    /// 测试目的：旧集合为空时，新集合中所有键均应进入 CreateList。
    /// </summary>
    [Fact]
    public void Compare_AllNew_ShouldAllGoToCreateList()
    {
        // Arrange
        var newList = new List<int> { 1, 2, 3 };
        var oldList = new List<int>();

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.Count.ShouldBe(3);
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.ShouldBeEmpty();
        result.CreateList.ShouldContain(1);
        result.CreateList.ShouldContain(2);
        result.CreateList.ShouldContain(3);
    }

    #endregion

    #region 全删除场景

    /// <summary>
    /// 测试目的：新集合为空时，旧集合中所有键均应进入 DeleteList。
    /// </summary>
    [Fact]
    public void Compare_AllRemoved_ShouldAllGoToDeleteList()
    {
        // Arrange
        var newList = new List<int>();
        var oldList = new List<int> { 10, 20 };

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.Count.ShouldBe(2);
        result.DeleteList.ShouldContain(10);
        result.DeleteList.ShouldContain(20);
    }

    #endregion

    #region 全更新场景

    /// <summary>
    /// 测试目的：新旧集合包含相同键时，全部应进入 UpdateList。
    /// </summary>
    [Fact]
    public void Compare_SameKeys_ShouldAllGoToUpdateList()
    {
        // Arrange
        var newList = new List<int> { 1, 2 };
        var oldList = new List<int> { 1, 2 };

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.Count.ShouldBe(2);
        result.DeleteList.ShouldBeEmpty();
        result.UpdateList.ShouldContain(1);
        result.UpdateList.ShouldContain(2);
    }

    #endregion

    #region 混合场景

    /// <summary>
    /// 测试目的：混合场景下，新增、更新、删除的键应正确分拣到三个列表。
    /// </summary>
    [Fact]
    public void Compare_Mixed_ShouldSplitIntoCorrectLists()
    {
        // Arrange
        // 1 只在旧 → 删除；2 新旧都有 → 更新；3 只在新 → 新增
        var newList = new List<int> { 2, 3 };
        var oldList = new List<int> { 1, 2 };

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.Count.ShouldBe(1);
        result.CreateList.ShouldContain(3);

        result.UpdateList.Count.ShouldBe(1);
        result.UpdateList.ShouldContain(2);

        result.DeleteList.Count.ShouldBe(1);
        result.DeleteList.ShouldContain(1);
    }

    /// <summary>
    /// 测试目的：string 类型键的混合比较，验证泛型通用性。
    /// </summary>
    [Fact]
    public void Compare_StringKeys_Mixed_ShouldSplitCorrectly()
    {
        // Arrange
        var comparator = new KeyListComparator<string>();
        var newList = new List<string> { "B", "C" };
        var oldList = new List<string> { "A", "B" };

        // Act
        var result = comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.ShouldContain("C");
        result.UpdateList.ShouldContain("B");
        result.DeleteList.ShouldContain("A");
    }

    #endregion

    #region 结果对象属性

    /// <summary>
    /// 测试目的：KeyListCompareResult 的三个属性应按构造顺序保持引用。
    /// </summary>
    [Fact]
    public void KeyListCompareResult_Properties_ShouldMatchConstructorArguments()
    {
        // Arrange
        var createList = new List<int> { 1 };
        var updateList = new List<int> { 2 };
        var deleteList = new List<int> { 3 };

        // Act
        var result = new KeyListCompareResult<int>(createList, updateList, deleteList);

        // Assert
        result.CreateList.ShouldBeSameAs(createList);
        result.UpdateList.ShouldBeSameAs(updateList);
        result.DeleteList.ShouldBeSameAs(deleteList);
    }

    #endregion
}
