namespace Bing.Tools.ExpressDelivery.Kdniao.Enums
{
    /// <summary>
    /// 快递接口请求类型
    /// </summary>
    public enum KdniaoRequestType
    {
        /// <summary>
        /// 下单类接口-预约取件
        /// </summary>
        Order = 1001,
        /// <summary>
        /// 下单类接口-电子面单
        /// </summary>
        EOrder = 1007,
        /// <summary>
        /// 下单类接口-代收货款
        /// </summary>
        Cod = 9001,

        /// <summary>
        /// 查询类接口-即时查询
        /// </summary>
        Track = 1002,
        /// <summary>
        /// 查询类接口-物流跟踪
        /// </summary>
        Follow = 1008,
        /// <summary>
        /// 查询类接口-在途监控
        /// </summary>
        Monitor = 8001,

        /// <summary>
        /// 增值类接口-隐私快递
        /// </summary>
        SafeMail = 3001,
        /// <summary>
        /// 增值类接口-智选物流
        /// </summary>
        ExpreCommend = 2006,
        /// <summary>
        /// 增值类接口-物流短信
        /// </summary>
        Sms = 8102,
        /// <summary>
        /// 增值类接口-单号识别
        /// </summary>
        Recognise = 2002,
        /// <summary>
        /// 增值类接口-物流评价
        /// </summary>
        Evaluate = 1011,
        /// <summary>
        /// 增值类接口-实名寄递
        /// </summary>
        Verified = 1021,

        /// <summary>
        /// 其他类接口-申请电子面单客户号
        /// </summary>
        ApplyEOrder = 1107,
        /// <summary>
        /// 其他类接口-发送短信
        /// </summary>
        SmsSend = 8101,
        /// <summary>
        /// 其他类接口-短信黑名单
        /// </summary>
        SmsBlacklisk = 8103,
        /// <summary>
        /// 其他类接口-导入运费模板
        /// </summary>
        ImportTemplate = 2004,
    }
}
