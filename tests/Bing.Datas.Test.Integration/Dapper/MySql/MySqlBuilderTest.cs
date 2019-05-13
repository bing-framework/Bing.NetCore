using Bing.Datas.Dapper.MySql;
using Xunit.Abstractions;

namespace Bing.Datas.Test.Integration.Dapper.MySql
{
    /// <summary>
    /// MySql Sql生成器测试
    /// </summary>
    public partial class MySqlBuilderTest
    {
        /// <summary>
        /// 输出工具
        /// </summary>
        private readonly ITestOutputHelper _output;
        /// <summary>
        /// MySql Sql生成器
        /// </summary>
        private MySqlBuilder _builder;

        public MySqlBuilderTest(ITestOutputHelper output)
        {
            _output = output;
            _builder = new MySqlBuilder();
        }
    }
}
