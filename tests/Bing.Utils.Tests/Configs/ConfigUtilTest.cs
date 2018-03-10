using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Configs;
using Bing.Utils.Tests.Samples;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bing.Utils.Tests.Configs
{

    public class ConfigUtilTest
    {
        [Fact]
        public void Test_GetJsonConfig()
        {
            var config = ConfigUtil.GetJsonConfig("sampleConfig.json", "/Configs")
                .GetSection("Sample")
                .Get<SampleConfig>();
            Assert.Equal("TestSample",config.StringValue);
            Assert.Equal(20,config.DecimalValue);            
            Assert.Equal(1, config.IntValue);
        }
    }
}
