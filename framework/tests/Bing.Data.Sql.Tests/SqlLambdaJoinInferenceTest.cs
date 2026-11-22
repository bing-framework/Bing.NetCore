using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Tests.Samples;
using Moq;
using Xunit;

namespace Bing.Data.Sql.Tests;

/// <summary>
/// 验证 Lambda 查询连接来源推断的测试。
/// </summary>
/// <remarks>
/// 自动别名配置、Join 来源角色及失败回滚的直接合同。
/// </remarks>
public sealed class SqlLambdaJoinInferenceTest
{
    /// <summary>
    /// 创建使用指定选项的连接推断测试查询。
    /// </summary>
    /// <param name="options">需要配置或使用的测试选项。</param>
    /// <returns>已配置测试来源或投影的 Lambda 查询。</returns>
    private static SqlLambdaQuery Create(SqlOptions options = null) =>
        SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, new TestSqlBuilder(options: options));

    /// <summary>
    /// 验证表别名策略变化后连接仍限定两个来源。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Join_WhenAliasPolicyChanges_ShouldQualifyBothSources(bool automatic)
    {
        var query = Create(new SqlOptions { AutoGenerateTableAlias = automatic }).From<Node>()
            .Select<Node>(n => new { n.Id })
            .Join<Node, Kind>((n, k) => n.KindId == k.Id)
            .Select<Kind>(k => new { Kind = k.Name });
        var expected = automatic
            ? "Select [n].[NodeId] As [Id],[k].[Name] As [Kind] \r\nFrom [Nodes] As [n] \r\nJoin [Kinds] As [k] On [n].[KindId]=[k].[Id]"
            : "Select [Nodes].[NodeId] As [Id],[Kinds].[Name] As [Kind] \r\nFrom [Nodes] \r\nJoin [Kinds] On [Nodes].[KindId]=[Kinds].[Id]";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>
    /// 验证连续自连接使用已有左来源与新建右来源。
    /// </summary>
    [Fact]
    public void Join_WhenSelfJoinedSequentially_ShouldUseExistingLeftAndNewRight()
    {
        var query = Create().From<Node>()
            .Select<Node>(employee => new { employee.Id })
            .LeftJoin<Node, Node>((employee, manager) => employee.ParentId == manager.Id)
            .LeftJoin<Node, Node>((manager, director) => manager.ParentId == director.Id)
            .Select<Node>(director => new { Boss = director.Name });
        const string expected = "Select [employee].[NodeId] As [Id],[director].[Name] As [Boss] \r\nFrom [Nodes] As [employee] \r\nLeft Join [Nodes] As [manager] On [employee].[ParentId]=[manager].[NodeId] \r\nLeft Join [Nodes] As [director] On [manager].[ParentId]=[director].[NodeId]";
        Assert.Equal(expected, query.ToSql());
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>
    /// 验证同类型多个来源保留各自的参数角色。
    /// </summary>
    [Fact]
    public void Join_WhenSameTypeAddedTwice_ShouldPreserveRoleNames()
    {
        var query = Create().From<Node>()
            .Join<Node, Kind>((n, creator) => n.KindId == creator.Id)
            .Join<Node, Kind>((n, approver) => n.ParentId == approver.Id)
            .Select<Kind, Kind, Roles>((creator, approver) => new Roles { First = creator.Name, Second = approver.Name });
        const string expected = "Select [creator].[Name] As [First],[approver].[Name] As [Second] \r\nFrom [Nodes] As [n] \r\nJoin [Kinds] As [creator] On [n].[KindId]=[creator].[Id] \r\nJoin [Kinds] As [approver] On [n].[ParentId]=[approver].[Id]";
        Assert.Equal(expected, query.ToSql());
        Assert.Throws<InvalidOperationException>(() => query.Select<Kind>(unknown => unknown.Name));
        Assert.Throws<InvalidOperationException>(() => query.Join<Kind, Node>((unknown, child) => unknown.Id == child.KindId));
        Assert.Equal(expected, query.ToSql());
    }

    /// <summary>
    /// 验证显式来源别名优先于表达式参数名。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Join_WhenExplicitAliasesDifferFromParameters_ShouldKeepExplicitAliases(bool automatic)
    {
        var query = Create(new SqlOptions { AutoGenerateTableAlias = automatic }).From<Node>("fixed")
            .Select<Node>(n => new { n.Id })
            .Join<Node, Kind>((n, k) => n.KindId == k.Id, "chosen");
        Assert.Equal("Select [fixed].[NodeId] As [Id] \r\nFrom [Nodes] As [fixed] \r\nJoin [Kinds] As [chosen] On [fixed].[KindId]=[chosen].[Id]", query.ToSql());
    }

    /// <summary>
    /// 验证推断别名冲突时回滚并允许改用不同名称。
    /// </summary>
    [Fact]
    public void Join_WhenInferredNameCollides_ShouldRollbackAndPermitDifferentName()
    {
        var query = Create().From<Node>().Select<Node>(n => new { n.Id })
            .Join<Node, Kind>((n, k) => n.KindId == k.Id);
        const string expected = "Select [n].[NodeId] As [Id] \r\nFrom [Nodes] As [n] \r\nJoin [Kinds] As [k] On [n].[KindId]=[k].[Id]";
        Assert.Throws<InvalidOperationException>(() => query.Join<Node, Kind>((n, k) => n.ParentId == k.Id));
        Assert.Equal(expected, query.ToSql());
        query.Join<Kind, Node>((k, other) => k.Id == other.KindId);
        Assert.Equal(expected + " \r\nJoin [Nodes] As [other] On [k].[Id]=[other].[KindId]", query.ToSql());
    }

    /// <summary>
    /// 验证禁用自动别名时重复物理表连接被原子拒绝。
    /// </summary>
    [Fact]
    public void Join_WhenDisabledAndSamePhysicalTableAppears_ShouldRejectAtomically()
    {
        var query = Create(new SqlOptions { AutoGenerateTableAlias = false }).From<Node>().Select<Node>(n => new { n.Id });
        const string expected = "Select [Nodes].[NodeId] As [Id] \r\nFrom [Nodes]";
        Assert.Throws<InvalidOperationException>(() => query.Join<Node, Node>((n, parent) => n.ParentId == parent.Id));
        Assert.Equal(expected, query.ToSql());
        Assert.Throws<InvalidOperationException>(() => query.Join<Node, OtherNode>((n, other) => n.ParentId == other.Id));
        Assert.Equal(expected, query.ToSql());
    }

    /// <summary>
    /// 验证选项修改不影响已有查询、克隆和子构建器。
    /// </summary>
    [Fact]
    public void Create_WhenOptionsMutate_ShouldKeepExistingQueriesClonesAndChildBuildersFixed()
    {
        var options = new SqlOptions { AutoGenerateTableAlias = false };
        var builder = new TestSqlBuilder(options: options);
        var root = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder);
        var query = root.From<Node>().Select<Node>(n => new { n.Id });
        options.AutoGenerateTableAlias = true;
        var clone = query.Clone().Join<Node, Kind>((n, k) => n.KindId == k.Id);
        Assert.Equal("Select [Nodes].[NodeId] As [Id] \r\nFrom [Nodes]", query.ToSql());
        Assert.Equal("Select [Nodes].[NodeId] As [Id] \r\nFrom [Nodes] \r\nJoin [Kinds] On [Nodes].[KindId]=[Kinds].[Id]", clone.ToSql());
        var child = SqlQueryRuntimeFactory.CreateLambdaQuery(new Mock<ISqlQueryPlanExecutor>().Object, builder.New())
            .From<Kind>().Select<Kind>(k => k.Name);
        Assert.Equal("Select [Kinds].[Name] \r\nFrom [Kinds]", child.ToSql());
        Assert.Equal("Select [n].[NodeId] As [Id] \r\nFrom [Nodes] As [n]", Create(options).From<Node>().Select<Node>(n => new { n.Id }).ToSql());
    }

    /// <summary>
    /// 验证连接前登记的排除列跟随来源而不创建新别名。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Exclude_WhenRegisteredBeforeJoin_ShouldFollowSourceRatherThanCreateAlias(bool automatic)
    {
        var query = Create(new SqlOptions { AutoGenerateTableAlias = automatic }).From<Node>("n").SelectAll()
            .ExcludeSelect<Kind>(pending => pending.Name)
            .Join<Node, Kind>((n, k) => n.KindId == k.Id);
        var expected = "Select [n].[NodeId] As [Id],[n].[KindId],[n].[Name],[n].[ParentId]," +
            (automatic ? "[k].[Id] \r\nFrom [Nodes] As [n] \r\nJoin [Kinds] As [k] On [n].[KindId]=[k].[Id]"
                : "[Kinds].[Id] \r\nFrom [Nodes] As [n] \r\nJoin [Kinds] On [n].[KindId]=[Kinds].[Id]");
        Assert.Equal(expected, query.ToSql());
        if (!automatic)
        {
            var wrong = query.Clone().ExcludeSelect("k.Name");
            Assert.Throws<InvalidOperationException>(() => wrong.ToSql());
            Assert.Equal(expected, query.ToSql());
        }
    }

    /// <summary>
    /// 验证生成连接在不同别名策略与角色下匹配运行时。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GeneratedJoin_WhenPolicyAndRoleChange_ShouldMatchRuntime(bool automatic)
    {
        SqlLambdaQuery Start() => Create(new SqlOptions { AutoGenerateTableAlias = automatic })
            .From<Node>().Select<Node>(n => new { n.Id });
        var runtime = Start().Join<Node, Kind>((n, k) => n.KindId == k.Id);
        var generated = Start();
        SqlGeneratedQueryBridge.Join(generated, false, typeof(Node), typeof(Kind), "n", "k", "KindId", "Id", null, null);
        var expected = automatic
            ? "Select [n].[NodeId] As [Id] \r\nFrom [Nodes] As [n] \r\nJoin [Kinds] As [k] On [n].[KindId]=[k].[Id]"
            : "Select [Nodes].[NodeId] As [Id] \r\nFrom [Nodes] \r\nJoin [Kinds] On [Nodes].[KindId]=[Kinds].[Id]";
        Assert.Equal(expected, runtime.ToSql());
        Assert.Equal(expected, generated.ToSql());
    }

    /// <summary>
    /// 提供Lambda 关联推断测试使用的实体记录。
    /// </summary>
    [Table("Nodes")]
    private sealed class Node
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, Column("NodeId")] public int Id { get; set; }
        /// <summary>
        /// 获取或设置父记录标识。
        /// </summary>
        public int ParentId { get; set; }
        /// <summary>
        /// 获取或设置关联类别标识。
        /// </summary>
        public int KindId { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 提供Lambda 关联推断测试使用的实体记录。
    /// </summary>
    [Table("Nodes")]
    private sealed class OtherNode {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key, Column("NodeId")] public int Id { get; set; } }

    /// <summary>
    /// 提供Lambda 关联推断测试使用的实体记录。
    /// </summary>
    [Table("Kinds")]
    private sealed class Kind
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key] public int Id { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// 提供Lambda 关联推断测试使用的查询数据模型。
    /// </summary>
    private sealed class Roles {
        /// <summary>
        /// 获取或设置第一项投影结果。
        /// </summary>
        public string First { get; set; }
        /// <summary>
        /// 获取或设置第二项投影结果。
        /// </summary>
        public string Second { get; set; } }
}
