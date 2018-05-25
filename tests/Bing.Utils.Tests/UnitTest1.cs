using System;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests
{
    public class UnitTest1: TestBase
    {
        public UnitTest1(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test1()
        {
            DateTimeOffset offset=DateTimeOffset.Now;
            Output.WriteLine(offset.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [Fact]
        public void Test_GetArrayLowerBound()
        {
            var strs = new string[] {"1", "2", "3", "4", "5"};
            var lowerBound = strs.GetLowerBound(0);
            Output.WriteLine(lowerBound.ToString());
        }

        [Fact]
        public void Test_GetArrayLowerBound_2()
        {            
            string[,] strs = new string[,] {
                {
                    "1","2","3","4","7"
                },
                {
                    "2","3","4","5","6"
                }
            };
            var lowerBound = strs.GetLowerBound(1);
            Output.WriteLine(lowerBound.ToString());
            var upperBound = strs.GetUpperBound(1);
            Output.WriteLine(upperBound.ToString());
        }

        [Fact]
        public void Test_GetArrayUpperBound()
        {
            var strs = new string[] { "1", "2", "3", "4", "5" };
            var upperBound = strs.GetUpperBound(0);
            Output.WriteLine(upperBound.ToString());
        }
    }
}
