﻿using Bing.DependencyInjection;

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
    IMigrationFileService MigrationsPath(string path);

    /// <summary>
    /// 设置迁移名称
    /// </summary>
    /// <param name="name">迁移名称。范例：init</param>
    IMigrationFileService MigrationName(string name);

    /// <summary>
    /// 是否移除所有外键，将删除迁移文件中的外键设置
    /// </summary>
    IMigrationFileService RemoveForeignKeys();

    /// <summary>
    /// 获取迁移文件路径
    /// </summary>
    string GetFilePath();

    /// <summary>
    /// 获取文件内容
    /// </summary>
    string GetContent();

    /// <summary>
    /// 保存文件
    /// </summary>
    /// <param name="filePath">文件绝对路径，传入null则覆盖原文件</param>
    void Save(string filePath = null);
}
