using Xunit.Abstractions;

namespace Bing.Tests
{
    /// <summary>
    /// 测试基类
    /// </summary>
    public abstract class TestBase
    {
        /// <summary>
        /// 输出
        /// </summary>
        protected ITestOutputHelper Output { get; }

        /// <summary>
        /// 初始化一个<see cref="TestBase"/>类型的实例
        /// </summary>
        protected TestBase(ITestOutputHelper output) => Output = output;
    }
}
