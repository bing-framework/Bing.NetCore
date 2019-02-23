using System;
using System.IO;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Bing.Utils.Extensions
{
    /// <summary>
    /// 字节数组(<see cref="Byte"/>[]) 扩展
    /// </summary>
    public static class ByteArrayExtensions
    {
        #region ToString(将byte[]转换成字符串)

        /// <summary>
        /// 将byte[]转换成字符串，默认字符编码：<see cref="Encoding.UTF8"/>
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string ToString(this byte[] value, Encoding encoding)
        {
            encoding = (encoding ?? Encoding.UTF8);
            return encoding.GetString(value);
        }

        #endregion

        #region ToHexString(将byte[]转换成16进制字符串表示形式)

        /// <summary>
        /// 将byte[]转换成16进制字符串表示形式
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToHexString(this byte[] value)
        {
            StringBuilder sb=new StringBuilder();
            foreach (var b in value)
            {
                sb.AppendFormat(" {0}", b.ToString("X2").PadLeft(2, '0'));
            }

            return sb.Length > 0 ? sb.ToString().Substring(1) : sb.ToString();
        }

        #endregion

        #region ToInt(将byte[]转换成int)

        /// <summary>
        /// 将byte[]转换成int
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static int ToInt(this byte[] value)
        {
            // 如果传入的字节数组长度小于4,则返回0
            if (value.Length < 4)
            {
                return 0;
            }

            int num = 0;
            // 如果传入的字节数组长度大于4,需要进行处理
            if (value.Length >= 4)
            {
                // 创建一个临时缓冲区
                byte[] tempBuffer = new byte[4];
                // 将传入的字节数组的前4个字节复制到临时缓冲区
                Buffer.BlockCopy(value, 0, tempBuffer, 0, 4);
                // 将临时缓冲区的值转换成整数，并赋给num
                num = BitConverter.ToInt32(tempBuffer, 0);
            }

            return num;
        }

        #endregion

        #region ToLong(将byte[]转换成long)

        /// <summary>
        /// 将byte[]转换成long
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static long ToLong(this byte[] value)
        {
            // 如果传入的字节数组长度小于8,则返回0
            if (value.Length < 8)
            {
                return 0;
            }
            long num = 0;
            if (value.Length >= 8)
            {
                // 创建一个临时缓冲区
                byte[] tempBuffer = new byte[8];
                // 将传入的字节数组的前8个字节复制到临时缓冲区
                Buffer.BlockCopy(value, 0, tempBuffer, 0, 8);
                // 将临时缓冲区的值转换成证书，并赋给num
                num = BitConverter.ToInt64(tempBuffer, 0);
            }
            return num;
        }

        #endregion

        #region ToBase64String(将byte[]转换成Base64字符串)

        /// <summary>
        /// 将byte[]转换成Base64字符串
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static string ToBase64String(this byte[] value)
        {
            return Convert.ToBase64String(value);
        }

        #endregion

        #region ToMemoryStream(将byte[]转换成内存流)

        /// <summary>
        /// 将byte[]转换成内存流
        /// </summary>
        /// <param name="value">值</param>
        /// <returns></returns>
        public static MemoryStream ToMemoryStream(this byte[] value)
        {
            return new MemoryStream(value);
        }

        #endregion

        #region Copy(复制一份二维数组的副本)

        /// <summary>
        /// 复制一份二维数组的副本
        /// </summary>
        /// <param name="bytes">二维数组</param>
        /// <returns></returns>
        public static byte[,] Copy(this byte[,] bytes)
        {
            int width = bytes.GetLength(0), height = bytes.GetLength(1);
            byte[,] newBytes = new byte[width, height];
            Array.Copy(bytes, newBytes, bytes.Length);
            return newBytes;
        }

        #endregion
    }
}
