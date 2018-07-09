using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QRCoder;

namespace Bing.Tools.QrCode.QRCoder
{
    /// <summary>
    /// QRCoder 二维码服务
    /// </summary>
    public class QRCoderQrCodeService:IQrCodeService
    {
        /// <summary>
        /// 二维码尺寸
        /// </summary>
        private int _size;

        /// <summary>
        /// 容错级别
        /// </summary>
        private QRCodeGenerator.ECCLevel _level;

        /// <summary>
        /// Logo路径
        /// </summary>
        private string _logoPath;

        /// <summary>
        /// 初始化一个<see cref="QRCoderQrCodeService"/>类型的实例
        /// </summary>
        public QRCoderQrCodeService()
        {
            _size = 10;
            _level = QRCodeGenerator.ECCLevel.L;
            _logoPath = string.Empty;
        }

        /// <summary>
        /// 设置二维码尺寸
        /// </summary>
        /// <param name="size">二维码尺寸</param>
        /// <returns></returns>
        public IQrCodeService Size(int size)
        {
            _size = size;
            return this;
        }

        /// <summary>
        /// 设置容错处理
        /// </summary>
        /// <param name="level">容错级别</param>
        /// <returns></returns>
        public IQrCodeService Correction(ErrorCorrectionLevel level)
        {
            switch (level)
            {
                case ErrorCorrectionLevel.L:
                    _level = QRCodeGenerator.ECCLevel.L;
                    break;
                case ErrorCorrectionLevel.M:
                    _level = QRCodeGenerator.ECCLevel.M;
                    break;
                case ErrorCorrectionLevel.Q:
                    _level = QRCodeGenerator.ECCLevel.Q;
                    break;
                case ErrorCorrectionLevel.H:
                    _level = QRCodeGenerator.ECCLevel.H;
                    break;
            }

            return this;
        }

        /// <summary>
        /// 设置Logo
        /// </summary>
        /// <param name="logoPath">logo文件路径</param>
        /// <returns></returns>
        public IQrCodeService Logo(string logoPath)
        {
            _logoPath = logoPath;
            return this;
        }

        /// <summary>
        /// 创建二维码
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        public virtual byte[] CreateQrCode(string content)
        {
            QRCodeGenerator generator=new QRCodeGenerator();
            QRCodeData data = generator.CreateQrCode(content, _level);
            if (string.IsNullOrWhiteSpace(_logoPath))
            {
                BitmapByteQRCode bitmapByteQrCode = new BitmapByteQRCode(data);
                return bitmapByteQrCode.GetGraphic(_size);
            }

            QRCode qrCode=new QRCode(data);
            using (var bitmap= qrCode.GetGraphic(_size, Color.Black, Color.White, GetLogo()))
            {
                using (var ms=new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// 获取Logo文件
        /// </summary>
        /// <returns></returns>
        protected virtual Bitmap GetLogo()
        {
            if (string.IsNullOrWhiteSpace(_logoPath))
            {
                return null;
            }

            return (Bitmap) Image.FromFile(_logoPath);
        }
    }
}
