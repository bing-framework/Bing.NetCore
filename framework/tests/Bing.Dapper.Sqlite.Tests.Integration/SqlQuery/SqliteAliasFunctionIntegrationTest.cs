using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dapper.Tests.SqlQuery;

public sealed partial class SqliteProjectionIntegrationTest
{
    /// <summary>
    /// 创建指定自动别名模式的 SQLite 测试容器。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    /// <returns>注册了当前测试依赖的服务容器。</returns>
    private ServiceProvider CreateAliasProvider(bool automatic)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new SqlOptions<SqliteSqlQuery> { AutoGenerateTableAlias = automatic });
        services.AddSqlCore();
        services.AddSqlDataSource("default", DatabaseType.Sqlite, _fixture.FirstConnectionString);
        services.AddSqliteProvider();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 三种语法和两种配置均在真实 SQLite 中得到相同分组结果。
    /// </summary>
    /// <param name="automatic">是否自动生成表别名。</param>
    /// <param name="syntax">用于选择聚合表达式写法的场景序号。</param>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task Aggregate_WhenSyntaxAndAliasPolicyVary_ShouldExecuteSameSummary(bool automatic, int syntax)
    {
        using var provider = CreateAliasProvider(automatic);
        using var root = provider.GetRequiredService<ISqlQueryFactory>().Create();
        var query = root.From<Order>().Join<Order, Product>((o, p) => o.ProductNo == p.Id)
            .Select<Product>(p => new { Name = p.Name });
        if (syntax == 0) query.Select<Order>(o => new { OrderCount = SqlFn.Count(o.Quantity), TotalQuantity = SqlFn.Sum(o.Quantity) });
        if (syntax == 1) query.Select<Order>((o, f) => new { OrderCount = f.Count(o.Quantity), TotalQuantity = f.Sum(o.Quantity) });
        if (syntax == 2) query.Select<Order>((o, f) => new { OrderCount = f.Count(x => x.Quantity), TotalQuantity = f.Sum(x => x.Quantity) });
        query.GroupBy<Product>(p => p.Name).Having<Order>(o => SqlFn.Count(o.Quantity) > 0L)
            .OrderBy<Order>(o => SqlFn.Sum(o.Quantity), true);
        var order = automatic ? "o" : "sqp_orders";
        var product = automatic ? "p" : "sqp_products";
        var expected = $"Select `{product}`.`Name` As `Name`,Count(`{order}`.`Quantity`) As `OrderCount`,Sum(`{order}`.`Quantity`) As `TotalQuantity` \r\n" +
            $"From `sqp_orders`{(automatic ? " As `o`" : "")} \r\n" +
            $"Join `sqp_products`{(automatic ? " As `p`" : "")} On `{order}`.`ProductNo`=`{product}`.`ProductNo` \r\n" +
            $"Group By `{product}`.`Name` Having Count(`{order}`.`Quantity`)>@_p_0 \r\nOrder By Sum(`{order}`.`Quantity`) Desc";
        Assert.Equal(expected, query.ToSql());
        var rows = await query.ToListAsync<OrderSummary>();
        var summary = Assert.Single(rows);
        Assert.Equal("Widget 101", summary.Name);
        Assert.Equal(2, summary.OrderCount);
        Assert.Equal(12, summary.TotalQuantity);
        Assert.Equal(expected, query.Clone().ToSql());
    }

    /// <summary>
    /// 验证空值或空输入时列计数与行计数语义不同。
    /// </summary>
    /// <param name="empty">是否使用空的输入数据或内容。</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Aggregate_WhenInputIsNullOrEmpty_ShouldDistinguishColumnCountFromRowCount(bool empty)
    {
        using var root = _fixture.CreateQuery();
        var id = empty ? -1 : 3;
        var query = root.From<Order>().Where<Order>(o => o.Id == id)
            .Select<Order>(o => new NullCounts
            {
                Rows = SqlFn.CountAll(),
                NonNull = SqlFn.Count(o.Quantity),
                Total = SqlFn.Sum(o.Quantity)
            });
        Assert.Equal("Select Count(*) As `Rows`,Count(`o`.`Quantity`) As `NonNull`,Sum(`o`.`Quantity`) As `Total` \r\nFrom `sqp_orders` As `o` \r\nWhere `o`.`OrderId`=@_p_0", query.ToSql());
        var row = Assert.Single(await query.ToListAsync<NullCounts>());
        Assert.Equal(empty ? 0L : 1L, row.Rows);
        Assert.Equal(0L, row.NonNull);
        Assert.Null(row.Total);
    }

    /// <summary>
    /// 验证无显式别名的三个同类型角色正确读取层级关系。
    /// </summary>
    [Fact]
    public async Task Join_WhenSameTypeHasThreeRoles_ShouldExecuteCorrectHierarchyWithoutAliasStrings()
    {
        await using (var connection = new SqliteConnection(_fixture.FirstConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = connection.CreateCommand();
            seed.CommandText = "Create Table If Not Exists sqp_people(Id Integer Primary Key, ParentId Integer Null, Name Text); Delete From sqp_people; Insert Into sqp_people Values(1,2,'employee'),(2,3,'manager'),(3,NULL,'director');";
            await seed.ExecuteNonQueryAsync();
        }
        using var root = _fixture.CreateQuery();
        var query = root.From<Person>()
            .LeftJoin<Person, Person>((employee, manager) => employee.ParentId == manager.Id)
            .LeftJoin<Person, Person>((manager, director) => manager.ParentId == director.Id)
            .Select<Person, Person, Hierarchy>((employee, director) => new Hierarchy { Id = employee.Id, Director = director.Name })
            .Select<Person>(manager => new { Manager = manager.Name })
            .OrderBy<Person>(employee => employee.Id);
        const string expected = "Select `employee`.`Id` As `Id`,`director`.`Name` As `Director`,`manager`.`Name` As `Manager` \r\nFrom `sqp_people` As `employee` \r\nLeft Join `sqp_people` As `manager` On `employee`.`ParentId`=`manager`.`Id` \r\nLeft Join `sqp_people` As `director` On `manager`.`ParentId`=`director`.`Id` \r\nOrder By `employee`.`Id`";
        Assert.Equal(expected, query.ToSql());
        var rows = await query.ToListAsync<Hierarchy>();
        Assert.Equal(3, rows.Count);
        Assert.Equal((1, "manager", "director"), (rows[0].Id, rows[0].Manager, rows[0].Director));
        Assert.Equal((2, "director", (string)null), (rows[1].Id, rows[1].Manager, rows[1].Director));
        Assert.Null(rows[2].Manager);
        Assert.Null(rows[2].Director);
    }

    /// <summary>
    /// 验证同右类型的两个角色无需别名字符串即可分别读取。
    /// </summary>
    [Fact]
    public async Task Join_WhenSameRightTypeHasTwoRoles_ShouldReadEachRoleWithoutAliasStrings()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Product>()
            .Join<Product, Order>((product, first) => product.Id == first.ProductNo)
            .Join<Product, Order>((product, second) => product.Id == second.ProductNo)
            .Select<Order, Order, RolePair>((first, second) => new RolePair { First = first.Id, Second = second.Id })
            .OrderBy<Order>(first => first.Id).OrderBy<Order>(second => second.Id);
        Assert.Equal("Select `first`.`OrderId` As `First`,`second`.`OrderId` As `Second` \r\nFrom `sqp_products` As `product` \r\nJoin `sqp_orders` As `first` On `product`.`ProductNo`=`first`.`ProductNo` \r\nJoin `sqp_orders` As `second` On `product`.`ProductNo`=`second`.`ProductNo` \r\nOrder By `first`.`OrderId`,`second`.`OrderId`", query.ToSql());
        var rows = await query.ToListAsync<RolePair>();
        Assert.Equal(new[] { (1, 1), (1, 2), (2, 1), (2, 2), (3, 3) }, rows.Select(row => (row.First, row.Second)));
    }

    /// <summary>
    /// 验证未映射的聚合结果属性在 SQL 执行前被拒绝。
    /// </summary>
    [Fact]
    public void Aggregate_WhenResultPropertyIsNotMapped_ShouldRejectBeforeExecutingSql()
    {
        using var root = _fixture.CreateQuery();
        var query = root.From<Order>().Select<Order>(o => new { OrderCount = SqlFn.CountAll() });
        Assert.Throws<InvalidOperationException>(() => query.ToList<IgnoredCount>());
        Assert.Equal("Select Count(*) As `OrderCount` \r\nFrom `sqp_orders` As `o`", query.ToSql());
    }

    /// <summary>
    /// 汇总 SQLite 别名查询中的行数、非空数和总值。
    /// </summary>
    public sealed class NullCounts {
        /// <summary>
        /// 获取或设置输入记录的总行数。
        /// </summary>
        public long Rows { get; set; }
        /// <summary>
        /// 获取或设置非空值数量。
        /// </summary>
        public long NonNull { get; set; }
        /// <summary>
        /// 获取或设置非空值的合计。
        /// </summary>
        public int? Total { get; set; } }
    /// <summary>
    /// 表示 SQLite 递归关系查询中的人员记录。
    /// </summary>
    [Table("sqp_people")]
    public sealed class Person {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key] public int Id { get; set; }
        /// <summary>
        /// 获取或设置父记录标识。
        /// </summary>
        public int? ParentId { get; set; }
        /// <summary>
        /// 获取或设置实体或结果名称。
        /// </summary>
        public string Name { get; set; } }
    /// <summary>
    /// 表示 SQLite 别名查询中的经理与主管关系。
    /// </summary>
    public sealed class Hierarchy {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 获取或设置关联的经理记录。
        /// </summary>
        public string Manager { get; set; }
        /// <summary>
        /// 获取或设置关联的主管记录。
        /// </summary>
        public string Director { get; set; } }
    /// <summary>
    /// 承载 SQLite 查询返回的两个角色值。
    /// </summary>
    public sealed class RolePair {
        /// <summary>
        /// 获取或设置第一项角色投影值。
        /// </summary>
        public int First { get; set; }
        /// <summary>
        /// 获取或设置第二项角色投影值。
        /// </summary>
        public int Second { get; set; } }
    /// <summary>
    /// 承载验证忽略列行为的订单计数结果。
    /// </summary>
    public sealed class IgnoredCount {
        /// <summary>
        /// 获取或设置汇总范围内的订单数量。
        /// </summary>
        [NotMapped] public long OrderCount { get; set; } }
}
