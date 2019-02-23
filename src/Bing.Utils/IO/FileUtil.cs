using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bing.Utils.Extensions;
using Bing.Utils.Files;
using Bing.Utils.Helpers;
using FileInfo = System.IO.FileInfo;

namespace Bing.Utils.IO
{
    /// <summary>
    /// 文件操作辅助类
    /// </summary>
    public static class FileUtil
    {
        #region CreateIfNotExists(创建文件，如果文件不存在)

        /// <summary>
        /// 创建文件，如果文件不存在
        /// </summary>
        /// <param name="fileName">文件名，绝对路径</param>
        public static void CreateIfNotExists(string fileName)
        {
            if (File.Exists(fileName))
            {
                return;
            }

            File.Create(fileName);
        }

        #endregion

        #region Delete(删除文件)

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="filePaths">文件集合的绝对路径</param>
        public static void Delete(IEnumerable<string> filePaths)
        {
            foreach (var filePath in filePaths)
            {
                Delete(filePath);
            }
        }

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        public static void Delete(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (File.Exists(filePath))
            {
                return;
            }

            File.Delete(filePath);
        }

        #endregion

        #region KillFile(强力粉碎文件)

        /// <summary>
        /// 强力粉碎文件，如果文件被打开，很难粉碎
        /// </summary>
        /// <param name="fileName">文件全路径</param>
        /// <param name="deleteCount">删除次数</param>
        /// <param name="randomData">随机数据填充文件，默认true</param>
        /// <param name="blanks">空白填充文件，默认false</param>
        /// <returns>true:粉碎成功,false:粉碎失败</returns>        
        public static bool KillFile(string fileName, int deleteCount, bool randomData = true, bool blanks = false)
        {
            const int bufferLength = 1024000;
            bool ret = true;
            try
            {
                using (
                    FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite,
                        FileShare.ReadWrite))
                {
                    FileInfo file = new FileInfo(fileName);
                    long count = file.Length;
                    long offset = 0;
                    var rowDataBuffer = new byte[bufferLength];
                    while (count >= 0)
                    {
                        int iNumOfDataRead = stream.Read(rowDataBuffer, 0, bufferLength);
                        if (iNumOfDataRead == 0)
                        {
                            break;
                        }

                        if (randomData)
                        {
                            Random randomByte = new Random();
                            randomByte.NextBytes(rowDataBuffer);
                        }
                        else if (blanks)
                        {
                            for (int i = 0; i < iNumOfDataRead; i++)
                            {
                                rowDataBuffer[i] = Convert.ToByte(Convert.ToChar(deleteCount));
                            }
                        }

                        //写新内容到文件
                        for (int i = 0; i < deleteCount; i++)
                        {
                            stream.Seek(offset, SeekOrigin.Begin);
                            stream.Write(rowDataBuffer, 0, iNumOfDataRead);
                            ;
                        }

                        offset += iNumOfDataRead;
                        count -= iNumOfDataRead;
                    }
                }

                //每一个文件名字符代替随机数从0到9
                string newName = "";
                do
                {
                    Random random = new Random();
                    string cleanName = Path.GetFileName(fileName);
                    string dirName = Path.GetDirectoryName(fileName);
                    int iMoreRandomLetters = random.Next(9);
                    //为了更安全，不要只使用原文件名的大小，添加一些随机字母
                    for (int i = 0; i < cleanName.Length + iMoreRandomLetters; i++)
                    {
                        newName += random.Next(9).ToString();
                    }

                    newName = dirName + "\\" + newName;
                } while (File.Exists(newName));

                //重命名文件的新随机的名字
                File.Move(fileName, newName);
                File.Delete(newName);
            }
            catch
            {
                //可能其他原因删除失败，使用我们自己的方法强制删除
                try
                {
                    string filename = fileName; //要检查被哪个进程占用的文件
                    Process tool = new Process()
                    {
                        StartInfo =
                        {
                            FileName = "handle.exe",
                            Arguments = filename + " /accepteula",
                            UseShellExecute = false,
                            RedirectStandardOutput = true
                        }
                    };
                    tool.Start();
                    tool.WaitForExit();
                    string outputTool = tool.StandardOutput.ReadToEnd();
                    string matchPattern = @"(?<=\s+pid:\s+)\b(\d+)\b(?=\s+)";
                    foreach (Match match in Regex.Matches(outputTool, matchPattern))
                    {
                        //结束掉所有正在使用这个文件的程序
                        Process.GetProcessById(int.Parse(match.Value)).Kill();
                    }

                    File.Delete(filename);
                }
                catch
                {

                    ret = false;
                }
            }

            return ret;
        }

        #endregion

        #region SetAttribute(设置文件属性)

        /// <summary>
        /// 设置文件属性
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="attribute">文件属性</param>
        /// <param name="isSet">是否为设置属性,true:设置,false:取消</param>
        public static void SetAttribute(string fileName, FileAttributes attribute, bool isSet)
        {
            FileInfo fi = new FileInfo(fileName);
            if (!fi.Exists)
            {
                throw new FileNotFoundException("要设置属性的文件不存在。", fileName);
            }

            if (isSet)
            {
                fi.Attributes = fi.Attributes | attribute;
            }
            else
            {
                fi.Attributes = fi.Attributes & ~attribute;
            }
        }

        #endregion

        #region GetVersion(获取文件版本号)

        /// <summary>
        /// 获取文件版本号
        /// </summary>
        /// <param name="fileName">完整文件名</param>
        /// <returns></returns>
        public static string GetVersion(string fileName)
        {
            if (File.Exists(fileName))
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(fileName);
                return fvi.FileVersion;
            }

            return null;
        }

        #endregion

        #region GetFileMd5(获取文件的MD5值)

        /// <summary>
        /// 获取文件的MD5值
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns></returns>
        public static string GetFileMd5(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            const int bufferSize = 1024 * 1024;
            byte[] buffer = new byte[bufferSize];

            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            md5.Initialize();

            long offset = 0;
            while (offset < fs.Length)
            {
                long readSize = bufferSize;
                if (offset + readSize > fs.Length)
                {
                    readSize = fs.Length - offset;
                }

                fs.Read(buffer, 0, (int) readSize);
                if (offset + readSize < fs.Length)
                {
                    md5.TransformBlock(buffer, 0, (int) readSize, buffer, 0);
                }
                else
                {
                    md5.TransformFinalBlock(buffer, 0, (int) readSize);
                }

                offset += bufferSize;
            }

            fs.Close();
            byte[] result = md5.Hash;
            md5.Clear();
            StringBuilder sb = new StringBuilder();
            foreach (var b in result)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }

        #endregion

        #region GetEncoding(获取文件编码)

        /// <summary>
        /// 获取文件编码
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <returns></returns>
        public static Encoding GetEncoding(string filePath)
        {
            return GetEncoding(filePath, Encoding.Default);
        }

        /// <summary>
        /// 获取文件编码
        /// </summary>
        /// <param name="filePath">文件绝对路径</param>
        /// <param name="defaultEncoding">默认编码</param>
        /// <returns></returns>
        public static Encoding GetEncoding(string filePath, Encoding defaultEncoding)
        {
            Encoding targetEncoding = defaultEncoding;
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4))
            {
                if (fs != null && fs.Length >= 2)
                {
                    long pos = fs.Position;
                    fs.Position = 0;
                    int[] buffer = new int[4];
                    buffer[0] = fs.ReadByte();
                    buffer[1] = fs.ReadByte();
                    buffer[2] = fs.ReadByte();
                    buffer[3] = fs.ReadByte();
                    fs.Position = pos;

                    if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                    {
                        targetEncoding = Encoding.BigEndianUnicode;
                    }

                    if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                    {
                        targetEncoding = Encoding.Unicode;
                    }

                    if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    {
                        targetEncoding = Encoding.UTF8;
                    }
                }
            }

            return targetEncoding;
        }

        #endregion

        #region GetAllFiles(获取目录中全部文件列表)

        /// <summary>
        /// 获取目录中全部文件列表，包括子目录
        /// </summary>
        /// <param name="directoryPath">目录绝对路径</param>
        /// <returns></returns>
        public static List<string> GetAllFiles(string directoryPath)
        {
            return Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories).ToList();
        }

        #endregion

        #region GetContentType(根据扩展名获取文件内容类型)

        /// <summary>
        /// 根据扩展名获取文件内容类型
        /// </summary>
        /// <param name="ext">扩展名</param>
        /// <returns></returns>
        public static string GetContentType(string ext)
        {
            string contentType = "";
            var dict = Const.FileExtensionDict;
            ext = ext.ToLower();
            if (!ext.StartsWith("."))
            {
                ext = "." + ext;
            }

            dict.TryGetValue(ext, out contentType);
            return contentType;
        }

        #endregion

        #region Read(读取文件到字符串)

        /// <summary>
        /// 读取文件到字符串
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <returns></returns>
        public static string Read(string filePath)
        {
            return Read(filePath, Encoding.UTF8);
        }

        /// <summary>
        /// 读取文件到字符串
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string Read(string filePath, Encoding encoding)
        {
            if (!File.Exists(filePath))
            {
                return string.Empty;
            }

            using (StreamReader reader = new StreamReader(filePath, encoding))
            {
                return reader.ReadToEnd();
            }
        }

        #endregion

        #region ReadToBytes(将文件读取到字节流中)

        /// <summary>
        /// 将文件读取到字节流中
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <returns></returns>
        public static byte[] ReadToBytes(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            FileInfo fileInfo = new FileInfo(filePath);
            int fileSize = (int) fileInfo.Length;
            using (BinaryReader reader = new BinaryReader(fileInfo.Open(FileMode.Open)))
            {
                return reader.ReadBytes(fileSize);
            }
        }

        #endregion

        #region Write(将字节流写入文件)

        /// <summary>
        /// 将字符串写入文件，文件不存在则创建
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="content">数据</param>
        public static void Write(string filePath, string content)
        {
            Write(filePath, ToBytes(content.SafeString()));
        }

        /// <summary>
        /// 将字符串写入文件，文件不存在则创建
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="bytes">数据</param>
        public static void Write(string filePath, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (bytes == null)
            {
                return;
            }

            File.WriteAllBytes(filePath, bytes);
        }

        #endregion

        #region ToString(转换成字符串)        

        /// <summary>
        /// 字节数组转换成字符串
        /// </summary>
        /// <param name="data">数据,默认字符编码utf-8</param>
        /// <returns></returns>
        public static string ToString(byte[] data)
        {
            return ToString(data, Encoding.UTF8);
        }

        /// <summary>
        /// 字节数组转换成字符串
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string ToString(byte[] data, Encoding encoding)
        {
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            return encoding.GetString(data);
        }

        /// <summary>
        /// 流转换成字符串
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="encoding">字符串编码</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="isCloseStream">读取完成是否释放流，默认为true</param>
        /// <returns></returns>
        public static string ToString(Stream stream, Encoding encoding = null, int bufferSize = 1024 * 2,
            bool isCloseStream = true)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            if (stream.CanRead == false)
            {
                return string.Empty;
            }

            using (var reader = new StreamReader(stream, encoding, true, bufferSize, !isCloseStream))
            {
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                var result = reader.ReadToEnd();
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                return result;
            }
        }

        /// <summary>
        /// 流转换成字符串
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="encoding">字符串编码</param>
        /// <param name="bufferSize">缓冲区大小</param>
        /// <param name="isCloseStream">读取完成是否释放流，默认为true</param>
        /// <returns></returns>
        public static async Task<string> ToStringAsync(Stream stream, Encoding encoding = null,
            int bufferSize = 1024 * 2,
            bool isCloseStream = true)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            if (stream.CanRead == false)
            {
                return string.Empty;
            }

            using (var reader = new StreamReader(stream, encoding, true, bufferSize, !isCloseStream))
            {
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                var result = await reader.ReadToEndAsync();
                if (stream.CanSeek)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                return result;
            }
        }

        #endregion

        #region ToStream(转换成流)

        /// <summary>
        /// 字符串转换成流
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        public static Stream ToStream(string data)
        {
            return ToStream(data, Encoding.UTF8);
        }

        /// <summary>
        /// 字符串转换成流
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static Stream ToStream(string data, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return Stream.Null;
            }

            return new MemoryStream(ToBytes(data, encoding));
        }

        #endregion

        #region ToBytes(转换成字节数组)

        /// <summary>
        /// 将字符串转换成字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        public static byte[] ToBytes(string data)
        {
            return ToBytes(data, Encoding.UTF8);
        }

        /// <summary>
        /// 字符串转换成字节数组
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static byte[] ToBytes(string data, Encoding encoding)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return new byte[] { };
            }

            return encoding.GetBytes(data);
        }

        /// <summary>
        /// 流转换成字节流
        /// </summary>
        /// <param name="stream">流</param>
        /// <returns></returns>
        public static byte[] ToBytes(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        #endregion

        #region ToInt(转换成整数)

        /// <summary>
        /// 字节数组转换成整数
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        public static int ToInt(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                return 0;
            }

            var buffer = new byte[4];
            Buffer.BlockCopy(data, 0, buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        #endregion

        #region JoinPath(连接基路径和子路径)

        /// <summary>
        /// 连接基路径和子路径，比如把 c: 与 test.doc 连接成 c:\test.doc
        /// </summary>
        /// <param name="basePath">基路径，范例：c:</param>
        /// <param name="subPath">子路径，可以是文件名，范例：test.doc</param>
        /// <returns></returns>
        public static string JoinPath(string basePath, string subPath)
        {
            basePath = basePath.TrimEnd('/').TrimEnd('\\');
            subPath = subPath.TrimStart('/').TrimStart('\\');
            string path = basePath + "\\" + subPath;
            return path.Replace("/", "\\").ToLower();
        }

        #endregion

        #region CopyToStringAsync(复制流并转换成字符串)

        /// <summary>
        /// 复制流并转换成字符串
        /// </summary>
        /// <param name="stream">流</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static async Task<string> CopyToStringAsync(Stream stream, Encoding encoding = null)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

            if (stream.CanRead == false)
            {
                return string.Empty;
            }

            using (var memoryStream = new MemoryStream())
            {
                using (var reader = new StreamReader(memoryStream, encoding))
                {
                    if (stream.CanSeek)
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                    }

                    stream.CopyTo(memoryStream);
                    if (memoryStream.CanSeek)
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                    }

                    var result = await reader.ReadToEndAsync();
                    if (stream.CanSeek)
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                    }

                    return result;
                }
            }
        }

        #endregion

        #region GetFileSize(获取文件大小)

        /// <summary>
        /// 获取文件大小
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns></returns>
        public static FileSize GetFileSize(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            return GetFileSize(new FileInfo(filePath));
        }

        /// <summary>
        /// 获取文件大小
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <returns></returns>
        public static FileSize GetFileSize(FileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                throw new ArgumentNullException(nameof(fileInfo));
            }

            return new FileSize(fileInfo.Length);
        }

        #endregion
    }
}
