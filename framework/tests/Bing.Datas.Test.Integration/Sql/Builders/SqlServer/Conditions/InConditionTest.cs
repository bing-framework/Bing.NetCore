using System.Collections.Generic;
using Bing.Data.Sql.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Data.Test.Integration.Sql.Builders.SqlServer.Conditions
{
    /// <summary>
    /// Sql In查询条件测试
    /// </summary>
    public class InConditionTest:TestBase
    {
        public InConditionTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 1个参数
        /// </summary>
        [Fact]
        public void Test_1()
        {
            var parameters = new List<string> {"b"};
            var condition = new InCondition("a", parameters);
            Assert.Equal("a In (b)", condition.GetCondition());
        }

        /// <summary>
        /// 2个参数
        /// </summary>
        [Fact]
        public void Test_2()
        {
            var parameters = new List<string> {"b", "c"};
            var condition = new InCondition("a", parameters);
            Assert.Equal("a In (b,c)", condition.GetCondition());
        }

        /// <summary>
        /// 0个参数
        /// </summary>
        [Fact]
        public void Test_3()
        {
            var parameters = new List<string> { };
            var condition = new InCondition("a", parameters);
            Assert.Null(condition.GetCondition());
        }

        /// <summary>
        /// 验证参数为null
        /// </summary>
        [Fact]
        public void Test_4()
        {            
            var condition = new InCondition("a", null);
            Assert.Null(condition.GetCondition());
        }

        /// <summary>
        /// 验证参数名为空
        /// </summary>
        [Fact]
        public void Test_5()
        {
            var parameters = new List<string> { "b","c" };
            var condition = new InCondition("", parameters);
            Assert.Null(condition.GetCondition());
        }
    }
}
