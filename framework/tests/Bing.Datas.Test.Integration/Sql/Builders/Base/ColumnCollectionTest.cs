using Bing.Datas.Sql.Builders.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Datas.Test.Integration.Sql.Builders.Base
{
    /// <summary>
    /// 列集合测试
    /// </summary>
    public class ColumnCollectionTest : TestBase
    {
        /// <summary>
        /// 列集合
        /// </summary>
        private readonly ColumnCollection _columns;

        /// <summary>
        /// 初始化一个<see cref="ColumnCollectionTest"/>类型的实例
        /// </summary>
        public ColumnCollectionTest(ITestOutputHelper output) : base(output)
        {
            _columns = new ColumnCollection();
        }

        /// <summary>
        /// 添加列集合
        /// </summary>
        [Fact]
        public void Test_AddColumns_1()
        {
            _columns.AddColumns("a");
            Assert.Equal(1, _columns.Count);
            Assert.Equal("a", _columns[0].Name);
        }

        /// <summary>
        /// 添加列集合 - 表别名
        /// </summary>
        [Fact]
        public void Test_AddColumns_2()
        {
            _columns.AddColumns("a", "b");
            Assert.Equal(1, _columns.Count);
            Assert.Equal("a", _columns[0].Name);
            Assert.Equal("b", _columns[0].TableAlias);
        }

        /// <summary>
        /// 添加列集合 - 多个列
        /// </summary>
        [Fact]
        public void TestAddColumns_3()
        {
            _columns.AddColumns("a,b");
            Assert.Equal(2, _columns.Count);
            Assert.Equal("a", _columns[0].Name);
            Assert.Equal("b", _columns[1].Name);
        }

        /// <summary>
        /// 添加列集合 - 列带表别名
        /// </summary>
        [Fact]
        public void Test_AddColumns_4()
        {
            _columns.AddColumns("a.b,c");
            Assert.Equal(2, _columns.Count);
            Assert.Equal("a", _columns[0].TableAlias);
            Assert.Equal("b", _columns[0].Name);
            Assert.Equal("c", _columns[1].Name);
        }

        /// <summary>
        /// 添加列集合 - 设置表别名
        /// </summary>
        [Fact]
        public void Test_AddColumns_5()
        {
            _columns.AddColumns("a.b,c", "d");
            Assert.Equal(2, _columns.Count);
            Assert.Equal("a", _columns[0].TableAlias);
            Assert.Equal("b", _columns[0].Name);
            Assert.Equal("d", _columns[1].TableAlias);
            Assert.Equal("c", _columns[1].Name);
        }

        /// <summary>
        /// 添加列集合 - 列别名
        /// </summary>
        [Fact]
        public void Test_AddColumns_6()
        {
            _columns.AddColumns("  a . b  as  e, c  f ", "d");
            Assert.Equal(2, _columns.Count);
            Assert.Equal("a", _columns[0].TableAlias);
            Assert.Equal("b", _columns[0].Name);
            Assert.Equal("e", _columns[0].ColumnAlias);
            Assert.Equal("d", _columns[1].TableAlias);
            Assert.Equal("c", _columns[1].Name);
            Assert.Equal("f", _columns[1].ColumnAlias);
        }

        /// <summary>
        /// 移除列
        /// </summary>
        [Fact]
        public void Test_AddColumns_7()
        {
            _columns.AddColumns("a.b,c", "d");
            _columns.AddColumns("e.f");
            _columns.RemoveColumns("d.c");
            Assert.Equal(2, _columns.Count);
            Assert.Equal("a", _columns[0].TableAlias);
            Assert.Equal("b", _columns[0].Name);
            Assert.Equal("e", _columns[1].TableAlias);
            Assert.Equal("f", _columns[1].Name);
        }
    }
}
