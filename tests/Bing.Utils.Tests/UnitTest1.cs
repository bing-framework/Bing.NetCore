using System;
using System.Linq;
using System.Text.RegularExpressions;
using Bing.Utils.Json;
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

        [Fact]
        public void Test_Except()
        {
            var list=new string[]{"1","2","3","4","5"};
            var newList = new string[] {"1", "2"};
            var result=list.Except(newList);
            Output.WriteLine(result.ToJson());
        }

        [Fact]
        public void Test_Except_Int()
        {
            var list = new int[] {1, 3, 5, 7, 9, 11};
            var newList = new int[] {1, 4, 7,8, 9, 11};
            var result = newList.Except(list);
            Output.WriteLine(result.ToJson());
        }

        [Fact]
        public void Test_Except_Guid()
        {
            var oneGuid = Guid.NewGuid();
            var twoGuid = Guid.NewGuid();
            var threeGuid = Guid.NewGuid();
            var fourGuid = Guid.NewGuid();
            var fiveGuid = Guid.NewGuid();
            var sixGuid = Guid.NewGuid();
            var sevenGuid = Guid.NewGuid();
            var list = new Guid[] {oneGuid, twoGuid, threeGuid, fourGuid, fiveGuid, sixGuid, sevenGuid};
            var newList = new Guid[] { fourGuid, fiveGuid, sixGuid, sevenGuid };
            var result = list.Except(newList);
            Output.WriteLine(list.ToJson());
            Output.WriteLine(newList.ToJson());
            Output.WriteLine(result.ToJson());
        }

        [Fact]
        public void Test_GetDomainName()
        {
            string url1 = "https://www.cnblogs.com";
            string url2 = "https://www.cnblogs.com/";
            string url3 = "https://www.cnblogs.com/jeffwongishandsome/archive/2010/10/14/1851217.html";
            string url4 = "http://www.cnblogs.com/jeffwongishandsome/archive/2010/10/14/1851217.html";
            string url5 = "https://www.cnblogs.gz.org/";
            Output.WriteLine(GetDomainName(url1));
            Output.WriteLine(GetDomainName(url2));
            Output.WriteLine(GetDomainName(url3));
            Output.WriteLine(GetDomainName(url4));
            Output.WriteLine(GetDomainName(url5));
        }

        private string GetDomainName(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(nameof(url));
            }
            Regex regex=new Regex(@"(http|https)://(?<domain>[^(:|/]*)", RegexOptions.IgnoreCase);
            return regex.Match(url, 0).Value;
        }
    }
}

