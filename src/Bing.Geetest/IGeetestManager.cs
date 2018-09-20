using System.Threading.Tasks;
using Bing.Geetest.Models;

namespace Bing.Geetest
{
    /// <summary>
    /// 极验管理器
    /// </summary>
    public interface IGeetestManager
    {
        /// <summary>
        /// 验证初始化预处理
        /// </summary>
        /// <param name="userId">用户标识</param>
        /// <param name="clientType">客户端类型，web=pc浏览器,h5=手机浏览器,native=原生app,unknown=未知</param>
        /// <param name="ipAddress">IP地址，客户端请求服务器的IP地址，unknnow表示未知</param>
        /// <returns></returns>
        Task<GeetestRegisterResult> Register(string userId = null, string clientType = "unknown",
            string ipAddress = "unknnow");

        /// <summary>
        /// 二次验证
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns></returns>
        Task<bool> Validate(GeetestValidateParameter parameter);
    }
}
