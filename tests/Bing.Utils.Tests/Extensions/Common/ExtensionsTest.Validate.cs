using System;
using System.Collections.Generic;
using System.Text;
using Bing.Tests;
using Bing.Utils.Extensions;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 系统扩展测试 - 验证扩展
    /// </summary>
    public partial class ExtensionsTest:TestBase
    {
        /// <summary>
        /// 检查控制，不为空则正常执行
        /// </summary>
        [Fact]
        public void Test_CheckNull()
        {
            object test = new object();
            test.CheckNull(nameof(test));            
        }
    }
}
