using Xunit;
using Bing.Utils.Extensions;
using System.Collections.Generic;

namespace Bing.Utils.Tests.Extensions.Base
{
    public class ObjectExtensionsTest
    {
        /// <summary>
        /// 测试属性值合并-同一对象
        /// </summary>
        [Fact]
        public void Test_ClonePropertyFrom_SameInstance()
        {
            var a = new TestClassA();
            var cnt = a.ClonePropertyFrom(a);

            Assert.Equal(0, cnt);
        }

        /// <summary>
        /// 测试属性值合并-null对象
        /// </summary>
        [Fact]
        public void Test_ClonePropertyFrom_NullInstance()
        {
            var a = new TestClassA();
            var cnt = a.ClonePropertyFrom(null);

            Assert.Equal(0, cnt);
        }

        /// <summary>
        /// 测试属性值合并-都为null对象
        /// </summary>
        [Fact]
        public void Test_ClonePropertyFrom_DNullInstance()
        {
            TestClassA a = null;
            var cnt = a.ClonePropertyFrom(null);

            Assert.Equal(0, cnt);
        }

        /// <summary>
        /// 测试属性值合并-相同类型
        /// </summary>
        [Fact]
        public void Test_ClonePropertyFrom_SameType()
        {
            var a = new TestClassA();
            a.Id = 0;
            a.Index = 1;
            a.Description = "test class A";

            var b = new TestClassA();
            var cnt = b.ClonePropertyFrom(a);

            Assert.Equal(3, cnt);
            Assert.Equal(b.Id, a.Id);
            Assert.Equal(b.Index, a.Index);
            Assert.Equal(b.Description, a.Description);
        }

        /// <summary>
        /// 测试属性值合并-相同类型-排除字段
        /// </summary>
        [Fact]
        public void Test_ClonePropertyFrom_SameType_ExcludeField()
        {
            var a = new TestClassA();
            a.Id = 1;
            a.Index = 1;
            a.Description = "test class A";

            var exField = new List<string>() { "Id" };
            var b = new TestClassA();
            var cnt = b.ClonePropertyFrom(a, exField);

            Assert.Equal(2, cnt);
            Assert.NotEqual(b.Id, a.Id);
            Assert.Equal(b.Index, a.Index);
            Assert.Equal(b.Description, a.Description);
        }

        /// <summary>
        /// 测试属性值合并-不同类型
        /// </summary>
        [Fact]
        public void Test_ClonePropertyFrom_DiffType()
        {
            var a = new TestClassA();
            a.Id = 1;
            a.Index = 1;
            a.Description = "test class A";

            var b = new TestClassB();
            var cnt = b.ClonePropertyFrom(a);

            Assert.Equal(2, cnt);
            Assert.Equal(b.Id, a.Id);
            Assert.Equal(b.Description, a.Description);
        }

        /// <summary>
        /// 测试属性值合并-不同类型
        /// </summary>
        [Fact]
        public void Test_ClonePropertyTo_DiffType()
        {
            var a = new TestClassA();
            a.Id = 1;
            a.Index = 1;
            a.Description = "test class A";

            var b = new TestClassB();
            var cnt = a.ClonePropertyTo(b);

            Assert.Equal(2, cnt);
            Assert.Equal(b.Id, a.Id);
            Assert.Equal(b.Description, a.Description);
        }
    }

    #region 辅助类

    internal class TestClassA
    {
        public int Id;
        public int Index { get; set; }
        public string Description { get; set; }
    }

    internal class TestClassB
    {
        public int Id;

        public string Name { get; set; }

        public string Description { get; set; }
    }

    #endregion
}
