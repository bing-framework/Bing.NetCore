using Bing.Datas.Sql.Queries.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Datas.Sql.Conditions
{
    /// <summary>
    /// Sql大于等于查询条件测试
    /// </summary>
    public class GreaterEqualConditionTest:TestBase
    {
        public GreaterEqualConditionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 获取条件
        /// </summary>
        [Fact]
        public void Test_1()
        {
            var condition = new GreaterEqualCondition("Age", "@Age");
            Assert.Equal("Age>=@Age", condition.GetCondition());
        }
    }
}
