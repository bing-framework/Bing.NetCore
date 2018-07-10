using System.Drawing;

namespace Bing.Tools.QrCode
{
    /// <summary>
    /// 二维码 服务
    /// </summary>
    public interface IQrCodeService
    {
        /// <summary>
        /// 设置二维码尺寸
        /// </summary>
        /// <param name="size">二维码尺寸</param>
        /// <returns></returns>
        IQrCodeService Size(int size);

        /// <summary>
        /// 设置容错处理
        /// </summary>
        /// <param name="level">容错级别</param>
        /// <returns></returns>
        IQrCodeService Correction(ErrorCorrectionLevel level);

        /// <summary>
        /// 设置Logo
        /// </summary>
        /// <param name="logoPath">logo文件路径</param>
        /// <returns></returns>
        IQrCodeService Logo(string logoPath);

        ///// <summary>
        ///// 设置前景色
        ///// </summary>
        ///// <param name="color">颜色</param>
        ///// <returns></returns>
        //IQrCodeService Foreground(Color color);

        ///// <summary>
        ///// 设置背景色
        ///// </summary>
        ///// <param name="color">颜色</param>
        ///// <returns></returns>
        //IQrCodeService Background(Color color);

        /// <summary>
        /// 创建二维码
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        byte[] CreateQrCode(string content);
    }
}
