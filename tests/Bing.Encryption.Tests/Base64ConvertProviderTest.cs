using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Bing.Encryption.Tests
{
    /// <summary>
    /// Base64 转换提供程序 测试
    /// </summary>
    public class Base64ConvertProviderTest
    {
        [Fact]
        public void Test_Encode_1()
        {
            var result = Base64ConvertProvider.Encode("you");
            Assert.Equal("eW91", result);
        }

        [Fact]
        public void Test_Encode_2()
        {
            var result = Base64ConvertProvider.Encode("12345");
            Assert.Equal("MTIzNDU=",result);
        }

        [Fact]
        public void Test_Encode_3()
        {
            var result = Base64ConvertProvider.Encode("bhavana");
            Assert.Equal("YmhhdmFuYQ==",result);
        }

        [Fact]
        public void Test_Encode_4()
        {
            var result = Base64ConvertProvider.Encode("中国");
            Assert.Equal("5Lit5Zu9", result);
        }

        [Fact]
        public void Test_Decode_1()
        {
            var result = Base64ConvertProvider.Decode("eW91");
            Assert.Equal("you", result);
        }

        [Fact]
        public void Test_Decode_2()
        {
            var result = Base64ConvertProvider.Decode("MTIzNDU=");
            Assert.Equal("12345", result);
        }

        [Fact]
        public void Test_Decode_3()
        {
            var result = Base64ConvertProvider.Decode("YmhhdmFuYQ==");
            Assert.Equal("bhavana", result);
        }

        [Fact]
        public void Test_Decode_4()
        {
            var result = Base64ConvertProvider.Decode("5Lit5Zu9");
            Assert.Equal("中国", result);
        }

        
    }
}
