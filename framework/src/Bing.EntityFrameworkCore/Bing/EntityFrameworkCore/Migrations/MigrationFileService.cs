using Bing.Helpers;
using Bing.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.EntityFrameworkCore.Migrations;

/// <summary>
/// 迁移文件服务
/// </summary>
public class MigrationFileService : IMigrationFileService
{
    /// <summary>
    /// 日志
    /// </summary>
    private readonly ILogger<MigrationFileService> _logger;

    /// <summary>
    /// 迁移目录绝对路径
    /// </summary>
    private string _migrationsPath;

    /// <summary>
    /// 迁移名称
    /// </summary>
    private string _migrationName;

    /// <summary>
    /// 是否移除所有外键
    /// </summary>
    private bool _isRemoveForeignKeys;

    /// <summary>
    /// 初始化一个<see cref="MigrationFileService"/>类型的实例
    /// </summary>
    /// <param name="logger">日志</param>
    public MigrationFileService(ILogger<MigrationFileService> logger)
    {
        _logger = logger ?? NullLogger<MigrationFileService>.Instance;
    }

    /// <summary>
    /// 设置迁移目录绝对路径。即：Migrations目录的绝对路径
    /// </summary>
    /// <param name="path">迁移目录绝对路径</param>
    public IMigrationFileService MigrationsPath(string path)
    {
        _migrationsPath = path;
        return this;
    }

    /// <summary>
    /// 设置迁移名称
    /// </summary>
    /// <param name="name">迁移名称。范例：init</param>
    public IMigrationFileService MigrationName(string name)
    {
        _migrationName = name;
        return this;
    }

    /// <summary>
    /// 是否移除所有外键，将删除迁移文件中的外键设置
    /// </summary>
    public IMigrationFileService RemoveForeignKeys()
    {
        _isRemoveForeignKeys = true;
        return this;
    }

    /// <summary>
    /// 获取迁移文件路径
    /// </summary>
    public string GetFilePath()
    {
        if (string.IsNullOrWhiteSpace(_migrationsPath))
            return null;
        if (string.IsNullOrWhiteSpace(_migrationName))
            return null;
        var files = GetAllFiles(_migrationsPath, "*.cs");
        var file = files.FirstOrDefault(t => t.Name.EndsWith($"{_migrationName}.cs"));
        if (file == null)
            return null;
        return file.FullName;
    }

    /// <summary>
    /// 获取全部文件，包括所有子目录
    /// </summary>
    /// <param name="path">目录路径</param>
    /// <param name="searchPattern">搜索模式</param>
    private static List<FileInfo> GetAllFiles(string path, string searchPattern)
    {
        return Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories)
            .Select(filePath => new FileInfo(filePath))
            .ToList();
    }

    /// <summary>
    /// 获取文件内容
    /// </summary>
    public string GetContent()
    {
        var filePath = GetFilePath();
        if (string.IsNullOrWhiteSpace(filePath))
            return null;
        return FileHelper.ReadToString(filePath);
    }

    /// <summary>
    /// 保存文件
    /// </summary>
    /// <param name="filePath">文件绝对路径，传入null则覆盖原文件</param>
    public void Save(string filePath = null)
    {
        if (_isRemoveForeignKeys == false)
            return;
        if (string.IsNullOrWhiteSpace(filePath))
            filePath = GetFilePath();
        var content = GetContent();
        var pattern = @"table.ForeignKey\([\s\S]+?\);";
        var result = Regexs.Replace(content, pattern, "");
        pattern = @$"\s+{Common.Line}\s+{Common.Line}";
        result = Regexs.Replace(result, pattern, Common.Line);
        FileHelper.Write(filePath, result);
        _logger.LogTrace($"修改迁移文件并保存成功，路径：{filePath}");
    }
}
