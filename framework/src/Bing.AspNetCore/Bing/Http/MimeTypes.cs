// ReSharper disable UnusedMember.Global
namespace Bing.Http;

/// <summary>
/// 媒体类型。
/// 参考：https://gist.github.com/markwhitaker/b29c0142360714688a7cf863ab33e5c9
/// </summary>
public static class MimeTypes
{
    /// <summary>
    /// 应用程序类型
    /// </summary>
    public static class Application
    {
        /// <summary>Atom XML 媒体类型。</summary>
        public const string AtomXml = "application/atom+xml";
        /// <summary>Atom 服务文档媒体类型。</summary>
        public const string AtomcatXml = "application/atomcat+xml";
        /// <summary>ECMAScript 脚本媒体类型。</summary>
        public const string Ecmascript = "application/ecmascript";
        /// <summary>Java Archive 媒体类型。</summary>
        public const string JavaArchive = "application/java-archive";
        /// <summary>JavaScript 脚本媒体类型。</summary>
        public const string Javascript = "application/javascript";
        /// <summary>JSON 数据媒体类型。</summary>
        public const string Json = "application/json";
        /// <summary>MP4 应用程序媒体类型。</summary>
        public const string Mp4 = "application/mp4";
        /// <summary>通用二进制流媒体类型。</summary>
        public const string OctetStream = "application/octet-stream";
        /// <summary>PDF 文档媒体类型。</summary>
        public const string Pdf = "application/pdf";
        /// <summary>PKCS #10 证书请求媒体类型。</summary>
        public const string Pkcs10 = "application/pkcs10";
        /// <summary>PKCS #7 MIME 媒体类型。</summary>
        public const string Pkcs7Mime = "application/pkcs7-mime";
        /// <summary>PKCS #7 签名媒体类型。</summary>
        public const string Pkcs7Signature = "application/pkcs7-signature";
        /// <summary>PKCS #8 媒体类型。</summary>
        public const string Pkcs8 = "application/pkcs8";
        /// <summary>PostScript 文档媒体类型。</summary>
        public const string Postscript = "application/postscript";
        /// <summary>RDF XML 媒体类型。</summary>
        public const string RdfXml = "application/rdf+xml";
        /// <summary>RSS XML 媒体类型。</summary>
        public const string RssXml = "application/rss+xml";
        /// <summary>RTF 文档媒体类型。</summary>
        public const string Rtf = "application/rtf";
        /// <summary>SMIL XML 媒体类型。</summary>
        public const string SmilXml = "application/smil+xml";
        /// <summary>OpenType 字体媒体类型。</summary>
        public const string XFontOtf = "application/x-font-otf";
        /// <summary>TrueType 字体媒体类型。</summary>
        public const string XFontTtf = "application/x-font-ttf";
        /// <summary>Web Open Font Format 字体媒体类型。</summary>
        public const string XFontWoff = "application/x-font-woff";
        /// <summary>PKCS #12 媒体类型。</summary>
        public const string XPkcs12 = "application/x-pkcs12";
        /// <summary>Shockwave Flash 媒体类型。</summary>
        public const string XShockwaveFlash = "application/x-shockwave-flash";
        /// <summary>Silverlight 应用程序媒体类型。</summary>
        public const string XSilverlightApp = "application/x-silverlight-app";
        /// <summary>XHTML XML 媒体类型。</summary>
        public const string XhtmlXml = "application/xhtml+xml";
        /// <summary>XML 文档媒体类型。</summary>
        public const string Xml = "application/xml";
        /// <summary>XML DTD 媒体类型。</summary>
        public const string XmlDtd = "application/xml-dtd";
        /// <summary>XSLT XML 媒体类型。</summary>
        public const string XsltXml = "application/xslt+xml";
        /// <summary>ZIP 压缩文件媒体类型。</summary>
        public const string Zip = "application/zip";
    }

    /// <summary>
    /// 音频类型
    /// </summary>
    public static class Audio
    {
        /// <summary>MIDI 音频媒体类型。</summary>
        public const string Midi = "audio/midi";
        /// <summary>MP4 音频媒体类型。</summary>
        public const string Mp4 = "audio/mp4";
        /// <summary>MPEG 音频媒体类型。</summary>
        public const string Mpeg = "audio/mpeg";
        /// <summary>Ogg 音频媒体类型。</summary>
        public const string Ogg = "audio/ogg";
        /// <summary>WebM 音频媒体类型。</summary>
        public const string Webm = "audio/webm";
        /// <summary>AAC 音频媒体类型。</summary>
        public const string XAac = "audio/x-aac";
        /// <summary>AIFF 音频媒体类型。</summary>
        public const string XAiff = "audio/x-aiff";
        /// <summary>MPEG URL 音频媒体类型。</summary>
        public const string XMpegurl = "audio/x-mpegurl";
        /// <summary>Windows Media Audio 媒体类型。</summary>
        public const string XMsWma = "audio/x-ms-wma";
        /// <summary>WAV 音频媒体类型。</summary>
        public const string XWav = "audio/x-wav";
    }

    /// <summary>
    /// 图片类型
    /// </summary>
    public static class Image
    {
        /// <summary>BMP 图片媒体类型。</summary>
        public const string Bmp = "image/bmp";
        /// <summary>GIF 图片媒体类型。</summary>
        public const string Gif = "image/gif";
        /// <summary>JPEG 图片媒体类型。</summary>
        public const string Jpeg = "image/jpeg";
        /// <summary>PNG 图片媒体类型。</summary>
        public const string Png = "image/png";
        /// <summary>SVG XML 图片媒体类型。</summary>
        public const string SvgXml = "image/svg+xml";
        /// <summary>TIFF 图片媒体类型。</summary>
        public const string Tiff = "image/tiff";
        /// <summary>WebP 图片媒体类型。</summary>
        public const string Webp = "image/webp";
    }

    /// <summary>
    /// 文本类型
    /// </summary>
    public static class Text
    {
        /// <summary>CSS 文本媒体类型。</summary>
        public const string Css = "text/css";
        /// <summary>CSV 文本媒体类型。</summary>
        public const string Csv = "text/csv";
        /// <summary>HTML 文本媒体类型。</summary>
        public const string Html = "text/html";
        /// <summary>纯文本媒体类型。</summary>
        public const string Plain = "text/plain";
        /// <summary>富文本媒体类型。</summary>
        public const string RichText = "text/richtext";
        /// <summary>SGML 文本媒体类型。</summary>
        public const string Sgml = "text/sgml";
        /// <summary>YAML 文本媒体类型。</summary>
        public const string Yaml = "text/yaml";
    }

    /// <summary>
    /// 视频类型
    /// </summary>
    public static class Video
    {
        /// <summary>3GPP 视频媒体类型。</summary>
        public const string Threegpp = "video/3gpp";
        /// <summary>H.264 视频媒体类型。</summary>
        public const string H264 = "video/h264";
        /// <summary>MP4 视频媒体类型。</summary>
        public const string Mp4 = "video/mp4";
        /// <summary>MPEG 视频媒体类型。</summary>
        public const string Mpeg = "video/mpeg";
        /// <summary>Ogg 视频媒体类型。</summary>
        public const string Ogg = "video/ogg";
        /// <summary>QuickTime 视频媒体类型。</summary>
        public const string Quicktime = "video/quicktime";
        /// <summary>WebM 视频媒体类型。</summary>
        public const string Webm = "video/webm";
    }

    /// <summary>
    /// 通过扩展名获取MIME类型
    /// </summary>
    /// <param name="extensions">扩展名</param>
    /// <returns>默认：application/octet-stream</returns>
    public static string GetByExtensions(string extensions)
    {
        extensions = extensions.TrimStart('.').ToLowerInvariant();
        switch (extensions)
        {
            case "png":
                return Image.Png;
            case "gif":
                return Image.Gif;
            case "jpg":
            case "jpeg":
                return Image.Jpeg;
            default:
                return Application.OctetStream;
        }
    }
}
