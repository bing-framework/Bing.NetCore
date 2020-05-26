using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Bing.Extensions;
using Bing.Utils.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Internal;

namespace Bing.Helpers
{
    /// <summary>
    /// Web操作
    /// </summary>
    public static partial class Web
    {
        #region 属性

        #region HttpContextAccessor(Http上下文访问器)

        /// <summary>
        /// Http上下文访问器
        /// </summary>
        public static IHttpContextAccessor HttpContextAccessor { get; set; }

        #endregion

        #region HttpContext(当前Http上下文)

        /// <summary>
        /// 当前Http上下文
        /// </summary>
        public static HttpContext HttpContext => HttpContextAccessor?.HttpContext;

        #endregion

        #region Environment(宿主环境)

        /// <summary>
        /// 宿主环境
        /// </summary>
        public static IHostingEnvironment Environment { get; set; }

        #endregion

        #region Request(当前Http请求)

        /// <summary>
        /// 当前Http请求
        /// </summary>
        public static HttpRequest Request => HttpContext?.Request;

        #endregion

        #region Response(当前Http响应)

        /// <summary>
        /// 当前Http响应
        /// </summary>
        public static HttpResponse Response => HttpContext?.Response;

        #endregion

        #region LocalIpAddress(本地IP)

        /// <summary>
        /// 本地IP
        /// </summary>
        public static string LocalIpAddress
        {
            get
            {
                try
                {
                    var ipAddress = HttpContext.Connection.LocalIpAddress;
                    return IPAddress.IsLoopback(ipAddress)
                        ? IPAddress.Loopback.ToString()
                        : ipAddress.MapToIPv4().ToString();
                }
                catch
                {
                    return IPAddress.Loopback.ToString();
                }
            }
        }

        #endregion

        #region RequestType(请求类型)

        /// <summary>
        /// 请求类型
        /// </summary>
        public static string RequestType => HttpContext?.Request?.Method;

        #endregion

        #region Form(表单)

        /// <summary>
        /// Form表单
        /// </summary>
        public static IFormCollection Form => HttpContext?.Request?.Form;

        #endregion

        #region Body(请求正文)

        /// <summary>
        /// 请求正文
        /// </summary>
        public static string Body
        {
            get
            {
                Request.EnableRewind();
                return FileHelper.ToString(Request.Body, isCloseStream: false);
            }
        }

        #endregion

        #region Url(请求地址)

        /// <summary>
        /// 请求地址
        /// </summary>
        public static string Url => HttpContext?.Request?.GetDisplayUrl();

        #endregion

        #region IP(客户端IP地址)

        /// <summary>
        /// IP地址
        /// </summary>
        private static string _ip;

        /// <summary>
        /// 设置IP地址
        /// </summary>
        /// <param name="ip">IP地址</param>
        public static void SetIp(string ip) => _ip = ip;

        /// <summary>
        /// 重置IP地址
        /// </summary>
        public static void ResetIp() => _ip = null;

        /// <summary>
        /// 客户端IP地址
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public static string IP
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_ip) == false)
                    return _ip;
                var list = new[] { "127.0.0.1", "::1" };
                var result = HttpContext?.Connection?.RemoteIpAddress.SafeString();
                if (string.IsNullOrWhiteSpace(result) || list.Contains(result))
                    result = Sys.IsWindows ? GetLanIP() : GetLanIP(NetworkInterfaceType.Ethernet);
                return result;
            }
        }

        /// <summary>
        /// 获取局域网IP
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static string GetLanIP()
        {
            foreach (var hostAddress in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (hostAddress.AddressFamily == AddressFamily.InterNetwork)
                    return hostAddress.ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// 获取局域网IP。
        /// 参考地址：https://stackoverflow.com/questions/6803073/get-local-ip-address/28621250#28621250
        /// 解决OSX下获取IP地址产生"Device not configured"的问题
        /// </summary>
        /// <param name="type">网络接口类型</param>
        // ReSharper disable once InconsistentNaming
        private static string GetLanIP(NetworkInterfaceType type)
        {
            try
            {
                foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (item.NetworkInterfaceType != type || item.OperationalStatus != OperationalStatus.Up)
                        continue;
                    var ipProperties = item.GetIPProperties();
                    if (ipProperties.GatewayAddresses.FirstOrDefault() == null)
                        continue;
                    foreach (var ip in ipProperties.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            return ip.Address.ToString();
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
            return string.Empty;
        }

        #endregion

        #region Host(主机)

        /// <summary>
        /// 主机
        /// </summary>
        public static string Host => HttpContext == null ? Dns.GetHostName() : GetClientHostName();

        /// <summary>
        /// 获取Web客户端主机名
        /// </summary>
        private static string GetClientHostName()
        {
            var address = GetRemoteAddress();
            if (string.IsNullOrWhiteSpace(address))
                return Dns.GetHostName();
            var result = Dns.GetHostEntry(IPAddress.Parse(address)).HostName;
            if (result == "localhost.localdomain")
                result = Dns.GetHostName();
            return result;
        }

        /// <summary>
        /// 获取远程地址
        /// </summary>
        private static string GetRemoteAddress() =>
            HttpContext?.Request?.Headers["HTTP_X_FORWARDED_FOR"] ??
            HttpContext?.Request?.Headers["REMOTE_ADDR"];

        #endregion

        #region Browser(浏览器)

        /// <summary>
        /// 浏览器
        /// </summary>
        public static string Browser => HttpContext?.Request?.Headers["User-Agent"];

        #endregion

        #region RootPath(根路径)

        /// <summary>
        /// 根路径
        /// </summary>
        public static string RootPath => Environment?.ContentRootPath;

        #endregion

        #region WebRootPath(Web根路径)

        /// <summary>
        /// Web根路径，即wwwroot
        /// </summary>
        public static string WebRootPath => Environment?.WebRootPath;

        #endregion

        #region ContentType(内容类型)

        /// <summary>
        /// 内容类型
        /// </summary>
        public static string ContentType => HttpContext?.Request?.ContentType;

        #endregion

        #region QueryString(参数)

        /// <summary>
        /// 参数
        /// </summary>
        public static string QueryString => HttpContext?.Request?.QueryString.ToString();

        #endregion

        #region IsLocal(是否本地请求)

        /// <summary>
        /// 是否本地请求
        /// </summary>
        public static bool IsLocal
        {
            get
            {
                var connection = HttpContext?.Request?.HttpContext?.Connection;
                if (connection == null)
                    throw new ArgumentNullException(nameof(connection));
                if (connection.RemoteIpAddress.IsSet())
                    return connection.LocalIpAddress.IsSet()
                        ? connection.RemoteIpAddress.Equals(connection.LocalIpAddress)
                        : IPAddress.IsLoopback(connection.RemoteIpAddress);
                return true;
            }
        }

        /// <summary>
        /// 空IP地址
        /// </summary>
        private const string NullIpAddress = "::1";

        /// <summary>
        /// 是否已设置IP地址
        /// </summary>
        /// <param name="address">IP地址</param>
        private static bool IsSet(this IPAddress address) => address != null && address.ToString() != NullIpAddress;

        #endregion

        #endregion

        #region 构造函数

        /// <summary>
        /// 静态构造函数
        /// </summary>
        static Web() => ServicePointManager.DefaultConnectionLimit = 200;

        #endregion

        #region Client(Web客户端)

        /// <summary>
        /// Web客户端，用于发送Http请求
        /// </summary>
        public static Bing.Utils.Webs.Clients.WebClient Client() => new Bing.Utils.Webs.Clients.WebClient();

        /// <summary>
        /// Web客户端，用于发送Http请求
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        public static Bing.Utils.Webs.Clients.WebClient<TResult> Client<TResult>() where TResult : class => new Bing.Utils.Webs.Clients.WebClient<TResult>();

        #endregion

        #region GetFiles(获取客户端文件集合)

        /// <summary>
        /// 获取客户端文件集合
        /// </summary>
        public static List<IFormFile> GetFiles()
        {
            var result = new List<IFormFile>();
            var files = HttpContext.Request.Form.Files;
            if (files == null || files.Count == 0)
                return result;
            result.AddRange(files.Where(file => file?.Length > 0));
            return result;
        }

        #endregion

        #region GetFile(获取客户端文件)

        /// <summary>
        /// 获取客户端文件
        /// </summary>
        public static IFormFile GetFile()
        {
            var files = GetFiles();
            return files.Count == 0 ? null : files[0];
        }

        #endregion

        #region GetParam(获取请求参数)

        /// <summary>
        /// 获取请求参数，搜索路径：查询参数->表单参数->请求头
        /// </summary>
        /// <param name="name">参数名</param>
        public static string GetParam(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            if (Request == null)
                return string.Empty;
            var result = string.Empty;
            if (Request.Query != null)
                result = Request.Query[name];
            if (string.IsNullOrWhiteSpace(result) == false)
                return result;
            if (Request.HasFormContentType && Request.Form != null)
                result = Request.Form[name];
            if (string.IsNullOrWhiteSpace(result) == false)
                return result;
            if (Request.Headers != null)
                result = Request.Headers[name];
            return result;
        }

        #endregion

        #region UrlEncode(Url编码)

        /// <summary>
        /// Url编码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="isUpper">编码字符是否转成大写，范例："http://"转成"http%3A%2F%2F"</param>
        public static string UrlEncode(string url, bool isUpper = false) => UrlEncode(url, Encoding.UTF8, isUpper);

        /// <summary>
        /// Url编码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="isUpper">编码字符是否转成大写，范例："http://"转成"http%3A%2F%2F"</param>
        public static string UrlEncode(string url, string encoding, bool isUpper = false)
        {
            encoding = string.IsNullOrWhiteSpace(encoding) ? "UTF-8" : encoding;
            return UrlEncode(url, Encoding.GetEncoding(encoding), isUpper);
        }

        /// <summary>
        /// Url编码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="encoding">字符编码</param>
        /// <param name="isUpper">编码字符是否转成大写，范例："http://"转成"http%3A%2F%2F"</param>
        public static string UrlEncode(string url, Encoding encoding, bool isUpper = false)
        {
            var result = HttpUtility.UrlEncode(url, encoding);
            return isUpper == false ? result : GetUpperEncode(result);
        }

        /// <summary>
        /// 获取大写编码字符串
        /// </summary>
        /// <param name="encode">编码字符串</param>
        private static string GetUpperEncode(string encode)
        {
            var result = new StringBuilder();
            var index = int.MinValue;
            for (var i = 0; i < encode.Length; i++)
            {
                var character = encode[i].ToString();
                if (character == "%")
                    index = i;
                if (i - index == 1 || i - index == 2)
                    character = character.ToUpper();
                result.Append(character);
            }
            return result.ToString();
        }

        #endregion

        #region UrlDecode(Url解码)

        /// <summary>
        /// Url解码
        /// </summary>
        /// <param name="url">url</param>
        public static string UrlDecode(string url) => HttpUtility.UrlDecode(url);

        /// <summary>
        /// Url解码
        /// </summary>
        /// <param name="url">url</param>
        /// <param name="encoding">字符编码</param>
        public static string UrlDecode(string url, Encoding encoding) => HttpUtility.UrlDecode(url, encoding);

        #endregion

        #region Redirect(跳转到指定链接)

        /// <summary>
        /// 跳转到指定链接
        /// </summary>
        /// <param name="url">链接</param>
        public static void Redirect(string url) => Response?.Redirect(url);

        #endregion

        #region Write(输出内容)

        /// <summary>
        /// 输出内容
        /// </summary>
        /// <param name="text">内容</param>
        public static void Write(string text)
        {
            Response.ContentType = "text/plain;charset=utf-8";
            Task.Run(async () => { await Response.WriteAsync(text); }).GetAwaiter().GetResult();
        }

        #endregion

        #region Write(输出文件)

        /// <summary>
        /// 输出文件
        /// </summary>
        /// <param name="stream">文件流</param>
        public static void Write(FileStream stream)
        {
            long size = stream.Length;
            byte[] buffer = new byte[size];
            stream.Read(buffer, 0, (int)size);
            stream.Dispose();
            File.Delete(stream.Name);

            Response.ContentType = "application/octet-stream";
            Response.Headers.Add("Content-Disposition", "attachment;filename=" + WebUtility.UrlEncode(Path.GetFileName(stream.Name)));
            Response.Headers.Add("Content-Length", size.ToString());

            Task.Run(async () => { await Response.Body.WriteAsync(buffer, 0, (int)size); }).GetAwaiter().GetResult();
            Response.Body.Close();
        }

        #endregion

        #region GetBodyAsync(获取请求正文)

        /// <summary>
        /// 获取请求正文
        /// </summary>
        public static async Task<string> GetBodyAsync()
        {
            Request.EnableRewind();
            return await FileHelper.ToStringAsync(Request.Body, isCloseStream: false);
        }

        #endregion

        #region DownloadAsync(下载)

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <param name="fileName">文件名。包含扩展名</param>
        public static async Task DownloadFileAsync(string filePath, string fileName) => await DownloadFileAsync(filePath, fileName, Encoding.UTF8);

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <param name="fileName">文件名。包含扩展名</param>
        /// <param name="encoding">字符编码</param>
        public static async Task DownloadFileAsync(string filePath, string fileName, Encoding encoding)
        {
            var bytes = FileHelper.ReadToBytes(filePath);
            await DownloadAsync(bytes, fileName, encoding);
        }

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="fileName">文件名。包含扩展名</param>
        public static async Task DownloadAsync(Stream stream, string fileName) => await DownloadAsync(stream, fileName, Encoding.UTF8);

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="fileName">文件名。包含扩展名</param>
        /// <param name="encoding">字符编码</param>
        public static async Task DownloadAsync(Stream stream, string fileName, Encoding encoding) => await DownloadAsync(await FileHelper.ToBytesAsync(stream), fileName, encoding);

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="bytes">字节流</param>
        /// <param name="fileName">文件名。包含扩展名</param>
        public static async Task DownloadAsync(byte[] bytes, string fileName) => await DownloadAsync(bytes, fileName, Encoding.UTF8);

        /// <summary>
        /// 下载
        /// </summary>
        /// <param name="bytes">字节流</param>
        /// <param name="fileName">文件名。包含扩展名</param>
        /// <param name="encoding">字符编码</param>
        public static async Task DownloadAsync(byte[] bytes, string fileName, Encoding encoding)
        {
            if (bytes == null || bytes.Length == 0)
                return;
            fileName = fileName.Replace(" ", "");
            fileName = UrlEncode(fileName, encoding);
            Response.ContentType = "application/octet-stream";
            Response.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");
            Response.Headers.Add("Content-Length", bytes.Length.ToString());
            await Response.Body.WriteAsync(bytes, 0, bytes.Length);
        }

        #endregion

        #region GetCookie(获取Cookie值)

        /// <summary>
        /// 读取Cookie值
        /// </summary>
        /// <param name="name">名称</param>
        public static string GetCookie(string name)
        {
            if (HttpContext.Request.Cookies != null && HttpContext.Request.Cookies[name] != null)
                return HttpContext.Request.Cookies[name];
            return string.Empty;
        }

        #endregion

        #region ClearCookie(清空Cookie)

        /// <summary>
        /// 清空Cookie
        /// </summary>
        public static void ClearCookie()
        {
            foreach (var cookie in HttpContext.Request.Cookies.Keys) 
                HttpContext.Response.Cookies.Delete(cookie);
        }

        #endregion

        #region SetCookie(设置Cookie值)

        /// <summary>
        /// 设置Cookie值。未设置过期时间，则写的是浏览器进程Cookie，一旦浏览器（是浏览器，非标签页）关闭，则Cookie自动失效
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="value">值</param>
        public static void SetCookie(string name, string value)
        {
            var cookieOptions = new CookieOptions {HttpOnly = true};
            HttpContext.Response.Cookies.Append(name, value, cookieOptions);
        }

        #endregion
    }
}
