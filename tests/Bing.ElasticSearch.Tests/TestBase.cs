using Bing.ElasticSearch.Configs;
using Bing.Utils.Develops;
using Xunit.Abstractions;

namespace Bing.ElasticSearch.Tests
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

        protected IElasticSearchManager CreateManager()
        {
            IElasticSearchManager manager=new ElasticSearchManager(GetConfig());
            return manager;
        }

        protected IElasticSearchConfigProvider GetConfig()
        {
            IElasticSearchConfigProvider provider=new DefaultElasticSearchConfigProvider(new ElasticSearchConfig()
            {
                ConnectionString = "http://192.168.205.129:9200",
                //UserName = "",
                //Password = ""
            });
            return provider;
        }
    }
}
