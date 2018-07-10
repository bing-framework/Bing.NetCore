using System;
using System.DrawingCore;
using System.DrawingCore.Drawing2D;
using System.DrawingCore.Imaging;
using System.IO;
using ZXing;
using ZXing.QrCode;
using ZXing.Rendering;
using ZXing.ZKWeb.Rendering;
using ZQI = global::ZXing.QrCode.Internal;
using ZR=global::ZXing.Rendering;

namespace Bing.Tools.QrCode.ZXing
{
    /// <summary>
    /// ZXing.Net 二维码服务
    /// </summary>
    public class ZXingQrCodeService:IQrCodeService
    {
        /// <summary>
        /// 二维码尺寸
        /// </summary>
        private int _size;

        /// <summary>
        /// 容错级别
        /// </summary>
        private ZQI.ErrorCorrectionLevel _level;

        /// <summary>
        /// Logo路径
        /// </summary>
        private string _logoPath;

        /// <summary>
        /// 边距
        /// </summary>
        private int _margin;

        /// <summary>
        /// 前景色
        /// </summary>
        private System.Drawing.Color _foregroundColor;

        /// <summary>
        /// 背景色
        /// </summary>
        private System.Drawing.Color _backgroundColor;

        /// <summary>
        /// 初始化一个<see cref="ZXingQrCodeService"/>类型的实例
        /// </summary>
        public ZXingQrCodeService()
        {
            _size = 250;
            _level = ZQI.ErrorCorrectionLevel.L;
            _margin = 0;
            _logoPath = string.Empty;
            _foregroundColor = System.Drawing.Color.Black;
            _backgroundColor = System.Drawing.Color.White;
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
                    _level = ZQI.ErrorCorrectionLevel.L;
                    break;
                case ErrorCorrectionLevel.M:
                    _level = ZQI.ErrorCorrectionLevel.M;
                    break;
                case ErrorCorrectionLevel.Q:
                    _level = ZQI.ErrorCorrectionLevel.Q;
                    break;
                case ErrorCorrectionLevel.H:
                    _level = ZQI.ErrorCorrectionLevel.H;
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
        /// 设置前景色
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns></returns>
        public IQrCodeService Foreground(System.Drawing.Color color)
        {
            _foregroundColor = color;
            return this;
        }

        /// <summary>
        /// 设置背景色
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns></returns>
        public IQrCodeService Background(System.Drawing.Color color)
        {
            _backgroundColor = color;
            return this;
        }

        /// <summary>
        /// 创建二维码
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        public byte[] CreateQrCode(string content)
        {
            return string.IsNullOrWhiteSpace(_logoPath) ? CreateBaseQrCode(content) : CreateLogoQrCode(content);
        }

        /// <summary>
        /// 创建普通二维码
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        private byte[] CreateBaseQrCode(string content)
        {
            using (var bitmap = GetBitmap(content))
            {
                using (var ms = new MemoryStream())
                {
                    // 此处会导致容错等级 Q H 无效
                    //bitmap.MakeTransparent();
                    bitmap.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// 创建带有Logo的二维码
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        private byte[] CreateLogoQrCode(string content)
        {
            using (var bitmap = GetBitmap(content))
            {
                Bitmap logo = new Bitmap(_logoPath);
                return MergeQrImg(bitmap, logo);
            }
        }

        /// <summary>
        /// 获取二维码图片
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        private Bitmap GetBitmap(string content)
        {
            BarcodeWriter<Bitmap> bitmapBarcodeWriter = new BarcodeWriter<Bitmap>()
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions()
                {
                    CharacterSet = "UTF-8",
                    ErrorCorrection = _level,
                    Margin = _margin,
                    Width = _size,
                    Height = _size,
                },
                Renderer = new BitmapRenderer()
                {
                    Foreground = Color.FromName(_foregroundColor.Name),
                    Background = Color.FromName(_backgroundColor.Name)
                }
            };

            return bitmapBarcodeWriter.Write(content);
        }

        /// <summary>
        /// 合并二维码以及Logo
        /// 参考：http://www.cnblogs.com/zoro-zero/p/6225697.html
        /// </summary>
        /// <param name="qrImg">二维码图片</param>
        /// <param name="logoImg">logo图片</param>
        /// <param name="n"></param>
        /// <returns></returns>
        private static byte[] MergeQrImg(Bitmap qrImg, Bitmap logoImg, double n = 0.23)
        {
            int margin = 10;
            float dpix = qrImg.HorizontalResolution;
            float dpiy = qrImg.VerticalResolution;

            var newWidth = (10 * qrImg.Width - 46 * margin) * 1.0f / 46;
            var newLogoImg = ZoomPic(logoImg, newWidth / logoImg.Width);
            // 处理Logo
            int newImgWidth = newLogoImg.Width + margin;
            Bitmap logoBgImg = new Bitmap(newImgWidth, newImgWidth);
            logoBgImg.MakeTransparent();
            Graphics g = Graphics.FromImage(logoBgImg);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.Clear(Color.Transparent);
            Pen p = new Pen(new SolidBrush(Color.White));
            Rectangle rect = new Rectangle(0, 0, newImgWidth - 1, newImgWidth - 1);
            using (GraphicsPath path = CreateRoundedRectanglePath(rect, 7))
            {
                g.DrawPath(p, path);
                g.FillPath(new SolidBrush(Color.White), path);
            }
            // 画Logo
            Bitmap img1 = new Bitmap(newLogoImg.Width, newLogoImg.Height);
            Graphics g1 = Graphics.FromImage(img1);
            g1.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g1.SmoothingMode = SmoothingMode.HighQuality;
            g1.Clear(Color.Transparent);
            Pen p1 = new Pen(new SolidBrush(Color.Gray));
            Rectangle rect1 = new Rectangle(0, 0, newLogoImg.Width - 1, newLogoImg.Height - 1);
            using (GraphicsPath path1 = CreateRoundedRectanglePath(rect1, 7))
            {
                g1.DrawPath(p1, path1);
                TextureBrush brush = new TextureBrush(newLogoImg);
                g1.FillPath(brush, path1);
            }
            g1.Dispose();

            PointF center = new PointF((newImgWidth - newLogoImg.Width) / 2, (newImgWidth - newLogoImg.Height) / 2);
            g.DrawImage(img1, center.X, center.Y, newLogoImg.Width, newLogoImg.Height);
            g.Dispose();

            Bitmap backgroundImg = new Bitmap(qrImg.Width, qrImg.Height);
            backgroundImg.MakeTransparent();
            backgroundImg.SetResolution(dpix, dpiy);
            logoBgImg.SetResolution(dpix, dpiy);

            Graphics g2 = Graphics.FromImage(backgroundImg);
            g2.Clear(Color.Transparent);
            g2.DrawImage(qrImg, 0, 0);
            PointF center2 = new PointF((qrImg.Width - logoBgImg.Width) / 2, (qrImg.Height - logoBgImg.Height) / 2);
            g2.DrawImage(logoBgImg, center2);
            g2.Dispose();

            using (var ms = new MemoryStream())
            {
                backgroundImg.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// 创建圆角矩形
        /// </summary>
        /// <param name="rect">区域</param>
        /// <param name="cornerRadius">圆角角度</param>
        /// <returns></returns>
        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int cornerRadius)
        {
            GraphicsPath roundedRect = new GraphicsPath();
            roundedRect.AddArc(rect.X, rect.Y, cornerRadius * 2, cornerRadius * 2, 180, 90);
            roundedRect.AddLine(rect.X + cornerRadius, rect.Y, rect.Right - cornerRadius * 2, rect.Y);
            roundedRect.AddArc(rect.X + rect.Width - cornerRadius * 2, rect.Y, cornerRadius * 2, cornerRadius * 2, 270,
                90);
            roundedRect.AddLine(rect.Right, rect.Y + cornerRadius * 2, rect.Right,
                rect.Y + rect.Height - cornerRadius * 2);
            roundedRect.AddArc(rect.X + rect.Width - cornerRadius * 2, rect.Y + rect.Height - cornerRadius * 2,
                cornerRadius * 2, cornerRadius * 2, 0, 90);
            roundedRect.AddLine(rect.Right - cornerRadius * 2, rect.Bottom, rect.X + cornerRadius * 2, rect.Bottom);
            roundedRect.AddArc(rect.X, rect.Bottom - cornerRadius * 2, cornerRadius * 2, cornerRadius * 2, 90, 90);
            roundedRect.AddLine(rect.X, rect.Bottom - cornerRadius * 2, rect.X, rect.Y + cornerRadius * 2);

            roundedRect.CloseFigure();

            return roundedRect;
        }

        /// <summary>
        /// 图片按比例缩放
        /// </summary>
        /// <param name="initImage">需要缩放的图片</param>
        /// <param name="n">缩放比例</param>
        /// <returns></returns>
        private static Image ZoomPic(Image initImage, double n)
        {
            // 缩略图宽、高计算
            var newWidth = n * initImage.Width;
            var newHeight = n * initImage.Height;
            // 生成新图
            Image newImage = new Bitmap((int)newWidth, (int)newHeight);
            // 新建一个画板
            Graphics g = Graphics.FromImage(newImage);
            // 设置质量
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            // 设置背景色
            g.Clear(Color.Transparent);
            // 画图
            g.DrawImage(initImage, new Rectangle(0, 0, newImage.Width, newImage.Height),
                new Rectangle(0, 0, initImage.Width, initImage.Height),GraphicsUnit.Pixel);
            g.Dispose();
            return newImage;
        }
    }
}
