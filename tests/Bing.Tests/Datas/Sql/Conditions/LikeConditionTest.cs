using Bing.Datas.Sql.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Datas.Sql.Conditions
{
    /// <summary>
    /// Sql模糊查询条件测试
    /// </summary>
    public class LikeConditionTest:TestBase
    {
        public LikeConditionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 获取条件
        /// </summary>
        [Fact]
        public void Test_1()
        {
            var condition = new LikeCondition("Name", "@Name");
            Assert.Equal("Name Like @Name", condition.GetCondition());
        }
    }
}
