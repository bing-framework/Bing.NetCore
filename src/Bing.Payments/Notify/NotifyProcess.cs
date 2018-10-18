using System;
using System.IO;
using System.Linq;
using Bing.Payments.Gateways;
using Bing.Utils;
using Bing.Utils.Helpers;

namespace Bing.Payments.Notify
{
    /// <summary>
    /// 网关通知处理。通过对返回数据的分析识别网关类型
    /// </summary>
    internal static class NotifyProcess
    {
        /// <summary>
        /// 是否Xml格式数据
        /// </summary>
        private static bool IsXmlData => Web.ContentType == "text/xml" || Web.ContentType == "application/xml";

        /// <summary>
        /// 是否Get请求
        /// </summary>
        private static bool IsGetRequest => Web.RequestType == "GET";

        /// <summary>
        /// 获取网关
        /// </summary>
        /// <param name="contoiner">网关容器</param>
        /// <returns></returns>
        public static GatewayBase GetGateway(IGatewayContoiner contoiner)
        {
            var gatewayData = ReadNotifData();
            GatewayBase gateway = null;
            foreach (var item in contoiner.GetList())
            {
                if (ExistParameter(item.NotifyVerifyParameter, gatewayData))
                {
                    if (item.Merchant.AppId == gatewayData.GetStringValue(item.NotifyVerifyParameter.FirstOrDefault()))
                    {
                        gateway = item;
                        break;
                    }
                }
            }

            if (gateway is null)
            {
                gateway = new NullGateway();
            }

            gateway.GatewayData = gatewayData;
            return gateway;
        }

        /// <summary>
        /// 网关参数数据项中是否存在指定的所有参数名
        /// </summary>
        /// <param name="paraNames">参数名数组</param>
        /// <param name="gatewayData">网关数据</param>
        /// <returns></returns>
        public static bool ExistParameter(string[] paraNames, GatewayData gatewayData)
        {
            int compareCount = 0;
            foreach (var item in paraNames)
            {
                if (gatewayData.Exists(item))
                {
                    compareCount++;
                }
            }

            if (compareCount == paraNames.Length)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 读取网关发回的数据
        /// </summary>
        /// <returns></returns>
        public static GatewayData ReadNotifData()
        {
            var gatewayData = new GatewayData();
            if (IsGetRequest)
            {
                gatewayData.FromUrl(Web.QueryString);
            }
            else
            {
                if (IsXmlData)
                {
                    var xmlData = Web.Body;
                    gatewayData.FromXml(xmlData);
                }
                else
                {
                    try
                    {
                        gatewayData.FromForm(Web.Form);
                    }
                    catch { }
                }
            }

            return gatewayData;
        }
    }
}
