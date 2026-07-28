using Bing;
using Bing.Data;
using Bing.Data.Queries;
using Bing.Data.Queries.Conditions;
using Xunit;

namespace Bing.Data.Tests.Extensions;

/// <summary>
/// <see cref="QueryableExtensions"/> 单元测试。
/// </summary>
public class QueryableExtensionsTest
{
    /// <summary>
    /// 测试目的：条件未生成表达式时应返回原始查询对象。
    /// </summary>
    [Fact]
    public void Where_WhenConditionReturnsNull_ShouldReturnOriginalQuery()
    {
        // Arrange
        var query = CreateItems().AsQueryable();
        var condition = new DefaultCondition<QueryItem>(null);

        // Act
        var result = query.Where(condition);

        // Assert
        Assert.Same(query, result);
    }

    /// <summary>
    /// 测试目的：验证查询对象或条件为空时应拒绝调用。
    /// </summary>
    [Fact]
    public void Where_WhenQueryOrConditionIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        IQueryable<QueryItem> query = null;
        var condition = new DefaultCondition<QueryItem>(item => item.Id > 0);

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => query.Where(condition));
        Assert.Throws<ArgumentNullException>(() => CreateItems().AsQueryable().Where(null));
    }

    /// <summary>
    /// 测试目的：条件为 false 时应跳过谓词并保留原始查询对象。
    /// </summary>
    [Fact]
    public void WhereIf_WhenConditionIsFalse_ShouldReturnOriginalQuery()
    {
        // Arrange
        var query = CreateItems().AsQueryable();

        // Act
        var result = query.WhereIf(item => item.Id > 1, false);

        // Assert
        Assert.Same(query, result);
    }

    /// <summary>
    /// 测试目的：捕获值为空时应忽略 WhereIfNotEmpty 谓词。
    /// </summary>
    [Fact]
    public void WhereIfNotEmpty_WhenCapturedValueIsEmpty_ShouldReturnOriginalQuery()
    {
        // Arrange
        var query = CreateItems().AsQueryable();
        var name = string.Empty;

        // Act
        var result = query.WhereIfNotEmpty(item => item.Name == name);

        // Assert
        Assert.Same(query, result);
    }

    /// <summary>
    /// 测试目的：指定左包含右排除的整数范围时应返回边界内项目。
    /// </summary>
    [Fact]
    public void Between_WhenLeftBoundaryIncluded_ShouldFilterExpectedItems()
    {
        // Act
        var result = CreateItems()
            .AsQueryable()
            .Between(item => item.Id, 2, 4, Boundary.Left)
            .Select(item => item.Id)
            .ToList();

        // Assert
        Assert.Equal(new[] { 2, 3 }, result);
    }

    /// <summary>
    /// 测试目的：缺少排序字段时应默认按 Id 排序并初始化总记录数。
    /// </summary>
    [Fact]
    public void Page_WhenOrderMissing_ShouldUseIdAndInitializeTotalCount()
    {
        // Arrange
        var pager = new Pager(page: 2, pageSize: 2);

        // Act
        var result = CreateItems().AsQueryable().Page(pager).Select(item => item.Id).ToList();

        // Assert
        Assert.Equal("Id", pager.Order);
        Assert.Equal(4, pager.TotalCount);
        Assert.Equal(new[] { 3, 4 }, result);
    }

    /// <summary>
    /// 测试目的：查询已包含降序排序时，分页不应覆盖为默认 Id 升序。
    /// </summary>
    [Fact]
    public void Page_WhenQueryIsAlreadyOrderedDescending_ShouldKeepExistingOrder()
    {
        // Arrange
        var pager = new Pager(page: 1, pageSize: 2);
        var query = CreateItems().AsQueryable().OrderByDescending(item => item.Id);

        // Act
        var result = query.Page(pager).Select(item => item.Id).ToList();

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(pager.Order));
        Assert.Equal(4, pager.TotalCount);
        Assert.Equal(new[] { 4, 3 }, result);
    }

    /// <summary>
    /// 测试目的：指定跳过数量和页大小时应返回正确的页片段。
    /// </summary>
    [Fact]
    public void PageBy_WhenRangeProvided_ShouldApplySkipAndTake()
    {
        // Act
        var result = CreateItems().AsQueryable().PageBy(1, 2).Select(item => item.Id).ToList();

        // Assert
        Assert.Equal(new[] { 2, 3 }, result);
    }

    /// <summary>
    /// 测试目的：转换分页列表时应复制分页元数据并物化当前页数据。
    /// </summary>
    [Fact]
    public void ToPagerList_WhenPagerProvided_ShouldCopyMetadataAndMaterializeCurrentPage()
    {
        // Arrange
        var pager = new Pager(page: 2, pageSize: 2, order: "Id desc");

        // Act
        var result = CreateItems().AsQueryable().ToPagerList(pager);

        // Assert
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(4, result.TotalCount);
        Assert.Equal("Id desc", result.Order);
        Assert.Equal(new[] { 2, 1 }, result.Data.Select(item => item.Id));
    }

    /// <summary>
    /// 创建用于内存查询的固定数据集。
    /// </summary>
    private static IReadOnlyList<QueryItem> CreateItems() => new[]
    {
        new QueryItem { Id = 1, Name = "Alpha" },
        new QueryItem { Id = 2, Name = "Beta" },
        new QueryItem { Id = 3, Name = "Gamma" },
        new QueryItem { Id = 4, Name = "Delta" }
    };

    /// <summary>
    /// 内存查询测试实体。
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