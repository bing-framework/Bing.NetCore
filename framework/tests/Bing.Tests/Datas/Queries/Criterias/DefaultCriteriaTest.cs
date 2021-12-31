using Bing.Data.Queries.Conditions;
using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Datas.Queries.Criterias
{
    /// <summary>
    /// 测试默认查询条件
    /// </summary>
    public class DefaultCriteriaTest
    {
        /// <summary>
        /// 测试 - 获取查询条件
        /// </summary>
        [Fact]
        public void Test_GetPredicate()
        {
            var criteria = new DefaultCondition<AggregateRootSample>(t => t.Name == "a");
            Assert.Equal("t => (t.Name == \"a\")", criteria.GetCondition().ToString());
        }
    }
}
