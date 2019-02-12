using Bing.Datas.Sql.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Datas.Sql.Conditions
{
    /// <summary>
    /// Sql小于查询条件测试
    /// </summary>
    public class LessConditionTest:TestBase
    {
        public LessConditionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 获取条件
        /// </summary>
        [Fact]
        public void Test_1()
        {
            var condition = new LessCondition("Age", "@Age");
            Assert.Equal("Age<@Age", condition.GetCondition());
        }
    }
}
