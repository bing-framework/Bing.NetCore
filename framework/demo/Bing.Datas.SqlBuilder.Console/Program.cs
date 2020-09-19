using System;
using Bing.Data.Sql;
using Bing.Data.Sql.Matedatas;
using Bing.Datas.Dapper.SqlServer;
using Bing.Datas.SqlBuilder.Models;

// ReSharper disable once CheckNamespace
namespace Bing.Datas.SqlBuilder
{
    class Program
    {
        /// <summary>
        /// 打印输出
        /// </summary>
        /// <param name="action">操作</param>
        /// <param name="description">描述</param>
        /// <param name="separator">分隔符</param>
        public static void Print(Action<ISqlBuilder> action, string description = "", string separator = "")
        {
            ISqlBuilder builder = new SqlServerBuilder(new DefaultEntityMatedata());
            action.Invoke(builder);
            if (!string.IsNullOrEmpty(separator))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"--------------------------------[ {separator} ]----------------------------------");
                Console.WriteLine();
            }
            if (!string.IsNullOrEmpty(description))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(description);
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(builder.ToSql());
            if (builder.GetParams() != null)
            {
                foreach (var item in builder.GetParams())
                {
                    Console.WriteLine(item.ToString());
                }
            }
            Console.WriteLine();
        }

        /// <summary>
        /// 主函数，程序入口
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            #region Select

            Print(
                builder => builder.Select<UserInfo>().From<UserInfo>(),
                "查询单表所有字段",
                "Select");

            Print(
                builder => builder.Select<UserInfo>().From<UserInfo>().Where<UserInfo>(x => x.Id > 0 || x.Email != ""),
                "查询单表所有字段，linq拼接条件");

            Print(
                builder => builder.Select<UserInfo>(x => x.Id).From<UserInfo>(),
                "查询单表单个字段");

            Print(
                builder => builder.Select<UserInfo>(x => new {x.Id, x.Name}).From<UserInfo>(),
                "查询单表多个字段");

            Print(
                builder => builder.Select<UserInfo>(x => new { x.Id, x.Name }).From<UserInfo>(),
                "查询单表多个字段，并返回指定TOP数量的数据");

            #endregion
            Console.ReadLine();
        }
    }
}
