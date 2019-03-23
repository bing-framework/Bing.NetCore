using Bing.Datas.Sql.Builders.Core;
using Bing.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Datas.Sql
{
    /// <summary>
    /// 表连接项测试
    /// </summary>
    public class JoinItemTest:TestBase
    {
        public JoinItemTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_1()
        {
            var item = new JoinItem("join", "b");
            item.On("a.A", "b.B", Operator.Equal);

            // 复制一份
            var copy = item.Clone(null);
            Assert.Equal("join b On a.A=b.B", item.ToSql());
            Assert.Equal("join b On a.A=b.B", copy.ToSql());

            // 修改副本
            copy.On("a.C", "b.D", Operator.Equal);
            Assert.Equal("join b On a.A=b.B", item.ToSql());
            Assert.Equal("join b On a.A=b.B And a.C=b.D", copy.ToSql());
        }
    }
}
