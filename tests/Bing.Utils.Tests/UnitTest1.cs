using System;
using Xunit;

namespace Bing.Utils.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            DateTimeOffset offset=DateTimeOffset.Now;
            Console.WriteLine(offset.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
