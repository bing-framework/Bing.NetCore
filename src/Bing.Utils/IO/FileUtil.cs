using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bing.Utils.Extensions;
using FileInfo = System.IO.FileInfo;

namespace Bing.Utils.IO
{
    /// <summary>
    /// 文件操作辅助类
    /// </summary>
    public static partial class FileUtil
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

        #region Read(读取文件到字符串)        

        /// <summary>
        /// 读取文件到字符串
        /// </summary>
        /// <param name="filePath">文件的绝对路径</param>
        /// <param name="encoding">字符编码</param>
        /// <returns></returns>
        public static string Read(string filePath, Encoding encoding = null)
        {
            if (encoding == null)
            {
                encoding = Encoding.UTF8;
            }

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

            return ReadToBytes(new FileInfo(filePath));
        }

        /// <summary>
        /// 将文件读取到字节流中
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        /// <returns></returns>
        public static byte[] ReadToBytes(FileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                return null;
            }

            int fileSize = (int)fileInfo.Length;
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
    }
}
