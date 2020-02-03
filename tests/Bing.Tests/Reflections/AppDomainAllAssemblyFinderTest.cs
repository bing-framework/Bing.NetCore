using Bing.Reflections;
using Bing.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Reflections
{
    /// <summary>
    /// 应用程序目录程序集查找器测试
    /// </summary>
    public class AppDomainAllAssemblyFinderTest : TestBase
    {
        /// <summary>
        /// 所有程序集查找器
        /// </summary>
        private readonly IAllAssemblyFinder _allAssemblyFinder;

        /// <summary>
        /// 初始化一个<see cref="AppDomainAllAssemblyFinderTest"/>类型的实例
        /// </summary>
        public AppDomainAllAssemblyFinderTest(ITestOutputHelper output) : base(output)
        {
            _allAssemblyFinder = new AppDomainAllAssemblyFinder();
        }

        /// <summary>
        /// 查找类型集合
        /// </summary>
        [Fact]
        public void Test_Find()
        {
            var assemblies = _allAssemblyFinder.FindAll();
            assemblies.ForEach(x =>
            {
                Output.WriteLine(x.FullName);
            });
        }
    }
}
