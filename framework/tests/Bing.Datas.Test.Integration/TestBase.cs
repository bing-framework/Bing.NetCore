using Xunit.Abstractions;

namespace Bing.Datas.Test.Integration
{
    public class TestBase
    {
        protected ITestOutputHelper Output;

        public TestBase(ITestOutputHelper output)
        {
            Output = output;
        }
    }
}
