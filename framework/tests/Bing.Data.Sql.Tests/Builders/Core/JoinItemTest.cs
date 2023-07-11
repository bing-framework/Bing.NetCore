using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>
/// 表连接项测试
/// </summary>
public class JoinItemTest
{
    /// <summary>
    /// 测试初始化
    /// </summary>
    public JoinItemTest()
    {
    }

    /// <summary>
    /// 测试复制
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var item = new JoinItem("join", "b");
        item.On(SqlConditionFactory.Create("a.A", "b.B", Operator.Equal));

        //复制一份
        var copy = item.Clone(null);
        Assert.Equal("join b On a.A=b.B", item.ToSql());
        Assert.Equal("join b On a.A=b.B", copy.ToSql());

        //修改副本
        copy.On(SqlConditionFactory.Create("a.C", "b.D", Operator.Equal));
        Assert.Equal("join b On a.A=b.B", item.ToSql());
        Assert.Equal("join b On a.A=b.B And a.C=b.D", copy.ToSql());
    }
}
