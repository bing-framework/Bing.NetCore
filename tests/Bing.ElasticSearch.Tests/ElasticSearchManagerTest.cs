using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Bing.ElasticSearch.Tests
{
    public class ElasticSearchManagerTest:TestBase
    {
        private IElasticSearchManager _manager;
        public ElasticSearchManagerTest(ITestOutputHelper output) : base(output)
        {
            _manager = CreateManager();
        }

        [Fact]
        public async Task Test_CreateIndex()
        {
            await _manager.CreateIndexAsync("test");
        }
    }
}
