using Xunit.Abstractions;

namespace Bing.Utils.Tests
{
    /// <summary>
    /// 测试基类
    /// </summary>
    public class TestBase
    {
        /// <summary>
        /// 输出
        /// </summary>
        protected ITestOutputHelper Output;

        /// <summary>
        /// 初始化一个<see cref="TestBase"/>类型的实例
        /// </summary>
        public TestBase(ITestOutputHelper output) => Output = output;
    }
}
