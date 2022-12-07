using Bing.Data.Queries.Conditions;
using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Datas.Queries.Criterias;

/// <summary>
/// 测试或查询条件
/// </summary>
public class OrCriteriaTest
{
    /// <summary>
    /// 测试 - 获取查询条件
    /// </summary>
    [Fact]
    public void Test_GetPredicate()
    {
        var criteria = new OrCondition<AggregateRootSample>(t => t.Name == "a", t => t.Name != "b");
        Assert.Equal("t => ((t.Name == \"a\") OrElse (t.Name != \"b\"))", criteria.GetCondition().ToString());
    }
}