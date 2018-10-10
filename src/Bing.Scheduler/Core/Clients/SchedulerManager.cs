using System;
using System.Net.Http;
using System.Text;
using Bing.Scheduler.Abstractions;
using Bing.Scheduler.Abstractions.Clients;
using Bing.Scheduler.Exceptions;
using Newtonsoft.Json;

namespace Bing.Scheduler.Core.Clients
{
    /// <summary>
    /// 任务调度管理器
    /// </summary>
    public class SchedulerManager:ISchedulerManager
    {
        /// <summary>
        /// Http客户端
        /// </summary>
        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>
        /// 主机名
        /// </summary>
        private readonly string _host;

        /// <summary>
        /// 版本号
        /// </summary>
        private readonly string _version;

        /// <summary>
        /// 访问令牌
        /// </summary>
        private readonly string _token;

        /// <summary>
        /// 普通任务
        /// </summary>
        private const string Job = "job";

        /// <summary>
        /// 回调任务
        /// </summary>
        private const string CallbackJob = "callbackjob";

        /// <summary>
        /// 服务鉴权的 Header 名称
        /// </summary>
        public string TokenHeader { get; set; } = "BingScheduler";

        /// <summary>
        /// 初始化一个<see cref="SchedulerManager"/>类型的实例
        /// </summary>
        /// <param name="service">任务调度中心服务地址</param>
        /// <param name="token">访问令牌</param>
        /// <param name="version">Api版本</param>
        public SchedulerManager(string service, string token = null, string version = "v1.0")
        {
            _host = new Uri(service).ToString();
            _version = version;
            _token = token;
        }

        /// <summary>
        /// 创建普通任务
        /// </summary>
        /// <param name="job">任务信息</param>
        /// <returns></returns>
        public string CreateJob(Job job)
        {
            return Create(Job, job);
        }

        /// <summary>
        /// 创建回调任务信息
        /// </summary>
        /// <param name="job">回调任务信息</param>
        /// <returns></returns>
        public string CreateCallbackJob(CallbackJob job)
        {
            return Create(CallbackJob, job);
        }

        /// <summary>
        /// 更新普通任务
        /// </summary>
        /// <param name="job">任务信息</param>
        public void UpdateJob(Job job)
        {
            Update(Job, job);
        }

        /// <summary>
        /// 更新回调任务信息
        /// </summary>
        /// <param name="job">回调任务信息</param>
        public void UpdateCallbackJob(CallbackJob job)
        {
            Update(CallbackJob, job);
        }

        /// <summary>
        /// 删除普通任务
        /// </summary>
        /// <param name="id">任务标识</param>
        public void DeleteJob(string id)
        {
            Delete(Job, id);
        }

        /// <summary>
        /// 删除回调任务
        /// </summary>
        /// <param name="id">任务标识</param>
        public void DeleteCallbackJob(string id)
        {
            Delete(CallbackJob, id);
        }

        /// <summary>
        /// 触发普通任务
        /// </summary>
        /// <param name="id">任务标识</param>
        public void FireJob(string id)
        {
            Fire(Job, id);
        }

        /// <summary>
        /// 触发回调任务
        /// </summary>
        /// <param name="id">任务标识</param>
        public void FireCallbackJob(string id)
        {
            Fire(CallbackJob, id);
        }

        /// <summary>
        /// 创建任务
        /// </summary>
        /// <param name="jobType">任务类型</param>
        /// <param name="job">任务信息</param>
        /// <returns></returns>
        private string Create(string jobType, IJob job)
        {
            var url = $"{_host}api/{_version}/{jobType}";
            var msg = new HttpRequestMessage(HttpMethod.Post, url);
            AddTokenHeader(msg);
            job.Id = null;
            msg.Content = new StringContent(JsonConvert.SerializeObject(job), Encoding.UTF8, "application/json");
            var response = HttpClient.SendAsync(msg).Result;
            return CheckResult(response);
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        /// <param name="jobType">任务类型</param>
        /// <param name="id">任务标识</param>
        /// <returns></returns>
        private string Delete(string jobType, string id)
        {
            var url = $"{_host}api/{_version}/{jobType}/{id}";
            var msg = new HttpRequestMessage(HttpMethod.Delete, url);
            AddTokenHeader(msg);
            var response = HttpClient.SendAsync(msg).Result;
            return CheckResult(response);
        }

        /// <summary>
        /// 更新任务
        /// </summary>
        /// <param name="jobType">任务类型</param>
        /// <param name="job">任务信息</param>
        private void Update(string jobType, IJob job)
        {
            var url = $"{_host}api/{_version}/{jobType}";
            var msg = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(job), Encoding.UTF8, "application/json")
            };
            AddTokenHeader(msg);
            var response = HttpClient.SendAsync(msg).Result;
            CheckResult(response);
        }

        /// <summary>
        /// 触发任务
        /// </summary>
        /// <param name="jobType">任务类型</param>
        /// <param name="id">任务信息</param>
        private void Fire(string jobType, string id)
        {
            var url = $"{_host}api/{_version}/{jobType}";
            var msg = new HttpRequestMessage(HttpMethod.Get, url);
            AddTokenHeader(msg);
            var response = HttpClient.SendAsync(msg).Result;
            CheckResult(response);
        }

        /// <summary>
        /// 验证结果
        /// </summary>
        /// <param name="response">响应消息</param>
        /// <returns></returns>
        private string CheckResult(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();
            var result = response.Content.ReadAsStringAsync().Result;
            var json = JsonConvert.DeserializeObject<Result>(result);
            if (!json.Success)
            {
                throw new SchedulerException(json.Message);
            }

            return json.Data?.ToString();
        }

        /// <summary>
        /// 添加访问令牌请求头
        /// </summary>
        /// <param name="request">请求消息</param>
        private void AddTokenHeader(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(_token))
            {
                request.Headers.Add(TokenHeader,_token);
            }
        }
    }
}
