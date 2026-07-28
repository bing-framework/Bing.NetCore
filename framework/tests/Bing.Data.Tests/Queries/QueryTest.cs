using Bing.Data.Queries;
using Xunit;

namespace Bing.Data.Tests.Queries;

/// <summary>
/// <see cref="Query{TEntity, TKey}"/> 单元测试。
/// </summary>
public class QueryTest
{
    /// <summary>
    /// 测试目的：验证查询参数和追加排序应复制到分页对象。
    /// </summary>
    [Fact]
    public void GetPager_WhenParameterAndAdditionalOrderProvided_ShouldCopyPagingAndCombineOrder()
    {
        // Arrange
        var parameter = new QueryParameter
        {
            Page = 2,
            PageSize = 30,
            TotalCount = 40,
            Order = "Name"
        };
        var query = new Query<QueryItem, int>(parameter);
        query.OrderBy("Id", true);

        // Act
        var pager = query.GetPager();

        // Assert
        Assert.Equal(2, pager.Page);
        Assert.Equal(30, pager.PageSize);
        Assert.Equal(40, pager.TotalCount);
        Assert.Equal("Name,Id desc", pager.Order);
    }

    /// <summary>
    /// 测试目的：验证多个 Where 条件应以逻辑与组合。
    /// </summary>
    [Fact]
    public void Where_WhenMultiplePredicatesProvided_ShouldComposeWithAnd()
    {
        // Arrange
        var query = new Query<QueryItem, int>();
        query.Where(item => item.Name == "Alpha");
        query.Where(item => item.Id > 1);

        // Act
        var predicate = query.GetCondition().Compile();

        // Assert
        Assert.True(predicate(new QueryItem { Id = 2, Name = "Alpha" }));
        Assert.False(predicate(new QueryItem { Id = 1, Name = "Alpha" }));
        Assert.False(predicate(new QueryItem { Id = 2, Name = "Beta" }));
    }

    /// <summary>
    /// 测试目的：验证 false 条件和空捕获值不应追加查询表达式。
    /// </summary>
    [Fact]
    public void WhereIfAndWhereIfNotEmpty_WhenConditionIsDisabledOrValueIsEmpty_ShouldNotAddPredicate()
    {
        // Arrange
        var query = new Query<QueryItem, int>();
        var name = string.Empty;

        // Act
        query.WhereIf(item => item.Id > 1, false);
        query.WhereIfNotEmpty(item => item.Name == name);

        // Assert
        Assert.Null(query.GetCondition());
    }

    /// <summary>
    /// 测试目的：验证 WhereIfNotEmpty 不支持包含多个条件的表达式。
    /// </summary>
    [Fact]
    public void WhereIfNotEmpty_WhenPredicateContainsMultipleConditions_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var query = new Query<QueryItem, int>();

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => query.WhereIfNotEmpty(item => item.Name == "Alpha" && item.Id == 1));
    }

    /// <summary>
    /// 测试目的：验证合并另一查询对象时应同时合并条件和排序。
    /// </summary>
    [Fact]
    public void And_WhenQueryProvided_ShouldMergeConditionAndOrder()
    {
        // Arrange
        var query = new Query<QueryItem, int>();
        query.Where(item => item.Name == "Alpha");
        query.OrderBy("Name");
        var additional = new Query<QueryItem, int>();
        additional.Where(item => item.Id > 1);
        additional.OrderBy("Id", true);

        // Act
        query.And(additional);
        var predicate = query.GetCondition().Compile();

        // Assert
        Assert.True(predicate(new QueryItem { Id = 2, Name = "Alpha" }));
        Assert.False(predicate(new QueryItem { Id = 1, Name = "Alpha" }));
        Assert.Equal("Name,Id desc", query.GetOrder());
    }

    /// <summary>
    /// 测试目的：验证 Or 应忽略空条件并组合有效条件。
    /// </summary>
    [Fact]
    public void Or_WhenPredicatesIncludeEmptyValue_ShouldIgnoreEmptyPredicate()
    {
        // Arrange
        var query = new Query<QueryItem, int>();
        var name = string.Empty;

        // Act
        query.Or(item => item.Name == "Alpha", item => item.Name == name, item => item.Id == 2);
        var predicate = query.GetCondition().Compile();

        // Assert
        Assert.True(predicate(new QueryItem { Id = 1, Name = "Alpha" }));
        Assert.True(predicate(new QueryItem { Id = 2, Name = "Beta" }));
        Assert.False(predicate(new QueryItem { Id = 1, Name = "Beta" }));
    }

    /// <summary>
    /// 查询测试实体。
    /// </summary>
    private sealed class QueryItem
    {
        /// <summary>
        /// 获取或设置标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }
    }
}