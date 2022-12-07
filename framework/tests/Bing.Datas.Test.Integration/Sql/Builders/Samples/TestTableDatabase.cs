﻿using Bing.Data.Sql.Matedatas;

namespace Bing.Data.Test.Integration.Sql.Builders.Samples;

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