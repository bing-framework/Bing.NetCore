using System.Security.Cryptography;
using Bing.AspNetCore.Uploads.Params;
using Bing.Exceptions;
using Bing.IO;
using Microsoft.AspNetCore.Http;

namespace Bing.AspNetCore.Uploads;

/// <summary>
/// 默认文件上传服务
/// </summary>
internal class DefaultFileUploadService : IFileUploadService
{
    /// <inheritdoc />
    public async Task<FileDescriptor> UploadAsync(SingleFileUploadParam param, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (param.FormFile == null || param.FormFile.Length < 1)
        {
            if (param.Request.Form.Files != null && param.Request.Form.Files.Any())
                param.FormFile = param.Request.Form.Files[0];
        }

        if (param.FormFile == null || param.FormFile.Length < 1)
            throw new Warning("请选择文件!");

        return await SaveAsync(param.FormFile, param.RelativePath, param.RootPath, cancellationToken);
    }

    /// <summary>
    /// 保存文件
    /// </summary>
    /// <param name="formFile">表单文件</param>
    /// <param name="relativePath">相对路径</param>
    /// <param name="rootPath">根路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务，结果为已保存文件的描述信息。</returns>
    private async Task<FileDescriptor> SaveAsync(IFormFile formFile, string relativePath, string rootPath,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        var date = DateTime.Now;

        var name = formFile.FileName;
        var size = formFile.Length;
        var path = System.IO.Path.Combine(relativePath, date.ToString("yyyy"), date.ToString("MM"),
            date.ToString("dd"));
        var id = Guid.NewGuid();
        var fileInfo = new FileDescriptor(name, size)
        {
            Id = id.ToString(),
            DirectoryName = path,
        };
        fileInfo.StorageName = $"{id.ToString().Replace("-", "")}.{fileInfo.Extension}";

        var fullDir = System.IO.Path.Combine(rootPath, fileInfo.DirectoryName);
        if (!Directory.Exists(fullDir)) 
            Directory.CreateDirectory(fullDir);

        var fullPath = Path.Combine(fullDir, fileInfo.StorageName);
        fileInfo.Md5 = await SaveWithMd5Async(formFile, fullPath, cancellationToken);
        return fileInfo;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FileDescriptor>> UploadAsync(MultipleFileUploadParam param, CancellationToken cancellationToken = default(CancellationToken))
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

        var tasks = new List<Task<FileDescriptor>>();
        foreach (var formFile in param.FormFiles)
        {
            tasks.Add(SaveAsync(formFile, param.RelativePath, param.RootPath, cancellationToken));
        }

        return await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public async Task SaveAsync(IFormFile formFile, string savePath, CancellationToken cancellationToken = default(CancellationToken))
    {
        using (var stream = new FileStream(savePath, FileMode.Create))
        {
            await formFile.CopyToAsync(stream, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<string> SaveWithMd5Async(IFormFile formFile, string savePath,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        string md5;
        using (var stream = new FileStream(savePath, FileMode.Create))
        {
            await formFile.CopyToAsync(stream, cancellationToken);
            // 摘要必须针对已经写入的内容；原来在空流上计算会得到固定空文件摘要。
            stream.Position = 0;
            using var hash = MD5.Create();
            md5 = BitConverter.ToString(await hash.ComputeHashAsync(stream, cancellationToken)).Replace("-", "");
        }

        return md5;
    }

}
