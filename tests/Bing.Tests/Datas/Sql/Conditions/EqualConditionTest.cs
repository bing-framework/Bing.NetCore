using Bing.Datas.Sql.Queries.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Datas.Sql.Conditions
{
    /// <summary>
    /// Sql相同查询条件测试
    /// </summary>
    public class EqualConditionTest:TestBase
    {
        public EqualConditionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 获取条件
        /// </summary>
        [Fact]
        public void Test_1()
        {
            var condition = new EqualCondition("Name", "@Name");
            Assert.Equal("Name=@Name", condition.GetCondition());
        }
    }
}
