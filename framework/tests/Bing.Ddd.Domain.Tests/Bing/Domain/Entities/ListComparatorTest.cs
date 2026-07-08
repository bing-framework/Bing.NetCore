using Bing.Domain.Entities;

namespace Bing.Domain.Entities;

/// <summary>
/// 实体列表比较器 测试
/// </summary>
public class ListComparatorTest
{
    private readonly ListComparator<IntAggregateRootSample, int> _comparator;

    /// <summary>
    /// 初始化
    /// </summary>
    public ListComparatorTest()
    {
        _comparator = new ListComparator<IntAggregateRootSample, int>();
    }

    #region 参数校验

    /// <summary>
    /// 测试目的：newList 为 null 时应抛出 ArgumentNullException。
    /// </summary>
    [Fact]
    public void Compare_NullNewList_ShouldThrowArgumentNullException()
    {
        // Arrange
        var oldList = new List<IntAggregateRootSample>();

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
        var newList = new List<IntAggregateRootSample>();

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
        // Arrange
        var newList = new List<IntAggregateRootSample>();
        var oldList = new List<IntAggregateRootSample>();

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.ShouldBeEmpty();
    }

    #endregion

    #region 全新增场景

    /// <summary>
    /// 测试目的：旧集合为空、新集合有元素时，全部应进入 CreateList。
    /// </summary>
    [Fact]
    public void Compare_AllNew_ShouldAllGoToCreateList()
    {
        // Arrange
        var a = new IntAggregateRootSample(1) { Name = "A" };
        var b = new IntAggregateRootSample(2) { Name = "B" };
        var newList = new List<IntAggregateRootSample> { a, b };
        var oldList = new List<IntAggregateRootSample>();

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.Count.ShouldBe(2);
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.ShouldBeEmpty();
        result.CreateList.ShouldContain(a);
        result.CreateList.ShouldContain(b);
    }

    #endregion

    #region 全删除场景

    /// <summary>
    /// 测试目的：新集合为空、旧集合有元素时，全部应进入 DeleteList。
    /// </summary>
    [Fact]
    public void Compare_AllRemoved_ShouldAllGoToDeleteList()
    {
        // Arrange
        var a = new IntAggregateRootSample(1) { Name = "A" };
        var b = new IntAggregateRootSample(2) { Name = "B" };
        var newList = new List<IntAggregateRootSample>();
        var oldList = new List<IntAggregateRootSample> { a, b };

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.ShouldBeEmpty();
        result.DeleteList.Count.ShouldBe(2);
        result.DeleteList.ShouldContain(a);
        result.DeleteList.ShouldContain(b);
    }

    #endregion

    #region 全更新场景

    /// <summary>
    /// 测试目的：新旧集合包含相同 ID 元素时，全部应进入 UpdateList。
    /// </summary>
    [Fact]
    public void Compare_SameIds_ShouldAllGoToUpdateList()
    {
        // Arrange
        var oldA = new IntAggregateRootSample(1) { Name = "OldA" };
        var oldB = new IntAggregateRootSample(2) { Name = "OldB" };
        var newA = new IntAggregateRootSample(1) { Name = "NewA" };
        var newB = new IntAggregateRootSample(2) { Name = "NewB" };
        var newList = new List<IntAggregateRootSample> { newA, newB };
        var oldList = new List<IntAggregateRootSample> { oldA, oldB };

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.Count.ShouldBe(2);
        result.DeleteList.ShouldBeEmpty();
    }

    #endregion

    #region 混合场景

    /// <summary>
    /// 测试目的：新旧集合各有新增、更新、删除时，应正确分拣到三个列表。
    /// </summary>
    [Fact]
    public void Compare_Mixed_ShouldSplitIntoCorrectLists()
    {
        // Arrange
        // ID=1 只在旧集合 → 删除
        // ID=2 在新旧集合  → 更新
        // ID=3 只在新集合 → 新增
        var oldItem1 = new IntAggregateRootSample(1) { Name = "Old1" };
        var oldItem2 = new IntAggregateRootSample(2) { Name = "Old2" };
        var newItem2 = new IntAggregateRootSample(2) { Name = "New2" };
        var newItem3 = new IntAggregateRootSample(3) { Name = "New3" };
        var newList = new List<IntAggregateRootSample> { newItem2, newItem3 };
        var oldList = new List<IntAggregateRootSample> { oldItem1, oldItem2 };

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.CreateList.Count.ShouldBe(1);
        result.CreateList.ShouldContain(newItem3);

        result.UpdateList.Count.ShouldBe(1);
        result.UpdateList.First().Id.ShouldBe(2);

        result.DeleteList.Count.ShouldBe(1);
        result.DeleteList.ShouldContain(oldItem1);
    }

    /// <summary>
    /// 测试目的：单个元素从旧列表移除时，仅应出现在 DeleteList。
    /// </summary>
    [Fact]
    public void Compare_SingleDelete_ShouldOnlyAppearInDeleteList()
    {
        // Arrange
        var item = new IntAggregateRootSample(10) { Name = "X" };
        var newList = new List<IntAggregateRootSample>();
        var oldList = new List<IntAggregateRootSample> { item };

        // Act
        var result = _comparator.Compare(newList, oldList);

        // Assert
        result.DeleteList.ShouldContain(item);
        result.CreateList.ShouldBeEmpty();
        result.UpdateList.ShouldBeEmpty();
    }

    #endregion

    #region 结果对象属性

    /// <summary>
    /// 测试目的：ListCompareResult 三个属性在构造后应按传入顺序保持引用。
    /// </summary>
    [Fact]
    public void ListCompareResult_Properties_ShouldMatchConstructorArguments()
    {
        // Arrange
        var createList = new List<IntAggregateRootSample> { new IntAggregateRootSample(1) };
        var updateList = new List<IntAggregateRootSample> { new IntAggregateRootSample(2) };
        var deleteList = new List<IntAggregateRootSample> { new IntAggregateRootSample(3) };

        // Act
        var result = new ListCompareResult<IntAggregateRootSample, int>(createList, updateList, deleteList);

        // Assert
        result.CreateList.ShouldBeSameAs(createList);
        result.UpdateList.ShouldBeSameAs(updateList);
        result.DeleteList.ShouldBeSameAs(deleteList);
    }

    #endregion
}
