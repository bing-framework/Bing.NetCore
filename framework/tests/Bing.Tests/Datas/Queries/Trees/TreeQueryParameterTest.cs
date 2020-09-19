using Bing.Data.Queries;
using Xunit;

namespace Bing.Tests.Datas.Queries.Trees
{
    /// <summary>
    /// 树型查询参数测试
    /// </summary>
    public class TreeQueryParameterTest
    {
        /// <summary>
        /// 树型查询参数
        /// </summary>
        private readonly TreeQueryParameter _parameter;

        /// <summary>
        /// 初始化一个<see cref="TreeQueryParameterTest"/>类型的实例
        /// </summary>
        public TreeQueryParameterTest() => _parameter = new TreeQueryParameter();

        /// <summary>
        /// 测试 - 是否搜索 - 默认false
        /// </summary>
        [Fact]
        public void Test_IsSearch_Default()
        {
            Assert.False(_parameter.IsSearch());
        }

        /// <summary>
        /// 测试 - 是否搜索 - 忽略排序
        /// </summary>
        [Fact]
        public void Test_IsSearch_Ignore_Order()
        {
            _parameter.Order = "a";
            Assert.False(_parameter.IsSearch());
        }

        /// <summary>
        /// 测试 - 是否搜索 - 忽略分页大小
        /// </summary>
        [Fact]
        public void Test_IsSearch_Ignore_PageSize()
        {
            _parameter.PageSize = 10;
            Assert.False(_parameter.IsSearch());
        }

        /// <summary>
        /// 测试 - 是否搜索 - 忽略分页
        /// </summary>
        [Fact]
        public void Test_IsSearch_Ignore_Page()
        {
            _parameter.Page = 10;
            Assert.False(_parameter.IsSearch());
        }

        /// <summary>
        /// 测试 - 是否搜索 - 忽略总行数
        /// </summary>
        [Fact]
        public void Test_IsSearch_Ignore_TotalCount()
        {
            _parameter.TotalCount = 10;
            Assert.False(_parameter.IsSearch());
        }

        /// <summary>
        /// 测试 - 是否搜索 - 启用
        /// </summary>
        [Fact]
        public void Test_IsSearch_Enabled()
        {
            _parameter.Enabled = true;
            Assert.True(_parameter.IsSearch());
        }

        /// <summary>
        /// 测试 - 是否搜索 - 搜索关键字
        /// </summary>
        [Fact]
        public void Test_IsSearch_Keyword()
        {
            _parameter.Keyword = "a";
            Assert.True(_parameter.IsSearch());
        }
    }
}
