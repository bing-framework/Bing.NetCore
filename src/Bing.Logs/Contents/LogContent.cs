using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Bing.Logs.Abstractions;
using Bing.Logs.Properties;

namespace Bing.Logs.Contents
{
    /// <summary>
    /// 日志内容
    /// </summary>
    public class LogContent : ILogContent, ICaption, ILogConvert
    {
        #region 属性

        /// <summary>
        /// 日志名称
        /// </summary>
        public string LogName { get; set; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// 日志标识
        /// </summary>
        public string LogId { get; set; }

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 操作时间
        /// </summary>
        public string OperationTime { get; set; }

        /// <summary>
        /// 执行时间
        /// </summary>
        public string Duration { get; set; }

        /// <summary>
        /// IP
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// 主机
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 线程号
        /// </summary>
        public string ThreadId { get; set; }

        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser { get; set; }

        /// <summary>
        /// 请求地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 业务编号
        /// </summary>
        public string BusinessId { get; set; }

        /// <summary>
        /// 租户
        /// </summary>
        public string Tenant { get; set; }

        /// <summary>
        /// 应用程序
        /// </summary>
        public string Application { get; set; }

        /// <summary>
        /// 模块
        /// </summary>
        public string Module { get; set; }

        /// <summary>
        /// 类名
        /// </summary>
        public string Class { get; set; }

        /// <summary>
        /// 方法
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 参数
        /// </summary>
        public StringBuilder Params { get; set; }

        /// <summary>
        /// 操作人编号
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 操作人
        /// </summary>
        public string Operator { get; set; }

        /// <summary>
        /// 操作人角色
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Caption { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public StringBuilder Content { get; set; }

        /// <summary>
        /// Sql语句
        /// </summary>
        public StringBuilder Sql { get; set; }

        /// <summary>
        /// Sql参数
        /// </summary>
        public StringBuilder SqlParams { get; set; }

        /// <summary>
        /// 错误码
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// 异常
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<string> Tags { get; set; }

        /// <summary>
        /// 扩展属性
        /// </summary>
        public IDictionary<string, object> ExtraProperties { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        public int Order { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="LogContent"/>类型的实例
        /// </summary>
        public LogContent()
        {
            Params = new StringBuilder();
            Content = new StringBuilder();
            Sql = new StringBuilder();
            SqlParams = new StringBuilder();
            Tags = new List<string>();
            ExtraProperties = new ConcurrentDictionary<string, object>();
        }

        #endregion

        /// <summary>
        /// 转换
        /// </summary>
        public List<Item> To()
        {
            return new List<Item>
            {
                {new Item(LogResource.LogId, LogId, 0)},
                {new Item(LogResource.LogName, LogName, 1)},
                {new Item(LogResource.TraceId, TraceId, 2)},
                {new Item(LogResource.OperationTime, OperationTime, 3)},
                {new Item(LogResource.Duration, Duration, 4)},
                {new Item(LogResource.ThreadId, ThreadId, 5)},
                {new Item("Url", Url, 6)},
                {new Item(LogResource.UserId, UserId, 7)},
                {new Item(LogResource.Operator, Operator, 8)},
                {new Item(LogResource.Role, Role, 9)},
                {new Item(LogResource.BusinessId, BusinessId, 10)},
                {new Item(LogResource.Tenant, Tenant, 11)},
                {new Item(LogResource.Application, Application, 12)},
                {new Item(LogResource.Module, Module, 13)},
                {new Item(LogResource.Class, Class, 14)},
                {new Item(LogResource.Method, Method, 15)},
                {new Item(LogResource.Params, Params.ToString(), 16)},
                {new Item(LogResource.Caption, Caption, 17)},
                {new Item(LogResource.Content, Content.ToString(), 18)},
                {new Item(LogResource.Sql, Sql.ToString(), 19)},
                {new Item(LogResource.SqlParams, SqlParams.ToString(), 20)},
                {new Item(LogResource.ErrorCode, ErrorCode, 21)},
            };
        }
    }
}
