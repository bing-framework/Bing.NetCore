using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Bing.Tools.ExpressDelivery.Exceptions;
using Bing.Tools.ExpressDelivery.Kdniao.Configuration;
using Bing.Tools.ExpressDelivery.Kdniao.Core;
using Bing.Tools.ExpressDelivery.Kdniao.Enums;
using Bing.Tools.ExpressDelivery.Kdniao.Models;
using Bing.Tools.ExpressDelivery.Kdniao.Models.Results;
using Newtonsoft.Json;
using WebApiClient;

namespace Bing.Tools.ExpressDelivery.Kdniao
{
    /// <summary>
    /// 快递鸟 客户端
    /// </summary>
    public class KdniaoClient
    {
        /// <summary>
        /// 配置
        /// </summary>
        private readonly KdniaoConfig _config;

        /// <summary>
        /// 账户
        /// </summary>
        private readonly KdniaoAccount _account;

        /// <summary>
        /// 代理接口
        /// </summary>
        private readonly IKdniaoApi _proxy;

        /// <summary>
        /// 异常处理器
        /// </summary>
        private readonly Action<Exception> _exceptionHandler;

        /// <summary>
        /// 初始化一个<see cref="KdniaoClient"/>类型的实例
        /// </summary>
        /// <param name="config">配置</param>
        /// <param name="exceptionHandler">异常处理操作</param>
        public KdniaoClient(KdniaoConfig config, Action<Exception> exceptionHandler = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _account = config.Account ?? throw new ArgumentNullException(nameof(config.Account));
            _proxy = HttpApiClient.Create<IKdniaoApi>();

            var globalHandle = ExceptionHandleResolver.ResolveHandler();
            globalHandle += exceptionHandler;
            _exceptionHandler = globalHandle;
        }

        /// <summary>
        /// 即时查询
        /// </summary>
        /// <param name="request">即时查询请求</param>
        /// <returns></returns>
        public async Task<QueryKdniaoTrackResult> TracKAsync(QueryKdniaoTrack request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (string.IsNullOrWhiteSpace(_account.MerchantId))
            {
                throw new ArgumentNullException(nameof(_account.MerchantId));
            }
            if (string.IsNullOrWhiteSpace(_account.ApiKey))
            {
                throw new ArgumentNullException(nameof(_account.ApiKey));
            }

            request.CheckParameters();

            var json = JsonConvert.SerializeObject(request);

            var bizParams = new SortedDictionary<string, string>()
            {
                // 请求内容需要进行URL(utf-8)编码。请求内容JSON格式，须和DataType一致
                {"RequestData", HttpUtility.UrlEncode(json,Encoding.UTF8)},
                //商户ID，请在我的服务页面查看
                {"EBusinessID", _account.MerchantId},
                // 请求指令类型
                {"RequestType", ((int) KdniaoRequestType.Track).ToString()},
                // 数据内容签名。把(请求内容(未编码)+AppKey)进行MD5加密，然后Base64编码，最后进行URL(utf-8)编码
                {"DataSign", HttpUtility.UrlEncode(Sign(json, _account.ApiKey), Encoding.UTF8)},
                // 请求、返回数据类型那：2=json
                {"DataType", "2"},
            };

            var content = new FormUrlEncodedContent(bizParams);            
            return await _proxy.TrackAsync(content)
                .Retry(_config.RetryTimes)
                .Handle()
                .WhenCatch<Exception>(e =>
                {
                    _exceptionHandler?.Invoke(e);
                    return ReturnAsDefaultResponse();
                });
        }

        /// <summary>
        /// 数据签名
        /// </summary>
        /// <param name="content">内容</param>
        /// <param name="apiKey">ApiKey</param>
        /// <returns></returns>
        private static string Sign(string content,string apiKey)
        {
            return Base64(Md5($"{content}{apiKey}"));
        }

        /// <summary>
        /// MD5加密
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        private static string Md5(string value, Encoding encoding = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            var md5 = new MD5CryptoServiceProvider();
            string result;
            try
            {
                var hash = md5.ComputeHash(encoding.GetBytes(value));
                result = BitConverter.ToString(hash);                
            }
            finally
            {
                md5.Clear();
            }

            return result.Replace("-", "").ToLower();
        }

        /// <summary>
        /// Base64加密
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        private static string Base64(string value, Encoding encoding = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            return Convert.ToBase64String(encoding.GetBytes(value));
        }

        /// <summary>
        /// 返回默认响应结果
        /// </summary>
        /// <returns></returns>
        private static QueryKdniaoTrackResult ReturnAsDefaultResponse() => new QueryKdniaoTrackResult()
        {
            Success = false,
            Reason = "解析错误，返回默认结果"
        };
    }
}
