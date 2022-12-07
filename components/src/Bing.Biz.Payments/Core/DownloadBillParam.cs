namespace Bing.Biz.Payments.Core;

/// <summary>
/// 下载账单参数
/// </summary>
public class DownloadBillParam: DownloadBillParamBase
{
    /// <summary>
    /// 账单类型
    /// </summary>
    public string BillType { get; set; }

    /// <summary>
    /// 资金账户类型
    /// </summary>
    public string AccountType { get; set; }

    /// <summary>
    /// 压缩账单
    /// </summary>
    public string TarType { get; set; }
}