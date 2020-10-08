using Bing.Reflection;
using Bing.Tests.Samples;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    public class ReflectionsTest : TestBase
    {
        /// <summary>
        /// 测试样例
        /// </summary>
        private readonly Sample _sample;

        /// <summary>
        /// 初始化一个<see cref="TestBase"/>类型的实例
        /// </summary>
        public ReflectionsTest(ITestOutputHelper output) : base(output)
        {
            _sample = new Sample();
        }

        /// <summary>
        /// 测试 - 获取类成员描述
        /// </summary>
        [Fact]
        public void Test_GetDescription()
        {
            Assert.Equal("", Reflections.GetDescription<EnumSample>("X"));
            Assert.Equal("A", Reflections.GetDescription<EnumSample>("A"));
            Assert.Equal("B2", Reflections.GetDescription<EnumSample>("B"));
            Assert.Equal("IntValue", Reflections.GetDescription<Sample>("IntValue"));
        }
    }
}
