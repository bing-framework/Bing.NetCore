﻿using System;

namespace Bing.Data;

/// <summary>
/// 数据配置
/// </summary>
public class DataConfig
{
    /// <summary>
    /// 数据日志级别
    /// </summary>
    public Data.DataLogLevel LogLevel { get; set; } = Data.DataLogLevel.Sql;

    /// <summary>
    /// Sql查询配置
    /// </summary>
    public SqlOptions SqlOptions { get; set; }

    /// <summary>
    /// 自动提交，默认禁用
    /// </summary>
    public bool AutoCommit { get; set; } = false;

    /// <summary>
    /// 是否启用验证版本号，默认启用
    /// </summary>
    public bool EnabledValidateVersion { get; set; } = true;

    /// <summary>
    /// ADO日志拦截器，
    /// </summary>
    public Action<string, string, object> AdoLogInterceptor { get; set; } = null;

    /// <summary>
    /// 是否启用逻辑删除过滤
    /// </summary>
    public bool EnabledDeleteFilter { get; set; } = true;

    /// <summary>
    /// 初始化一个<see cref="DataConfig"/>类型的实例
    /// </summary>
    public DataConfig() => SqlOptions = new SqlOptions();
}