using System;
using System.Linq.Expressions;
using System.Text;
using Bing.Data.Queries;
using Bing.Data.Queries.Conditions;
using Bing.Extensions;
using Bing.Properties;
using Bing.Tests.Samples;
using Bing.Tests.XUnitHelpers;
using Bing.Extensions;
using Xunit;

namespace Bing.Tests.Datas.Queries
{
    /// <summary>
    /// 查询对象测试
    /// </summary>
    public class QueryTest
    {
        /// <summary>
        /// 查询对象
        /// </summary>
        private IQuery<AggregateRootSample> _query;

        /// <summary>
        /// 初始化一个<see cref="QueryTest"/>类型的实例
        /// </summary>
        public QueryTest() => _query = new Query<AggregateRootSample>();

        /// <summary>
        /// 测试 - 获取分页
        /// </summary>
        [Fact]
        public void Test_GetPager()
        {
            QueryParameterSample sample = new QueryParameterSample()
            {
                Order = "A",
                Page = 2,
                PageSize = 30,
                TotalCount = 40
            };
            _query = new Query<AggregateRootSample>(sample);
            _query.OrderBy("B", true);
            var pager = _query.GetPager();
            Assert.Equal(2, pager.Page);
            Assert.Equal(30, pager.PageSize);
            Assert.Equal(40, pager.TotalCount);
            Assert.Equal("A,B desc", pager.Order);
        }

        /// <summary>
        /// 测试 - 添加查询条件- 当值为空时，不会被忽略
        /// </summary>
        [Fact]
        public void Test_Where()
        {
            _query.Where(t => t.Name == "A");
            Assert.Equal("t => (t.Name == \"A\")", _query.GetCondition().SafeString());

            _query.Where(t => t.Tel == 1);
            Assert.Equal("t => ((t.Name == \"A\") AndAlso (t.Tel == 1))", _query.GetCondition().SafeString());

            _query = new Query<AggregateRootSample>();
            _query.Where(t => t.Name == "A" && t.Tel == 1);
            Assert.Equal("t => ((t.Name == \"A\") AndAlso (t.Tel == 1))", _query.GetCondition().SafeString());

            _query = new Query<AggregateRootSample>();
            _query.Where(t => t.Name == "");
            Assert.NotNull(_query.GetCondition());
        }

        /// <summary>
        /// 测试 - 添加查询条件 - 添加规约对象,当值为空时，不会被忽略
        /// </summary>
        [Fact]
        public void Test_Where_Criteria()
        {
            _query.Where(new ConditionSample());
            Assert.Equal("t => ((t.Name == \"A\") AndAlso (t.Tel == 1))", _query.GetCondition().ToString());

            _query = new Query<AggregateRootSample>();
            _query.Where(new DefaultCondition<AggregateRootSample>(t => t.Name == null));
            Assert.NotNull(_query.GetCondition());
        }

        /// <summary>
        /// 测试 - 添加查询条件 - 当第二个参数为false表示不添加条件
        /// </summary>
        [Fact]
        public void Test_WhereIf_False()
        {
            _query.WhereIf(t => t.Name == "A", false);
            Assert.Null(_query.GetCondition());

            _query.WhereIf(t => t.Name == "A", true);
            Assert.Equal("t => (t.Name == \"A\")", _query.GetCondition().SafeString());
        }

        /// <summary>
        /// 测试 - 添加查询条件
        /// </summary>
        [Fact]
        public void Test_WhereIfNotEmpty()
        {
            _query.WhereIfNotEmpty(t => t.Name == "");
            Assert.Null(_query.GetCondition());

            _query.WhereIfNotEmpty(t => t.Name == null);
            Assert.Null(_query.GetCondition());

            _query.WhereIfNotEmpty(t => t.Name == "A");
            Assert.Equal("t => (t.Name == \"A\")", _query.GetCondition().ToString());

            _query.WhereIfNotEmpty(d => d.Tel == 1);
            Assert.Equal("t => ((t.Name == \"A\") AndAlso (t.Tel == 1))", _query.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 添加查询条件 - 同时添加2个查询条件，抛出异常
        /// </summary>
        [Fact]
        public void Test_WhereIfNotEmpty_2Condition_Throw()
        {
            AssertHelper.Throws<InvalidOperationException>(() =>
            {
                _query.WhereIfNotEmpty(t => t.Name == "A" && t.Tel == 1);
            }, string.Format(LibraryResource.CanOnlyOneCondition, "t => ((t.Name == \"A\") AndAlso (t.Tel == 1))"));
        }

        /// <summary>
        /// 添加范围查询条件 - 整型
        /// </summary>
        [Fact]
        public void Test_Between_Int()
        {
            _query.Between(t => t.Tel, 1, 10, Boundary.Left);
            Assert.Equal("t => ((t.Tel >= 1) AndAlso (t.Tel < 10))", _query.GetCondition().ToString());
        }

        /// <summary>
        /// 添加范围查询条件 - double
        /// </summary>
        [Fact]
        public void Test_Between_Double()
        {
            _query.Between(t => t.DoubleValue, 1.1, 10.1, Boundary.Right);
            Assert.Equal("t => ((t.DoubleValue > 1.1) AndAlso (t.DoubleValue <= 10.1))", _query.GetCondition().ToString());
        }

        /// <summary>
        /// 添加范围查询条件 - decimal
        /// </summary>
        [Fact]
        public void Test_Between_Decimal()
        {
            _query.Between(t => t.DecimalValue, 1.1M, 10.1M, Boundary.Neither);
            Assert.Equal("t => ((t.DecimalValue > 1.1) AndAlso (t.DecimalValue < 10.1))", _query.GetCondition().ToString());
        }

        /// <summary>
        /// 添加范围查询条件 - 日期  - 不包含时间
        /// </summary>
        [Fact]
        public void Test_Between_Date()
        {
            var min = DateTime.Parse("2000-1-1 10:10:10");
            var max = DateTime.Parse("2000-1-2 10:10:10");
            var result = new StringBuilder();
            result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/1 0:00:00\"), DateTime))");
            result.Append(" AndAlso (t.DateValue < Convert(Parse(\"2000/1/2 0:00:00\"), DateTime)))");

            _query.Between(t => t.DateValue, min, max, false);
            Assert.Equal(result.ToString(), _query.GetCondition().ToString());
        }

        /// <summary>
        /// 添加范围查询条件 - 日期  - 包含时间
        /// </summary>
        [Fact]
        public void Test_Between_DateTime()
        {
            var min = DateTime.Parse("2000-1-1 10:10:10");
            var max = DateTime.Parse("2000-1-2 10:10:10");
            var result = new StringBuilder();
            result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/1 10:10:10\"), DateTime))");
            result.Append(" AndAlso (t.DateValue <= Convert(Parse(\"2000/1/2 10:10:10\"), DateTime)))");

            _query.Between(t => t.DateValue, min, max);
            Assert.Equal(result.ToString(), _query.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 排序
        /// </summary>
        [Fact]
        public void Test_OrderBy()
        {
            QueryParameterSample sample = new QueryParameterSample { Order = "Name" };
            _query = new Query<AggregateRootSample>(sample);
            Assert.Equal("Name", _query.GetOrder());

            _query.OrderBy("Age", true);
            Assert.Equal("Name,Age desc", _query.GetOrder());

            _query.OrderBy(t => t.Tel, true);
            Assert.Equal("Name,Age desc,Tel desc", _query.GetOrder());
        }

        /// <summary>
        /// 测试 - 与连接
        /// </summary>
        [Fact]
        public void Test_And()
        {
            Expression<Func<AggregateRootSample, bool>> expression = null;
            _query.And(expression);
            _query.And(t => t.Name == "A");
            Assert.Equal("t => (t.Name == \"A\")", _query.GetCondition().ToString());

            _query.And(t => t.Tel == 1);
            Assert.Equal("t => ((t.Name == \"A\") AndAlso (t.Tel == 1))", _query.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 与连接 - 连接查询对象
        /// </summary>
        [Fact]
        public void Test_And_Query()
        {
            var query = new Query<AggregateRootSample>();
            query.Where(t => t.Name == "A");
            query.OrderBy(t => t.Name);
            _query.And(query);
            Assert.Equal("t => (t.Name == \"A\")", _query.GetCondition().ToString());
            Assert.Equal("Name", _query.GetOrder());

            query = new Query<AggregateRootSample>();
            query.Where(t => t.Tel == 1);
            query.OrderBy(t => t.Tel, true);
            _query.And(query);
            Assert.Equal("t => ((t.Name == \"A\") AndAlso (t.Tel == 1))", _query.GetCondition().ToString());
            Assert.Equal("Name,Tel desc", _query.GetOrder());
        }

        /// <summary>
        /// 测试 - 或连接
        /// </summary>
        [Fact]
        public void Test_Or()
        {
            Expression<Func<AggregateRootSample, bool>> expression = null;
            _query.Or(expression);

            _query.Where(t => t.Name == "A");
            var query = new Query<AggregateRootSample>();
            query.Where(t => t.Tel == 1);
            _query.Or(query);
            Assert.Equal("t => ((t.Name == \"A\") OrElse (t.Tel == 1))", _query.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 或连接
        /// </summary>
        [Fact]
        public void Test_Or_2()
        {
            _query.Or(t => t.Name == "A", t => t.Name == "", t => t.Tel == 1);
            Assert.Equal("t => ((t.Name == \"A\") OrElse (t.Tel == 1))", _query.GetCondition().ToString());
        }

        /// <summary>
        /// 测试 - 或连接 - 连接查询对象
        /// </summary>
        [Fact]
        public void Test_Or_Query()
        {
            _query.OrderBy(t => t.Name);
            _query.Where(t => t.Name == "A");
            _query.Where(t => t.EnglishName == "A");

            var query2 = new Query<AggregateRootSample>();
            query2.OrderBy(t => t.Age, true);
            query2.Where(t => t.Name == "B");
            query2.Where(t => t.Age == 1);
            _query.Or(query2);

            var query3 = new Query<AggregateRootSample>();
            query3.OrderBy(t => t.Tel);
            query3.Where(t => t.Name == "C");
            query3.Where(t => t.Age > 10);
            _query.And(query3);

            Expression<Func<AggregateRootSample, bool>> expected = t => ((t.Name == "A" && t.EnglishName == "A")
                || (t.Name == "B" && t.Age == 1)) && (t.Name == "C" && t.Age > 10);
            Assert.Equal(expected.ToString(), _query.GetCondition().ToString());
            Assert.Equal("Name,Age desc,Tel", _query.GetOrder());
        }
    }
}
