using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Extensions;
using Xunit;

namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 可枚举类型 扩展测试
    /// </summary>
    public class EnumerableExtensionsTest
    {
        /// <summary>
        /// 测试 - 展开集合并转换为字符串
        /// </summary>
        [Fact]
        public void Test_ExpandAndToString()
        {
            var list = new List<int>() { 1, 2, 3, 4, 5, 6 };
            var result = "1,2,3,4,5,6";
            Assert.Equal(list.ExpandAndToString(), result);
        }

        /// <summary>
        /// 测试 - 展开集合并转换为字符串 - 带有项目包裹符
        /// </summary>
        [Fact]
        public void Test_ExpandAndToString_With_WrapItem()
        {
            var list = new List<int>() { 1, 2, 3, 4, 5, 6 };
            var result = "'1','2','3','4','5','6'";
            Assert.Equal(list.ExpandAndToString(wrapItem: "'"), result);
        }
    }
}
