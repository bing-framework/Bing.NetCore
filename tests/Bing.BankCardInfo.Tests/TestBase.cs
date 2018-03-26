using System;
using Bing.Utils.Json;
using Xunit;
using Xunit.Abstractions;

namespace Bing.BankCardInfo.Tests
{
    public class TestBase
    {
        protected ITestOutputHelper Output;

        private readonly BankCardClient _client;

        public TestBase(ITestOutputHelper output)
        {
            Output = output;
            _client=new BankCardClient();
        }

        [Fact]
        public async void Test_Validate()
        {
            string cardNo = "6214863078427119";
            var result = await _client.ValidateAsync(cardNo);
            Output.WriteLine(result.ToJson());
        }
    }
}
