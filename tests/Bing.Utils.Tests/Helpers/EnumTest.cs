using System;
using System.Linq;
using Bing.Tests;
using Bing.Tests.Samples;
using Bing.Tests.XUnitHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// 枚举操作测试
    /// </summary>
    public class EnumTest : TestBase
    {
        /// <summary>
        /// 初始化一个<see cref="EnumTest"/>类型的实例
        /// </summary>
        public EnumTest(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// 测试获取枚举实例
        /// </summary>
        [Theory]
        [InlineData("C", EnumSample.C)]
        [InlineData("3", EnumSample.C)]
        public void Test_Parse(string member, EnumSample sample)
        {
            Assert.Equal(sample, Bing.Utils.Helpers.Enum.Parse<EnumSample>(member));
        }

        /// <summary>
        /// 测试获取枚举实例 - 参数为空，抛出异常
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Test_Parse_MemberIsEmpty(string member)
        {
            AssertHelper.Throws<ArgumentNullException>(() =>
            {
                Bing.Utils.Helpers.Enum.Parse<EnumSample>(member);
            }, "member");
        }

        /// <summary>
        /// 测试获取枚举实例 - 可空枚举
        /// </summary>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData(" ", null)]
        [InlineData("C", EnumSample.C)]
        [InlineData("3", EnumSample.C)]
        public void Test_Parse_Nullable(string member, EnumSample? sample)
        {
            Assert.Equal(sample, Bing.Utils.Helpers.Enum.Parse<EnumSample?>(member));
        }

        /// <summary>
        /// 测试通过描述获取实例
        /// </summary>
        [Theory]
        [InlineData("B2",EnumSample.B)]
        [InlineData("C3", EnumSample.C)]
        [InlineData("D4", EnumSample.D)]
        [InlineData("E5", EnumSample.E)]
        public void Test_ParseByDescription(string desc, EnumSample sample)
        {
            Assert.Equal(sample, Bing.Utils.Helpers.Enum.ParseByDescription<EnumSample>(desc));
        }

        /// <summary>
        /// 测试通过描述获取实例 - 参数为空，抛出异常
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Test_ParseByDescription_MemberIsEmpty(string desc)
        {
            AssertHelper.Throws<ArgumentNullException>(() =>
            {
                Bing.Utils.Helpers.Enum.ParseByDescription<EnumSample>(desc);
            }, "desc");
        }

        /// <summary>
        /// 测试通过描述获取实例 - 可空枚举
        /// </summary>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData(" ", null)]
        [InlineData("C3", EnumSample.C)]
        public void Test_ParseByDescription_Nullable(string member, EnumSample? sample)
        {
            Assert.Equal(sample, Bing.Utils.Helpers.Enum.ParseByDescription<EnumSample?>(member));
        }

        /// <summary>
        /// 测试获取枚举成员名
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", " ")]
        [InlineData("C", "C")]
        [InlineData(3, "C")]
        [InlineData(EnumSample.C, "C")]
        public void Test_GetName(object member, string name)
        {
            Assert.Equal(name, Bing.Utils.Helpers.Enum.GetName<EnumSample>(member));
        }

        /// <summary>
        /// 测试获取枚举成员名 - 验证传入的枚举参数并非枚举类型
        /// </summary>
        [Fact]
        public void Test_GetName_Validate()
        {
            Assert.Equal(string.Empty, Bing.Utils.Helpers.Enum.GetName(typeof(Sample), 3));
        }

        /// <summary>
        /// 测试获取枚举成员名 - 可空枚举
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData(" ", " ")]
        [InlineData("C", "C")]
        [InlineData(3, "C")]
        [InlineData(EnumSample.C, "C")]
        public void Test_GetName_Nullable(object member, string name)
        {
            Assert.Equal(name, Bing.Utils.Helpers.Enum.GetName<EnumSample?>(member));
        }

        /// <summary>
        /// 测试获取枚举成员值 - 验证
        /// </summary>
        [Fact]
        public void Test_GetValue_Validate()
        {
            AssertHelper.Throws<ArgumentNullException>(() => Bing.Utils.Helpers.Enum.GetValue<EnumSample>(null), "member");
            AssertHelper.Throws<ArgumentNullException>(() => Bing.Utils.Helpers.Enum.GetValue<EnumSample>(string.Empty), "member");
            AssertHelper.Throws<ArgumentNullException>(() => Bing.Utils.Helpers.Enum.GetValue<Sample>(string.Empty), "member");
        }

        /// <summary>
        /// 测试获取枚举成员值
        /// </summary>
        [Theory]
        [InlineData("C", 3)]
        [InlineData(3, 3)]
        [InlineData(EnumSample.C, 3)]
        public void Test_GetValue(object member, int value)
        {
            Assert.Equal(value, Bing.Utils.Helpers.Enum.GetValue<EnumSample>(member));
        }

        /// <summary>
        /// 测试获取枚举成员值 - 可空枚举
        /// </summary>
        [Theory]
        [InlineData("C", 3)]
        [InlineData(3, 3)]
        [InlineData(EnumSample.C, 3)]
        public void Test_GetValue_Nullable(object member, int value)
        {
            Assert.Equal(value, Bing.Utils.Helpers.Enum.GetValue<EnumSample?>(member));
        }

        /// <summary>
        /// 测试获取枚举描述
        ///</summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("A", "A")]
        [InlineData("B", "B2")]
        [InlineData(2, "B2")]
        [InlineData(EnumSample.B, "B2")]
        public void Test_GetDescription(object member, string description)
        {
            Assert.Equal(description, Bing.Utils.Helpers.Enum.GetDescription<EnumSample>(member));
        }

        /// <summary>
        /// 测试获取枚举描述 - 可空枚举
        ///</summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("A", "A")]
        [InlineData("B", "B2")]
        [InlineData(2, "B2")]
        [InlineData(EnumSample.B, "B2")]
        public void Test_GetDescription_Nullable(object member, string description)
        {
            Assert.Equal(description, Bing.Utils.Helpers.Enum.GetDescription<EnumSample?>(member));
        }

        /// <summary>
        /// 测试获取项集合
        /// </summary>
        [Fact]
        public void Test_GetItems()
        {
            var items = Bing.Utils.Helpers.Enum.GetItems<EnumSample>();
            Assert.Equal(5, items.Count);
            Assert.Equal("A", items[0].Text);
            Assert.Equal(1, items[0].Value);
            Assert.Equal("D4", items[3].Text);
            Assert.Equal(4, items[3].Value);
            Assert.Equal("E5", items[4].Text);
            Assert.Equal(5, items[4].Value);
        }

        /// <summary>
        /// 测试获取项集合
        /// </summary>
        [Fact]
        public void Test_GetItems_Type()
        {
            var items = Bing.Utils.Helpers.Enum.GetItems(typeof(EnumSample));
            Assert.Equal(5, items.Count);
            Assert.Equal("A", items[0].Text);
            Assert.Equal(1, items[0].Value);
            Assert.Equal("D4", items[3].Text);
            Assert.Equal(4, items[3].Value);
            Assert.Equal("E5", items[4].Text);
            Assert.Equal(5, items[4].Value);
        }

        /// <summary>
        /// 测试获取项集合 - 可空枚举
        /// </summary>
        [Fact]
        public void Test_GetItems_Nullable()
        {
            var items = Bing.Utils.Helpers.Enum.GetItems<EnumSample?>();
            Assert.Equal(5, items.Count);
            Assert.Equal("A", items[0].Text);
            Assert.Equal(1, items[0].Value);
            Assert.Equal("D4", items[3].Text);
            Assert.Equal(4, items[3].Value);
            Assert.Equal("E5", items[4].Text);
            Assert.Equal(5, items[4].Value);
        }

        /// <summary>
        /// 测试获取项集合 - 可空枚举
        /// </summary>
        [Fact]
        public void Test_GetItems_Nullable_Type()
        {
            var items = Bing.Utils.Helpers.Enum.GetItems(typeof(EnumSample?));
            Assert.Equal(5, items.Count);
            Assert.Equal("A", items[0].Text);
            Assert.Equal(1, items[0].Value);
            Assert.Equal("D4", items[3].Text);
            Assert.Equal(4, items[3].Value);
            Assert.Equal("E5", items[4].Text);
            Assert.Equal(5, items[4].Value);
        }

        /// <summary>
        /// 测试获取项集合 - 验证枚举类型
        /// </summary>
        [Fact]
        public void Test_GetItems_Validate()
        {
            AssertHelper.Throws<InvalidOperationException>(() => {
                Bing.Utils.Helpers.Enum.GetItems<Sample>();
            }, "类型 Bing.Utils.Tests.Samples.Sample 不是枚举");
        }

        /// <summary>
        /// 测试获取名称集合
        /// </summary>
        [Fact]
        public void Test_GetNames()
        {
            var names = Bing.Utils.Helpers.Enum.GetNames<EnumSample>().OrderBy(t => t).ToList();
            Assert.Equal(5, names.Count);
            Assert.Equal("A", names[0]);
            Assert.Equal("D", names[3]);
            Assert.Equal("E", names[4]);
        }
    }
}
