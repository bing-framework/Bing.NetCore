using System.Collections.Generic;
using Bing.Utils.Extensions;
using Shouldly;
using Xunit;

namespace Bing.Utils.Tests.Extensions
{
    /// <summary>
    /// 集合 扩展测试
    /// </summary>
    public class CollectionExtensionsTest
    {
        /// <summary>
        /// 测试添加项 - 单个添加
        /// </summary>
        [Fact]
        public void Test_AddIfNotContains_With_Single()
        {
            var collection = new List<int> { 4, 5, 6 };

            collection.AddIfNotContains(7);
            collection.Count.ShouldBe(4);

            collection.AddIfNotContains(7);
            collection.Count.ShouldBe(4);

            collection.AddIfNotContains(4);
            collection.Count.ShouldBe(4);

            collection.AddIfNotContains(8);
            collection.Count.ShouldBe(5);
        }

        /// <summary>
        /// 测试添加项 - 集合添加
        /// </summary>
        [Fact]
        public void Test_AddIfNotContains_With_Collection()
        {
            var collection = new List<int> { 4, 5, 6 };
            var one= new List<int> { 4, 5, 6 };
            var two = new List<int> { 6, 7, 8 };
            var three = new List<int> { 9, 10, 11 };

            collection.AddIfNotContains(one);
            collection.Count.ShouldBe(3);

            collection.AddIfNotContains(two);
            collection.Count.ShouldBe(5);

            collection.AddIfNotContains(three);
            collection.Count.ShouldBe(8);
        }

        /// <summary>
        /// 测试添加项 - 根据条件
        /// </summary>
        [Fact]
        public void Test_AddIfNotContains_With_Predicate()
        {
            var collection = new List<int>{ 4,5,6 };

            collection.AddIfNotContains(x => x == 5, () => 5);
            collection.Count.ShouldBe(3);

            collection.AddIfNotContains(x => x == 42, () => 42);
            collection.Count.ShouldBe(4);

            collection.AddIfNotContains(x => x < 8, () => 8);
            collection.Count.ShouldBe(4);

            collection.AddIfNotContains(x => x > 999, () => 8);
            collection.Count.ShouldBe(5);
        }

        /// <summary>
        /// 测试移除项 - 移除集合
        /// </summary>
        [Fact]
        public void Test_RemoveAll_With_Collection()
        {
            var collection = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var one = new List<int> { 1, 2, 3 };
            var two = new List<int> { 3, 4, 5 };

            collection.RemoveAll(one);
            collection.Count.ShouldBe(6);

            collection.RemoveAll(two);
            collection.Count.ShouldBe(4);
        }

        /// <summary>
        /// 测试移除项 - 根据条件
        /// </summary>
        [Fact]
        public void Test_RemoveAll_With_Predicate()
        {
            var collection = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            collection.RemoveAll<int>(x => x == 1);
            collection.Count.ShouldBe(8);

            collection.RemoveAll<int>(x => x <= 5);
            collection.Count.ShouldBe(4);
        }
    }
}
