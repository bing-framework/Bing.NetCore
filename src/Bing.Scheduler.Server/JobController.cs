using System.Threading.Tasks;
using Bing.Scheduler.Abstractions.Servers;
using Bing.Scheduler.Core;
using Bing.Scheduler.Core.Models;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Scheduler.Server
{
    /// <summary>
    /// 任务调度 控制器
    /// </summary>
    [Route("api/[conroller]/[Action]")]
    [EnableCors("AllowSameDomain")]//允许跨域
    public class JobController:Controller
    {
        /// <summary>
        /// 任务调度中心
        /// </summary>
        private readonly ISchedulerCenter _schedulerCenter;

        /// <summary>
        /// 初始化一个<see cref="JobController"/>类型的实例
        /// </summary>
        /// <param name="schedulerCenter">任务调度中心</param>
        public JobController(ISchedulerCenter schedulerCenter)
        {
            _schedulerCenter = schedulerCenter;
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="info">调度信息</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> AddJob([FromBody] ScheduleInfo info)
        {
            return await _schedulerCenter.AddJobAsync(info);
        }

        /// <summary>
        /// 暂停任务
        /// </summary>
        /// <param name="job">任务键</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> StopJob([FromBody] JobKey job)
        {
            return await _schedulerCenter.StopOrDeleteJobAsync(job.Group, job.Name);
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        /// <param name="job">任务键</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> RemoveJob([FromBody] JobKey job)
        {
            return await _schedulerCenter.StopOrDeleteJobAsync(job.Group, job.Name, true);
        }

        /// <summary>
        /// 恢复暂停中的任务
        /// </summary>
        /// <param name="job">任务键</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> ResumeJob([FromBody] JobKey job)
        {
            return await _schedulerCenter.ResumeJobAsync(job.Group, job.Name);
        }

        /// <summary>
        /// 查询任务
        /// </summary>
        /// <param name="job">任务键</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> QueryJob([FromBody] JobKey job)
        {
            var result= await _schedulerCenter.QueryJobAsync(job.Group, job.Name);
            return new Result() {Code = 1, Success = true, Data = result};
        }

        /// <summary>
        /// 修改任务
        /// </summary>
        /// <param name="entity">调度信息</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> UpdateJob([FromBody] ScheduleInfo entity)
        {
            await _schedulerCenter.StopOrDeleteJobAsync(entity.JobGroup, entity.JobName, true);
            await _schedulerCenter.AddJobAsync(entity);
            return new Result() {Code = 1, Success = true, Message = "修改计划任务成功"};
        }

        /// <summary>
        /// 立即执行任务
        /// </summary>
        /// <param name="job">任务键</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> TriggerJob([FromBody] JobKey job)
        {
            var result = await _schedulerCenter.TriggerJobAsync(job.Group, job.Name);
            return result
                ? new Result() {Code = 1, Success = true, Message = "执行任务成功"}
                : new Result() {Code = 2, Success = false, Message = "执行任务失败"};
        }

        /// <summary>
        /// 获取任务日志
        /// </summary>
        /// <param name="job">任务键</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> GetJobLog([FromBody] JobKey job)
        {
            var result = await _schedulerCenter.GetJobLogAsync(job.Group, job.Name);
            return new Result() {Code = 1, Success = true, Data = result};
        }

        /// <summary>
        /// 启动调度
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> Start()
        {
            var result = await _schedulerCenter.StartAsync();
            return result
                ? new Result() { Code = 1, Success = true, Message = "启动调度成功" }
                : new Result() { Code = 2, Success = false, Message = "启动调度失败" };
        }

        /// <summary>
        /// 停止调度
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result> Stop()
        {
            var result = await _schedulerCenter.StopAsync();
            return result
                ? new Result() { Code = 1, Success = true, Message = "停止调度成功" }
                : new Result() { Code = 2, Success = false, Message = "停止调度失败" };
        }

        /// <summary>
        /// 获取所有任务
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result> GetAllJob()
        {
            var result = await _schedulerCenter.GetAllJobAsync();
            return new Result() { Code = 1, Success = true, Data = result };
        }

        /// <summary>
        /// 获取所有任务信息（简要信息 - 刷新数据的时候使用）
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result> GetAllJobBrief()
        {
            var result = await _schedulerCenter.GetAllJobBriefAsync();
            return new Result() { Code = 1, Success = true, Data = result };
        }
    }
}
