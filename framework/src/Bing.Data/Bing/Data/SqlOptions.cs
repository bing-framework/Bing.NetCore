﻿using System.Data;
using Bing.Data.Enums;

namespace Bing.Data;

/// <summary>
/// Sql配置
/// </summary>
public class SqlOptions
{
    /// <summary>
    /// 数据库类型，默认为Sql Server
    /// </summary>
    public DatabaseType DatabaseType { get; set; } = DatabaseType.SqlServer;

    /// <summary>
    /// 是否在执行之后清空Sql和参数，默认为 true
    /// </summary>
    public bool IsClearAfterExecution { get; set; } = true;

    /// <summary>
    /// 数据日志级别
    /// </summary>
    public DataLogLevel LogLevel { get; set; } = DataLogLevel.Sql;

    /// <summary>
    /// 日志类别
    /// </summary>
    public string LogCategory { get; set; } = "Bing.Data.Sql";

    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// 数据库连接
    /// </summary>
    public IDbConnection Connection { get; set; }
}

/// <summary>
/// Sql配置
/// </summary>
/// <typeparam name="T">泛型类型</typeparam>
public class SqlOptions<T> : SqlOptions where T : class
{
}
