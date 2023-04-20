using Bing.Data.Queries.Conditions;
using Bing.Data.Tests.Samples;
using Xunit;

namespace Bing.Data.Tests.Queries.Conditions;

/// <summary>
/// 测试或查询条件
/// </summary>
public class OrCriteriaTest
{
    /// <summary>
    /// 测试 - 获取查询条件
    /// </summary>
    [Fact]
    public void Test_GetCondition()
    {
        var criteria = new OrCondition<Sample>(t => t.StringValue == "a", t => t.StringValue != "b");
        Assert.Equal("t => ((t.StringValue == \"a\") OrElse (t.StringValue != \"b\"))", criteria.GetCondition().ToString());
    }
}
