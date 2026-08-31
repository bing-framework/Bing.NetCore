using Bing.DependencyInjection;

namespace Bing.EntityFrameworkCore.Migrations;

/// <summary>
/// 迁移文件服务
/// </summary>
public interface IMigrationFileService : ITransientDependency
{
    /// <summary>
    /// 设置迁移目录绝对路径。即：Migrations目录的绝对路径
    /// </summary>
    /// <param name="path">迁移目录绝对路径</param>
    /// <returns>当前迁移文件服务实例。</returns>
    IMigrationFileService MigrationsPath(string path);

    /// <summary>
    /// 设置迁移名称
    /// </summary>
    /// <param name="name">迁移名称。范例：init</param>
    /// <returns>当前迁移文件服务实例。</returns>
    IMigrationFileService MigrationName(string name);

    /// <summary>
    /// 是否移除所有外键，将删除迁移文件中的外键设置
    /// </summary>
    /// <returns>当前迁移文件服务实例。</returns>
    IMigrationFileService RemoveForeignKeys();

    /// <summary>
    /// 获取迁移文件路径
    /// </summary>
    /// <returns>迁移文件绝对路径；未找到时返回 null。</returns>
    string GetFilePath();

    /// <summary>
    /// 获取文件内容
    /// </summary>
    /// <returns>迁移文件内容；未找到文件时返回 null。</returns>
    string GetContent();

    /// <summary>
    /// 保存文件
    /// </summary>
    /// <param name="filePath">文件绝对路径，传入null则覆盖原文件</param>
    void Save(string filePath = null);
}
