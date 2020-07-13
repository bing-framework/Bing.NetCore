using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bing.Utils.Json;

namespace Bing.IO
{
    /// <summary>
    /// 文件队列工具
    /// </summary>
    public class FileQueueHelper
    {
        /// <summary>
        /// 将文件加入到本地队列
        /// </summary>
        /// <param name="queueDir">队列目录</param>
        /// <param name="fileName">文件名</param>
        /// <param name="fileContent">文件内容</param>
        public static void AddFileToEnqueue(string queueDir, string fileName, string fileContent)
        {
            var saveDir = GetSaveDir(queueDir);
            if (!Directory.Exists(saveDir)) 
                Directory.CreateDirectory(saveDir);
            var savePath = Path.Combine(saveDir, fileName);
            var tempFilePath = $"{savePath}.bak";
            using var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite);
            using var sw = new StreamWriter(fs);
            sw.Write(fs);

            File.Copy(tempFilePath, savePath, true);
            File.Delete(tempFilePath);
        }

        /// <summary>
        /// 获取保存目录
        /// </summary>
        /// <param name="queuePath">队列路径</param>
        private static string GetSaveDir(string queuePath)
        {
            if (!Directory.Exists(queuePath)) 
                Directory.CreateDirectory(queuePath);
            return Path.Combine(queuePath, DateTime.Now.ToString("yyyyMMddHHmm"));
        }

        /// <summary>
        /// 从队列中移除文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void RemoveFileFromQueue(string filePath) => File.Delete(filePath);

        /// <summary>
        /// 从队列中移除文件
        /// </summary>
        /// <param name="fileInfo">文件信息</param>
        public static void RemoveFileFromQueue(FileInfo fileInfo) => File.Delete(fileInfo.FullName);

        /// <summary>
        /// 获取文件
        /// </summary>
        /// <param name="queueDir">队列目录</param>
        /// <param name="takeCount">获取文件数量</param>
        /// <param name="type">类型</param>
        public static List<FileInfo> GetFilesFromQueue(string queueDir, int takeCount, string type = "")
        {
            var items = new List<FileInfo>();
            if (!Directory.Exists(queueDir))
            {
                return items;
            }

            var homeDir = new DirectoryInfo(queueDir);
            var dirs = homeDir.GetDirectories().OrderBy(p => Convert.ToInt32(p.Name)).ToArray();
            for (var i = 0; i < dirs.Length; i++)
            {
                var dir = dirs[i];
                var fileInfos = !string.IsNullOrWhiteSpace(type) ? dir.GetFiles(type) : dir.GetFiles();
                if (fileInfos.Length == 0)
                {
                    // 队列延迟
                    if (dir.CreationTime < DateTime.Now.AddMinutes(-2))
                    {
                        var files = dir.GetFiles();
                        if (files.Length == 0)
                        {
                            Directory.Delete(dir.FullName, false);
                        }
                        else
                        {
                            foreach (var file in files)
                            {
                                if (file.Name.EndsWith(".data"))
                                {
                                    continue;
                                }

                                if (file.Name.EndsWith(".bak"))
                                {
                                    file.MoveTo(file.FullName.Replace(".bak", ""));
                                }
                            }
                        }
                    }
                }

                foreach (var fileInfo in fileInfos)
                {
                    items.Add(fileInfo);
                    if (items.Count >= takeCount)
                    {
                        return items;
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="filePath">文件路径</param>
        public static T ReadObjectFromQueue<T>(string filePath)
        {
            var t = default(T);
            var fi = new FileInfo(filePath);
            if (fi.Exists)
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                string content;
                using (var sr = new StreamReader(fs)) 
                    content = sr.ReadToEnd();

                if (!string.IsNullOrWhiteSpace(content)) 
                    t = JsonHelper.ToObject<T>(content);
                fs.Close();
            }

            return t;
        }

        /// <summary>
        /// 读取文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static string ReadStringFromQueue(string filePath)
        {
            var content = string.Empty;
            var fi = new FileInfo(filePath);
            if (fi.Exists)
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (var sr = new StreamReader(fs))
                {
                    content = sr.ReadToEnd();
                }
                fs.Close();
            }

            return content;
        }

        /// <summary>
        /// 获取队列文件夹
        /// </summary>
        /// <param name="queueDir">队列目录</param>
        public static DirectoryInfo[] GetQueueDirs(string queueDir)
        {
            var homeDir = new DirectoryInfo(queueDir);
            return homeDir.GetDirectories();
        }
    }
}
