﻿namespace Bing.Data.Sql.Metadata;

/// <summary>
/// 表数据库
/// </summary>
public class DefaultTableDatabase : ITableDatabase
{
    /// <summary>
    /// 获取数据库
    /// </summary>
    /// <param name="table">表</param>
    public string GetDatabase(string table) => null;
}
