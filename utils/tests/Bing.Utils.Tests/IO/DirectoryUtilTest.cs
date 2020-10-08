using System.IO;
using Bing.IO;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.IO
{
    /// <summary>
    /// 目录操作辅助类测试
    /// </summary>
    public class DirectoryUtilTest:TestBase
    {
        public DirectoryUtilTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test_GetFileNames()
        {
            var result = DirectoryHelper.GetFiles(Directory.GetCurrentDirectory());
            foreach (var item in result)
            {
                Output.WriteLine(item);
            }
        }
    }
}
