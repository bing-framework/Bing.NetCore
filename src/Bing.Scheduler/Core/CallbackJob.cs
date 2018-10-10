using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using Newtonsoft.Json;

namespace Bing.Scheduler.Core
{
    /// <summary>
    /// 回调任务
    /// </summary>
    public sealed class CallbackJob : JobBase
    {
        /// <summary>
        /// 回调地址
        /// </summary>
        [Required]
        [Url]
        public string Url { get; set; }

        /// <summary>
        /// 调用方法
        /// </summary>
        public HttpMethod Method { get; set; }

        /// <summary>
        /// 重写转换成字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
