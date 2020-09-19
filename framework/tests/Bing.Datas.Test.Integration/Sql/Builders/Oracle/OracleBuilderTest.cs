using Bing.Datas.Dapper.Oracle;
using Bing.Data.Sql;
using Bing.Data.Test.Integration.Samples;
using Bing.Utils;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;
using Str = Bing.Helpers.Str;

namespace Bing.Data.Test.Integration.Sql.Builders.Oracle
{
    /// <summary>
    /// Oracle Sql生成器测试
    /// </summary>
    public class OracleBuilderTest : TestBase
    {
        /// <summary>
        /// Oracle Sql生成器s
        /// </summary>
        private OracleBuilder _builder;

        /// <summary>
        /// 初始化一个<see cref="OracleBuilderTest"/>类型的实例
        /// </summary>
        public OracleBuilderTest(ITestOutputHelper output) : base(output)
        {
            _builder = new OracleBuilder();
        }

        /// <summary>
        /// 设置条件 - 属性表达式
        /// </summary>
        [Fact]
        public void TestWhere()
        {
            //结果
            var result = new Str();
            result.AppendLine("Select \"a\".\"Email\" ");
            result.AppendLine("From \"Sample\" \"a\" ");
            result.Append("Where \"a\".\"Email\"<>:p_0");

            //执行
            _builder.Select<Sample>(t => new object[] { t.Email })
                .From<Sample>("a")
                .Where<Sample>(t => t.Email, "abc", Operator.NotEqual);

            //验证
            Assert.Equal(result.ToString(), _builder.ToSql());
            Assert.Single(_builder.GetParams());
            Assert.Equal("abc", _builder.GetParams()["p_0"]);
        }
    }
}
