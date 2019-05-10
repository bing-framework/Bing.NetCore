using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
    public static partial class FileHelper
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

            if (!File.Exists(filePath))
            {
                return;
            }

            // 设置文件的属性为正常（如果文件为只读的话直接删除会报错）
            File.SetAttributes(filePath, FileAttributes.Normal);
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

            using (var reader = new StreamReader(filePath, encoding))
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
            using (var reader = new BinaryReader(fileInfo.Open(FileMode.Open)))
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

        #region Combine(合并文件)

        /// <summary>
        /// 合并文件
        /// </summary>
        /// <param name="files">文件路径列表</param>
        /// <param name="fileName">生成文件名</param>
        /// <param name="delete">合并后是否删除源文件</param>
        /// <param name="encrypt">是否加密</param>
        /// <param name="sign">签名</param>
        public static void Combine(IList<string> files, string fileName, bool delete = false, bool encrypt = false, int sign = 0)
        {
            if (files == null || files.Count == 0)
            {
                return;
            }

            files.Sort();
            using (var ws = new FileStream(fileName, FileMode.Create))
            {
                foreach (var file in files)
                {
                    if (file == null || !File.Exists(file))
                    {
                        continue;
                    }

                    using (var rs = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        var data = new byte[1024];
                        var readLen = 0;
                        while ((readLen = rs.Read(data, 0, data.Length)) > 0)
                        {
                            ws.Write(data, 0, readLen);
                            ws.Flush();
                        }
                    }
                    if (delete)
                    {
                        Delete(file);
                    }
                }
            }
        }

        #endregion

        #region Split(分割文件)

        /// <summary>
        /// 分割文件
        /// </summary>
        /// <param name="file">文件</param>
        /// <param name="dirPath">生成文件路径。不含文件名</param>
        /// <param name="suffix">后缀名</param>
        /// <param name="size">分割大小。单位：KB</param>
        /// <param name="delete">分割后是否删除源文件</param>
        /// <param name="encrypt">是否加密</param>
        /// <param name="sign">签名</param>
        public static void Split(string file, string dirPath,string suffix = "bin", int size = 2048, bool delete = false, bool encrypt = false,
            int sign = 0)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
            {
                return;
            }

            var fileName = Path.GetFileNameWithoutExtension(file);
            var fileSize = GetFileSize(file);
            var total = GetSplitFileTotal(fileSize.GetSize(), size);
            using (var rs=new FileStream(file, FileMode.Open,FileAccess.Read))
            {
                var data = new byte[1024];
                int len = 0, i = 1;
                int readLen = 0;
                FileStream ws = null;
                while (readLen>0||(readLen=rs.Read(data,0,data.Length))>0)
                {
                    if (len == 0 || ws == null)
                    {
                        ws?.Dispose();
                        ws = new FileStream($"{dirPath}\\{fileName}.{i++}.{total}.{suffix}", FileMode.Create);
                    }

                    // 输出，缓存数据写入子文件
                    ws.Write(data, 0, readLen);
                    ws.Flush();
                    // 预读下一轮缓存数据
                    readLen = rs.Read(data, 0, data.Length);
                    // 子文件达到指定大小或者文件已读完
                    if (++len >= size || readLen == 0)
                    {
                        ws.Close();
                        len = 0;
                    }
                }
            }

            if (delete)
            {
                Delete(file);
            }
        }

        /// <summary>
        /// 获取分割文件数量
        /// </summary>
        /// <param name="fileSize">文件大小</param>
        /// <param name="splitSize">分割大小。单位：字节</param>
        /// <returns></returns>
        private static int GetSplitFileTotal(int fileSize, int splitSize)
        {
            fileSize = fileSize / 1024;
            if (fileSize % splitSize == 0)
            {
                return fileSize / splitSize;
            }

            return fileSize / splitSize + 1;
        }

        #endregion

        #region Compress(压缩)

        /// <summary>
        /// 压缩
        /// </summary>
        /// <param name="file">文件</param>
        /// <param name="saveFile">保存文件</param>
        /// <returns></returns>
        public static bool Compress(string file, string saveFile)
        {
            if (string.IsNullOrWhiteSpace(file)|| string.IsNullOrWhiteSpace(saveFile))
            {
                return false;
            }

            if (!File.Exists(file))
            {
                return false;
            }

            try
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    using (var ws = new FileStream(saveFile, FileMode.Create))
                    {
                        using (var zip = new GZipStream(ws, CompressionMode.Compress))
                        {
                            fs.CopyTo(zip);
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
            
        }

        #endregion

        #region Decompress(解压缩)

        /// <summary>
        /// 解压缩
        /// </summary>
        /// <param name="file">文件</param>
        /// <param name="saveFile">保存文件</param>
        /// <returns></returns>
        public static bool Decompress(string file, string saveFile)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveFile))
            {
                return false;
            }

            if (!File.Exists(file))
            {
                return false;
            }

            try
            {
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    using (var ws = new FileStream(saveFile, FileMode.Create))
                    {
                        using (var zip = new GZipStream(fs, CompressionMode.Decompress))
                        {
                            zip.CopyTo(ws);
                            return true;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region CompressMulti(多文件压缩)

        /// <summary>
        /// 多文件压缩。（生成的压缩包和第三方的压缩文件解压不兼容）
        /// </summary>
        /// <param name="sourceFileList">文件列表</param>
        /// <param name="saveFullPath">压缩包全路径</param>
        public static void CompressMulti(string[] sourceFileList, string saveFullPath)
        {
            if (sourceFileList == null || sourceFileList.Length == 0 || string.IsNullOrWhiteSpace(saveFullPath))
            {
                return;
            }

            using (var ms = new MemoryStream())
            {
                foreach (var filePath in sourceFileList)
                {
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(filePath);
                    byte[] fileNameBytes = Encoding.UTF8.GetBytes(fileName);
                    byte[] sizeBytes = BitConverter.GetBytes(fileNameBytes.Length);
                    ms.Write(sizeBytes, 0, sizeBytes.Length);
                    ms.Write(fileNameBytes, 0, fileNameBytes.Length);
                    byte[] fileContentBytes = File.ReadAllBytes(filePath);
                    ms.Write(BitConverter.GetBytes(fileContentBytes.Length), 0, 4);
                    ms.Write(fileContentBytes, 0, fileContentBytes.Length);
                }

                ms.Flush();
                ms.Position = 0;

                using (var fs = File.Create(saveFullPath))
                {
                    using (var zipStream=new GZipStream(fs,CompressionMode.Compress))
                    {
                        ms.Position = 0;
                        ms.CopyTo(zipStream);
                    }
                }
            }
        }

        #endregion

        #region DecompressMulti(多文件解压缩)

        /// <summary>
        /// 多文件解压缩
        /// </summary>
        /// <param name="zipPath">压缩文件路径</param>
        /// <param name="targetPath">解压目录</param>
        public static void DecompressMulti(string zipPath,string targetPath)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                return;
            }

            byte[] fileSize = new byte[4];
            if (!File.Exists(zipPath))
            {
                return;
            }

            using (var fs=File.Open(zipPath,FileMode.Open))
            {
                using (var ms=new MemoryStream())
                {
                    using (var zipStream=new GZipStream(fs,CompressionMode.Decompress))
                    {
                        zipStream.CopyTo(ms);
                    }

                    ms.Position = 0;
                    while (ms.Position!=ms.Length)
                    {
                        ms.Read(fileSize, 0, fileSize.Length);
                        var fileNameLength = BitConverter.ToInt32(fileSize, 0);
                        var fileNameBytes = new byte[fileNameLength];
                        ms.Read(fileNameBytes, 0, fileNameBytes.Length);
                        var fileName = Encoding.UTF8.GetString(fileNameBytes);
                        var fileFullName = targetPath + fileName;
                        ms.Read(fileSize, 0, 4);
                        var fileContentLength = BitConverter.ToInt32(fileSize, 0);
                        var fileContentBytes = new byte[fileContentLength];
                        ms.Read(fileContentBytes, 0, fileContentBytes.Length);
                        using (var childFileStream=File.Create(fileFullName))
                        {
                            childFileStream.Write(fileContentBytes, 0, fileContentBytes.Length);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
