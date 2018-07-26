using Bing.Sequence.Abstractions;
using Bing.Sequence.Snowflake;
using Bing.Utils.Develops;
using Xunit.Abstractions;

namespace Bing.Sequence.Tests
{
    public class TestBase
    {
        protected ITestOutputHelper Output;

        public TestBase(ITestOutputHelper output)
        {
            Output = output;
            UnitTester.WriteLine = Output.WriteLine;
            CodeRamer.WriteLine = Output.WriteLine;
            CodeTimer.WriteLine = Output.WriteLine;
        }

        /// <summary>
        /// 获取雪花算法序列号生成器
        /// </summary>
        /// <returns></returns>
        protected ISequence GetSnowflakeSequence()
        {
            return SnowflakeSequenceBuilder.Create(1, 2).Build();
        }
    }
}
