namespace Bing.EntityFrameworkCore.Migrations;

/// <summary>
/// 迁移文件服务
/// </summary>
public class MigrationFileService : IMigrationFileService
{
    /// <summary>
    /// 设置迁移目录绝对路径。即：Migrations目录的绝对路径
    /// </summary>
    /// <param name="path">迁移目录绝对路径</param>
    public IMigrationFileService MigrationsPath(string path)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 设置迁移名称
    /// </summary>
    /// <param name="name">迁移名称。范例：init</param>
    public IMigrationFileService MigrationName(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 是否移除所有外键，将删除迁移文件中的外键设置
    /// </summary>
    public IMigrationFileService RemoveForeignKeys()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取迁移文件路径
    /// </summary>
    public string GetFilePath()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 获取文件内容
    /// </summary>
    public string GetContent()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 保存文件
    /// </summary>
    /// <param name="filePath">文件绝对路径，传入null则覆盖原文件</param>
    public void Save(string filePath = null)
    {
        throw new NotImplementedException();
    }
}
