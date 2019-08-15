using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// Class ZipHelper.
    /// </summary>
    public class ZipHelper
    {
        #region 公共方法 (4) 

        /// <summary>
        /// 解压缩文件
        /// </summary>
        /// <param name="zipFile">The zip file.</param>
        /// <param name="destDir">The dest dir.</param>
        public static void Unzip(string zipFile, string destDir)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            using (var zipStream = new ZipInputStream(File.OpenRead(zipFile)))
            {
                ZipEntry zipEntry;

                var data = new byte[4096];

                while ((zipEntry = zipStream.GetNextEntry()) != null)
                {
                    string directoryName = Path.GetDirectoryName(zipEntry.Name);
                    string fileName = Path.GetFileName(zipEntry.Name);

                    if (directoryName != String.Empty)
                        Directory.CreateDirectory(Path.Combine(destDir, directoryName));

                    if (fileName != String.Empty)
                    {
                        using (var streamWriter = File.Create(Path.Combine(destDir, zipEntry.Name)))
                        {
                            while (true)
                            {
                                int size = zipStream.Read(data, 0, data.Length);
                                if (size > 0)
                                {
                                    streamWriter.Write(data, 0, size);
                                }
                                else
                                {
                                    break;
                                }
                            }

                            streamWriter.Close();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 压缩文件
        /// </summary>
        /// <param name="zipFile">The zip file.</param>
        /// <param name="destDir">The dest dir.</param>
        public static void Compress(string zipFile, string destDir)
        {
            if (Directory.Exists(destDir))
            {
                string[] filenames = Directory.GetFiles(destDir, "*.*", System.IO.SearchOption.AllDirectories);
                var zipStream = new ZipOutputStream(File.Create(zipFile));
                foreach (string f in filenames)
                {
                    //向压缩文件流加入内容
                    AddZipEntry(f, zipStream, out zipStream, destDir);
                }
                DirectoryInfo[] dirts = new DirectoryInfo(destDir).GetDirectories();
                foreach (var dir in dirts)
                {
                    //向压缩文件流加入内容
                    AddZipEntry(dir.Name, zipStream, out zipStream, destDir);
                }
                // 结束压缩
                zipStream.Finish();
                zipStream.Close();
            }
            else if (File.Exists(destDir))
            {
                var zipStream = new ZipOutputStream(File.Create(zipFile));
                AddZipEntry(destDir, zipStream, out zipStream, destDir);
                // 结束压缩
                zipStream.Finish();
                zipStream.Close();
            }
        }

        /// <summary>
        /// 压缩文件
        /// </summary>
        /// <param name="zipFile">The zip file.</param>
        /// <param name="destFiles">The dest files.</param>
        public static void Compress(string zipFile, List<string> destFiles)
        {
            if (destFiles != null && destFiles.Count > 0)
            {
                var zipStream = new ZipOutputStream(File.Create(zipFile));
                foreach (var item in destFiles)
                {
                    if (File.Exists(item))
                    {
                        AddZipEntry(item, zipStream, out zipStream, item);
                    }
                }
                // 结束压缩
                zipStream.Finish();
                zipStream.Close();
            }
        }

        /// <summary>
        /// 添加压缩项目：p 为需压缩的文件或文件夹； u 为现有的源ZipOutputStream；  out j为已添加“ZipEntry”的“ZipOutputStream”
        /// </summary>
        /// <param name="p">The p.</param>
        /// <param name="zipOutput">The zip output.</param>
        /// <param name="outZipOutput">The out zip output.</param>
        /// <param name="destDir">The dest dir.</param>
        public static void AddZipEntry(string p, ZipOutputStream zipOutput, out ZipOutputStream outZipOutput, string destDir)
        {
            var ServerDir = destDir + "\\";
            string s = Path.Combine(ServerDir, p);

            if (Directory.Exists(s))  //文件夹的处理
            {
                var di = new DirectoryInfo(s);
                if (di.GetDirectories().Length <= 0)   //没有子目录
                {
                    var z = new ZipEntry(p + "\\"); //末尾“\\”用于文件夹的标记
                    zipOutput.PutNextEntry(z);
                }

                foreach (var tem in di.GetDirectories())  //获取子目录
                {
                    //排除隐藏文件夹及svn目录.
                    if ((tem.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden || tem.Name.ToLower().EndsWith(".svn"))
                        continue;
                    var z = new ZipEntry(tem.FullName.Replace(ServerDir, "") + "\\"); //末尾“\\”用于文件夹的标记
                    zipOutput.PutNextEntry(z);    //此句不可少，否则空目录不会被添加
                    s = tem.FullName.Replace(ServerDir, "");
                    AddZipEntry(s, zipOutput, out zipOutput, destDir);       //递归
                }
                foreach (var temp in di.GetFiles())  //获取此目录的文件
                {
                    //排除隐藏文件
                    if ((temp.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;
                    s = temp.FullName.Replace(ServerDir, "");
                    AddZipEntry(s, zipOutput, out zipOutput, destDir);      //递归
                }
            }
            else if (File.Exists(s))  //文件的处理
            {
                zipOutput.SetLevel(9);      //压缩等级
                using (var f = File.OpenRead(s))
                {
                    byte[] b = new byte[f.Length];
                    f.Read(b, 0, b.Length);          //将文件流加入缓冲字节中
                    var array = s.Split('\\');
                    if (array != null && array.Count() > 2)
                    {
                        var fileName = array[array.Length - 1];
                        var z = new ZipEntry(fileName);
                        zipOutput.PutNextEntry(z);             //为压缩文件流提供一个容器
                        zipOutput.Write(b, 0, b.Length);  //写入字节
                    }
                    f.Close();
                }
            }
            outZipOutput = zipOutput;    //返回已添加数据的“ZipOutputStream”
        }

        #endregion 公共方法 
    }
}
