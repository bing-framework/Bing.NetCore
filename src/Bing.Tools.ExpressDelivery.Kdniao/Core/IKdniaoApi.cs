using System.Net.Http;
using Bing.Tools.ExpressDelivery.Kdniao.Models.Results;
using WebApiClient;
using WebApiClient.Attributes;

namespace Bing.Tools.ExpressDelivery.Kdniao.Core
{
    /// <summary>
    /// 快递鸟 API
    /// </summary>
    [HttpHost("http://api.kdniao.cc")]
    public interface IKdniaoApi:IHttpApiClient
    {
        /// <summary>
        /// 即时查询
        /// </summary>
        /// <param name="content">请求内容</param>
        /// <returns></returns>
        [HttpPost("/Ebusiness/EbusinessOrderHandle.aspx")]
        [JsonReturn]
        ITask<QueryKdniaoTrackResult> TrackAsync(FormUrlEncodedContent content);
    }
}
