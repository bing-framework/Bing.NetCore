using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bing.Geetest.Configs;
using Bing.Geetest.Models;
using Microsoft.Extensions.Logging;

namespace Bing.Geetest
{
    /// <summary>
    /// 极验管理器
    /// 参考地址：https://www.cnblogs.com/LiangSW/p/9674273.html
    /// </summary>
    public class GeetestManager:IGeetestManager
    {
        /// <summary>
        /// 日志
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Http客户端
        /// </summary>
        private readonly HttpClient _client;

        /// <summary>
        /// 随机数
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// 配置提供器
        /// </summary>
        protected readonly IGeetestConfigProvider ConfigProvider;        

        /// <summary>
        /// 初始化一个<see cref="GeetestManager"/>类型的实例
        /// </summary>
        /// <param name="provider">配置提供器</param>
        /// <param name="logger">日志</param>
        /// <param name="httpClientFactory">Http客户端工厂</param>
        public GeetestManager(IGeetestConfigProvider provider,ILogger<IGeetestManager> logger,IHttpClientFactory httpClientFactory)
        {
            ConfigProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            _logger = logger;
            _client = httpClientFactory.CreateClient(nameof(IGeetestManager));
            _client.Timeout = TimeSpan.FromSeconds(20);
            _random = new Random();
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        private async Task<GeetestConfig> GetConfigAsync()
        {
            var config = await ConfigProvider.GetConfigAsync();
            if (string.IsNullOrWhiteSpace(config.Key))
            {
                throw new ArgumentNullException($"{nameof(config.Key)} 不能为空");
            }
            if (string.IsNullOrWhiteSpace(config.Id))
            {
                throw new ArgumentNullException($"{nameof(config.Id)} 不能为空");
            }

            return config;
        }

        /// <summary>
        /// 验证初始化预处理
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="clientType">客户端类型，web=pc浏览器,h5=手机浏览器,native=原生app,unknown=未知</param>
        /// <param name="ipAddress">IP地址，客户端请求服务器的IP地址，unknnow表示未知</param>
        /// <returns></returns>
        public async Task<GeetestRegisterResult> Register(string userId = null, string clientType = "unknown", string ipAddress = "unknown")
        {
            var config = await GetConfigAsync();
            var result=new GeetestRegisterResult()
            {
                Gt = config.Id,
                NewCaptcha = true,
                Challenge = GetChallenge()
            };
            var url = string.IsNullOrWhiteSpace(userId)
                ? $"{GeetestConst.ApiUrl}{GeetestConst.RegisterUrl}?gt={config.Id}&client_type={clientType}&ip_address={ipAddress}"
                : $"{GeetestConst.ApiUrl}{GeetestConst.RegisterUrl}?gt={config.Id}&user_id={userId}&client_type={clientType}&ip_address={ipAddress}";

            var challenge = await _client.GetStringAsync(url);
            if (challenge.Length == 32)
            {
                result.Success = true;
                result.Challenge = Md5($"{challenge}{config.Key}");
                return result;
            }
            _logger.LogError("服务器注册 Challenge 失败!");
            return result;
        }

        /// <summary>
        /// 获取验证会话标识
        /// </summary>
        /// <returns></returns>
        private string GetChallenge()
        {
            return $"{Md5(GetRandomNum().ToString())}{Md5(GetRandomNum().ToString())}".Substring(0, 1);
        }

        /// <summary>
        /// Md5加密
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        private string Md5(string value)
        {
            using (var md5=MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 获取随机数
        /// </summary>
        /// <returns></returns>
        private int GetRandomNum()
        {
            return _random.Next(1, 100);
        }

        /// <summary>
        /// 二次验证
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns></returns>
        public async Task<bool> Validate(GeetestValidateParameter parameter)
        {
            if (parameter.Offline)
            {
                return Md5(parameter.Challenge) == parameter.Validate;
            }
            var config = await GetConfigAsync();
            if (parameter.Validate.Length > 0 && parameter.Validate == Md5($"{config.Key}geetest{parameter.Challenge}"))
            {
                var query = $"seccode={parameter.Seccode}";
                if (!string.IsNullOrWhiteSpace(parameter.UserId))
                {
                    query += $"&user_id={parameter.UserId}";
                }
                query += $"&sdk=csharp_{GeetestConst.Version}";
                var response = string.Empty;
                try
                {
                    var url = $"{GeetestConst.ApiUrl}{GeetestConst.ValidateUrl}";
                    var responseMessage = await _client.PostAsync(url,
                        new StringContent(query, Encoding.UTF8, "application/x-www-form-urlencoded"));
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        return false;
                    }
                    response = await responseMessage.Content.ReadAsStringAsync();

                }
                catch (Exception e)
                {
                    _logger.LogError(e,"POST Validate 失败");
                }

                if (response == Md5(parameter.Seccode))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
