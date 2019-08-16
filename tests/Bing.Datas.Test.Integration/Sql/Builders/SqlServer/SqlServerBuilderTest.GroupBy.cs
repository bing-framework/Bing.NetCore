using System;
using Bing.Datas.Sql;
using Bing.Datas.Test.Integration.Samples;
using Bing.Utils.Helpers;
using Xunit;

namespace Bing.Datas.Test.Integration.Sql.Builders.SqlServer
{
    /// <summary>
    /// Sql Server Sql生成器测试 - GroupBy子句
    /// </summary>
    public partial class SqlServerBuilderTest
    {
        /// <summary>
        /// 测试分组
        /// </summary>
        [Fact]
        public void Test_GroupBy_1()
        {
            //结果
            var result = new Str();
            result.AppendLine("Select [a].[Email] ");
            result.AppendLine("From [Sample] As [a] ");
            result.Append("Group By [b] Having c");

            //执行
            _builder.Select<Sample>(t => t.Email)
                .From<Sample>("a")
                .GroupBy("b", "c");

            //验证
            Assert.Equal(result.ToString(), _builder.ToSql());
        }

        /// <summary>
        /// 测试分组 - 属性表达式
        /// </summary>
        [Fact]
        public void Test_GroupBy_2()
        {
            //结果
            var result = new Str();
            result.AppendLine("Select [a].[Email] ");
            result.AppendLine("From [Sample] As [a] ");
            result.Append("Group By [a].[Email] Having b");

            //执行
            _builder.Select<Sample>(t => t.Email)
                .From<Sample>("a")
                .GroupBy<Sample>(t => t.Email, "b");

            //验证
            Assert.Equal(result.ToString(), _builder.ToSql());
        }

        /// <summary>
        /// 测试分组 - 多个属性表达式
        /// </summary>
        [Fact]
        public void Test_GroupBy_3()
        {
            //结果
            var result = new Str();
            result.AppendLine("Select [a].[Email] ");
            result.AppendLine("From [Sample] As [a] ");
            result.Append("Group By [a].[Email],[a].[Url]");

            //执行
            _builder.Select<Sample>(t => t.Email)
                .From<Sample>("a")
                .GroupBy<Sample>(t => t.Email, t => t.Url);

            //验证
            Assert.Equal(result.ToString(), _builder.ToSql());
        }

        /// <summary>
        /// 测试分组
        /// </summary>
        [Fact]
        public void Test_AppendGroupBy_1()
        {
            //结果
            var result = new Str();
            result.AppendLine("Select [a].[Email] ");
            result.AppendLine("From [Sample] As [a] ");
            result.Append("Group By b");

            //执行
            _builder.Select<Sample>(t => t.Email)
                .From<Sample>("a")
                .AppendGroupBy("b");

            //验证
            Assert.Equal(result.ToString(), _builder.ToSql());
        }

        /// <summary>
        /// 测试分组 - 条件
        /// </summary>
        [Fact]
        public void Test_AppendGroupBy_2()
        {
            //结果
            var result = new Str();
            result.AppendLine("Select [a].[Email] ");
            result.AppendLine("From [Sample] As [a] ");
            result.Append("Group By b");

            //执行
            _builder.Select<Sample>(t => t.Email)
                .From<Sample>("a")
                .AppendGroupBy("c", false)
                .AppendGroupBy("b", true);

            //验证
            Assert.Equal(result.ToString(), _builder.ToSql());
        }
    }
}
