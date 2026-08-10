using Bing.Data.Sql;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// MySQL SQL 查询对象树状表真实执行集成测试。
/// </summary>
public partial class MySqlQueryTest
{
    /// <summary>
    /// 测试目的：MySQL 8 邻接表递归 CTE 应返回完整层级、父子关系和稳定深度排序。
    /// </summary>
    [IntegrationFact("MySql")]
    [Trait("Category", "Integration")]
    [Trait("Database", "MySql")]
    public async Task MySql_RecursiveCte_WhenQueryingHierarchy_ShouldReturnOrderedTree()
    {
        // Arrange
        await SeedHierarchyNodesAsync();
        using var query = _fixture.CreateQuery();
        var tree = query.Sql<HierarchyNode>().AppendSelect("Id,ParentId,Name,0 As Depth").From("IntegrationHierarchyNode")
            .IsNull("ParentId")
            .UnionAll(query.Sql<HierarchyNode>().AppendSelect("n.Id,n.ParentId,n.Name,t.Depth + 1 As Depth")
                .From("IntegrationHierarchyNode", "n").Join("tree", "t").AppendOn("n.ParentId=t.Id"));
        var description = query.Sql<HierarchyNode>().With("tree", tree).Select("Id,ParentId,Name,Depth")
            .From("tree").OrderBy("Depth").OrderBy("Id");

        // Act
        var sql = description.ToSql();
        var firstResult = await description.ToListAsync();
        var secondResult = await description.ToListAsync();

        // Assert
        Assert.StartsWith("With Recursive", sql);
        Assert.Equal(new[] { "root:0", "child-a:1", "child-b:1", "leaf:2" },
            firstResult.Select(item => $"{item.Name}:{item.Depth}"));
        Assert.Equal(new int?[] { null, 1, 1, 2 }, firstResult.Select(item => item.ParentId));
        Assert.Equal(firstResult.Select(item => item.Name), secondResult.Select(item => item.Name));
    }

    /// <summary>
    /// 写入固定邻接表树状测试数据。
    /// </summary>
    /// <returns>写入任务。</returns>
    private async Task SeedHierarchyNodesAsync()
    {
        foreach (var node in new[]
                 {
                     new { id = 1, parentId = (int?)null, name = "root" },
                     new { id = 2, parentId = (int?)1, name = "child-a" },
                     new { id = 3, parentId = (int?)1, name = "child-b" },
                     new { id = 4, parentId = (int?)2, name = "leaf" }
                 })
        {
            await _sqlExecutor.ExecuteSqlAsync(
                "Insert Into IntegrationHierarchyNode(Id,ParentId,Name) Values(@id,@parentId,@name)", node);
        }
    }

    /// <summary>
    /// MySQL 邻接表递归查询投影。
    /// </summary>
    private sealed class HierarchyNode
    {
        /// <summary>
        /// 节点标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 父节点标识；根节点为 <see langword="null"/>。
        /// </summary>
        public int? ParentId { get; set; }

        /// <summary>
        /// 节点名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 从根节点开始计算的层级深度。
        /// </summary>
        public int Depth { get; set; }
    }
}