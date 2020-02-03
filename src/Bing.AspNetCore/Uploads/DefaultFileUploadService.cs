using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Bing.AspNetCore.Uploads.Params;
using Bing.Exceptions;
using Bing.Extensions;
using Microsoft.AspNetCore.Http;
using FileInfo = Bing.Utils.Files.FileInfo;

namespace Bing.AspNetCore.Uploads
{
    /// <summary>
    /// 默认文件上传服务
    /// </summary>
    internal class DefaultFileUploadService : IFileUploadService
    {
        /// <summary>
        /// 上传文件。单文件
        /// </summary>
        /// <param name="param">参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<FileInfo> UploadAsync(SingleFileUploadParam param, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (param.FormFile == null || param.FormFile.Length < 1)
            {
                if (param.Request.Form.Files != null && param.Request.Form.Files.Any())
                {
                    param.FormFile = param.Request.Form.Files[0];
                }
            }

            if (param.FormFile == null || param.FormFile.Length < 1)
            {
                throw new Warning("请选择文件!");
            }

            return await SaveAsync(param.FormFile, param.RelativePath, param.RootPath, cancellationToken);
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="formFile">表单文件</param>
        /// <param name="relativePath">相对路径</param>
        /// <param name="rootPath">根路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task<FileInfo> SaveAsync(IFormFile formFile, string relativePath, string rootPath,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var date = DateTime.Now;

            var name = formFile.FileName;
            var size = formFile.Length;
            var path = System.IO.Path.Combine(relativePath, date.ToString("yyyy"), date.ToString("MM"),
                date.ToString("dd"));
            var id = Guid.NewGuid();
            var fileInfo = new FileInfo(path, size, name, id.ToString());
            fileInfo.SaveName = $"{id.ToString().Replace("-", "")}.{fileInfo.Extension}";

            var fullDir = System.IO.Path.Combine(rootPath, fileInfo.Path);
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir);
            }

            var fullPath = Path.Combine(fullDir, fileInfo.SaveName);
            fileInfo.Md5 = await SaveWithMd5Async(formFile, fullPath, cancellationToken);
            return fileInfo;
        }

        /// <summary>
        /// 上传文件。多文件
        /// </summary>
        /// <param name="param">参数</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<IEnumerable<FileInfo>> UploadAsync(MultipleFileUploadParam param, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (param.FormFiles == null || !param.FormFiles.Any())
            {
                if (param.Request.Form.Files != null && param.Request.Form.Files.Any())
                {
                    param.FormFiles = param.Request.Form.Files.ToList();
                }
            }

            if (param.FormFiles == null || !param.FormFiles.Any())
            {
                throw new Warning("请选择文件!");
            }

            var tasks = new List<Task<FileInfo>>();
            foreach (var formFile in param.FormFiles)
            {
                tasks.Add(SaveAsync(formFile, param.RelativePath, param.RootPath, cancellationToken));
            }

            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 保存文件
        /// </summary>
        /// <param name="formFile">表单文件</param>
        /// <param name="savePath">保存路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task SaveAsync(IFormFile formFile, string savePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var stream=new FileStream(savePath,FileMode.Create))
            {
                await formFile.CopyToAsync(stream, cancellationToken);
            }
        }

        /// <summary>
        /// 保存文件并返回文件MD5值
        /// </summary>
        /// <param name="formFile">表单文件</param>
        /// <param name="savePath">保存路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task<string> SaveWithMd5Async(IFormFile formFile, string savePath,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string md5;
            using (var stream = new FileStream(savePath, FileMode.Create))
            {
                md5 = Md5(stream);
                await formFile.CopyToAsync(stream, cancellationToken);
            }

            return md5;
        }

        /// <summary>
        /// MD5哈希
        /// </summary>
        /// <param name="stream">流</param>
        private static string Md5(Stream stream)
        {
            if (stream == null)
            {
                return string.Empty;
            }

            using (var md5Hash = MD5.Create())
            {
                return BitConverter.ToString(md5Hash.ComputeHash(stream)).Replace("-", "");
            }
        }
    }
}
