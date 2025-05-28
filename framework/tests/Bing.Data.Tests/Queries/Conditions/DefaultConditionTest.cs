using Bing.Data.Queries.Conditions;
using Bing.Data.Tests.Samples;
using Xunit;

namespace Bing.Data.Tests.Queries.Conditions;

/// <summary>
/// 测试默认查询条件
/// </summary>
public class DefaultConditionTest
{
    /// <summary>
    /// 测试 - 获取查询条件
    /// </summary>
    [Fact]
    public void Test_GetCondition()
    {
        var criteria = new DefaultCondition<Sample>(t => t.StringValue == "a");
        Assert.Equal("t => (t.StringValue == \"a\")", criteria.GetCondition().ToString());
    }
}
