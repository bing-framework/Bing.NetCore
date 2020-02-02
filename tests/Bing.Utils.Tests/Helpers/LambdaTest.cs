using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bing.Helpers;
using Bing.Tests;
using Bing.Tests.Samples;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Xunit;
using Xunit.Abstractions;
using Enum = Bing.Helpers.Enum;

namespace Bing.Utils.Tests.Helpers
{
    /// <summary>
    /// 测试Lambda表达式操作
    /// </summary>
    public class LambdaTest : TestBase
    {
        public LambdaTest(ITestOutputHelper output) : base(output)
        {
        }

        #region GetValue(获取成员值)

        /// <summary>
        /// 测试获取成员值 - 返回类型为Object
        /// </summary>
        [Fact]
        public void TestGetValue_Object()
        {
            Expression<Func<Sample, object>> expression = t => t.StringValue == "A";
            Assert.Equal("A", Lambda.GetValue(expression));
        }

        /// <summary>
        /// 测试获取成员值
        /// </summary>
        [Fact]
        public void TestGetValue()
        {
            Assert.Null(Lambda.GetValue(null));

            Expression<Func<Sample, bool>> expression = t => t.StringValue == "A";
            Assert.Equal("A", Lambda.GetValue(expression));

            Expression<Func<Sample, bool>> expression2 = t => t.Test2.IntValue == 1;
            Assert.Equal(1, Lambda.GetValue(expression2));

            Expression<Func<Sample, bool>> expression3 = t => t.Test2.Test3.StringValue == "B";
            Assert.Equal("B", Lambda.GetValue(expression3));

            var value = Guid.NewGuid();
            Expression<Func<Sample, bool>> expression4 = t => t.GuidValue == value;
            Assert.Equal(value, Lambda.GetValue(expression4));

            Expression<Func<Sample, bool>> expression5 = t => 1 == t.Test2.IntValue;
            Assert.Equal(1, Lambda.GetValue(expression5));
        }

        /// <summary>
        /// 测试获取成员值 - 布尔属性
        /// </summary>
        [Fact]
        public void TestGetValue_Bool()
        {
            Expression<Func<Sample, bool>> expression = t => t.BoolValue;
            Assert.Equal("True", Lambda.GetValue(expression).ToString());

            expression = t => !t.BoolValue;
            Assert.Equal("False", Lambda.GetValue(expression).ToString());

            expression = t => t.Test2.BoolValue;
            Assert.Equal("True", Lambda.GetValue(expression).ToString());

            expression = t => !t.Test2.BoolValue;
            Assert.Equal("False", Lambda.GetValue(expression).ToString());

            expression = t => t.BoolValue == true;
            Assert.Equal("True", Lambda.GetValue(expression).ToString());

            expression = t => t.BoolValue == false;
            Assert.Equal("False", Lambda.GetValue(expression).ToString());
        }

        /// <summary>
        /// 测试获取成员值 - Guid.NewGuid
        /// </summary>
        [Fact]
        public void TestGetValue_NewGuid()
        {
            Expression<Func<Sample, bool>> expression = t => t.GuidValue == Guid.NewGuid();
            var value = Lambda.GetValue(expression);
            Assert.NotEqual(Guid.Empty, Conv.ToGuid(value));
        }

        /// <summary>
        /// 测试获取成员值 - Guid属性
        /// </summary>
        [Fact]
        public void TestGetValue_Guid()
        {
            var id = Guid.NewGuid();
            Expression<Func<Sample, bool>> expression = t => t.GuidValue == id;
            var value = Lambda.GetValue(expression);
            Assert.NotEqual(Guid.Empty, Conv.ToGuid(value));
        }

        /// <summary>
        /// 测试获取成员之 - Guid属性
        /// </summary>
        [Fact]
        public void TestGetValue_Guid_Method()
        {
            var id = Guid.NewGuid();
            Assert.NotEqual(Guid.Empty, Conv.ToGuid(GetGuidValue(id)));
        }

        private object GetGuidValue(Guid id)
        {
            Expression<Func<Sample, bool>> expression = t => t.GuidValue == id;
            var value = Lambda.GetValue(expression);
            return value;
        }

        /// <summary>
        /// 测试获取成员值 - DateTime.Now
        /// </summary>
        [Fact]
        public void TestGetValue_DateTimeNow()
        {
            Expression<Func<Sample, bool>> expression = t => t.DateValue == DateTime.Now;
            var value = Lambda.GetValue(expression);
            Assert.NotNull(Conv.ToDateOrNull(value));
        }

        /// <summary>
        /// 测试获取成员值 - 运算符
        /// </summary>
        [Fact]
        public void TestGetValue_Operation()
        {
            Expression<Func<Sample, bool>> expression = t => t.Test2.IntValue != 1;
            Assert.Equal(1, Lambda.GetValue(expression));

            expression = t => t.Test2.IntValue > 1;
            Assert.Equal(1, Lambda.GetValue(expression));

            expression = t => t.Test2.IntValue < 1;
            Assert.Equal(1, Lambda.GetValue(expression));

            expression = t => t.Test2.IntValue >= 1;
            Assert.Equal(1, Lambda.GetValue(expression));

            expression = t => t.Test2.IntValue <= 1;
            Assert.Equal(1, Lambda.GetValue(expression));
        }

        /// <summary>
        /// 测试获取成员值 - 可空类型
        /// </summary>
        [Fact]
        public void TestGetValue_Nullable()
        {
            Expression<Func<Sample, bool>> expression = t => t.NullableIntValue == 1;
            Assert.Equal(1, Lambda.GetValue(expression));

            expression = t => t.NullableDecimalValue == 1.5M;
            Assert.Equal(1.5M, Lambda.GetValue(expression));

            var sample = new Sample();
            expression = t => t.BoolValue == sample.NullableBoolValue;
            Assert.Null(Lambda.GetValue(expression));
        }

        /// <summary>
        /// 测试获取成员值 - 方法
        /// </summary>
        [Fact]
        public void TestGetValue_Method()
        {
            Expression<Func<Sample, bool>> expression = t => t.StringValue.Contains("A");
            Assert.Equal("A", Lambda.GetValue(expression));

            expression = t => t.Test2.StringValue.Contains("B");
            Assert.Equal("B", Lambda.GetValue(expression));

            expression = t => t.Test2.Test3.StringValue.StartsWith("C");
            Assert.Equal("C", Lambda.GetValue(expression));

            var test = new Sample { Email = "a" };
            expression = t => t.StringValue.Contains(test.Email);
            Assert.Equal("a", Lambda.GetValue(expression));
        }

        /// <summary>
        /// 测试获取成员值 - 实例
        /// </summary>
        [Fact]
        public void TestGetValue_Instance()
        {
            var test = new Sample() { StringValue = "a", BoolValue = true, Test2 = new Sample2() { StringValue = "b", Test3 = new Sample3() { StringValue = "c" } } };

            Expression<Func<string>> expression = () => test.StringValue;
            Assert.Equal("a", Lambda.GetValue(expression));

            Expression<Func<string>> expression2 = () => test.Test2.StringValue;
            Assert.Equal("b", Lambda.GetValue(expression2));

            Expression<Func<string>> expression3 = () => test.Test2.Test3.StringValue;
            Assert.Equal("c", Lambda.GetValue(expression3));

            Expression<Func<bool>> expression4 = () => test.BoolValue;
            Assert.True(Conv.ToBool(Lambda.GetValue(expression4)));
        }

        /// <summary>
        /// 测试获取成员值 - 复杂类型
        /// </summary>
        [Fact]
        public void TestGetValue_Complex()
        {
            var test = new Sample() { StringValue = "a", Test2 = new Sample2() { StringValue = "b" } };

            Expression<Func<Sample, bool>> expression = t => t.StringValue == test.StringValue;
            Assert.Equal("a", Lambda.GetValue(expression));
            Expression<Func<Sample, bool>> expression2 = t => t.StringValue == test.Test2.StringValue;
            Assert.Equal("b", Lambda.GetValue(expression2));

            Expression<Func<Sample, bool>> expression3 = t => t.StringValue.Contains(test.StringValue);
            Assert.Equal("a", Lambda.GetValue(expression3));
            Expression<Func<Sample, bool>> expression4 = t => t.StringValue.Contains(test.Test2.StringValue);
            Assert.Equal("b", Lambda.GetValue(expression4));
        }

        /// <summary>
        /// 测试获取成员值 - 枚举
        /// </summary>
        [Fact]
        public void TestGetValue_Enum()
        {
            var test1 = new Sample { NullableEnumValue = EnumSample.C };

            Expression<Func<Sample, bool>> expression = test => test.EnumValue == EnumSample.D;
            Assert.Equal(EnumSample.D.Value(), Enum.GetValue<EnumSample>(Lambda.GetValue(expression)));

            expression = test => test.EnumValue == test1.NullableEnumValue;
            Assert.Equal(EnumSample.C, Lambda.GetValue(expression));

            expression = test => test.NullableEnumValue == EnumSample.E;
            Assert.Equal(EnumSample.E, Lambda.GetValue(expression));

            expression = test => test.NullableEnumValue == test1.NullableEnumValue;
            Assert.Equal(EnumSample.C, Lambda.GetValue(expression));

            test1.NullableEnumValue = null;
            expression = test => test.NullableEnumValue == test1.NullableEnumValue;
            Assert.Null(Lambda.GetValue(expression));
        }

        /// <summary>
        /// 测试获取成员值 - 集合
        /// </summary>
        [Fact]
        public void TestGetValue_List()
        {
            var list = new List<string> { "a", "b" };
            Expression<Func<Sample, bool>> expression = t => list.Contains(t.StringValue);
            Assert.Equal(list, Lambda.GetValue(expression));
        }

        /// <summary>
        /// 测试获取成员值 - 静态成员
        /// </summary>
        [Fact]
        public void TestGetValue_Static()
        {
            Expression<Func<Sample, bool>> expression = t => t.StringValue == Sample.StaticString;
            Assert.Equal("TestStaticString", Lambda.GetValue(expression));

            expression = t => t.StringValue == Sample.StaticSample.StringValue;
            Assert.Equal("TestStaticSample", Lambda.GetValue(expression));

            expression = t => t.Test2.StringValue == Sample.StaticString;
            Assert.Equal("TestStaticString", Lambda.GetValue(expression));

            expression = t => t.Test2.StringValue == Sample.StaticSample.StringValue;
            Assert.Equal("TestStaticSample", Lambda.GetValue(expression));

            expression = t => t.Test2.StringValue.Contains(Sample.StaticSample.StringValue);
            Assert.Equal("TestStaticSample", Lambda.GetValue(expression));
        }

        #endregion
    }
}
