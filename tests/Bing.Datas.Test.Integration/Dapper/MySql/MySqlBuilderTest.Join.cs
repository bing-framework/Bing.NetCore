using Bing.Datas.Sql;
using Bing.Datas.Test.Integration.Samples;
using Bing.Utils;
using Bing.Utils.Helpers;
using Xunit;

namespace Bing.Datas.Test.Integration.Dapper.MySql
{
    /// <summary>
    /// MySql Sql生成器测试 - Join子句
    /// </summary>
    public partial class MySqlBuilderTest
    {
        /// <summary>
        /// 连接条件 - 属性表达式
        /// </summary>
        [Fact]
        public void TestOn_2()
        {
            //结果
            var result = new Str();
            result.AppendLine("Select `a` ");
            result.AppendLine("From `Sample` As `b` ");
            result.Append("Join `Sample2` As `c` On `b`.`IntValue`<>`c`.`IntValue`");

            //执行
            _builder.Select("a")
                .From<Sample>("b")
                .Join<Sample2>("c").On<Sample, Sample2>(t => t.IntValue, t => t.IntValue, Operator.NotEqual);

            //验证
            Assert.Equal(result.ToString(), _builder.ToSql());
        }
    }
}
