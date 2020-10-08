using System;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using ComponentAce.Compression.Libs.zlib;
using Xunit;
using FileHelper = Bing.IO.FileHelper;

namespace Bing.Utils.Tests
{
    public class CompressTest
    {
        [Fact]
        public void Test_Zlib_Compress()
        {
            var compressCount = 4;
            for (int i = 0; i <= compressCount; i++)
            {
                var json = FileHelper.Read(i == 0
                    ? "D:\\iTestRunner_R1_format.txt"
                    : $"D:\\test_compression_result_{i - 1}.txt");
                var initBytes = Encoding.UTF8.GetBytes(json);
                var result = ZlibHelper.CompressBytes(initBytes, zlibConst.Z_BEST_COMPRESSION);
                FileHelper.Write($"D:\\test_compression_result_{i}.txt", result);
            }
        }

        //[Fact]
        //public void Test_Zlib_Compress_1()
        //{
        //    var compressCount = 4;
        //    var initBytes = FileUtil.ReadToBytes("D:\\iTestRunner_R1_format.txt");
        //    for (int i = 0; i <= compressCount; i++)
        //    {
        //        initBytes = ZlibHelper.CompressBytes(initBytes, zlibConst.Z_BEST_COMPRESSION);
        //        Thread.Sleep(2000);
        //    }
        //    FileUtil.Write($"D:\\test_compression_result_0.txt", initBytes);
        //}

        [Fact]
        public void Test_Zlib_Decompress()
        {
            var compressCount = 4;
            for (int i = compressCount; i >= 0; i--)
            {
                var json = FileHelper.ReadToBytes(i == compressCount
                    ? $"D:\\test_compression_result_{i}.txt"
                    : $"D:\\test_decompress_result_{i + 1}.txt");
                var result = ZlibHelper.DeCompressBytes(json);
                FileHelper.Write($"D:\\test_decompress_result_{i}.txt", result);
            }
        }


        /// <summary>
        /// 压缩帮助类
        /// </summary>
        public class ZlibHelper
        {
            #region CompressBytes 对原始字节数组进行zlib压缩，得到处理结果字节数组
            /// <summary>
            /// 对原始字节数组进行zlib压缩，得到处理结果字节数组
            /// </summary>
            /// <param name="orgByte">需要被压缩的原始Byte数组数据</param>
            /// <param name="compressRate">压缩率：默认为zlibConst.Z_DEFAULT_COMPRESSION</param>
            /// <returns>压缩后的字节数组，如果出错则返回null</returns>
            public static byte[] CompressBytes(byte[] orgByte, int compressRate = zlibConst.Z_BEST_SPEED)
            {
                if (orgByte == null) return null;

                using (var orgStream = new MemoryStream(orgByte))
                {
                    using (var compressedStream = new MemoryStream())
                    {
                        using (var outZStream = new ZOutputStream(compressedStream, compressRate))
                        {
                            try
                            {
                                CopyStream(orgStream, outZStream);
                                outZStream.finish();//重要！否则结果数据不完整！
                                //程序执行到这里，CompressedStream就是压缩后的数据
                                return compressedStream.ToArray();
                            }
                            catch
                            {
                                return null;
                            }
                        }
                    }
                }
            }
            #endregion

            #region DeCompressBytes 对经过zlib压缩的数据，进行解密和zlib解压缩，得到原始字节数组
            /// <summary>
            /// 对经过zlib压缩的数据，进行解密和zlib解压缩，得到原始字节数组
            /// </summary>
            /// <param name="compressedBytes">被压缩的Byte数组数据</param>
            /// <returns>解压缩后的字节数组，如果出错则返回null</returns>
            public static byte[] DeCompressBytes(byte[] compressedBytes)
            {
                if (compressedBytes == null) return null;

                using (var compressedStream = new MemoryStream(compressedBytes))
                {
                    using (var orgStream = new MemoryStream())
                    {
                        using (var outZStream = new ZOutputStream(orgStream))
                        {
                            try
                            {
                                //-----------------------
                                //解压缩
                                //-----------------------
                                CopyStream(compressedStream, outZStream);
                                outZStream.finish();
                                //重要！
                                //程序执行到这里，OrgStream就是解压缩后的数据

                                return orgStream.ToArray();
                            }
                            catch
                            {
                                return null;
                            }
                        }
                    }
                }
            }
            #endregion

            #region CompressString 压缩字符串

            /// <summary>
            /// 压缩字符串
            /// </summary>
            /// <param name="sourceString">需要被压缩的字符串</param>
            /// <param name="compressRate"></param>
            /// <returns>压缩后的字符串，如果失败则返回null</returns>
            public static string CompressString(string sourceString, int compressRate = zlibConst.Z_DEFAULT_COMPRESSION)
            {
                byte[] byteSource = System.Text.Encoding.UTF8.GetBytes(sourceString);
                byte[] byteCompress = CompressBytes(byteSource, compressRate);
                if (byteCompress != null)
                {
                    return Convert.ToBase64String(byteCompress);
                }
                else
                {
                    return null;
                }
            }
            #endregion

            #region DecompressString 解压字符串
            /// <summary>
            /// 解压字符串
            /// </summary>
            /// <param name="sourceString">需要被解压的字符串</param>
            /// <returns>解压后的字符串，如果处所则返回null</returns>
            public static string DecompressString(string sourceString)
            {
                byte[] byteSource = Convert.FromBase64String(sourceString);
                byte[] byteDecompress = DeCompressBytes(byteSource);
                if (byteDecompress != null)
                {

                    return System.Text.Encoding.UTF8.GetString(byteDecompress);
                }
                else
                {
                    return null;
                }
            }
            #endregion

            #region CopyStream 拷贝流
            /// <summary>
            /// 拷贝流
            /// </summary>
            /// <param name="input"></param>
            /// <param name="output"></param>
            private static void CopyStream(Stream input, Stream output)
            {
                byte[] buffer = new byte[2000];
                int len;
                while ((len = input.Read(buffer, 0, 2000)) > 0)
                {
                    output.Write(buffer, 0, len);
                }
                output.Flush();
            }
            #endregion

            #region GetStringByGZIPData 将解压缩过的二进制数据转换回字符串
            /// <summary>
            /// 将解压缩过的二进制数据转换回字符串
            /// </summary>
            /// <param name="zipData"></param>
            /// <returns></returns>
            public static string GetStringByGzipData(byte[] zipData)
            {
                return (string)(System.Text.Encoding.UTF8.GetString(zipData));
            }
            #endregion
        }
    }
}
