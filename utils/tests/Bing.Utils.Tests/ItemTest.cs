using System.Collections.Generic;
using System.Linq;
using Bing.Utils.Json;
using Shouldly;
using Xunit;

namespace Bing.Utils.Tests
{
    public class ItemTest
    {
        /// <summary>
        /// 测试 - 无效值为空
        /// </summary>
        [Fact]
        public void Test_Null()
        {
            var json = "[{\"text\":\"Test\",\"value\":\"666\"}]";
            var items = JsonHelper.ToObject<List<Item>>(json);
            var item = items.FirstOrDefault();
            Assert.NotNull(item);
            Assert.Equal("Test", item.Text);
            Assert.Equal("666", item.Value);
            Assert.Null(item.SortId);
            Assert.Null(item.Group);
            Assert.Null(item.Disabled);
        }

        /// <summary>
        /// 测试 - 不包含空值
        /// </summary>
        [Fact]
        public void Test_NotContainsNull()
        {
            var items = new List<Item> {new Item("Test", "666")};
            var json = JsonHelper.ToJson(items);
            json.ShouldNotContain("sortId");
            json.ShouldNotContain("group");
            json.ShouldNotContain("disabled");
            json.ShouldNotContain("SortId");
            json.ShouldNotContain("Group");
            json.ShouldNotContain("Disabled");
        }
    }
}
