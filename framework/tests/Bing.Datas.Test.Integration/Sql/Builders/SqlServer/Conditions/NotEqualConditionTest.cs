using Bing.Datas.Sql.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Datas.Test.Integration.Sql.Builders.SqlServer.Conditions
{
    /// <summary>
    /// Sql不相等查询条件测试
    /// </summary>
    public class NotEqualConditionTest:TestBase
    {
        public NotEqualConditionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 获取条件
        /// </summary>
        [Fact]
        public void Test_1()
        {
            var condition = new NotEqualCondition("Name", "@Name");
            Assert.Equal("Name<>@Name", condition.GetCondition());
        }
    }
}
