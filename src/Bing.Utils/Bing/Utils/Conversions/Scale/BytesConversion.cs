using System.Linq;
using System.Text;

namespace Bing.Utils.Conversions.Scale
{
    /// <summary>
    /// 字节转换操作
    /// </summary>
    public static class BytesConversion
    {
        /// <summary>
        /// 转换为ASCII字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <example>in: new byte[] {65, 66, 67}; out: ABC</example>
        public static string ToAscii(byte[] bytes) => Encoding.ASCII.GetString(bytes, 0, bytes.Length);

        /// <summary>
        /// 转换为二进制字符串
        /// </summary>
        /// <param name="byte">byte</param>
        /// <example>in: (byte)128; out: 10000000</example>
        public static string ToBinary(byte @byte) => DecimalismConversion.ToBinary(@byte);

        /// <summary>
        /// 转换为十进制
        /// </summary>
        /// <param name="h">高地址字节</param>
        /// <param name="l">低地址字节</param>
        /// <example>in: (byte)65, (byte)66; out: 16706</example>
        /// <example>in: (byte)66, (byte)65; out: 16961</example>
        public static int ToDecimalism(byte h, byte l) => h << 8 | l;

        /// <summary>
        /// 转换为十进制
        /// </summary>
        /// <param name="h">高地址字节</param>
        /// <param name="l">低地址字节</param>
        /// <param name="isRadix">是否基数</param>
        /// <example>in: (byte)255, (byte)66; out: 65346</example>
        /// <example>in: (byte)66, (byte)255; out: -190</example>
        public static int ToDecimalism(byte h, byte l, bool isRadix)
        {
            var v = (ushort)(h << 0 | l);
            if (isRadix && h > 127)
            {
                v = (ushort)~v;
                v = (ushort)(v + 1);
                return -1 * v;
            }
            return v;
        }

        /// <summary>
        /// 转换为十六进制字符串
        /// </summary>
        /// <param name="byte">byte</param>
        /// <example>in: (byte)128; out: 80</example>
        public static string ToHexadecimal(byte @byte) => @byte.ToString("X2");

        /// <summary>
        /// 转换为十六进制字符串
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <example>in: new byte[] {65, 66, 67}; out: 414243</example>
        public static string ToHexadecimal(byte[] bytes)
        {
            var ret = "";
            if (bytes != null)
                ret = bytes.Aggregate(ret, (current, t) => $"{current}{t:X2}");
            return ret;
        }

        /// <summary>
        /// 转换为十六进制字符串
        /// </summary>
        /// <param name="h">高地址字节</param>
        /// <param name="l">低地址字节</param>
        /// <example>in: (byte)65, (byte)66; out: 4142</example>
        /// <example>in: (byte)66, (byte)65; out: 4241</example>
        public static string ToHexadecimal(byte h, byte l) => $"{ToHexadecimal(h)}{ToHexadecimal(l)}";
    }
}
