using Bing.Data.Queries.Conditions;
using Bing.Data.Tests.Samples;
using Xunit;

namespace Bing.Data.Tests.Queries.Conditions;

/// <summary>
/// 测试与查询条件
/// </summary>
public class AndConditionTest
{
    /// <summary>
    /// 测试 - 获取查询条件
    /// </summary>
    [Fact]
    public void Test_GetCondition()
    {
        var criteria = new AndCondition<Sample>(t => t.StringValue == "a", t => t.StringValue != "b");
        Assert.Equal("t => ((t.StringValue == \"a\") AndAlso (t.StringValue != \"b\"))", criteria.GetCondition().ToString());
    }
}
