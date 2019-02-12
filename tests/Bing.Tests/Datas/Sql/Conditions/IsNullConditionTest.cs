using Bing.Datas.Sql.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Datas.Sql.Conditions
{
    /// <summary>
    /// Sql Is Null查询条件测试
    /// </summary>
    public class IsNullConditionTest:TestBase
    {
        public IsNullConditionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 获取条件
        /// </summary>
        [Fact]
        public void Test_1()
        {
            var condition = new IsNullCondition("Email");
            Assert.Equal("Email Is Null", condition.GetCondition());
        }

        /// <summary>
        /// 获取条件 - 验证列为空
        /// </summary>
        [Fact]
        public void Test_2()
        {
            var condition = new IsNullCondition("");
            Assert.Null(condition.GetCondition());
        }
    }
}
