using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Bing.Extensions.Tests
{
    /// <summary>
    /// 程序集 扩展测试
    /// </summary>
    public class AssemblyExtensionsTest
    {
        /// <summary>
        /// 测试获取程序集文件版本
        /// </summary>
        [Fact]
        public void Test_GetFileVersion()
        {
            var assembly = typeof(AssemblyExtensionsTest).Assembly;
            var result = assembly.GetFileVersion();
            Assert.Equal("1.0.0.0",result.ToString());
        }

        /// <summary>
        /// 测试获取程序集产品版本
        /// </summary>
        [Fact]
        public void Test_GetProductVersion()
        {
            var assembly = typeof(AssemblyExtensionsTest).Assembly;
            var result = assembly.GetProductVersion();
            Assert.Equal("1.0.0", result.ToString());
        }
    }
}
