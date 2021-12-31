using Bing.Data.Queries.Conditions;
using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Datas.Queries.Criterias
{
    /// <summary>
    /// 测试与查询条件
    /// </summary>
    public class AndCriteriaTest
    {
        /// <summary>
        /// 测试 - 获取查询条件
        /// </summary>
        [Fact]
        public void Test_GetPredicate()
        {
            var criteria = new AndCondition<AggregateRootSample>(t => t.Name == "a", t => t.Name != "b");
            Assert.Equal("t => ((t.Name == \"a\") AndAlso (t.Name != \"b\"))", criteria.GetCondition().ToString());
        }
    }
}
