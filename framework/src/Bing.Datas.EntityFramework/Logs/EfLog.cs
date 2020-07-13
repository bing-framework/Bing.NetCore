using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Bing.Datas.Configs;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.UnitOfWorks;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Logs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Bing.Datas.EntityFramework.Logs
{
    /// <summary>
    /// EF日志记录器
    /// </summary>
    public class EfLog : ILogger
    {
        /// <summary>
        /// EF跟踪日志名
        /// </summary>
        public const string TraceLogName = "EfTraceLog";

        /// <summary>
        /// 日志记录
        /// </summary>
        /// <typeparam name="TState">状态类型</typeparam>
        /// <param name="logLevel">日志级别</param>
        /// <param name="eventId">事件编号</param>
        /// <param name="state">状态</param>
        /// <param name="exception">异常</param>
        /// <param name="formatter">日志内容</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            var config = GetConfig();
            var log = GetLog();
            if (IsEnabled(eventId, config) == false)
                return;
            log.Caption($"执行EF操作：")
                .Content($"工作单元跟踪号：{GetUnitOfWork()?.TraceId}")
                .Content($"事件ID：{eventId.Id}")
                .Content($"事件名称：{eventId.Name}");
            Debug.WriteLine(state);
            AddContent(state, config, log);
            log.Exception(exception).Trace();
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        private DataConfig GetConfig()
        {
            try
            {
                var options = Ioc.Create<IOptionsSnapshot<DataConfig>>();
                return options.Value;
            }
            catch
            {
                return new DataConfig { LogLevel = DataLogLevel.Sql };
            }
        }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        protected virtual ILog GetLog()
        {
            try
            {
                return Bing.Logs.Log.GetLog(TraceLogName);
            }
            catch
            {
                return Bing.Logs.Log.Null;
            }
        }

        /// <summary>
        /// 获取工作单元
        /// </summary>
        protected virtual UnitOfWorkBase GetUnitOfWork()
        {
            try
            {
                var unitOfWork = Ioc.Create<IUnitOfWork>();
                return unitOfWork as UnitOfWorkBase;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 是否启用EF日志
        /// </summary>
        /// <param name="eventId">事件ID</param>
        /// <param name="config">数据配置</param>
        private bool IsEnabled(EventId eventId, DataConfig config)
        {
            if (config.LogLevel == DataLogLevel.Off)
                return false;
            if (config.LogLevel == DataLogLevel.All)
                return true;
            if (eventId.Name == "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted")
                return true;
            return false;
        }

        /// <summary>
        /// 添加日志内容
        /// </summary>
        private void AddContent<TState>(TState state, DataConfig config, ILog log)
        {
            if (config.LogLevel == DataLogLevel.All) 
                log.Content("事件内容：").Content(state.SafeString());
            if (!(state is IEnumerable list))
                return;
            var dictionary = new Dictionary<string, string>();
            foreach (KeyValuePair<string, object> item in list) 
                dictionary.Add(item.Key, item.Value.SafeString());
            AddDictionary(dictionary, log);
        }

        /// <summary>
        /// 添加字典内容
        /// </summary>
        /// <param name="dictionary">字典</param>
        /// <param name="log">日志操作</param>
        private void AddDictionary(IDictionary<string, string> dictionary, ILog log)
        {
            AddElapsed(GetValue(dictionary, "elapsed"), log);
            var sqlParams = GetValue(dictionary, "parameters");
            AddSql(GetValue(dictionary, "commandText"), log);
            AddSqlParams(sqlParams, log);
        }

        /// <summary>
        /// 获取值
        /// </summary>
        /// <param name="dictionary">参数字典</param>
        /// <param name="key">参数名</param>
        private string GetValue(IDictionary<string, string> dictionary, string key)
        {
            if (dictionary.ContainsKey(key))
                return dictionary[key];
            return string.Empty;
        }

        /// <summary>
        /// 添加执行时间
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="log">日志操作</param>
        private void AddElapsed(string value, ILog log)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            log.Content($"执行时间：{value} 毫秒");
        }

        /// <summary>
        /// 添加Sql
        /// </summary>
        /// <param name="sql">Sql语句</param>
        /// <param name="log">日志操作</param>
        private void AddSql(string sql, ILog log)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return;
            log.Sql($"{sql}{Common.Line}");
        }

        /// <summary>
        /// 添加Sql参数
        /// </summary>
        /// <param name="value">Sql参数</param>
        /// <param name="log">日志操作</param>
        private void AddSqlParams(string value, ILog log)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            log.SqlParams(value);
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        /// <param name="logLevel">日志级别</param>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>
        /// 起始范围
        /// </summary>
        public IDisposable BeginScope<TState>(TState state) => null;
    }
}
