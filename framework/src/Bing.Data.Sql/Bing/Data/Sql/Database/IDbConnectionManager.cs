﻿using System.Data;

namespace Bing.Data.Sql.Database;

/// <summary>
/// 数据库连接管理器
/// </summary>
public interface IDbConnectionManager
{
    /// <summary>
    /// 设置数据库连接
    /// </summary>
    /// <param name="connection">数据库连接</param>
    void SetConnection(IDbConnection connection);

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    IDbConnection GetConnection();
}
