using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展 - 安全
    /// </summary>
    public static partial class StringExtensions
    {
        #region UrlEncode(Url编码)

        /// <summary>
        /// Url编码
        /// </summary>
        /// <param name="source">url编码字符串</param>
        /// <param name="encoding">编码格式</param>
        public static string UrlEncode(this string source, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
            return HttpUtility.UrlEncode(source, encoding);
        }

        #endregion

        #region UrlDecode(Url解码)

        /// <summary>
        /// Url解码
        /// </summary>
        /// <param name="source">url编码字符串</param>
        /// <param name="encoding">编码格式</param>
        public static string UrlDecode(this string source, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
            return HttpUtility.UrlDecode(source, encoding);
        }

        #endregion

        #region ToHtmlSafe(Html字符串进行安全编码)

        /// <summary>
        /// Html字符串进行安全编码。返回安全的Html字符串
        /// </summary>
        /// <param name="value">当前Html字符串实例</param>
        public static string ToHtmlSafe(this string value) => value.ToHtmlSafe(false, false);

        /// <summary>
        /// Html字符串进行安全编码。返回安全的Html字符串
        /// </summary>
        /// <param name="value">当前Html字符串实例</param>
        /// <param name="all">是否所有字符进行安全编码，或只是部分需要</param>
        public static string ToHtmlSafe(this string value, bool all) => value.ToHtmlSafe(all, false);

        /// <summary>
        /// Html字符串进行安全编码。返回安全的Html字符串
        /// </summary>
        /// <param name="value">当前Html字符串实例</param>
        /// <param name="all">是否所有字符进行安全编码，或只是部分需要</param>
        /// <param name="replace">是否对空格以及换行符进行编码</param>
        public static string ToHtmlSafe(this string value, bool all, bool replace)
        {
            if (value.IsEmpty())
                return string.Empty;
            var entities = new[]
            {
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 28, 29,
                30, 31, 34, 39, 38, 60, 62, 123, 124, 125, 126, 127, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169,
                170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190,
                191, 215, 247, 192, 193, 194, 195, 196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209,
                210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230,
                231, 232, 233, 234, 235, 236, 237, 238, 239, 240, 241, 242, 243, 244, 245, 246, 247, 248, 249, 250, 251,
                252, 253, 254, 255, 256, 8704, 8706, 8707, 8709, 8711, 8712, 8713, 8715, 8719, 8721, 8722, 8727, 8730,
                8733, 8734, 8736, 8743, 8744, 8745, 8746, 8747, 8756, 8764, 8773, 8776, 8800, 8801, 8804, 8805, 8834,
                8835, 8836, 8838, 8839, 8853, 8855, 8869, 8901, 913, 914, 915, 916, 917, 918, 919, 920, 921, 922, 923,
                924, 925, 926, 927, 928, 929, 931, 932, 933, 934, 935, 936, 937, 945, 946, 947, 948, 949, 950, 951, 952,
                953, 954, 955, 956, 957, 958, 959, 960, 961, 962, 963, 964, 965, 966, 967, 968, 969, 977, 978, 982, 338,
                339, 352, 353, 376, 402, 710, 732, 8194, 8195, 8201, 8204, 8205, 8206, 8207, 8211, 8212, 8216, 8217,
                8218, 8220, 8221, 8222, 8224, 8225, 8226, 8230, 8240, 8242, 8243, 8249, 8250, 8254, 8364, 8482, 8592,
                8593, 8594, 8595, 8596, 8629, 8968, 8969, 8970, 8971, 9674, 9824, 9827, 9829, 9830
            };
            var sb = new StringBuilder();
            foreach (var item in value)
            {
                if (all || entities.Contains(item))
                    sb.Append("&#" + ((int)item) + ";");
                else
                    sb.Append(item);
            }
            return replace
                ? sb.Replace("", "<br />").Replace("\n", "<br />").Replace(" ", "&nbsp;").ToString()
                : sb.ToString();
        }

        #endregion

        #region EncodeBase64(Base64字符串编码)

        /// <summary>
        /// 对字符串进行Base64字符串编码，默认编码为<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="value">字符串</param>
        /// <param name="encoding">编码格式</param>
        public static string EncodeBase64(this string value, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = encoding.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }

        #endregion

        #region DecodeBase64(Base64字符串解码)

        /// <summary>
        /// 对字符串进行Base64字符串解码
        /// </summary>
        /// <param name="value">字符串</param>
        /// <param name="encoding">编码格式</param>
        public static string DecodeBase64(this string value, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            var bytes = Convert.FromBase64String(value);
            return encoding.GetString(bytes);
        }

        #endregion

        #region EncryptToBytes(字符串加密为字节数组)

        /// <summary>
        /// 将字符串加密成字节数组
        /// </summary>
        /// <param name="value">值，需要加密的字符串</param>
        /// <param name="pwd">密匙，使用密匙来加密字符串</param>
        public static byte[] EncryptToBytes(this string value, string pwd)
        {
            var asciiEncoder = new ASCIIEncoding();
            byte[] bytes = asciiEncoder.GetBytes(value);
            return CryptBytes(pwd, bytes, true);
        }

        /// <summary>
        /// 加密或解密字节数组，使用Rfc2898DeriveBytes与TripleDESCryptoServiceProvider的加密提供程序生成的密匙和初始化向量
        /// </summary>
        /// <param name="pwd">需要加密或解密的密码字符串</param>
        /// <param name="bytes">用来加密的字节数组</param>
        /// <param name="encrypt">true：加密，false：解密</param>
        private static byte[] CryptBytes(string pwd, byte[] bytes, bool encrypt)
        {
            //第三方加密服务商
            var desProvider = new TripleDESCryptoServiceProvider();
            //找到此提供程序的有效密钥大小
            int keySizeBits = 0;
            for (int i = 1024; i >= 1; i--)
            {
                if (desProvider.ValidKeySize(i))
                {
                    keySizeBits = i;
                    break;
                }
            }
            //获取此提供程序的块大小
            int blockSizeBits = desProvider.BlockSize;
            //生成密钥和初始化向量
            byte[] key = null;
            byte[] iv = null;
            byte[] salt =
            {
                0x10, 0x20, 0x12, 0x23, 0x37, 0xA4, 0xC5, 0xA6, 0xF1, 0xF0, 0xEE, 0x21, 0x22, 0x45
            };
            MakeKeyAndIv(pwd, salt, keySizeBits, blockSizeBits, ref key, ref iv);
            //进行加密或解密
            var cryptoTransform = encrypt
                ? desProvider.CreateEncryptor(key, iv)
                : desProvider.CreateDecryptor(key, iv);
            //创建输出流
            var outStream = new MemoryStream();
            //附加一个加密流输出流
            var cryptoStream = new CryptoStream(outStream, cryptoTransform, CryptoStreamMode.Write);
            //写字节到加密流中
            cryptoStream.Write(bytes, 0, bytes.Length);
            try
            {
                cryptoStream.FlushFinalBlock();
            }
            catch (CryptographicException)
            {
                // Ignore this one. The password is bad.
            }
            //保存结果
            byte[] result = outStream.ToArray();
            //关闭流
            try
            {
                cryptoStream.Close();
            }
            catch (CryptographicException)
            {
                // Ignore this one. The password is bad.
            }
            outStream.Close();
            return result;
        }

        /// <summary>
        /// 使用密码生成密匙和一个初始化向量（Rfc2898DeriveBytes）
        /// </summary>
        /// <param name="pwd">用于生成字节的输入密码</param>
        /// <param name="salt">用于生成字节的salt值</param>
        /// <param name="keySizeBits">生成密匙的大小</param>
        /// <param name="blockSizeBits">加密提供程序所使用的输入块的大小</param>
        /// <param name="key">生成输出密匙字节</param>
        /// <param name="iv">生成输出初始化向量</param>
        private static void MakeKeyAndIv(string pwd, byte[] salt, int keySizeBits, int blockSizeBits, ref byte[] key,
            ref byte[] iv)
        {
            var deriveBytes = new Rfc2898DeriveBytes(pwd, salt, 1234);
            key = deriveBytes.GetBytes(keySizeBits / 8);
            iv = deriveBytes.GetBytes(blockSizeBits / 8);
        }

        #endregion

        #region DecryptFromBytes(字节数组解密为字符串)

        /// <summary>
        /// 将字节数组解密成字符串，前提该字节数组已加密
        /// </summary>
        /// <param name="value">值，要解密的字节数组</param>
        /// <param name="pwd">密匙，使用密匙来解密字符串</param>
        public static string DecryptFromBytes(this byte[] value, string pwd)
        {
            byte[] bytes = CryptBytes(pwd, value, false);
            var asciiEncoder = new ASCIIEncoding();
            return new string(asciiEncoder.GetChars(bytes));
        }

        #endregion

        #region EncryptToString(字符串加密)

        /// <summary>
        /// 字符串加密
        /// </summary>
        /// <param name="value">值，需要加密的字符串</param>
        /// <param name="pwd">密匙，使用密匙来加密字符串</param>
        public static string EncryptToString(this string value, string pwd) => value.EncryptToBytes(pwd).ToString();

        #endregion

        #region DecryptFromString(字符串解密)

        /// <summary>
        /// 字符串解密，前提字符串已加密
        /// </summary>
        /// <param name="value">值，要解密的字符串</param>
        /// <param name="pwd">密匙，使用密匙来解密字符串</param>
        public static string DecryptFromString(this string value, string pwd)
        {
            var asciiEncoder = new ASCIIEncoding();
            byte[] bytes = asciiEncoder.GetBytes(value);
            return DecryptFromBytes(bytes, pwd);
        }

        #endregion

        #region FilterHtml(过滤Html标签)

        /// <summary>
        /// 过滤Html标签
        /// </summary>
        /// <param name="value">值，待过滤的字符串</param>
        public static string FilterHtml(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = Regex.Replace(value, @"<script[^>]*?>.*?</script>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"<style[^>]*?>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"</style>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"<p[^>]*?>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"<div[^>]*?>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"</p>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"</div>", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"-->", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"<!--.*", "", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, "<[^>]*>", "", RegexOptions.Compiled);
            value = Regex.Replace(value, @"([\r\n])[\s]+", " ", RegexOptions.Compiled);
            return value.Replace("&nbsp;", " ");
        }

        #endregion

    }
}
