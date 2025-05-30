﻿using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 表数据库
/// </summary>
public class TestTableDatabase : ITableDatabase
{
    /// <summary>
    /// 获取数据库
    /// </summary>
    /// <param name="table">表</param>
    public string GetDatabase(string table) => "test";
}
