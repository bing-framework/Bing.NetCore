using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bing.Scheduler.Abstractions.Servers;
using Bing.Scheduler.Core;
using Bing.Scheduler.Core.Models;
using Bing.Utils.Extensions;
using Microsoft.Data.Sqlite;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.AdoJobStore;
using Quartz.Impl.AdoJobStore.Common;
using Quartz.Simpl;
using Quartz.Util;

namespace Bing.Scheduler.Quartz
{
    /// <summary>
    /// 基于Quartz.Net的任务调度中心
    /// </summary>
    public class QuartzSchedulerCenter : ISchedulerCenter
    {
        /// <summary>
        /// 调度器
        /// </summary>
        private IScheduler _scheduler;

        /// <summary>
        /// 调度器
        /// </summary>
        private IScheduler Scheduler
        {
            get
            {
                if (_scheduler != null)
                {
                    return _scheduler;
                }
                // 如果不存在sqlite数据库，则创建
                CreateSqliteDb();
                DBConnectionManager.Instance.AddConnectionProvider("default",
                    new DbProvider("SQLite-Microsoft", "Data Source=Db/sqliteScheduler.db"));
                var serializer = new JsonObjectSerializer();
                var jobStore = new JobStoreTX()
                {
                    DataSource = "default",
                    TablePrefix = "QRTZ_",
                    InstanceId = "AUTO",
                    DriverDelegateType = typeof(SQLiteDelegate).AssemblyQualifiedName,
                    ObjectSerializer = serializer
                };
                DirectSchedulerFactory.Instance.CreateScheduler("BingScheduler", "AUTO", new DefaultThreadPool(),
                    jobStore);
                _scheduler = SchedulerRepository.Instance.Lookup("BingScheduler").Result;
                _scheduler.Start();
                return _scheduler;
            }
        }

        /// <summary>
        /// 创建SqliteDb
        /// </summary>
        private void CreateSqliteDb()
        {
            if (File.Exists("Db/sqliteScheduler.db"))
            {
                return;
            }

            if (!Directory.Exists("Db"))
            {
                Directory.CreateDirectory("Db");
            }

            using (var connection=new SqliteConnection("Data Source=Db/sqliteScheduler.db"))
            {
                connection.OpenAsync().Wait();
                string sql = File.ReadAllText("tables_sqlite.sql");
                var command = new SqliteCommand(sql, connection);
                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        public async Task<Result> AddJobAsync(ScheduleInfo entity)
        {
            throw new System.NotImplementedException();
        }

        public async Task<Result> StopOrDeleteJobAsync(string jobGroup, string jobName, bool isDelete = false)
        {
            throw new System.NotImplementedException();
        }

        public async Task<Result> ResumeJobAsync(string jobGroup, string jobName)
        {
            throw new System.NotImplementedException();
        }

        public async Task<ScheduleInfo> QueryJobAsync(string jobGroup, string jobName)
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> TriggerJobAsync(string jobGroup, string jobName)
        {
            throw new System.NotImplementedException();
        }

        public async Task<List<string>> GetJobLogAsync(string jobGroup, string jobName)
        {
            throw new System.NotImplementedException();
        }

        public async Task<List<JobInfo>> GetAllJobAsync()
        {
            throw new System.NotImplementedException();
        }

        public async Task<List<JobInfoBrief>> GetAllJobBriefAsync()
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> StartAsync()
        {
            // 开启调度器
            if (Scheduler.InStandbyMode)
            {
                await Scheduler.Start();
            }
            return Scheduler.InStandbyMode;
        }

        public async Task<bool> StopAsync()
        {
            // 判断调度是否已经关闭
            if (!Scheduler.InStandbyMode)
            {
                // 等待任务运行完成
                await Scheduler.Standby();
            }
            return !Scheduler.InStandbyMode;
        }

        /// <summary>
        /// 创建简单触发器
        /// </summary>
        /// <param name="entity">调度信息</param>
        /// <returns></returns>
        private ITrigger CreateSimpleTrigger(ScheduleInfo entity)
        {
            return TriggerBuilder.Create()
                .WithIdentity(entity.JobName, entity.JobGroup)
                .StartAt(entity.BeginTime)
                .EndAt(entity.EndTime)
                .WithSimpleSchedule(x =>
                {
                    // 执行时间间隔，单位：秒。
                    x.WithIntervalInSeconds(entity.IntervalSecond.SafeValue());
                    if (entity.RunTimes.HasValue && entity.RunTimes > 0)
                    {
                        // 执行次数，指定数量
                        x.WithRepeatCount(entity.RunTimes.SafeValue());
                    }
                    else
                    {
                        // 执行次数，无线
                        x.RepeatForever();
                    }
                })
                .ForJob(entity.JobName, entity.JobGroup)
                .Build();
        }

        /// <summary>
        /// 创建Cron触发器
        /// </summary>
        /// <param name="entity">调度信息</param>
        /// <returns></returns>
        private ITrigger CreateCronTrigger(ScheduleInfo entity)
        {
            return TriggerBuilder.Create()
                .WithIdentity(entity.JobName, entity.JobGroup)
                .StartAt(entity.BeginTime)
                .EndAt(entity.EndTime)
                .WithCronSchedule(entity.Cron) // 指定Cron表达式
                .ForJob(entity.JobName, entity.JobGroup)
                .Build();
        }
    }
}
